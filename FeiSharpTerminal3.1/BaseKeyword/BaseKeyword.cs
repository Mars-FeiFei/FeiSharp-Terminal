namespace FeiSharpTerminal3._1;

public abstract class BaseKeyword
{
    public abstract string Name { get; }
    public virtual string? Description => null;
    public abstract void Execute(KeywordContext context);
}

public enum KeywordTokenType
{
    Keyword,
    Identifier,
    Number,
    String,
    Character,
    Punctuation,
    Operator,
    EndOfFile,
    Type,
    Bool,
    FuncName,
    Code
}

public sealed record KeywordToken(KeywordTokenType Type, string Value);

public sealed class KeywordContext
{
    private readonly Func<KeywordToken> _current;
    private readonly Func<KeywordToken> _readToken;
    private readonly Func<KeywordTokenType, string?, bool> _match;
    private readonly Func<object?> _readExpression;
    private readonly Action<string> _executeFeiSharp;

    public KeywordContext(
        Func<KeywordToken> current,
        Func<KeywordToken> readToken,
        Func<KeywordTokenType, string?, bool> match,
        Func<object?> readExpression,
        Action<string> executeFeiSharp)
    {
        _current = current;
        _readToken = readToken;
        _match = match;
        _readExpression = readExpression;
        _executeFeiSharp = executeFeiSharp;
    }

    public KeywordToken Current => _current();
    public bool IsAtEnd => Current.Type == KeywordTokenType.EndOfFile;
    public KeywordToken ReadToken() => _readToken();
    public bool Match(KeywordTokenType type, string? value = null) => _match(type, value);
    public bool MatchPunctuation(string value) => Match(KeywordTokenType.Punctuation, value);
    public bool MatchKeyword(string value) => Match(KeywordTokenType.Keyword, value);
    public void RequirePunctuation(string value)
    {
        if (!MatchPunctuation(value)) throw new InvalidOperationException($"Expected '{value}', found '{Current.Value}'.");
    }
    public object? ReadExpression() => _readExpression();
    public void ExecuteFeiSharp(string code) => _executeFeiSharp(code);
    public void Write(object? value) => Console.Write(value);
    public void WriteLine(object? value = null) => Console.WriteLine(value);
}
