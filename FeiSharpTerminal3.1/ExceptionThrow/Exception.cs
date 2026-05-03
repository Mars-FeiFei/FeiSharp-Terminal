using FeiSharpStudio;
using System.Text;
namespace FeiSharpTerminal3._1.ExceptionThrow
{
    internal class Exception : System.Exception
    {
        public StringBuilder stringBuilder { get; private set; }
        public Exception(List<Token> tokens, int current, string message, string code = "FS1001") : base(message)
        {
            try
            {
                var semicolonIndices = tokens
                    .Select((token, index) => (token, index))
                    .Where(x => x.token.Value == ";" && x.token.Type == TokenTypes.Punctuation)
                    .Select(x => x.index)
                    .ToList();
                int currentLine = semicolonIndices.FindLastIndex(i => i <= current) + 1;
                int startLine = Math.Max(0, currentLine - 2);
                int endLine = Math.Min(semicolonIndices.Count - 1, currentLine + 2);
                int startToken = startLine == 0 ? 0 : semicolonIndices[startLine - 1] + 1;
                int endToken = semicolonIndices[endLine];
                var contextTokens = tokens.Skip(startToken).Take(endToken - startToken + 1).ToList();
                var sb = new StringBuilder();
                sb.AppendLine($"\n[Parser Error]: {code.ToUpperInvariant()}: {message}, help link: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{code.ToLowerInvariant()}");
                sb.AppendLine($"   --> At line {currentLine + 1}, token: '{tokens[current].Value}'");
                int lineStart = startToken;
                for (int line = startLine; line <= endLine; line++)
                {
                    int lineEnd = semicolonIndices[line];
                    var lineTokens = tokens.Skip(lineStart).Take(lineEnd - lineStart + 1).ToList();
                    sb.Append($"   {line + 1} | ");
                    foreach (var token in lineTokens)
                    {
                        sb.Append(token.Value != ";" ? token.Value  + " " : token.Value);
                    }
                    sb.AppendLine();
                    if (line == currentLine)
                    {
                        int errorPos = current - lineStart;
                        string errorTokenValue = tokens[current].Value;
                        sb.AppendLine($"     | {new string(' ', errorPos)}{new string('^', errorTokenValue.Length)}");
                        sb.AppendLine($"     | {new string(' ', errorPos)}^-- {message}");
                    }
                    lineStart = lineEnd + 1;
                }
                stringBuilder = sb;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(sb.ToString());
                Console.ResetColor();
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Parser Error]: {code.ToUpperInvariant()}: {message}, help link: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{code.ToLowerInvariant()} (failed to display context)");
                stringBuilder = new StringBuilder().Append($"[Parser Error]: {code.ToUpperInvariant()}: {message}, help link: https://mars-feifei.github.io/feitools.github.io/feisharp/documents/learn/#{code.ToLowerInvariant()}");
                Console.ResetColor();
            }
        }
    }
}
