using FeiSharpStudio;
using System.Text;
using Spectre.Console;
namespace FeiSharpTerminal3._1.ExceptionThrow
{
    internal class Exception : System.Exception
    {
        private const string Reset = "[/]";
        private const string Dim = "[dim]";
        private const string Red = "[bold red]";
        private const string Yellow = "[yellow]";
        private const string Green = "[green]";
        private const string Cyan = "[cyan]";
        private const string Blue = "[blue]";
        private const string Magenta = "[magenta]";
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
            Number = NormalizeErrorCode(code, message);
            ErrorType = errorType ?? InferErrorType(Number);

            try
            {
                var errorInfo = BuildDetailedError(tokens, current);
                Line = errorInfo.Line + 1;
                Column = errorInfo.Column;
                stringBuilder = errorInfo.MessageBuilder;
            }
            catch
            {
                Line = 1;
                Column = 1;
                stringBuilder = new StringBuilder()
                    .AppendLine($"{Red}{Number}{Reset} {Markup.Escape(Description)}")
                    .AppendLine($"{Dim}Location: Line {Line}, Column {Column}{Reset}")
                    .AppendLine($"Help: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{Number.ToLowerInvariant()}");
            }
        }

        private (StringBuilder MessageBuilder, int Line, int Column) BuildDetailedError(
            List<Token> tokens,
            int current)
        {
            var sb = new StringBuilder();
            if (tokens.Count == 0)
            {
                return (new StringBuilder()
                    .AppendLine($"{Red}✗ {Number}{Reset}  {Yellow}{GetFriendlyErrorName(Number, ErrorType)}{Reset}")
                    .AppendLine()
                    .AppendLine($"{Blue}Reason:{Reset} {Markup.Escape(Description)}"), 0, 0);
            }

            current = Math.Clamp(current, 0, tokens.Count - 1);
            var errorToken = tokens[current];
            var allLines = CodeHighlighter.SplitLines(tokens);
            int currentLine = allLines.FindIndex(l => l.Start <= current && current <= l.End);
            if (currentLine < 0) currentLine = Math.Max(0, allLines.Count - 1);

            int sourceLineOffset = FeiSharpTerminal3._1.DebugProtocol.SourceLineOffset;
            int sourceLineNumber = Math.Max(1, errorToken.Line - sourceLineOffset);
            int column = Math.Max(1, errorToken.Column);

            string sourceName = string.IsNullOrWhiteSpace(FeiSharpTerminal3._1.DebugProtocol.SourcePath)
                ? "<script>"
                : Path.GetFileName(FeiSharpTerminal3._1.DebugProtocol.SourcePath);

            sb.AppendLine();
            sb.AppendLine($"{Red}✗ {Number}{Reset}  {Yellow}{GetFriendlyErrorName(Number, ErrorType)}{Reset}");
            sb.AppendLine();
            sb.AppendLine($"{Blue}Location:{Reset}  {Markup.Escape(sourceName)}:{sourceLineNumber}:{column}");
            sb.AppendLine();
            sb.AppendLine($"{Blue}Code snippet:{Reset}");
            CodeHighlighter.AppendHighlightedCode(
                sb,
                tokens,
                current,
                contextRadius: 2,
                sourceLineOffset: sourceLineOffset,
                includeHeader: false);

            sb.AppendLine();
            sb.AppendLine($"{Blue}Reason:{Reset}");
            sb.AppendLine(Markup.Escape(Description));

            var suggestions = GetSuggestions(errorToken, ErrorType, Number);
            if (suggestions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{Blue}Suggested fixes:{Reset}");
                foreach (var suggestion in suggestions)
                {
                    sb.AppendLine($"{Cyan}▸{Reset} {Markup.Escape(suggestion)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"{Dim}Documentation: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{Number.ToLowerInvariant()}{Reset}");
            sb.AppendLine();

            return (sb, currentLine, column);
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
            else if (errorCode == FeiSharpErrorCodes.MissingDelimiter)
            {
                suggestions.Add("Add the missing parenthesis, bracket, brace, comma, or member-access dot");
            }
            else if (errorCode == FeiSharpErrorCodes.UndefinedSymbol)
            {
                suggestions.Add("Declare the variable, function, class, or member before using it");
                suggestions.Add("Check spelling and imported namespaces");
            }
            else if (errorCode == FeiSharpErrorCodes.InvalidArgument)
            {
                suggestions.Add("Check the function's argument count and allowed value range");
            }
            else if (errorCode == FeiSharpErrorCodes.TypeMismatch)
            {
                suggestions.Add("Convert the value to the type required by this operation");
            }
            else if (errorCode == FeiSharpErrorCodes.IndexOutOfRange)
            {
                suggestions.Add("Use an index between zero and the collection length minus one");
            }
            else if (errorCode == FeiSharpErrorCodes.NullReference)
            {
                suggestions.Add("Check for null with isNull(value) before member or index access");
            }
            else if (errorCode == FeiSharpErrorCodes.ReadonlyViolation)
            {
                suggestions.Add("Do not assign to const or readonly values after declaration");
            }
            else if (errorCode == FeiSharpErrorCodes.NamespaceNotImported)
            {
                suggestions.Add("Import the required namespace before using this API class");
                suggestions.Add("Use 'using FeiSharp.IO;' for file APIs or 'using FeiSharp.Net;' for network APIs");
            }
            else if (errorCode == FeiSharpErrorCodes.FileOperationFailed)
            {
                suggestions.Add("Check that the path exists and the process has permission to access it");
            }
            else if (errorCode == FeiSharpErrorCodes.NetworkRequestFailed)
            {
                suggestions.Add("Check the URL, protocol, network connection, and remote server status");
            }
            else if (errorCode == FeiSharpErrorCodes.NetworkTimeout)
            {
                suggestions.Add("Retry later or check whether the remote server responds within 30 seconds");
            }

            return suggestions;
        }

        internal void PrintPanel()
        {
            var panel = new Panel(new Markup(stringBuilder.ToString()))
                .Header("[bold cyan]FeiSharp Compilation Error[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Color.Cyan1)
                .Padding(1, 0);
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        private static string NormalizeErrorCode(string code, string message)
        {
            string normalized = code.ToUpperInvariant();
            if (normalized != "FS2003")
                return normalized;

            string text = message.ToLowerInvariant();
            if (text.Contains("return can only") || text.Contains("break can only") || text.Contains("continue can only"))
                return FeiSharpErrorCodes.InvalidControlFlow;
            if (text.Contains("const") || text.Contains("readonly") || text.Contains("assignment"))
                return FeiSharpErrorCodes.InvalidAssignment;
            if (text.Contains("declaration") || text.Contains("visibility") || text.Contains("modifier"))
                return FeiSharpErrorCodes.InvalidDeclaration;
            if (text.Contains("expected") || text.Contains("excepted") || text.Contains("unvalid token"))
            {
                if (text.Contains("'('") || text.Contains("')'") || text.Contains("'['")
                    || text.Contains("']'") || text.Contains("'{'") || text.Contains("'}'")
                    || text.Contains("','") || text.Contains("'.'"))
                    return FeiSharpErrorCodes.MissingDelimiter;
                return FeiSharpErrorCodes.UnexpectedToken;
            }
            return normalized;
        }

        private static string GetFriendlyErrorName(string code, string errorType)
        {
            return code switch
            {
                "FS2001" => "意外的 Token",
                "FS2002" => "缺少表达式",
                "FS2004" => "缺少分隔符",
                "FS2005" => "声明错误",
                "FS2006" => "控制流错误",
                "FS2007" => "赋值错误",
                "FS3004" => "未定义符号",
                "FS3005" => "参数无效",
                "FS3006" => "类型不匹配",
                "FS3007" => "索引越界",
                "FS3008" => "空引用",
                "FS3009" => "只读值被修改",
                _ => errorType
            };
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
            PrintPanel();
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
