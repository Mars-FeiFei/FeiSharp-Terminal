using FeiSharpStudio;
using System.Text;

namespace FeiSharpTerminal3._1.ExceptionThrow
{
    internal class Exception : System.Exception
    {
        public StringBuilder stringBuilder { get; private set; }
        public string ErrorType { get; }
        public string Description { get; }
        public string Number { get; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public Exception(
            List<Token> tokens,
            int current,
            string message,
            string code = "FS1001",
            string? errorType = null) : base(message)
        {
            Description = message;
            Number = code.ToUpperInvariant();
            ErrorType = errorType ?? InferErrorType(Number);

            try
            {
                var errorInfo = BuildDetailedError(tokens, current);
                Line = errorInfo.Line;
                Column = errorInfo.Column;
                stringBuilder = errorInfo.MessageBuilder;
            }
            catch
            {
                stringBuilder = new StringBuilder()
                    .AppendLine($"╔═══ Error ═══╗")
                    .AppendLine($"║ [{ErrorType}] {Number}           ")
                    .AppendLine($"║ {Description}                    ")
                    .AppendLine($"╚═════════════╝")
                    .AppendLine($"Help: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{Number.ToLowerInvariant()}");
            }
        }

        private (StringBuilder MessageBuilder, int Line, int Column) BuildDetailedError(
            List<Token> tokens,
            int current)
        {
            var sb = new StringBuilder();
            var errorToken = tokens[current];

            var lineInfo = GetLineInfo(tokens, current);
            int currentLine = lineInfo.LineNumber;
            int currentLineStart = lineInfo.LineStartIndex;
            int currentLineEnd = lineInfo.LineEndIndex;

            int column = CalculateColumn(tokens, currentLineStart, current);

            var contextLines = GetContextLines(tokens, currentLine, lineInfo.AllLines);

            sb.AppendLine();
            sb.AppendLine($"┌─── {ErrorType} ({Number}) ─────────────────────────────────────────────");
            sb.AppendLine($"│ {Description}");
            sb.AppendLine($"│ 🔗 https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{Number.ToLowerInvariant()}");
            sb.AppendLine("│");

            sb.AppendLine($"│ 📍 Location:");
            sb.AppendLine($"│    Line {currentLine + 1}, Column {column + 1}");
            sb.AppendLine($"│    Token: '{errorToken.Value}' (Type: {errorToken.Type})");
            sb.AppendLine("│");

            sb.AppendLine($"│ 💻 Code Context:");

            foreach (var ctxLine in contextLines)
            {
                string lineNumStr = (ctxLine.LineNumber + 1).ToString().PadLeft(3);
                sb.Append($"│ {lineNumStr} │ ");

                sb.Append(ctxLine.Content);
                sb.AppendLine();

                if (ctxLine.IsErrorLine)
                {
                    int errorColumn = (ctxLine.IsErrorLine && ctxLine.LineNumber == currentLine)
                        ? column : 0;
                    string pointer = GeneratePointer(ctxLine.Content, errorToken.Value, errorColumn);
                    sb.Append($"│     │ {pointer}");
                    sb.AppendLine();

                    sb.Append($"│     │ ╰─ {Description}");
                    sb.AppendLine();
                }
            }

            var suggestions = GetSuggestions(errorToken, ErrorType, Number);
            if (suggestions.Count > 0)
            {
                sb.AppendLine("│");
                sb.AppendLine($"│ 💡 Suggestions:");
                foreach (var suggestion in suggestions)
                {
                    sb.AppendLine($"│    • {suggestion}");
                }
            }

            sb.AppendLine("└───────────────────────────────────────────────────────────────");
            sb.AppendLine();

            return (sb, currentLine, column);
        }

        private class LineInfo
        {
            public int LineNumber { get; set; }
            public int LineStartIndex { get; set; }
            public int LineEndIndex { get; set; }
            public List<(int Start, int End)> AllLines { get; set; } = new();
        }

        private LineInfo GetLineInfo(List<Token> tokens, int current)
        {
            var lines = new List<(int Start, int End)>();
            int lineStart = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Value == ";" && tokens[i].Type == TokenTypes.Punctuation)
                {
                    lines.Add((lineStart, i));
                    lineStart = i + 1;
                }
            }

            if (lineStart < tokens.Count)
            {
                lines.Add((lineStart, tokens.Count - 1));
            }

            int currentLine = lines.FindIndex(l => l.Start <= current && current <= l.End);
            if (currentLine == -1) currentLine = lines.Count - 1;

            return new LineInfo
            {
                LineNumber = currentLine,
                LineStartIndex = lines[currentLine].Start,
                LineEndIndex = lines[currentLine].End,
                AllLines = lines
            };
        }

        private int CalculateColumn(List<Token> tokens, int lineStart, int current)
        {
            int col = 0;
            for (int i = lineStart; i < current; i++)
            {
                col += StringWidthHelper.GetDisplayWidth(tokens[i].Value ?? "");
                if (tokens[i].Value != ";")
                {
                    col += 1;
                }
            }
            return col;
        }

        private List<ContextLine> GetContextLines(List<Token> tokens, int currentLine, List<(int Start, int End)> allLines)
        {
            var context = new List<ContextLine>();
            int startLine = Math.Max(0, currentLine - 2);
            int endLine = Math.Min(allLines.Count - 1, currentLine + 2);

            for (int line = startLine; line <= endLine; line++)
            {
                var lineTokens = tokens.Skip(allLines[line].Start)
                                       .Take(allLines[line].End - allLines[line].Start + 1)
                                       .ToList();

                var content = new StringBuilder();
                foreach (var token in lineTokens)
                {
                    content.Append(token.Value);
                    if (token.Value == "{" || token.Value == "}" || token.Value == ";")
                    {
                        content.Append('\n');
                    }
                    if (token.Value == "var" || token.Value == "class" || token.Value == "function")
                    {
                        content.Append(' ');
                    }
                }
                string rawContent = content.ToString();
                string trimmedContent = rawContent.TrimEnd();
                context.Add(new ContextLine
                {
                    LineNumber = line,
                    Content = trimmedContent,
                    RawContent = rawContent,
                    IsErrorLine = (line == currentLine)
                });
            }

            return context;
        }

        private class ContextLine
        {
            public int LineNumber { get; set; }
            public string Content { get; set; } = "";
            public string RawContent { get; set; } = ""; public bool IsErrorLine { get; set; }
        }

        private string GeneratePointer(string lineContent, string errorToken, int column)
        {
            int pointerPos = column;
            var pointer = new StringBuilder();

            for (int i = 0; i < pointerPos; i++)
            {
                pointer.Append(' ');
            }

            pointer.Append('^');
            for (int i = 1; i < errorToken.Length; i++)
            {
                pointer.Append('~');
            }

            return pointer.ToString();
        }

        private List<string> GetSuggestions(Token errorToken, string errorType, string errorCode)
        {
            var suggestions = new List<string>();

            switch (errorType)
            {
                case "LexerError":
                    suggestions.Add($"Check if '{errorToken.Value}' is a valid character or keyword");
                    suggestions.Add("Try wrapping strings in double quotes \"...\" or characters in single quotes '...'");
                    break;

                case "SyntaxError":
                    suggestions.Add("Verify the syntax structure (missing parentheses, brackets, or operators?)");
                    suggestions.Add("Check if all statements end with semicolon ';'");
                    break;

                case "SemanticError":
                    suggestions.Add("Check variable/function declarations and their scope");
                    suggestions.Add("Check index is valid");
                    suggestions.Add("Verify type compatibility in operations");
                    break;

                case "UserException":
                    suggestions.Add("Review the error description and adjust your code logic");
                    suggestions.Add("Use try-catch blocks to handle this exception");
                    break;

                default:
                    suggestions.Add("Review the code around the marked position");
                    suggestions.Add("Check the documentation for more details");
                    break;
            }

            if (errorCode == "FS3001")
            {
                suggestions.Add("'public', 'private' keyword can only be used inside class declarations");
                suggestions.Add("Move this declaration inside a class or remove the 'public' or 'private' keyword");
            }

            return suggestions;
        }

        public static Exception FromSystemException(
            List<Token> tokens,
            int current,
            System.Exception ex,
            string code = "FS5000",
            string errorType = "RuntimeError")
        {
            return new Exception(tokens, current, ex.Message, code, errorType);
        }

        private static string InferErrorType(string code)
        {
            if (code.StartsWith("FS1", StringComparison.OrdinalIgnoreCase))
                return "LexerError";
            if (code.StartsWith("FS2", StringComparison.OrdinalIgnoreCase))
                return "SyntaxError";
            if (code.StartsWith("FS3", StringComparison.OrdinalIgnoreCase))
                return "SemanticError";
            if (code.StartsWith("FS4", StringComparison.OrdinalIgnoreCase))
                return "UserException";
            return "RuntimeError";
        }

        public void PrintColored()
        {
            var originalColor = Console.ForegroundColor;

            switch (ErrorType)
            {
                case "LexerError":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case "SyntaxError":
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case "SemanticError":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case "UserException":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }

            Console.Write(stringBuilder.ToString());
            Console.ForegroundColor = originalColor;
        }
    }
    public static class StringWidthHelper
    {
        public static int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text)
            {
                width += IsWideChar(c) ? 2 : 1;
            }
            return width;
        }

        public static bool IsWideChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0xFF00 && c <= 0xFFEF) ||
                   (c >= 0x3040 && c <= 0x30FF) ||
                   (c >= 0xAC00 && c <= 0xD7AF) ||
                   c == '　';
        }

        public static string PadRightToWidth(string text, int targetWidth)
        {
            int currentWidth = GetDisplayWidth(text);
            if (currentWidth >= targetWidth)
                return text;
            return text + new string(' ', targetWidth - currentWidth);
        }

        public static string PadLeftToWidth(string text, int targetWidth)
        {
            int currentWidth = GetDisplayWidth(text);
            if (currentWidth >= targetWidth)
                return text;
            return new string(' ', targetWidth - currentWidth) + text;
        }
    }
}