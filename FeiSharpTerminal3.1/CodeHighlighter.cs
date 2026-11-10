using FeiSharpStudio;
using System.Text;
using Spectre.Console;

namespace FeiSharpTerminal3._1.ExceptionThrow
{
    public static class CodeHighlighter
    {
        private const string Reset = "[/]";
        private const string Dim = "[dim]";
        private const string Red = "[bold red]";
        private const string Yellow = "[yellow]";
        private const string Green = "[green]";
        private const string Cyan = "[cyan]";
        private const string Blue = "[blue]";
        private const string Magenta = "[magenta]";

        public static List<(int Start, int End)> SplitLines(List<Token> tokens)
        {
            var lines = new List<(int Start, int End)>();
            int lineStart = 0;
            int line = tokens[0].Line > 0 ? tokens[0].Line : 1;

            for (int i = 1; i < tokens.Count; i++)
            {
                int tokenLine = tokens[i].Line > 0 ? tokens[i].Line : line;
                if (tokenLine != line)
                {
                    lines.Add((lineStart, i - 1));
                    lineStart = i;
                    line = tokenLine;
                }
            }
            lines.Add((lineStart, tokens.Count - 1));
            return lines;
        }

        public static void AppendHighlightedCode(
            StringBuilder sb,
            List<Token> tokens,
            int anchorIndex,
            int contextRadius,
            int sourceLineOffset,
            bool includeHeader,
            bool showPointer)
        {
            if (tokens == null || tokens.Count == 0)
                return;

            anchorIndex = Math.Clamp(anchorIndex, 0, tokens.Count - 1);

            // Tokens from Lexer retain their original source span.  Rendering that
            // source is essential: reconstructing a line from Token.Value drops
            // quotes, comments and invalid characters, moving the underline away
            // from the actual error.
            if (TryGetSource(tokens, anchorIndex, out string source))
            {
                AppendSourceHighlightedCode(
                    sb, tokens[anchorIndex], source, contextRadius, sourceLineOffset,
                    includeHeader, showPointer);
                return;
            }

            var allLines = SplitLines(tokens);
            int anchorLine = allLines.FindIndex(l => l.Start <= anchorIndex && anchorIndex <= l.End);
            if (anchorLine < 0) anchorLine = Math.Max(0, allLines.Count - 1);

            int column = Math.Max(1, tokens[anchorIndex].Column);

            if (includeHeader)
            {
                sb.AppendLine();
                sb.AppendLine($"{Blue}Code snippet:{Reset}");
            }

            int startLine, endLine;
            if (contextRadius < 0)
            {
                startLine = 0;
                endLine = allLines.Count - 1;
            }
            else
            {
                startLine = Math.Max(0, anchorLine - contextRadius);
                endLine = Math.Min(allLines.Count - 1, anchorLine + contextRadius);
            }

            for (int line = startLine; line <= endLine; line++)
            {
                var lineTokens = tokens
                    .Skip(allLines[line].Start)
                    .Take(allLines[line].End - allLines[line].Start + 1)
                    .ToList();

                if (lineTokens.Count == 0) continue;

                int sourceLineNumber = lineTokens[0].Line > 0
                    ? lineTokens[0].Line
                    : line + 1;

                if (sourceLineNumber <= sourceLineOffset) continue;

                int displayLineNum = Math.Max(1, sourceLineNumber - sourceLineOffset);
                string lineNumStr = displayLineNum.ToString().PadLeft(3);

                string rawContent = BuildRawLine(lineTokens);
                string trimmedContent = rawContent.TrimEnd();

                sb.Append($"{Dim}{lineNumStr} │{Reset} ");
                sb.Append(HighlightTokens(lineTokens));
                sb.AppendLine();

                if (showPointer && line == anchorLine)
                {
                    int errorColumn = column - 1;
                    string pointer = GeneratePointer(trimmedContent, tokens[anchorIndex].Value, errorColumn);
                    sb.Append($"{Dim}    │{Reset} {Red}{pointer}{Reset}");
                    sb.AppendLine();
                }
            }
        }

        public static void AppendHighlightedCode(
            StringBuilder sb,
            List<Token> tokens,
            int current,
            int contextRadius,
            int sourceLineOffset,
            bool includeHeader)
        {
            AppendHighlightedCode(sb, tokens, current, contextRadius, sourceLineOffset, includeHeader, showPointer: true);
        }

        public static void AppendHighlightedCode(
            StringBuilder sb,
            string code,
            int errorLine,
            int errorColumn,
            int contextRadius = 2,
            int sourceLineOffset = 0,
            bool includeHeader = false)
        {
            var tokens = Tokenize(code);
            if (tokens.Count == 0)
                return;

            int current = FindTokenIndex(tokens, errorLine, errorColumn);
            AppendHighlightedCode(sb, tokens, current, contextRadius, sourceLineOffset, includeHeader, showPointer: true);
        }

        public static void AppendHighlightedCode(
            StringBuilder sb,
            string code,
            int contextRadius = -1,
            int sourceLineOffset = 0,
            bool includeHeader = false)
        {
            var tokens = Tokenize(code);
            if (tokens.Count == 0)
                return;

            AppendHighlightedCode(sb, tokens, 0, contextRadius, sourceLineOffset, includeHeader, showPointer: false);
        }

        public static void PrintHighlightedCode(
            List<Token> tokens,
            int current,
            int contextRadius = 2,
            int sourceLineOffset = 0,
            string? sourceName = null)
        {
            var sb = new StringBuilder();

            if (tokens == null || tokens.Count == 0)
            {
                AnsiConsole.MarkupLine($"{Dim}(no code to display){Reset}");
                return;
            }

            current = Math.Clamp(current, 0, tokens.Count - 1);
            var errorToken = tokens[current];

            var allLines = SplitLines(tokens);
            int currentLine = allLines.FindIndex(l => l.Start <= current && current <= l.End);
            if (currentLine < 0) currentLine = Math.Max(0, allLines.Count - 1);

            int column = Math.Max(1, errorToken.Column);

            string name = string.IsNullOrWhiteSpace(sourceName)
                ? "<script>"
                : Path.GetFileName(sourceName);

            sb.AppendLine();
            sb.AppendLine($"{Blue}Location:{Reset}  {Markup.Escape(name)}:{errorToken.Line}:{column}");
            sb.AppendLine();
            sb.AppendLine($"{Blue}Code snippet:{Reset}");

            AppendHighlightedCode(sb, tokens, current, contextRadius, sourceLineOffset, includeHeader: false, showPointer: true);

            sb.AppendLine();

            var panel = new Panel(new Markup(sb.ToString()))
                .Header("[bold cyan]Code[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Color.Cyan1)
                .Padding(1, 0);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        public static void PrintHighlightedCode(
            List<Token> tokens,
            int contextRadius = -1,
            int sourceLineOffset = 0,
            string? sourceName = null)
        {
            var sb = new StringBuilder();

            if (tokens == null || tokens.Count == 0)
            {
                AnsiConsole.MarkupLine($"{Dim}(no code to display){Reset}");
                return;
            }

            string name = string.IsNullOrWhiteSpace(sourceName)
                ? "<script>"
                : Path.GetFileName(sourceName);

            sb.AppendLine();
            sb.AppendLine($"{Blue}Source:{Reset}  {Markup.Escape(name)}");
            sb.AppendLine();
            sb.AppendLine($"{Blue}Code snippet:{Reset}");

            AppendHighlightedCode(sb, tokens, 0, contextRadius, sourceLineOffset, includeHeader: false, showPointer: false);

            sb.AppendLine();

            var panel = new Panel(new Markup(sb.ToString()))
                .Header("[bold cyan]Code[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Color.Cyan1)
                .Padding(1, 0);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        public static void PrintHighlightedCode(
            string code,
            int errorLine,
            int errorColumn,
            int contextRadius = 2,
            int sourceLineOffset = 0,
            string? sourceName = null)
        {
            var tokens = Tokenize(code);
            if (tokens.Count == 0)
            {
                AnsiConsole.MarkupLine($"{Dim}(no code to display){Reset}");
                return;
            }

            int current = FindTokenIndex(tokens, errorLine, errorColumn);
            PrintHighlightedCode(tokens, current, contextRadius, sourceLineOffset, sourceName);
        }

        public static void PrintHighlightedCode(
            string code,
            int contextRadius = -1,
            int sourceLineOffset = 0,
            string? sourceName = null)
        {
            var tokens = Tokenize(code);
            if (tokens.Count == 0)
            {
                AnsiConsole.MarkupLine($"{Dim}(no code to display){Reset}");
                return;
            }

            PrintHighlightedCode(tokens, contextRadius, sourceLineOffset, sourceName);
        }

        private static List<Token> Tokenize(string code)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(code))
                return tokens;

            var lexer = new Lexer(code);
            try
            {
                while (true)
                {
                    var token = lexer.NextToken();
                    if (token.Type == TokenTypes.EndOfFile)
                        break;
                    tokens.Add(token);
                }
            }
            catch
            {
            }
            return tokens;
        }

        private static int FindTokenIndex(List<Token> tokens, int line, int column)
        {
            int best = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Line < line) continue;
                if (t.Line > line) { if (best == 0) best = i; break; }

                int dist = Math.Abs(t.Column - column);
                if (dist < bestDist) { bestDist = dist; best = i; }
            }

            return best;
        }

        private static string BuildRawLine(List<Token> lineTokens)
        {
            var sb = new StringBuilder();
            int displayColumn = 1;
            foreach (var token in lineTokens)
            {
                int tokenColumn = Math.Max(1, token.Column);
                if (tokenColumn > displayColumn)
                    sb.Append(' ', tokenColumn - displayColumn);
                sb.Append(token.Value);
                displayColumn = tokenColumn + StringWidthHelper.GetDisplayWidth(token.Value ?? "");
            }
            return sb.ToString();
        }

        private static string HighlightTokens(List<Token> tokens)
        {
            var content = new StringBuilder();
            int displayColumn = 1;

            foreach (var token in tokens)
            {
                int tokenColumn = Math.Max(1, token.Column);
                if (tokenColumn > displayColumn)
                    content.Append(' ', tokenColumn - displayColumn);

                string color = token.Type switch
                {
                    TokenTypes.Keyword => Blue,
                    TokenTypes.Number => Yellow,
                    TokenTypes.String or TokenTypes.Character => Green,
                    TokenTypes.Operator => Magenta,
                    TokenTypes.Punctuation => Cyan,
                    TokenTypes.Type => Yellow,
                    _ => Reset
                };

                string escaped = Markup.Escape(token.Value);
                if (color != Reset)
                {
                    content.Append(color).Append(escaped).Append(Reset);
                }
                else
                {
                    content.Append(escaped);
                }

                displayColumn = tokenColumn + StringWidthHelper.GetDisplayWidth(token.Value ?? "");
            }

            return content.ToString();
        }

        private static string GeneratePointer(string lineContent, string errorToken, int column)
        {
            int pointerPos = Math.Max(0, column);
            var pointer = new StringBuilder(new string(' ', pointerPos));
            int tokenWidth = StringWidthHelper.GetDisplayWidth(errorToken ?? "");
            pointer.Append('~', Math.Max(1, tokenWidth));
            return pointer.ToString();
        }

        private static bool TryGetSource(List<Token> tokens, int anchorIndex, out string source)
        {
            source = tokens[anchorIndex].Source ?? tokens.FirstOrDefault(t => t.Source != null)?.Source ?? string.Empty;
            return !string.IsNullOrEmpty(source) && tokens[anchorIndex].Offset >= 0;
        }

        private static void AppendSourceHighlightedCode(
            StringBuilder sb,
            Token errorToken,
            string source,
            int contextRadius,
            int sourceLineOffset,
            bool includeHeader,
            bool showPointer)
        {
            if (includeHeader)
            {
                sb.AppendLine();
                sb.AppendLine($"{Blue}Code snippet:{Reset}");
            }

            var lines = GetSourceLines(source);
            int offset = Math.Clamp(errorToken.Offset, 0, source.Length);
            int anchorLine = FindSourceLine(lines, offset);
            int startLine = contextRadius < 0 ? 0 : Math.Max(0, anchorLine - contextRadius);
            int endLine = contextRadius < 0 ? lines.Count - 1 : Math.Min(lines.Count - 1, anchorLine + contextRadius);

            for (int line = startLine; line <= endLine; line++)
            {
                var sourceLine = lines[line];
                int displayLineNumber = line + 1 - sourceLineOffset;
                if (displayLineNumber <= 0)
                    continue;

                string content = source[sourceLine.Start..sourceLine.End].TrimEnd('\r');
                sb.Append($"{Dim}{displayLineNumber.ToString().PadLeft(3)} │{Reset} ");
                sb.Append(Markup.Escape(ExpandTabs(content)));
                sb.AppendLine();

                if (showPointer && line == anchorLine)
                {
                    int tokenEnd = Math.Clamp(errorToken.Offset + Math.Max(0, errorToken.Length), offset, source.Length);
                    int visibleEnd = Math.Min(tokenEnd, sourceLine.End);
                    int prefixLength = Math.Max(0, offset - sourceLine.Start);
                    string prefix = source.Substring(sourceLine.Start, prefixLength);
                    string marked = source.Substring(offset, Math.Max(0, visibleEnd - offset));
                    int prefixWidth = StringWidthHelper.GetDisplayWidth(ExpandTabs(prefix));
                    int markedWidth = Math.Max(1, StringWidthHelper.GetDisplayWidth(ExpandTabs(marked)));
                    string pointer = new string(' ', prefixWidth) + new string('~', markedWidth);
                    sb.Append($"{Dim}    │{Reset} {Red}{pointer}{Reset}");
                    sb.AppendLine();
                }
            }
        }

        private static List<(int Start, int End)> GetSourceLines(string source)
        {
            var lines = new List<(int Start, int End)>();
            int start = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != '\n')
                    continue;
                lines.Add((start, i));
                start = i + 1;
            }
            lines.Add((start, source.Length));
            return lines;
        }

        private static int FindSourceLine(List<(int Start, int End)> lines, int offset)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (offset <= lines[i].End || i == lines.Count - 1)
                    return i;
            }
            return lines.Count - 1;
        }

        private static string ExpandTabs(string text)
        {
            const int TabWidth = 4;
            var result = new StringBuilder();
            int width = 0;
            foreach (char character in text)
            {
                if (character == '\t')
                {
                    int spaces = TabWidth - width % TabWidth;
                    result.Append(' ', spaces);
                    width += spaces;
                }
                else
                {
                    result.Append(character);
                    width += StringWidthHelper.GetDisplayWidth(character.ToString());
                }
            }
            return result.ToString();
        }
    }
}
