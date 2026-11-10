namespace FeiSharpStudio
{
    public class Token
    {
        public TokenTypes Type { get; }
        public string Value { get; }
        public int Offset { get; }
        public int Line { get; }
        public int Column { get; }
        // Keep the source span separate from the logical token value.  For example,
        // the value of a string token excludes its quotes, but diagnostics must point
        // at (and underline) the quoted source text.
        public string? Source { get; }
        public int Length { get; }

        public Token(
            TokenTypes type,
            string value,
            int offset = -1,
            int line = 1,
            int column = 1,
            string? source = null,
            int length = -1)
        {
            Type = type;
            Value = value;
            Offset = offset;
            Line = line;
            Column = column;
            Source = source;
            Length = length >= 0 ? length : value.Length;
        }
        public override string ToString()
        {
            return Value;
        }
    }
}
