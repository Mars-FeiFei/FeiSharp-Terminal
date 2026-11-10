using FeiSharpTerminal3._1;
using System.Data;
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
    private readonly List<int> _lineStarts = [0];
    private int _index;
    private static readonly Dictionary<char, Token> SingleCharTokens = new()
    {
        ['%'] = new(TokenTypes.Punctuation, "%"),
        ['#'] = new(TokenTypes.Punctuation, "#"),
        [':'] = new(TokenTypes.Punctuation, ":"),
        ['&'] = new(TokenTypes.Operator, "&"),
        ['|'] = new(TokenTypes.Operator, "|"),
        ['.'] = new(TokenTypes.Punctuation, "."),
        [']'] = new(TokenTypes.Punctuation, "]"),
        ['['] = new(TokenTypes.Punctuation, "["),
        ['!'] = new(TokenTypes.Operator, "!"),
        ['}'] = new(TokenTypes.Punctuation, "}"),
        ['{'] = new(TokenTypes.Punctuation, "{"),
        ['<'] = new(TokenTypes.Operator, "<"),
        ['>'] = new(TokenTypes.Operator, ">"),
        ['='] = new(TokenTypes.Operator, "="),
        ['^'] = new(TokenTypes.Operator, "^"),
        ['/'] = new(TokenTypes.Operator, "/"),
        ['*'] = new(TokenTypes.Operator, "*"),
        ['-'] = new(TokenTypes.Operator, "-"),
        [','] = new(TokenTypes.Punctuation, ","),
        ['+'] = new(TokenTypes.Operator, "+"),
        [';'] = new(TokenTypes.Punctuation, ";"),
        ['('] = new(TokenTypes.Punctuation, "("),
        [')'] = new(TokenTypes.Punctuation, ")"),
        ['$'] = new(TokenTypes.Operator, "$"),
    };
    public Lexer(string source)
    {
        _source = source;
        _index = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
            {
                _lineStarts.Add(index + 1);
            }
        }
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
            int tokenStart = _index;
            if (_index + 1 < _source.Length)
            {
                string twoCharOperator = _source.Substring(_index, 2);
                if (twoCharOperator is "++" or "--" or "==" or "!=" or "&&" or "||" or "<=" or ">=" or "=>" or "+=" or "-=")
                {
                    _index += 2;
                    return Located(new Token(TokenTypes.Operator, twoCharOperator), tokenStart);
                }
            }
            if (current == '+')
            {
                if (_index + 1 < _source.Length && _source[_index + 1] == '+')
                {
                    _index += 2;
                    return Located(new Token(TokenTypes.Operator, "++"), tokenStart);
                }
            }
            if (current == '-')
            {
                if (_index + 1 < _source.Length && _source[_index + 1] == '-')
                {
                    _index += 2;
                    return Located(new Token(TokenTypes.Operator, "--"), tokenStart);
                }
            }
            if (SingleCharTokens.TryGetValue(current, out Token? token))
            {
                _index++;
                return Located(token, tokenStart);
            }
            if (current == '"')
            {
                return ReadStringToken();
            }
            if (current == '\'')
            {
                return ReadCharacterToken();
            }
            if (char.IsDigit(current))
            {
                return ReadNumberToken();
            }
            if (char.IsLetter(current) || current == '_')
            {
                return ReadIdentifierOrKeywordToken();
            }
            CodeError.Throw("FS1001","Unexpected character: " + current + ", it will be skipped", -1);
            _index++;
        }
        return Located(new Token(TokenTypes.EndOfFile, ""), _index);
    }
    private Token Located(Token token, int offset)
    {
        int lineIndex = _lineStarts.BinarySearch(offset);
        if (lineIndex < 0)
            lineIndex = ~lineIndex - 1;
        lineIndex = Math.Max(0, lineIndex);
        int line = lineIndex + 1;
        int column = offset - _lineStarts[lineIndex] + 1;
        return new Token(token.Type, token.Value, offset, line, column, _source, _index - offset);
    }
    private Token ReadStringToken()
    {
        int start = ++_index;
        while (_index < _source.Length && _source[_index] != '"')
        {
            _index++;
        }
        if (_index >= _source.Length)
        {
            CodeError.Throw("FS1002", "Unterminated string literal");
            return Located(new Token(TokenTypes.EndOfFile, ""), start - 1);
        }
        string value = _source[start.._index];
        _index++;
        return Located(new Token(TokenTypes.String, value), start - 1);
    }
    private Token ReadCharacterToken()
    {
        _index++;
        if (_index >= _source.Length)
        {
            throw new FeiSharpTerminal3._1.ExceptionThrow.Exception(new(), 1, "Unterminated character literal.", "FS1001");
        }
        char value = _source[_index++];
        if (_index >= _source.Length || _source[_index] != '\'')
        {
            throw new FeiSharpTerminal3._1.ExceptionThrow.Exception(new(), 1, "Char type constants cannot have multiple characters. If you want to store multiple characters, please change them to String type constants.", "FS1001");
        }
        _index++;
        return Located(new Token(TokenTypes.Character, value.ToString()), _index - 3);
    }
    private Token ReadNumberToken()
    {
        int start = _index;
        while (_index < _source.Length && char.IsDigit(_source[_index]))
        {
            _index++;
        }
        // A dot belongs to a number only when it starts a fractional part.
        // This keeps `1.isClass()` tokenized as Number, '.', Identifier.
        if (_index + 1 < _source.Length
            && _source[_index] == '.'
            && char.IsDigit(_source[_index + 1]))
        {
            _index++;
            while (_index < _source.Length && char.IsDigit(_source[_index]))
            {
                _index++;
            }
        }
        return Located(new Token(TokenTypes.Number, _source[start.._index]), start);
    }
    private Token ReadIdentifierOrKeywordToken()
    {
        int start = _index;
        while (_index < _source.Length && (char.IsLetterOrDigit(_source[_index]) || _source[_index] == '_'))
        {
            _index++;
        }
        string identifier = _source[start.._index];
        if (FeiSharpKeywords.KeywordMap.TryGetValue(identifier, out Token keywordToken))
        {
            return Located(keywordToken, start);
        }
        return Located(new Token(TokenTypes.Identifier, identifier), start);
    }
}
