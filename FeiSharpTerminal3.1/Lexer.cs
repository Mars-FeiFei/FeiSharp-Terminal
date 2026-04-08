using FeiSharpTerminal3._1;
namespace FeiSharpStudio;
public class CodeError
{
    public static void Throw(string code, string msg, int exitCode = 1)
    {
        Console.WriteLine($"{code.ToUpperInvariant()}: {msg}, help link: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{code.ToLowerInvariant()}");
        isError = true;
    }
    public static bool isError = false;
}
public class Lexer
{
    private readonly string _source;
    private int _index;

    public Lexer(string source)
    {
        _source = source;
        _index = 0;
    }

    public Token NextToken()
    {
        while (_index < _source.Length)
        {
            char current = _source[_index];
            if (char.IsWhiteSpace(current))
            {
                _index++;
                continue;
            }
            if (current == '%') { _index++; return new Token(TokenTypes.Punctuation, "%"); }
            if (current == '#') { _index++; return new Token(TokenTypes.Punctuation, "#"); }
            if (current == ':') { _index++; return new Token(TokenTypes.Punctuation, ":"); }
            if (current == '&') { _index++; return new Token(TokenTypes.Operator, "&"); }
            if (current == '|') { _index++; return new Token(TokenTypes.Operator, "|"); }
            if (current == '.') { _index++; return new Token(TokenTypes.Punctuation, "."); }
            if (current == ']') { _index++; return new Token(TokenTypes.Punctuation, "]"); }
            if (current == '[') { _index++; return new Token(TokenTypes.Punctuation, "["); }
            if (current == '!') { _index++; return new Token(TokenTypes.Operator, "!"); }
            if (current == '}') { _index++; return new Token(TokenTypes.Punctuation, "}"); }
            if (current == '{') { _index++; return new Token(TokenTypes.Punctuation, "{"); }
            if (current == '<') { _index++; return new Token(TokenTypes.Operator, "<"); }
            if (current == '>') { _index++; return new Token(TokenTypes.Operator, ">"); }
            if (current == '=') { _index++; return new Token(TokenTypes.Operator, "="); }
            if (current == '|') { _index++; return new Token(TokenTypes.Operator, "|"); }
            if (current == '^') { _index++; return new Token(TokenTypes.Operator, "^"); }
            if (current == '/') { _index++; return new Token(TokenTypes.Operator, "/"); }
            if (current == '*') { _index++; return new Token(TokenTypes.Operator, "*"); }
            if (current == '-') { _index++; return new Token(TokenTypes.Operator, "-"); }
            if (current == ',') { _index++; return new Token(TokenTypes.Punctuation, ","); }
            if (current == '+') { _index++; return new Token(TokenTypes.Operator, "+"); }
            if (current == '-') { _index++; return new Token(TokenTypes.Operator, "-"); }
            if (current == '=') { _index++; return new Token(TokenTypes.Operator, "="); }
            if (current == ';') { _index++; return new Token(TokenTypes.Punctuation, ";"); }
            if (current == '(') { _index++; return new Token(TokenTypes.Punctuation, "("); }
            if (current == ')') { _index++; return new Token(TokenTypes.Punctuation, ")"); }
            if (current == '$') { _index++; return new Token(TokenTypes.Operator, "$"); }
            if (current == '"')
            {
                int start = ++_index;

                while (_index < _source.Length && _source[_index] != '"')
                {
                    if(_index == _source.Length - 1)
                    {
                        CodeError.Throw("FS1002", "Unterminated string literal");
                    }
                    _index++;
                }
                return new Token(TokenTypes.String, _source[start.._index++]);
            }
            if (current == '\'')
            {
                int start = ++_index;
                var res = new Token(TokenTypes.Character, new(_source[start], 1));
                _index++;
                if(current != '\'')
                {
                    throw new FeiSharpTerminal3._1.ExceptionThrow.Exception(new(), 1, "Char type constants cannot have multiple characters. If you want to store multiple characters, please change them to String type constants.", "FS1001");
                }
                _index++;
                return new Token(TokenTypes.Character, new(_source[start],1));
            }
            if (char.IsDigit(current))
            {
                int start = _index;
                while (_index < _source.Length && (char.IsDigit(_source[_index]) || _source[_index] == '.')) _index++;
                return new Token(TokenTypes.Number, _source[start.._index]);
            }
            if (char.IsLetter(current))
            {
                int start = _index;
                while (_index < _source.Length && char.IsLetterOrDigit(_source[_index]))
                    _index++;
                string identifier = _source[start.._index];
                if (FeiSharpKeywords.KeywordMap.TryGetValue(identifier, out Token keywordToken))
                    return keywordToken;
                return new Token(TokenTypes.Identifier, identifier);
            }
            CodeError.Throw("FS1001","Unexpected character: " + current + ", it will be skipped", -1);
            _index++;
        }
        return new Token(TokenTypes.EndOfFile, "");
    }
}
