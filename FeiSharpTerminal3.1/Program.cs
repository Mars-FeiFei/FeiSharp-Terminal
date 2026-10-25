using FeiSharpStudio;
using FeiSharpTerminal3._1;
using FeiSharpTerminal3._1.Tests;
using Spectre.Console;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FeiSharp8._5RuntimeSdk;
public class Program
{
    static string FEISHARP_IMPORT_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FeiSharpImportFiles");
    public static string MapPath(string vpath, string _applicationPath)
    {
        string path = "";
        if (vpath.StartsWith("~/"))
        {
            path = Path.Combine(Path.GetDirectoryName(_applicationPath), vpath[2..]);
        }
        else if (vpath.StartsWith("$"))
        {
            path = Path.Combine(FEISHARP_IMPORT_PATH, vpath[1..]);
        }
        path = path.Replace("/", "\\");
        return path;
    }
    static string[] SplitCommandArguments(string command)
    {
        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
    private static readonly HashSet<string> SyntaxKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "var", "const", "function", "return", "if", "else", "for", "while", "forEach", "in",
        "class", "public", "private", "protected", "static", "new", "true", "false", "null",
        "import", "using", "break", "continue", "try", "catch", "throw", "when", "otherwise"
    };

    private static readonly (string Name, string Description)[] TerminalCommands =
    [
        ("base", "Show the FeiSharp technology stack"),
        ("build", "Build a .fsc file or .feiproj project"),
        ("cd", "Show or change the current directory"),
        ("cd..", "Change to the parent directory"),
        ("clear", "Clear the terminal (alias for cls)"),
        ("cls", "Clear the terminal"),
        ("color", "Choose the terminal text color"),
        ("copyright", "Show copyright information"),
        ("create", "Create a new FeiSharp project"),
        ("credits", "Show acknowledgements"),
        ("disable", "Disable a FeiSharp runtime option"),
        ("enable", "Enable a FeiSharp runtime option"),
        ("exit", "Exit FeiSharp"),
        ("explorer", "Open a folder in Windows Explorer"),
        ("feedback", "Send feedback to the FeiSharp team"),
        ("file", "Run a FeiSharp source file or project"),
        ("help", "Open command help"),
        ("license", "Show license information"),
        ("open", "Open a folder in Visual Studio Code"),
        ("pwd", "Print the current directory"),
        ("resetColor", "Reset terminal colors"),
        ("run", "Enter interactive code input mode"),
        ("version", "Show or manage SDK version")
    ];
    private const int CommandMenuHeight = 6;
    private const int PromptBottomMargin = CommandMenuHeight + 2;
    private static int? CommandMenuTop;
    private static readonly string StartupDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());

    private static bool IsCommandMenuInput(StringBuilder input) => input.ToString().StartsWith("/", StringComparison.Ordinal);

    private static List<(string Name, string Description)> GetCommandMatches(StringBuilder input)
    {
        if (!IsCommandMenuInput(input)) return [];

        string filter = input.ToString()[1..];
        return TerminalCommands
            .Where(command => command.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void WriteCommandInput(StringBuilder input, string placeholder, int width)
    {
        if (input.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(placeholder.Length > width ? placeholder[..width] : placeholder);
            return;
        }

        string value = input.ToString();
        int visibleLength = Math.Min(value.Length, width);
        int commandLength = 0;
        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            string typedCommand = value[1..].Split(' ', 2)[0];
            if (TerminalCommands.Any(command => command.Name.StartsWith(typedCommand, StringComparison.OrdinalIgnoreCase)))
            {
                commandLength = typedCommand.Length;
            }
        }

        for (int i = 0; i < visibleLength; i++)
        {
            Console.ForegroundColor = i == 0 && value.StartsWith('/')
                ? ConsoleColor.Cyan
                : i >= 1 && i < 1 + commandLength
                    ? ConsoleColor.Cyan
                    : ConsoleColor.White;
            Console.Write(value[i]);
        }
    }
    private static void ClearCommandMenu(int menuTop, int width)
    {
        if (menuTop < 0 || menuTop + CommandMenuHeight > Console.BufferHeight) return;

        Console.ResetColor();
        for (int row = 0; row < CommandMenuHeight; row++)
        {
            Console.SetCursorPosition(0, menuTop + row);
            Console.Write(new string(' ', width));
        }
    }
    private static void ClearActiveCommandMenu()
    {
        if (CommandMenuTop is not int menuTop)
        {
            return;
        }

        ClearCommandMenu(menuTop, Math.Max(1, Console.WindowWidth - 1));
        CommandMenuTop = null;
    }

    private static int GetCommandMenuTop(int inputTop)
    {
        int belowInput = inputTop + 2;
        int firstUnsafeRow = Console.WindowTop + Console.WindowHeight - 1;
        return belowInput + CommandMenuHeight <= firstUnsafeRow
            ? belowInput
            : Math.Max(Console.WindowTop, inputTop - CommandMenuHeight - 1);
    }

    private static void EnsurePromptViewport()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return;
        }

        try
        {
            int firstUnsafeRow = Console.WindowTop + Console.WindowHeight - 1;
            if (Console.CursorTop + PromptBottomMargin > firstUnsafeRow)
            {
                MoveToSafeViewportRow(PromptBottomMargin);
            }
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private static void MoveToSafeViewportRow(int requiredRows)
    {
        try
        {
            for (int row = 0; row <= requiredRows; row++)
            {
                Console.WriteLine();
            }

            int firstUnsafeRow = Console.WindowTop + Console.WindowHeight - 1;
            int targetTop = Math.Max(Console.WindowTop, firstUnsafeRow - requiredRows);
            Console.SetCursorPosition(0, targetTop);
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private static void WriteRelativePrompt()
    {
        string relativePath = Path.GetRelativePath(StartupDirectory, Directory.GetCurrentDirectory())
            .Replace('\\', '/');
        if (!relativePath.Equals(".", StringComparison.Ordinal))
        {
            string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < segments.Length; index++)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(segments[index]);
                if (index < segments.Length - 1)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write('/');
                }
            }
            Console.ResetColor();
        }
        AnsiConsole.Markup("[white]>[/] ");
    }

    private static void WriteRunDuration(TimeSpan elapsed)
    {
        string duration = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s"
            : elapsed.TotalMinutes >= 1
                ? $"{elapsed.Minutes}m {elapsed.Seconds}s"
                : $"{Math.Max(0, elapsed.Seconds)}s";
        string label = $"─ Ran for {duration} ";
        int trailingLineLength = Math.Max(0, Console.WindowWidth - label.Length - 1);
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(label);
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(new string('─', trailingLineLength));
        Console.ResetColor();
    }

    private static void RenderCommandMenu(List<(string Name, string Description)> matches, int selectedIndex, int firstVisible, int menuTop, int width)
    {
        if (matches.Count == 0 || menuTop + CommandMenuHeight > Console.BufferHeight) return;

        for (int row = 0; row < CommandMenuHeight && firstVisible + row < matches.Count; row++)
        {
            var command = matches[firstVisible + row];
            bool selected = firstVisible + row == selectedIndex;
            Console.SetCursorPosition(0, menuTop + row);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = selected ? ConsoleColor.Blue : ConsoleColor.White;
            Console.Write(selected ? "› " : "  ");
            Console.ForegroundColor = selected ? ConsoleColor.Blue : ConsoleColor.White;
            string left = $"/{command.Name}";
            const int commandColumnWidth = 24;
            Console.Write(left.Length >= commandColumnWidth ? left[..commandColumnWidth] : left.PadRight(commandColumnWidth));
            Console.ForegroundColor = ConsoleColor.White;
            int descriptionWidth = Math.Max(0, width - 2 - commandColumnWidth);
            string description = command.Description.Length > descriptionWidth ? command.Description[..descriptionWidth] : command.Description;
            Console.Write(description.PadRight(descriptionWidth));
            Console.ResetColor();
        }
    }

    private static string? ReadCommandInput(string placeholder)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return Console.ReadLine();
        }

        var input = new StringBuilder();
        int cursor = 0;
        int selectedCommand = 0;
        int firstVisibleCommand = 0;
        int inputLeft = Console.CursorLeft;
        int inputTop = Console.CursorTop;
        void Render()
        {
            ClearActiveCommandMenu();
            int width = Math.Max(1, Console.WindowWidth - inputLeft - 1);
            int menuTop = GetCommandMenuTop(inputTop);
            List<(string Name, string Description)> matches = GetCommandMatches(input);
            selectedCommand = matches.Count == 0 ? 0 : Math.Clamp(selectedCommand, 0, matches.Count - 1);
            if (matches.Count == 0)
            {
                firstVisibleCommand = 0;
            }
            else
            {
                int maxFirstVisible = Math.Max(0, matches.Count - CommandMenuHeight);
                firstVisibleCommand = Math.Clamp(firstVisibleCommand, 0, maxFirstVisible);
                if (selectedCommand < firstVisibleCommand)
                {
                    firstVisibleCommand = selectedCommand;
                }
                else if (selectedCommand >= firstVisibleCommand + CommandMenuHeight)
                {
                    firstVisibleCommand = selectedCommand - CommandMenuHeight + 1;
                }
            }
            Console.SetCursorPosition(inputLeft, inputTop);
            WriteCommandInput(input, placeholder, width);
            Console.Write(new string(' ', Math.Max(0, width - Math.Min(input.Length == 0 ? placeholder.Length : input.Length, width))));
            Console.ResetColor();
            if (matches.Count > 0 && menuTop + CommandMenuHeight <= Console.BufferHeight)
            {
                RenderCommandMenu(matches, selectedCommand, firstVisibleCommand, menuTop, Console.WindowWidth - 1);
                CommandMenuTop = menuTop;
            }
            Console.SetCursorPosition(inputLeft + Math.Min(cursor, width - 1), inputTop);
        }

        Render();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            List<(string Name, string Description)> matches = GetCommandMatches(input);
            if (IsCommandMenuInput(input) && matches.Count > 0 && key.Key == ConsoleKey.UpArrow)
            {
                selectedCommand = Math.Max(0, selectedCommand - 1);
                Render();
                continue;
            }
            if (IsCommandMenuInput(input) && matches.Count > 0 && key.Key == ConsoleKey.DownArrow)
            {
                selectedCommand = Math.Min(matches.Count - 1, selectedCommand + 1);
                Render();
                continue;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                bool selectedCommandAlreadyCompleted = matches.Count > 0 &&
                    input.ToString().Equals($"/{matches[selectedCommand].Name}", StringComparison.OrdinalIgnoreCase);
                if (IsCommandMenuInput(input) && matches.Count > 0 && !selectedCommandAlreadyCompleted)
                {
                    input.Clear();
                    input.Append('/').Append(matches[selectedCommand].Name);
                    cursor = input.Length;
                    Render();
                    continue;
                }
                ClearActiveCommandMenu();
                Console.ResetColor();
                Console.SetCursorPosition(inputLeft + Math.Min(cursor, Math.Max(0, Console.WindowWidth - inputLeft - 2)), inputTop);
                Console.WriteLine();
                return input.ToString();
            }
            if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0))
            {
                ClearActiveCommandMenu();
                Console.ResetColor();
                Console.SetCursorPosition(inputLeft + Math.Min(cursor, Math.Max(0, Console.WindowWidth - inputLeft - 2)), inputTop);
                Console.WriteLine();
                return string.Empty;
            }
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow: cursor = Math.Max(0, cursor - 1); break;
                case ConsoleKey.RightArrow: cursor = Math.Min(input.Length, cursor + 1); break;
                case ConsoleKey.Home: cursor = 0; break;
                case ConsoleKey.End: cursor = input.Length; break;
                case ConsoleKey.Backspace:
                    if (cursor > 0) input.Remove(--cursor, 1);
                    break;
                case ConsoleKey.Delete:
                    if (cursor < input.Length) input.Remove(cursor, 1);
                    break;
                default:
                    if (!char.IsControl(key.KeyChar) && (key.Modifiers & ConsoleModifiers.Control) == 0)
                    {
                        input.Insert(cursor++, key.KeyChar);
                    }
                    break;
            }
            Render();
        }
    }
    static string GetUserCode()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return GetUserCodeFromRedirectedInput();
        }

        EnsureCodeEditorViewport();
        var code = new StringBuilder();
        int cursor = 0;
        int editorTop = Console.CursorTop;
        int renderedEditorRows = 0;
        int outputTop = editorTop;
        while (true)
        {
            if (ExecutionCancellation.IsCancellationRequested)
            {
                return "";
            }

            RenderCodeEditor(code.ToString(), cursor, editorTop, ref renderedEditorRows, out outputTop);
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                return GetUserCodeFromRedirectedInput();
            }

            bool shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
            bool control = (key.Modifiers & ConsoleModifiers.Control) != 0;
            if ((key.Key == ConsoleKey.C && control) || key.Key == ConsoleKey.Escape)
            {
                Console.SetCursorPosition(0, outputTop);
                AnsiConsole.MarkupLine("[yellow]Code input cancelled[/]");
                return "";
            }
            if (key.Key == ConsoleKey.Enter && !shift)
            {
                string result = code.ToString();
                Console.SetCursorPosition(0, outputTop);
                return result.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) ? "" : result;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                code.Insert(cursor, Environment.NewLine);
                cursor += Environment.NewLine.Length;
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    cursor = Math.Max(0, cursor - 1);
                    break;
                case ConsoleKey.RightArrow:
                    cursor = Math.Min(code.Length, cursor + 1);
                    break;
                case ConsoleKey.Home:
                    cursor = GetLineStart(code, cursor);
                    break;
                case ConsoleKey.End:
                    cursor = GetLineEnd(code, cursor);
                    break;
                case ConsoleKey.UpArrow:
                    cursor = MoveCursorVertically(code, cursor, -1);
                    break;
                case ConsoleKey.DownArrow:
                    cursor = MoveCursorVertically(code, cursor, 1);
                    break;
                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        int removeLength = cursor >= 2 && code[cursor - 2] == '\r' && code[cursor - 1] == '\n' ? 2 : 1;
                        code.Remove(cursor - removeLength, removeLength);
                        cursor -= removeLength;
                    }
                    break;
                case ConsoleKey.Delete:
                    if (cursor < code.Length)
                    {
                        int removeLength = cursor + 1 < code.Length && code[cursor] == '\r' && code[cursor + 1] == '\n' ? 2 : 1;
                        code.Remove(cursor, removeLength);
                    }
                    break;
                case ConsoleKey.Tab:
                    code.Insert(cursor, "    ");
                    cursor += 4;
                    break;
                default:
                    if (!control && key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                    {
                        code.Insert(cursor++, key.KeyChar);
                    }
                    break;
            }
        }
    }

    private static void EnsureCodeEditorViewport()
    {
        const int InitialEditorRows = 7;
        int firstUnsafeRow = Console.WindowTop + Console.WindowHeight - 1;
        if (Console.CursorTop + InitialEditorRows > firstUnsafeRow)
        {
            MoveToSafeViewportRow(InitialEditorRows);
        }
    }

    private static string GetUserCodeFromRedirectedInput()
    {
        var code = new StringBuilder();
        string? line;
        while ((line = Console.ReadLine()) is not null && !line.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            code.AppendLine(line);
        }
        return code.ToString();
    }

    private static void ClearCodeEditorArea(int editorTop, int renderedRows)
    {
        int width = Math.Max(1, Console.WindowWidth - 1);
        for (int row = 0; row < renderedRows && editorTop + row < Console.BufferHeight; row++)
        {
            Console.SetCursorPosition(0, editorTop + row);
            Console.Write(new string(' ', width));
        }
    }

    private static void RenderCodeEditor(string code, int cursor, int editorTop, ref int renderedRows, out int outputTop)
    {
        ClearCodeEditorArea(editorTop, renderedRows);
        Console.SetCursorPosition(0, editorTop);
        AnsiConsole.Write(new Rule("[deepskyblue1]FeiSharp Code Runner[/]")
        {
            Style = Style.Parse("deepskyblue1"),
            Justification = Justify.Center
        });
        AnsiConsole.MarkupLine("[grey]Enter[/] run  [grey]•[/]  [grey]Shift+Enter[/] new line  [grey]•[/]  [grey]Esc / Ctrl+C[/] cancel");
        AnsiConsole.MarkupLine("[grey]Arrow keys, Home/End, Backspace and Delete work like a text editor.[/]");
        Console.WriteLine();

        string normalized = code.Replace("\r\n", "\n");
        string[] lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"{i + 1,3} > ");
            Console.ResetColor();
            WriteHighlightedCodeLine(lines[i]);
            Console.ResetColor();
            Console.WriteLine();
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("─────┴────────────────────────────────────────────────────────");
        Console.WriteLine("Ready to run");
        Console.ResetColor();
        renderedRows = 6 + lines.Length;
        outputTop = Console.CursorTop;

        int lineIndex = code.Take(cursor).Count(c => c == '\n');
        int lineStart = GetLineStart(new StringBuilder(code), cursor);
        int column = GetDisplayWidth(code.AsSpan(lineStart, cursor - lineStart));
        try
        {
            int left = Math.Min(6 + column, Math.Max(0, Console.WindowWidth - 1));
            int top = editorTop + 4 + lineIndex;
            Console.SetCursorPosition(left, top);
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }
    private static void WriteHighlightedCodeLine(string line)
    {
        for (int index = 0; index < line.Length;)
        {
            if (index + 1 < line.Length && line[index] == '/' && line[index + 1] == '/')
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write(line[index..]);
                return;
            }
            if (line[index] == '"')
            {
                int end = index + 1;
                while (end < line.Length)
                {
                    if (line[end] == '"' && line[end - 1] != '\\')
                    {
                        end++;
                        break;
                    }
                    end++;
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(line[index..end]);
                index = end;
                continue;
            }
            if (char.IsLetter(line[index]) || line[index] == '_')
            {
                int end = index + 1;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_')) end++;
                string word = line[index..end];
                int next = end;
                while (next < line.Length && char.IsWhiteSpace(line[next])) next++;
                bool isMethodName = next < line.Length && line[next] == '(';
                Console.ForegroundColor = SyntaxKeywords.Contains(word)
                    ? ConsoleColor.Cyan
                    : isMethodName ? ConsoleColor.Green : ConsoleColor.White;
                Console.Write(word);
                index = end;
                continue;
            }
            if (char.IsDigit(line[index]))
            {
                int end = index + 1;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.')) end++;
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(line[index..end]);
                index = end;
                continue;
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(line[index++]);
        }
    }
    private static int GetLineStart(StringBuilder code, int cursor)
    {
        int index = Math.Min(cursor, code.Length) - 1;
        while (index >= 0 && code[index] != '\n') index--;
        return index + 1;
    }
    private static int GetLineEnd(StringBuilder code, int cursor)
    {
        int index = Math.Min(cursor, code.Length);
        while (index < code.Length && code[index] != '\r' && code[index] != '\n') index++;
        return index;
    }
    private static int MoveCursorVertically(StringBuilder code, int cursor, int direction)
    {
        int lineStart = GetLineStart(code, cursor);
        int column = GetDisplayWidth(code.ToString(lineStart, cursor - lineStart).AsSpan());
        if (direction < 0)
        {
            if (lineStart == 0) return cursor;
            int previousEnd = lineStart - 1;
            if (previousEnd > 0 && code[previousEnd - 1] == '\r') previousEnd--;
            int previousStart = previousEnd;
            while (previousStart > 0 && code[previousStart - 1] != '\n') previousStart--;
            return GetIndexAtDisplayColumn(code, previousStart, previousEnd, column);
        }

        int currentEnd = GetLineEnd(code, cursor);
        int nextStart = currentEnd;
        if (nextStart < code.Length && code[nextStart] == '\r') nextStart++;
        if (nextStart < code.Length && code[nextStart] == '\n') nextStart++;
        if (nextStart >= code.Length) return cursor;
        int nextEnd = GetLineEnd(code, nextStart);
        return GetIndexAtDisplayColumn(code, nextStart, nextEnd, column);
    }
    private static int GetIndexAtDisplayColumn(StringBuilder code, int start, int end, int targetColumn)
    {
        int index = start;
        int width = 0;
        while (index < end)
        {
            int characterWidth = GetDisplayWidth(code[index]);
            if (width + characterWidth > targetColumn)
            {
                break;
            }
            width += characterWidth;
            index++;
        }
        return index;
    }
    private static int GetDisplayWidth(ReadOnlySpan<char> text)
    {
        int width = 0;
        foreach (char character in text)
        {
            width += GetDisplayWidth(character);
        }
        return width;
    }
    private static int GetDisplayWidth(char character)
    {
        if (character is '\r' or '\n' || char.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
        {
            return 0;
        }

        return character is >= '\u1100' and <= '\u115F'
            or >= '\u2E80' and <= '\uA4CF'
            or >= '\uAC00' and <= '\uD7A3'
            or >= '\uF900' and <= '\uFAFF'
            or >= '\uFE10' and <= '\uFE19'
            or >= '\uFE30' and <= '\uFE6F'
            or >= '\uFF00' and <= '\uFF60'
            or >= '\uFFE0' and <= '\uFFE6'
            ? 2
            : 1;
    }
    public static string _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static Parser _parser;
    static string dynamicTitle = "FeiSharp - Inputing Command";
    const string IN_CMD = "Inputing Command";
    const string EDT_CDE = "Editing Code";
    const string EXEC = "Executing";
    static void ChangeDynamicTitle(string change)
    {
        dynamicTitle = "FeiSharp - " + change;
        Console.Title = dynamicTitle;
    }
    static void TryConfigureConsole(Action configureAction)
    {
        try
        {
            configureAction();
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
    static string? platform = "win-x64";
    static List<Dependency>? depen = new();
    static bool? isCleanBuildDirectory = false;
    static bool? isTrimmedByDotnet = false;
    static bool? isOnlyCopyExecutable = false;
    static string? ResolveFeiSharpSourcePath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }
        string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
        if (!File.Exists(fullInputPath))
        {
            return null;
        }
        if (!string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
        {
            return fullInputPath;
        }
        FeiSharpProjectFile? projectFile = JsonSerializer.Deserialize(
            File.ReadAllText(fullInputPath),
            FeiSharpJsonSerializerContext.Default.FeiSharpProjectFile);
        if (string.IsNullOrWhiteSpace(projectFile?.ProjectMainFile))
        {
            return null;
        }
        platform = !string.IsNullOrEmpty(projectFile.TargetPlatform) ? projectFile.TargetPlatform : platform;
        depen = projectFile.Dependencies ?? depen;
        isCleanBuildDirectory = projectFile.IsCleanBuildDirectory ?? false;
        isTrimmedByDotnet = projectFile.IsTrimmedByDotnet ?? false;
        isOnlyCopyExecutable = projectFile.IsOnlyCopyExecutable ?? false;
        FeiSharpProgramData.main_function_run = projectFile.IsMainFunctionRun ?? FeiSharpProgramData.main_function_run;
        string? mainFile = projectFile.ProjectMainFile;
        if (string.IsNullOrWhiteSpace(mainFile))
        {
            return null;
        }
        string projectDirectory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string resolvedMainFile = Path.GetFullPath(Path.Combine(projectDirectory, mainFile));
        return File.Exists(resolvedMainFile) ? resolvedMainFile : null;
    }
    static string GetBuildBaseDirectory(string inputPath, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
            if (File.Exists(fullInputPath) && string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
            }
        }
        return Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
    }
    static string GetBuildOutputName(string inputPath, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
            if (File.Exists(fullInputPath) && string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    FeiSharpProjectFile? projectFile = JsonSerializer.Deserialize(
                        File.ReadAllText(fullInputPath),
                        FeiSharpJsonSerializerContext.Default.FeiSharpProjectFile);
                    if (!string.IsNullOrWhiteSpace(projectFile?.ProjectName))
                    {
                        string? projectName = projectFile.ProjectName;
                        if (!string.IsNullOrWhiteSpace(projectName))
                        {
                            return string.Concat(projectName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
                        }
                    }
                }
                catch
                {
                }
            }
        }
        return Path.GetFileNameWithoutExtension(sourcePath);
    }
    static string ResolveBuildOutputPath(string sourcePath, string? requestedOutputPath)
    {
        if (string.IsNullOrWhiteSpace(requestedOutputPath))
        {
            return Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory(),
                Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }
        string trimmedPath = requestedOutputPath.Trim().Trim('"');
        if (trimmedPath.EndsWith("\\") || trimmedPath.EndsWith("/"))
        {
            return Path.Combine(
                Path.GetFullPath(trimmedPath),
                Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }
        string fullPath = Path.GetFullPath(trimmedPath);
        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }
        return string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath + ".exe";
    }
    static string ResolveBuildOutputPath(string inputPath, string sourcePath, string? requestedOutputPath)
    {
        if (string.IsNullOrWhiteSpace(requestedOutputPath))
        {
            string buildBaseDirectory = GetBuildBaseDirectory(inputPath, sourcePath);
            string outputName = GetBuildOutputName(inputPath, sourcePath);
            return Path.Combine(buildBaseDirectory, "bin", "obj", "feisharp.sdk 11.0", outputName + ".exe");
        }
        return ResolveBuildOutputPath(sourcePath, requestedOutputPath);
    }
    static string GetRuntimeAssemblyPath()
    {
        string assemblyName = typeof(Program).Assembly.GetName().Name ?? "feisharp";
        string bundledDllPath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        if (File.Exists(bundledDllPath))
        {
            return bundledDllPath;
        }
        throw new FileNotFoundException("Unable to locate the FeiSharp runtime assembly.", bundledDllPath);
    }
    public static string TransformCode(string code)
    {
        code = CodeTransformer.Transform(code);
        var result = new StringBuilder();
        int i = 0;

        while (i < code.Length)
        {
            char c = code[i];
            if (c != '"')
            {
                result.Append(c);
                i++;
                continue;
            }
            int start = i;
            i++;
            var contentBuilder = new StringBuilder();
            while (i < code.Length)
            {
                char ch = code[i];
                if (ch == '"' && (i == 0 || code[i - 1] != '\\'))
                {
                    break;
                }
                contentBuilder.Append(ch);
                i++;
            }
            string content = contentBuilder.ToString();
            string replacement = ProcessContent(content);
            if (replacement.StartsWith("\"") && replacement.EndsWith("\""))
            {
                replacement = replacement.Substring(1, replacement.Length - 2);
            }
            result.Append("\"" + replacement + "\"");
            if (i < code.Length && code[i] == '"')
                i++;
        }
        return result.ToString();
    }
    private static string ProcessContent(string content)
    {
        var items = new List<(bool isExpr, string text)>();
        int pos = 0;
        while (pos < content.Length)
        {
            int startDollar = content.IndexOf("${", pos);
            if (startDollar == -1)
            {
                if (pos < content.Length)
                    items.Add((false, content.Substring(pos)));
                break;
            }
            if (startDollar > pos)
                items.Add((false, content.Substring(pos, startDollar - pos)));
            int open = startDollar + 2;
            int braceCount = 1;
            int close = -1;
            for (int j = open; j < content.Length; j++)
            {
                if (content[j] == '{')
                    braceCount++;
                else if (content[j] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        close = j;
                        break;
                    }
                }
            }
            if (close == -1)
            {
                items.Add((false, content.Substring(pos)));
                break;
            }
            string expr = content.Substring(open, close - open);
            items.Add((true, expr));
            pos = close + 1;
        }
        if (!items.Exists(item => item.isExpr))
        {
            return "\"" + content + "\"";
        }
        var parts = new List<string>();
        foreach (var item in items)
        {
            if (item.isExpr)
            {
                parts.Add("tostr(" + item.text + ")");
            }
            else
            {
                parts.Add("\"" + EscapeString(item.text) + "\"");
            }
        }

        return string.Join(" + ", parts);
    }
    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
    static string GetWindowsRuntimeIdentifier()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "win-arm64",
            Architecture.X86 => "win-x86",
            _ => "win-x64"
        };
    }
    static bool TryBuildExecutable(string inputPath, string? requestedOutputPath, out string outputExePath, out string errorMessage)
    {
        outputExePath = string.Empty;
        errorMessage = string.Empty;
        string? sourcePath = ProProcesser(ResolveFeiSharpSourcePath(inputPath));
        if (sourcePath == null)
        {
            errorMessage = "The source file or project file could not be found.";
            return false;
        }
        string runtimeAssemblyPath;
        try
        {
            runtimeAssemblyPath = GetRuntimeAssemblyPath();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        outputExePath = ResolveBuildOutputPath(inputPath, sourcePath, requestedOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputExePath) ?? Directory.GetCurrentDirectory());
        string previousApplicationPath = _applicationPath;
        string tempRoot = Path.Combine(Path.GetTempPath(), "FeiSharpBuild", Guid.NewGuid().ToString("N"));
        string tempProjectPath = Path.Combine(tempRoot, "GeneratedLauncher.csproj");
        string tempProgramPath = Path.Combine(tempRoot, "Program.cs");
        string tempBuildDirectory = Path.Combine(tempRoot, "build");
        string tempPublishDirectory = Path.Combine(tempRoot, "publish");
        string assemblyName = Path.GetFileNameWithoutExtension(outputExePath);
        string base64Source;
        try
        {
            _applicationPath = sourcePath;
            string sourceCode = File.ReadAllText(sourcePath);
            string processedSource = ProProcesser(sourceCode);
            base64Source = Convert.ToBase64String(Encoding.UTF8.GetBytes(processedSource));
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to read or preprocess source: {ex.Message}";
            return false;
        }
        finally
        {
            _applicationPath = previousApplicationPath;
        }
        try
        {
            Directory.CreateDirectory(tempRoot);
            string escapedAssemblyName = System.Security.SecurityElement.Escape(assemblyName) ?? assemblyName;
            string escapedRuntimeAssemblyPath = System.Security.SecurityElement.Escape(runtimeAssemblyPath) ?? runtimeAssemblyPath;
            string runtimeIdentifier = GetWindowsRuntimeIdentifier();
            File.WriteAllText(tempProjectPath, $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>{{escapedAssemblyName}}</AssemblyName>
    <UseAppHost>true</UseAppHost>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>{{runtimeIdentifier}}</RuntimeIdentifier>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="feisharp">
      <HintPath>{{escapedRuntimeAssemblyPath}}</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""");

            File.WriteAllText(tempProgramPath, $$"""
using System.Text;
using FeiSharp8._5RuntimeSdk;
using FeiSharpTerminal3._1;
class Program{
    static string FEISHARP_IMPORT_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FeiSharpImportFiles");
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;
        ExecutionCancellation.Initialize();
        FeiSharpProgramData.AssemblyName = Guid.NewGuid().ToString("N");
        FeiSharp8._5RuntimeSdk.Program._applicationPath = AppContext.BaseDirectory;
        var sourceCode = Encoding.UTF8.GetString(Convert.FromBase64String("{{base64Source}}"));
if (Environment.GetEnvironmentVariable("FEISHARP_IMPORT_PATH", EnvironmentVariableTarget.User) == null)
    Environment.SetEnvironmentVariable("FEISHARP_IMPORT_PATH", FEISHARP_IMPORT_PATH, EnvironmentVariableTarget.User);
Directory.CreateDirectory(FEISHARP_IMPORT_PATH);
if (!File.Exists(Path.Combine(FEISHARP_IMPORT_PATH, "$prelude.fsc")))
{
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "prelude.fsc"), "function pow(afghj,beyua)\r\n{\r\n    oldpow(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sin(afghj,beyua)\r\n{\r\n    oldsin(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction cos(afghj,beyua)\r\n{\r\n    oldcos(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction tan(afghj,beyua)\r\n{\r\n    oldtan(\"casdf\", afghj, beyuab);\r\n    return casdf;\r\n}\r\nfunction asin(afghj,beyua)\r\n{\r\n    oldasin(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction acos(afghj,beyua)\r\n{\r\n    oldacos(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction atan(afghj,beyua)\r\n{\r\n    oldatan(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sqrt(afghj,beyua)\r\n{\r\n    oldsqrt(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction abs(afghj,beyua)\r\n{\r\n    oldabs(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction strfromindex(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrfromindex(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strindexof(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldindexof(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strreplace(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrreplafasfasfwfdsafeweeasadce(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad, bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strlen(afasfasfwfdsafeweeasad)\r\n{\r\n    oldstrlen(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction substr(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldsubstr(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction getcurrentfilepath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldgetcurrentfilepath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction getcurrentfolderpath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldgetcurrentfolderpath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction mappath(vpath)\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldmappath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\", vpath);\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nvar sign = \"{#,:,&,|,.,],[,!,<,>,=,^,/,*,-,+,;,(,),$,},//\";\r\nvar runProg = \"feisharp <src-code-file>\";\r\nvar commonSyntax = \"<code>;\\n<function>(args);\\n<function(no args)>;\\nclass.field;\";\r\nvar specSyntax = \"<keyword(def a struct, such as if, while etc.)>;<keyword-first-letter>begin:<code><keyword-first-letter>end;\";");
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "math.fsc"), "function pow(afghj,beyua)\r\n{\r\n    oldpow(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sin(afghj,beyua)\r\n{\r\n    oldsin(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction cos(afghj,beyua)\r\n{\r\n    oldcos(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction tan(afghj,beyua)\r\n{\r\n    oldtan(\"casdf\", afghj, beyuab);\r\n    return casdf;\r\n}\r\nfunction asin(afghj,beyua)\r\n{\r\n    oldasin(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction acos(afghj,beyua)\r\n{\r\n    oldacos(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction atan(afghj,beyua)\r\n{\r\n    oldatan(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sqrt(afghj,beyua)\r\n{\r\n    oldsqrt(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction abs(afghj,beyua)\r\n{\r\n    oldabs(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}");
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "io_extend.fsc"), "function getcurrentfilepath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldgetcurrentfilepath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction getcurrentfolderpath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldgetcurrentfolderpath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction mappath(vpath)\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldmappath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\", vpath);\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}");
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "text.fsc"), "function strfromindex(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrfromindex(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strindexof(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldindexof(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strreplace(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrreplafasfasfwfdsafeweeasadce(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad, bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strlen(afasfasfwfdsafeweeasad)\r\n{\r\n    oldstrlen(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction substr(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldsubstr(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}");
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "support.fsc"), "var sign = \"{#,:,&,|,.,],[,!,<,>,=,^,/,*,-,+,;,(,),$,},//\";\r\nvar runProg = \"feisharp <src-code-file>\";");
    File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "network.fsc"), "");

}
        FeiSharp8._5RuntimeSdk.Program.RunFeiSharpCodeWithProProcesser(sourceCode, args.Select(i => "{{"\\\""}}" + i + "{{"\\\""}}").ToArray());
    }
}

""");
            string[] args = [];
            foreach (var item in depen)
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "fpm.exe";
                    process.StartInfo.Arguments = "source " + item.SrcUrl;
                    process.Start();
                    process.WaitForExit();
                }
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "fpm.exe";
                    process.StartInfo.Arguments = "install " + item.Name;
                    process.Start();
                    process.WaitForExit();
                }
            }
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "fpm.exe";
                process.StartInfo.Arguments = "source default";
                process.Start();
                process.WaitForExit();
            }
            if ((bool)isCleanBuildDirectory && Directory.Exists(Path.GetDirectoryName(outputExePath)))
            {
                foreach (var item in Directory.GetFiles(Path.GetDirectoryName(outputExePath)))
                {
                    File.Delete(item);
                }
            }
            string argument = $"publish -c Release -r {platform} -p:PublishAot=true -p:OptimizationPreference=Size -p:DebugType=none -p:InvariantGlobalization=true -o " + tempBuildDirectory;
            if ((bool)isTrimmedByDotnet)
            {
                AnsiConsole.MarkupLine("[yellow]Warning: Use trimmed_by_dotnet must ensure the target machine has installed .NET 8.0[/]");
                argument = $"build -c Release -r {platform} -p:PublishSingleFile=true -p:OutputPath={tempBuildDirectory}";
            }
            var buildProcess = new Process();
            buildProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = argument,
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            buildProcess.Start();
            string buildStandardOutput = buildProcess.StandardOutput.ReadToEnd();
            string buildStandardError = buildProcess.StandardError.ReadToEnd();
            buildProcess.WaitForExit();
            if (buildProcess.ExitCode != 0)
            {
                errorMessage = string.IsNullOrWhiteSpace(buildStandardError) ? buildStandardOutput : buildStandardError;
                return false;
            }
            string publishedExePath = Path.Combine(tempBuildDirectory, assemblyName + ".exe");
            if (!File.Exists(publishedExePath))
            {
                errorMessage = $"Build succeeded, but the generated exe was not found at {publishedExePath}.";
                return false;
            }
            if ((bool)isOnlyCopyExecutable && (bool)isTrimmedByDotnet)
            {
                errorMessage = $"trimmed_by_dotnet and only_copy_executable cannot both be true";
                return false;
            }
            if ((bool)isOnlyCopyExecutable)
            {
                File.Copy(publishedExePath, outputExePath, true);
                return true;
            }
            foreach (string file in Directory.GetFiles(tempBuildDirectory))
            {
                File.Copy(file, Path.Combine(Path.GetDirectoryName(outputExePath), Path.GetFileName(file)), true);
            }
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch
            {
            }
        }
    }

    public static async Task Main(string[] args)
    {
        #region <head></head>
        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;
        ExecutionCancellation.Initialize();
        string? configuredImportPath = Environment.GetEnvironmentVariable("FeiSharpImportPat")
            ?? Environment.GetEnvironmentVariable("FEISHARP_IMPORT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredImportPath))
            FEISHARP_IMPORT_PATH = Path.GetFullPath(configuredImportPath);
        Directory.CreateDirectory(FEISHARP_IMPORT_PATH);
        string extensionDirectory = Path.Combine(FEISHARP_IMPORT_PATH, "extension");
        Directory.CreateDirectory(extensionDirectory);
        string baseKeywordPath = Path.Combine(extensionDirectory, "BaseKeyword.dll");
        if (!Path.GetFullPath(baseKeywordPath).Equals(typeof(BaseKeyword).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            File.Copy(typeof(BaseKeyword).Assembly.Location, baseKeywordPath, overwrite: true);
        ExtensionKeywordRegistry.Load(FEISHARP_IMPORT_PATH);
        if (Environment.GetEnvironmentVariable("FEISHARP_IMPORT_PATH", EnvironmentVariableTarget.User) == null)
            Environment.SetEnvironmentVariable("FEISHARP_IMPORT_PATH", FEISHARP_IMPORT_PATH, EnvironmentVariableTarget.User);
        Directory.CreateDirectory(FEISHARP_IMPORT_PATH);
        if (!File.Exists(Path.Combine(FEISHARP_IMPORT_PATH, "prelude.fsc")))
        {
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "prelude.fsc"), "function pow(afghj,beyua)\r\n{\r\n    oldpow(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sin(afghj)\r\n{\r\n    oldsin(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction cos(afghj)\r\n{\r\n    oldcos(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction tan(afghj)\r\n{\r\n    oldtan(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction asin(afghj)\r\n{\r\n    oldasin(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction acos(afghj)\r\n{\r\n    oldacos(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction atan(afghj)\r\n{\r\n    oldatan(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction sqrt(afghj,beyua)\r\n{\r\n    oldsqrt(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction abs(afghj)\r\n{\r\n    oldabs(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction strFromIndex(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldStrFromIndex(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strIndexOf(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldIndexOf(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strReplace(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldStrReplace(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad, bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strLen(afasfasfwfdsafeweeasad)\r\n{\r\n    oldStrLen(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction subStr(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldSubStr(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction getCurrentFilePath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldGetCurrentFilePath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction getCurrentFolderPath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldGetCurrentFolderPath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction mapPath(vpath)\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldMapPath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\", vpath);\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nvar sign = \"{#,:,&,|,.,],[,!,<,>,=,^,/,*,-,+,;,(,),$,},//\";\r\nvar runProg = \"feisharp <src-code-file>\";\r\nvar commonSyntax = \"<code>;\\n<function>(args);\\n<function(no args)>;\\nclass.field;\";\r\nvar specSyntax = \"<keyword(def a struct, such as if, while etc.)>;<keyword-first-letter>begin:<code><keyword-first-letter>end;\";");
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "math.fsc"), "function pow(afghj,beyua)\r\n{\r\n    oldpow(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction sin(afghj)\r\n{\r\n    oldsin(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction cos(afghj)\r\n{\r\n    oldcos(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction tan(afghj)\r\n{\r\n    oldtan(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction asin(afghj)\r\n{\r\n    oldasin(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction acos(afghj)\r\n{\r\n    oldacos(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction atan(afgh)\r\n{\r\n    oldatan(\"casdf\", afghj);\r\n    return casdf;\r\n}\r\nfunction sqrt(afghj,beyua)\r\n{\r\n    oldsqrt(\"casdf\", afghj, beyua);\r\n    return casdf;\r\n}\r\nfunction abs(afghj)\r\n{\r\n    oldabs(\"casdf\", afghj);\r\n    return casdf;\r\n}");
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "io_extend.fsc"), "function getCurrentFilePath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldGetCurrentFilePath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction getCurrentFolderPath()\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldGetCurrentFolderPath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\");\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}\r\nfunction mapPath(vpath)\r\n{\r\n    var ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga = \"\";\r\n    oldMapPath(\"ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga\", vpath);\r\n    return ahsjakfgskfgjagfuwukagsjfjaaskgfhlskwwga;\r\n}");
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "text.fsc"), "function strfromindex(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrfromindex(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strindexof(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldindexof(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strreplace(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldstrreplafasfasfwfdsafeweeasadce(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad, bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction strlen(afasfasfwfdsafeweeasad)\r\n{\r\n    oldstrlen(\"ccasfasgfagwgeegta\", afasfasfwfdsafeweeasad);\r\n    return ccasfasgfagwgeegta;\r\n}\r\nfunction substr(afasfasfwfdsafeweeasad,bahsdhfhashdhsahdaf)\r\n{\r\n    oldsubstr(afasfasfwfdsafeweeasad, \"ccasfasgfagwgeegta\", bahsdhfhashdhsahdaf);\r\n    return ccasfasgfagwgeegta;\r\n}");
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "support.fsc"), "var sign = \"{#,:,&,|,.,],[,!,<,>,=,^,/,*,-,+,;,(,),$,},//\";\r\nvar runProg = \"feisharp <src-code-file>\";");
            File.WriteAllText(Path.Combine(FEISHARP_IMPORT_PATH, "network.fsc"), "");

        }
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            AnsiConsole.MarkupLine("[yellow]FeiSharp Target SDK 11.0[/]");
            AnsiConsole.MarkupLine("[grey]Copyright (c) 2026 Feifei Ganxiner[/]");
            AnsiConsole.MarkupLine("[grey]Apache License 2.0[/]");
            return;
        }
        if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
        {
            AnsiConsole.MarkupLine("[cyan]Usage:[/]");
            AnsiConsole.MarkupLine("  [yellow]feisharp <file.fsc>[/]          Run a FeiSharp script file");
            AnsiConsole.MarkupLine("  [yellow]feisharp build <file>[/]        Build executable from .fsc or .feiproj");
            AnsiConsole.MarkupLine("  [yellow]feisharp create <type> <name>[/] Create a new project");
            AnsiConsole.MarkupLine("  [yellow]feisharp --version[/]           Show version information");
            AnsiConsole.MarkupLine("  [yellow]feisharp --help[/]              Show this help message");
            AnsiConsole.MarkupLine("  [yellow]For additional information, please type 'help' while in REPL mode");
            AnsiConsole.MarkupLine("\n[grey]Project types: Console, API, Data, fUnitTest[/]");
            return;
        }
        //if (args.Length == 0)
            //FeiSharpTests.RunAllTests();
        TryConfigureConsole(() => Console.CursorSize = 25);
        FeiSharpProgramData.AssemblyName = Guid.NewGuid().ToString("N");
        TryConfigureConsole(() => Console.CursorVisible = true);
        TryConfigureConsole(() => Console.Title = dynamicTitle);


        AnsiConsole.Write(
            new FigletText("FEI# Target SDK 11.0")
                .Color(Color.Cyan1));

        var rule = new Rule($"[yellow]Version 11.0[/]")
        {
            Style = Style.Parse("blue"),
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);


        var infoPanel = new Panel(
            "[grey]FeiSharp 11.0 (tags/v11.0, Jul 26 2026, 13:06:58) MSC v.2026 64 bit (AMD64) on win32[/]\n" +
            "[grey]Type [green]\"help\"[/], [green]\"copyright\"[/], [green]\"credits\"[/] or [green]\"license\"[/] for more information.[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Padding(1, 1, 1, 1);

        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        #endregion

        if (args.Length > 0 && string.Equals(args[0], "--build", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                var usagePanel = new Panel("[red]Usage: feisharp build <source.fsc|project.feiproj> [[output.exe]][/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red"));
                AnsiConsole.Write(usagePanel);
                return;
            }

            string? outputArg = "out\\feisharp-sdk-11.0-release\\" + (args.Length > 2 ? args[2] : null);
            await AnsiConsole.Status()
                .StartAsync("[yellow]Building executable...[/]", async ctx =>
                {
                    if (TryBuildExecutable(args[1], outputArg, out string builtExePath, out string buildError))
                    {
                        var successPanel = new Panel($"[green]✓ Build completed:[/] [yellow]{builtExePath}[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("green"));
                        AnsiConsole.Write(successPanel);
                        Process.Start(builtExePath);
                    }
                    else
                    {
                        var errorPanel = new Panel($"[red]✗ Build failed:[/] {buildError.EscapeMarkup()}")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("red"));
                        AnsiConsole.Write(errorPanel);
                    }

                    await Task.CompletedTask;
                });
            return;
        }
        else if (args.Length == 3 && string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
        {
            string type = args[1];
            string name = args[2];
            switch (type)
            {
                case "Console":
                    Directory.CreateDirectory(name);
                    Directory.SetCurrentDirectory(name);
                    File.WriteAllText($"test_{name}1.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\n");
                    File.WriteAllText($"main_{name}.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction main()\n{\n    printNl(\"Hello, World!\");\n}\n");
                    File.WriteAllText("data.fsc", "//Set your data in this file");
                    File.WriteAllText("api.fsc", "//Create your own API functions in this file");
                    File.WriteAllText("README.md", $"This is the console project {name}");
                    File.WriteAllText($"{name}.log", $"{DateTime.Now} Create console project {name} by FeiSharp Target SDK 11.0\n");
                    File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                    File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                    File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                    break;
                case "API":
                    Directory.CreateDirectory(name);
                    Directory.SetCurrentDirectory(name);
                    File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                    File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                    File.WriteAllText("data.fsc", "//Set your data in this file");
                    File.WriteAllText("api.fsc", "//Create your own API functions in this file");
                    File.WriteAllText("README.md", $"This is the api project {name}");
                    File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                    File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                    File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                    break;
                case "Data":
                    Directory.CreateDirectory(name);
                    Directory.SetCurrentDirectory(name);
                    File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                    File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                    File.WriteAllText("data.fsc", "//Set your data in this file");
                    File.WriteAllText("README.md", $"This is the api project {name}");
                    File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                    File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                    File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                    break;
                case "fUnitTest":
                    Directory.CreateDirectory(name);
                    Directory.SetCurrentDirectory(name);
                    File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                    File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/test_content.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                    File.WriteAllText("test_content.fsc", "//Set the test content");
                    File.WriteAllText("README.md", $"This is the api project {name}");
                    File.WriteAllText(".gitignore", @"
ouy/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                    File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                    File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                    break;
                default:
                    var errorPanel = new Panel("[red]✗ The type isn't correct[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("red"));
                    AnsiConsole.Write(errorPanel);
                    break;
            }
        }
        if (args.Length > 0)
        {
            string? sourcePath = ResolveFeiSharpSourcePath(args[0]);
            if (sourcePath == null)
            {
                var errorPanel = new Panel($"[red]✗ File not found:[/] {args[0].EscapeMarkup()}")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red"));
                AnsiConsole.Write(errorPanel);
                return;
            }

            global::System.String sourceCode = File.ReadAllText(sourcePath);
            _applicationPath = sourcePath;
            RunFeiSharpCodeWithProProcesser(sourceCode, args.Skip(1).ToArray());
            AnsiConsole.Markup("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return;
        }
        FeiSharpProgramData.main_function_run = true;
        Console.Clear();
        while (true)
        {
            try
            {
                EnsurePromptViewport();
                WriteRelativePrompt();
                global::System.String? command = null;
                try
                {
                    command = ReadCommandInput("Type / for terminal commands");
                }
                catch (OperationCanceledException)
                {

                    Console.WriteLine();
                    if (ExecutionCancellation._isExecuting)
                    {

                        ExecutionCancellation.CancelExecution();
                        AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                    }
                    continue;
                }


                if (command == null)
                {
                    Console.WriteLine();
                    if (ExecutionCancellation._isExecuting)
                    {
                        ExecutionCancellation.CancelExecution();
                        AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]Use '/exit' to quit FeiSharp[/]");
                    }
                    continue;
                }


                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }
                bool isTerminalCommand = command.StartsWith('/');
                // A leading slash reserves the line for terminal commands.  The
                // sentinel lets the established command dispatch fall through to
                // the code runner unchanged for ordinary FeiSharp input.
                command = isTerminalCommand ? command[1..] : "\u0001" + command;
                var commandParts = SplitCommandArguments(command);
                if (command == "exit")
                {
                    AnsiConsole.MarkupLine("[red]Shutting down FeiSharp... Goodbye![/]");
                    global::System.Environment.Exit(0);
                }
                else if (command == "cd")
                {
                    AnsiConsole.MarkupLine($"[cyan]Current Directory:[/] [green]{Directory.GetCurrentDirectory()}[/]");
                }
                else if (command.StartsWith("cd"))
                {
                    if (commandParts.Length < 2)
                    {
                        AnsiConsole.MarkupLine("[red]Error[/]: [yellow]cd requires a target path[/]");
                        continue;
                    }
                    Directory.SetCurrentDirectory(commandParts[1]);
                }
                else if (command.StartsWith("open"))
                {
                    string path = "";
                    if (command == "open")
                    {
                        path = Directory.GetCurrentDirectory();
                    }
                    else
                    {
                        path = commandParts.Length > 1 ? commandParts[1] : Directory.GetCurrentDirectory();
                    }
                    Task.Factory.StartNew(() =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "code.exe",
                            Arguments = $"--log=off \"{path}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });
                    });
                    if (Console.CursorTop > 0)
                    {
                        int currentLeft = Console.CursorLeft;
                        int currentTop = Console.CursorTop;
                        Console.SetCursorPosition(0, currentTop - 1);
                    }
                    Thread.Sleep(500);

                }
                else if (command.StartsWith("explorer"))
                {
                    string path = "";
                    if (command == "explorer")
                    {
                        path = Directory.GetCurrentDirectory();
                    }
                    else
                    {
                        path = commandParts.Length > 1 ? commandParts[1] : Directory.GetCurrentDirectory();
                    }
                    Task.Factory.StartNew(() =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{path}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });
                    });
                    Thread.Sleep(500);

                }
                else if (command.StartsWith("run"))
                {
                    ChangeDynamicTitle(EDT_CDE);
                    AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input your code... Enter 'exit' to exit this mode...[/]");
                    var rule1 = new Rule("[yellow]Code Input Mode[/]")
                    {
                        Style = Style.Parse("green"),
                        Justification = Justify.Center
                    };
                    AnsiConsole.Write(rule1);
                    string code = GetUserCode();
                    var runTimer = Stopwatch.StartNew();
                    _parser = RunFeiSharpCodeWithProProcesser(code, commandParts.Skip(1).ToArray(), _parser);
                    runTimer.Stop();
                    WriteRunDuration(runTimer.Elapsed);
                    ChangeDynamicTitle(IN_CMD);
                }
                else if (command == "file")
                {
                    AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input the file path...[/]");
                    AnsiConsole.Markup("[cyan]File Path: [/]");
                    string path = Console.ReadLine();
                    string? sourcePath = ResolveFeiSharpSourcePath(path);
                    if (sourcePath == null)
                    {
                        var errorPanel = new Panel($"[red]✗ File not found:[/] {path.EscapeMarkup()}")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("red"));
                        AnsiConsole.Write(errorPanel);
                        continue;
                    }

                    var statusPanel = new Panel($"[yellow]Loading file:[/] [green]{sourcePath}[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("green"));
                    AnsiConsole.Write(statusPanel);

                    global::System.String sourceCode = File.ReadAllText(sourcePath);
                    _applicationPath = sourcePath;

                    var runTimer = Stopwatch.StartNew();
                    RunFeiSharpCodeWithProProcesser(sourceCode);
                    runTimer.Stop();
                    WriteRunDuration(runTimer.Elapsed);

                    _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else if (command == "build" || command.StartsWith("build "))
                {
                    string sourceInput = command.Length > 5 ? command[5..].Trim() : string.Empty;
                    List<string> feiprojs = [];
                    if (string.IsNullOrWhiteSpace(sourceInput))
                    {
                        foreach (var item in Directory.GetFiles(Directory.GetCurrentDirectory()))
                        {
                            if (item.EndsWith(".feiproj"))
                            {
                                feiprojs.Add(item);
                            }
                        }
                        if (feiprojs.Count == 1)
                        {
                            sourceInput = feiprojs[0];
                        }
                        else
                        {
                            feiprojs.Add("Others");
                            sourceInput = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What source file would you like?")
            .AddChoices(feiprojs.ToArray())
    );
                        }
                        if (sourceInput == "Others")
                        {
                            AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input the source file or project path...[/]");
                            AnsiConsole.Markup("[cyan]Source Path: [/]");
                            sourceInput = Console.ReadLine() ?? string.Empty;
                        }
                    }
                    string? outputInput = "out\\feisharp-sdk-11.0-release\\" + Path.GetFileNameWithoutExtension(sourceInput) + ".exe";
                    await AnsiConsole.Status()
                        .StartAsync("[yellow]Building executable...[/]", async ctx =>
                        {
                            ChangeDynamicTitle("Generate Native Code");
                            if (TryBuildExecutable(sourceInput, outputInput, out string builtExePath, out string buildError))
                            {
                                var successPanel = new Panel($"[green]✓ Build completed:[/] [yellow]{builtExePath}[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("green"));
                                AnsiConsole.Write(successPanel);
                            }
                            else
                            {
                                var errorPanel = new Panel($"[red]✗ Build failed:[/] {buildError.EscapeMarkup()}")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(errorPanel);
                            }
                            ChangeDynamicTitle(IN_CMD);
                            await Task.CompletedTask;
                        });
                }
                else if (command == "pwd")
                {
                    var path = new TextPath(Directory.GetCurrentDirectory())
                        .RootColor(Color.Red)
                        .SeparatorColor(Color.Green)
                        .StemColor(Color.Blue)
                        .LeafColor(Color.Yellow);

                    AnsiConsole.Markup("[cyan]Current Directory: [/]");
                    AnsiConsole.Write(path);
                    AnsiConsole.WriteLine();
                }
                else if (command == "cd..")
                {
                    var oldPath = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory()).FullName);
                    var newPath = Directory.GetCurrentDirectory();

                    AnsiConsole.MarkupLine($"[grey]Changed directory from[/] [red]{oldPath}[/] [grey]to[/] [green]{newPath}[/]");
                }
                else if (command.StartsWith("cd "))
                {
                    var oldPath = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(command.Split(' ')[1]);
                    var newPath = Directory.GetCurrentDirectory();

                    AnsiConsole.MarkupLine($"[grey]Changed directory from[/] [red]{oldPath}[/] [grey]to[/] [green]{newPath}[/]");
                }
                else if (command == "cls" || command == "clear")
                {
                    Console.Clear();


                    AnsiConsole.Write(
                        new FigletText("FeiSharp")
                            .Color(Color.Cyan1));
                }
                else if (command.StartsWith("create "))
                {
                    if (command.Split(' ').Length == 3)
                    {
                        string name = string.Concat(command.Split(' ')[2].Select(c => Path.GetInvalidPathChars().Contains(c) ? '_' : c)).Trim();
                        switch (command.Split(' ')[1])
                        {
                            case "Console":
                                Directory.CreateDirectory(name);
                                Directory.SetCurrentDirectory(name);
                                File.WriteAllText($"test_{name}1.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\n");
                                File.WriteAllText($"main_{name}.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction main()\n{\n    printNl(\"Hello, World!\");\n}\n");
                                File.WriteAllText("data.fsc", "//Set your data in this file");
                                File.WriteAllText("api.fsc", "//Create your own API functions in this file");
                                File.WriteAllText("README.md", $"This is the console project {name}");
                                File.WriteAllText($"{name}.log", $"{DateTime.Now} Create console project {name} by FeiSharp Target SDK 11.0\n");
                                File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                                File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                                File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                                break;
                            case "API":
                                Directory.CreateDirectory(name);
                                Directory.SetCurrentDirectory(name);
                                File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                                File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                                File.WriteAllText("data.fsc", "//Set your data in this file");
                                File.WriteAllText("api.fsc", "//Create your own API functions in this file");
                                File.WriteAllText("README.md", $"This is the api project {name}");
                                File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                                File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                                File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                                break;
                            case "Data":
                                Directory.CreateDirectory(name);
                                Directory.SetCurrentDirectory(name);
                                File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                                File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                                File.WriteAllText("data.fsc", "//Set your data in this file");
                                File.WriteAllText("README.md", $"This is the api project {name}");
                                File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                                File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                                File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                                break;
                            case "fUnitTest":
                                Directory.CreateDirectory(name);
                                Directory.SetCurrentDirectory(name);
                                File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 11.0,
    ""trimmed_by_dotnet"": false, 
    ""clean_build_directory"": true,
    ""target_platform"": ""win-x64""
}}");
                                File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/test_content.fsc\";\nimport \"~/api.fsc\";\nfunction test()\n{\n    print(\"Hello, World!\");\n}\ntest();");
                                File.WriteAllText("test_content.fsc", "//Set the test content");
                                File.WriteAllText("README.md", $"This is the api project {name}");
                                File.WriteAllText(".gitignore", @"
ouy/
*.exe
*.dll
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                                File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 11.0\n");
                                File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                                break;
                            default:
                                var errorPanel = new Panel("[red]✗ The type isn't correct[/]")
                                        .Border(BoxBorder.Rounded)
                                        .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(errorPanel);
                                break;
                        }
                    }
                }
                else if (command == "feedback")
                {
                    AnsiConsole.Markup("[yellow]Please enter your feedback: [/]");
                    string feedback = Console.ReadLine();

                    await AnsiConsole.Status()
                        .StartAsync("[cyan]Sending feedback...[/]", async ctx =>
                        {
                            try
                            {
                                TcpClient client = new TcpClient(PublicIpFetcher.GetPublicIpAsync().Result, 6721);
                                try
                                {
                                    NetworkStream stream = client.GetStream();
                                    byte[] data = Encoding.UTF8.GetBytes(feedback);
                                    await stream.WriteAsync(data, 0, data.Length);

                                    var successPanel = new Panel("[green]✓ Feedback sent successfully![/]")
                                        .Border(BoxBorder.Rounded)
                                        .BorderStyle(Style.Parse("green"));
                                    AnsiConsole.Write(successPanel);
                                }
                                catch
                                {
                                    var errorPanel = new Panel("[red]✗ Failed to send feedback[/]")
                                        .Border(BoxBorder.Rounded)
                                        .BorderStyle(Style.Parse("red"));
                                    AnsiConsole.Write(errorPanel);
                                }
                                finally
                                {
                                    client.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                            }
                        });
                }
                else if (command.StartsWith("version"))
                {
                    var versionPanel = new Panel("[yellow]FeiSharp Target SDK 11.0 Pro(Release)[/]")
                        .Header(" [cyan]Version Info[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"));
                    AnsiConsole.Write(versionPanel);
                }
                else if (command == "license")
                {
                    var licensePanel = new Panel(
                        "[yellow]Copyright (c) 2026 Feifei Ganxiner[/]\n\n" +
                        "[grey]Licensed under the Apache License, Version 2.0 (the \"License\");\n" +
                        "you may not use this software except in compliance with the License.\n" +
                        "You may obtain a copy of the License at[/]\n\n" +
                        "[blue]http://www.apache.org/licenses/LICENSE-2.0.txt[/]\n\n" +
                        "[grey]Unless required by applicable law or agreed to in writing, software\n" +
                        "distributed under the License is distributed on an \"AS IS\" BASIS,\n" +
                        "WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\n" +
                        "See the License for the specific language governing permissions and\n" +
                        "limitations under the License.[/]")
                        .Header(" [cyan]Apache License 2.0[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"))
                        .Expand();

                    AnsiConsole.Write(licensePanel);
                }
                else if (command == "resetColor")
                {
                    Console.ResetColor();
                    AnsiConsole.MarkupLine("[green]Colors reset to default[/]");
                }
                else if (command == "color")
                {
                    var color = AnsiConsole.Prompt(
                        new SelectionPrompt<ConsoleColor>()
                            .Title("[cyan]Select a color:[/]")
                            .PageSize(10)
                            .AddChoices(Enum.GetValues<ConsoleColor>()));

                    Console.ForegroundColor = color;
                    AnsiConsole.MarkupLine($"[green]Color changed to {color}[/]");
                }
                else if (command.StartsWith("disable"))
                {
                    if (commandParts.Length > 1 && commandParts[1] == "main_function_run")
                    {
                        FeiSharpProgramData.main_function_run = false;
                    }
                }
                else if (command.StartsWith("enable"))
                {
                    if (commandParts.Length > 1 && commandParts[1] == "main_function_run")
                    {
                        FeiSharpProgramData.main_function_run = true;
                    }
                }
                else if (command.StartsWith("license "))
                {
                    global::System.String arg1 = commandParts[1];
                    if (arg1 == "--type")
                    {
                        var typePanel = new Panel("[yellow]Apache License[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("green"));
                        AnsiConsole.Write(typePanel);
                    }
                    else if (arg1 == "-v")
                    {
                        var versionPanel = new Panel("[yellow]2.0[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("green"));
                        AnsiConsole.Write(versionPanel);
                    }
                }
                else if (command == "copyright")
                {
                    var copyrightPanel = new Panel(
                        "[yellow]Copyright (c) 2026, Feifei Ganxiner[/]\n" +
                        "[red]All Rights Reserved.[/]")
                        .Header(" [cyan]Copyright Information[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"));

                    AnsiConsole.Write(copyrightPanel);
                    var copyrightPanel2 = new Panel(
                        "[yellow]Copyright (c) 2026, Yang Ganxiner[/]\n" +
                        "[red]All Rights Reserved.[/]")
                        .Header(" [cyan]Copyright Information[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"));

                    AnsiConsole.Write(copyrightPanel2);
                }
                else if (command == "base")
                {
                    var basePanel = new Panel(
                        "[cyan]FeiSharp is based on:[/]\n\n" +
                        "  [yellow]- C# .NET 8.0[/]\n" +
                        "  [yellow]- Microsoft IL[/]\n" +
                        "  [yellow]- Native Binary Code[/]")
                        .Header(" [green]Technology Stack[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("green"));

                    AnsiConsole.Write(basePanel);
                }
                else if (command == "credits")
                {
                    var credits = new Panel(
                        "[yellow]Special thanks to:[/]\n\n" +
                        "  [green]- Ben Bai[/]\n" +
                        "  [green]- Yolanda Yang[/]\n" +
                        "  [green]- Dean Liu[/]\n" +
                        "  [green]- Savannah Yang[/]\n" +
                        "  [green]- github.com[/]\n" +
                        "  [green]- Git[/]\n" +
                        "  [green]- Visual Studio[/]\n" +
                        "  [green]- Visual Studio Code[/]\n" +
                        "[grey]...and a cast of thousands for supporting FeiSharp development.[/]")
                        .Header(" [cyan]Credits[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"))
                        .Expand();

                    AnsiConsole.Write(credits);
                }
                else if (command == "help" || command.StartsWith("help "))
                {
                    var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var specificCommand = parts.Length > 1 ? parts[1] : null;

                    await HelpSystem.ShowInteractiveHelp(specificCommand);
                }
                else
                {
                    if (isTerminalCommand)
                    {
                        AnsiConsole.MarkupLine($"[red]Unknown terminal command:[/] [yellow]/{command.EscapeMarkup()}[/]");
                        continue;
                    }
                    command = command[1..];
                    bool result = true, r = true;
                    foreach(var item in FeiSharpKeywords.KeywordMap.Keys)
                    {
                        if (command.StartsWith(item))
                        {
                            r = false;
                        }
                    }
                    if (command.Contains('(') && !r) result = false;
                    else result = r;
                    var runTimer = Stopwatch.StartNew();
                    if (result)
                    {
                        var guid = "_" + Guid.NewGuid().ToString("N");
                        command = $"var {guid} = " + command + $";print({guid});";
                        await AnsiConsole.Status()
                        .StartAsync("[yellow]Executing code...[/]", async ctx =>
                        {
                            _parser = RunFeiSharpCodeWithProProcesser(command, null, _parser);
                        });
                    }
                    else
                    {
                        await AnsiConsole.Status()
                        .StartAsync("[yellow]Executing code...[/]", async ctx =>
                        {
                            _parser = RunFeiSharpCodeWithProProcesser(command, null, _parser);
                        });
                    }
                    runTimer.Stop();
                    WriteRunDuration(runTimer.Elapsed);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error[/]: [yellow]{ex.Message}[/]");
            }
        }
    }

    public static bool IsConnectedToInternet()
    {
        try
        {
            using (Ping ping = new Ping())
            {
                PingReply reply = ping.Send("8.8.8.8", 1000);
                return reply.Status == IPStatus.Success;
            }
        }
        catch (PingException)
        {
            return false;
        }
    }
    public static string RemoveCommentsSafe(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        string pattern = @"(""(?:[^""\\]|\\.)*""|@""(?:[^""]|"""")*""|'(?:[^'\\]|\\.)*')|//.*?$|/\*.*?\*/";
        return Regex.Replace(input, pattern, m =>
        {
            if (m.Groups[1].Success) return m.Value;
            return "";
        }, RegexOptions.Multiline | RegexOptions.Singleline);
    }
    public static string ProProcesser(string scode)
    {
        var loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(_applicationPath))
            loadedFiles.Add(Path.GetFullPath(_applicationPath));
        return PreprocessSource(scode, _applicationPath, loadedFiles, true);
    }

    private static string PreprocessSource(string scode, string sourcePath, HashSet<string> loadedFiles, bool runMain)
    {
        scode = TransformCode(RemoveCommentsSafe(scode));
        if (runMain && scode.Contains("function main(") && FeiSharpProgramData.main_function_run)
        {
            AnsiConsole.MarkupLine("[yellow]Warning: The code includes 'main' function, we will run it automatically[/]");
            AnsiConsole.MarkupLine("[yellow]Tips: If you run code directly in terminal, you can execute the command 'disable main_function_run'.Or you can add '\"main_function_run\": false' to the .feiproj file if it is a project.[/]");
            AnsiConsole.MarkupLine("[yellow]And we don't suggest you to write another code in the outside of 'main' function[/]");
            scode += "main();";
        }
        return ExpandImports(scode, sourcePath, loadedFiles);
    }

    private static string ExpandImports(string scode, string sourcePath, HashSet<string> loadedFiles)
    {
        if (string.IsNullOrWhiteSpace(scode))
        {
            return scode;
        }

        var importPattern = new Regex(@"\bimport\s+""(?<path>[^""]+)""\s*;?|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'");
        var importedContents = new StringBuilder();
        string result = importPattern.Replace(scode, match =>
        {
            if (!match.Groups["path"].Success) return match.Value;
            string vFilePath = match.Groups["path"].Value;
            bool isNamespaceReference = (!vFilePath.EndsWith(".fsc", StringComparison.OrdinalIgnoreCase)
                    && !vFilePath.Contains('/')
                    && !vFilePath.Contains('\\'));
            if (isNamespaceReference)
            {
                return $"import {vFilePath};";
            }

            string baseDirectory = Directory.Exists(sourcePath)
                ? sourcePath : Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
            string tFilePath = Path.GetFullPath(vFilePath.StartsWith('$')
                ? Path.Combine(FEISHARP_IMPORT_PATH, vFilePath[1..])
                : Path.Combine(baseDirectory, vFilePath.StartsWith("~/") ? vFilePath[2..] : vFilePath));
            if (!File.Exists(tFilePath))
                throw new FileNotFoundException($"Cannot import '{vFilePath}' from '{sourcePath}'.", tFilePath);
            if (!loadedFiles.Add(tFilePath)) return string.Empty;
            string content = PreprocessSource(File.ReadAllText(tFilePath), tFilePath, loadedFiles, false);
            importedContents.Append("namespace {\n").Append(content).AppendLine("\n}");

            return string.Empty;
        });

        if (importedContents.Length == 0)
        {
            return result;
        }

        importedContents.Append(result);
        return importedContents.ToString();
    }
    static string default_functions = "function cast(a, t){ ctype(a, t, \"res\"); return res; }function not(a)\n{\n\treturn a = false;\n}\nfunction or(a, b)\n{\n\treturn a | b;\n}\nfunction and(a, b)\n{\n\treturn a & b;\n}\nfunction tostr(txt)\r\n{\r\n    cstr(txt, \"aSFGas\");\r\n    return aSFGas;\r\n}\n" +
        "class number{ public static var minValue = -2147483648; public static var maxValue = 2147483647; public static function parse(t){ ctype(t, \"number\", \"res\"); return res; } }\n" +
        "class string{ public static var empty = \"\"; public static function parse(t){ ctype(t, \"string\", \"res\"); return res; } public static function join(s, arr){ var r = \"\"; for(var i = 0; i < arr.Length; i++){ r = r + tostr(arr[i]); if(i != arr.Length - 1){ r = r + s;}  } return r;} }\n" +
        "class boolean{ public static var _false = false; public static var _true = true; public static function parse(t){ ctype(t, \"boolean\", \"res\"); return res; } }\n" +
        "class music{ public var nameOrPath = \"\"; public var mode = \"online\"; constructor(a, b){ this.nameOrPath = a; this.mode = b; } public function play(){ musicPlay(this.nameOrPath, this.mode); } public function pause(){ musicPause(); } public function stop(){ musicStop(); } public function download(s){ musicDownload(this.nameOrPath, s); } }\n";
    public static Parser RunFeiSharpCodeWithProProcesser(string scode, string[] args = null, Parser _parser = null)
    {
        ChangeDynamicTitle(EXEC);

        ExecutionCancellation.SetExecuting(true);

        try
        {

            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                return _parser;
            }

            global::System.String sourceCode = scode;
            sourceCode = default_functions + ProProcesser(sourceCode);
            if(sourceCode.EndsWith("\""))
                sourceCode = sourceCode[..^1];
            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled during preprocessing[/]");
                return _parser;
            }
            if (args != null && args.Length > 0)
            {
                sourceCode = $"var args = [{string.Join(',', args.Select(i => "\"" + i + "\"").ToArray())}];\n" + sourceCode;
            }
            else

            {
                sourceCode = "var args = [];\n" + sourceCode;
            }
            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled during preprocessing[/]");
                return _parser;
            }

            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;
            do
            {
                if (ExecutionCancellation.IsCancellationRequested)
                {
                    AnsiConsole.MarkupLine("[yellow]Execution cancelled during lexing[/]");
                    return _parser;
                }

                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);
            if (CodeError.isError)
            {
                return _parser;
            }
            Parser parser = null;
            try
            {
                parser = new(tokens);
                if (_parser != null)
                {
                    parser._variables = _parser._variables;
                    parser._functions = _parser?._functions ?? new Dictionary<string, FunctionInfo>();
                    parser.CopyPlayerFrom(_parser);
                }
                parser.ShouldCancel = () => ExecutionCancellation.IsCancellationRequested;
                parser.ParseStatements();
                return parser;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                return _parser;
            }
            catch (FeiSharpTerminal3._1.ExceptionThrow.Exception ex)
            {
                if (!ExecutionCancellation.IsCancellationRequested)
                {
                    var errorPanel = new Panel($"[red]{ex.ErrorType}: {ex.Number}[/]\n{ex.Description}")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("red"));
                    AnsiConsole.Write(errorPanel);
                }
                return parser;
            }
        }
        finally
        {

            ExecutionCancellation.SetExecuting(false);
            Console.WriteLine();
            ChangeDynamicTitle(IN_CMD);
        }
    }
}
