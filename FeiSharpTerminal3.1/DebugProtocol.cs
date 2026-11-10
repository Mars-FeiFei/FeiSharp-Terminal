using FeiSharpStudio;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace FeiSharpTerminal3._1;

/// <summary>Small line-delimited JSON bridge used by the VS Code debug adapter.</summary>
internal static class DebugProtocol
{
    private sealed class ParserState { internal int Depth; }
    private static readonly ConditionalWeakTable<Parser, ParserState> ParserStates = new();
    private static readonly ConcurrentQueue<JsonElement> Commands = new();
    private static readonly Dictionary<string, HashSet<int>> Breakpoints = new(StringComparer.OrdinalIgnoreCase);
    private static StreamWriter? _writer;
    private static bool _enabled, _started, _stopOnEntry, _pauseRequested, _sourceLineOffsetInitialized;
    private static string _mode = "continue";
    private static int _targetDepth, _currentDepth, _sourceLineOffset;
    private static Parser? _currentParser;
    internal static string SourcePath { get; private set; } = "";
    internal static bool IsEnabled => _enabled;
    internal static int SourceLineOffset => _sourceLineOffsetInitialized ? _sourceLineOffset : 0;
    internal static void SetSourceLineOffset(int lines)
    {
        // RunFeiSharpCodeWithProProcesser is also used for generated/imported
        // snippets.  Keep the offset belonging to the user's main file so
        // those snippets cannot move the editor highlight to line 96, etc.
        _sourceLineOffset = Math.Max(0, lines);
        _sourceLineOffsetInitialized = true;
    }

    internal static void Initialize(int port, string sourcePath)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        var client = new TcpClient();
        client.Connect("127.0.0.1", port);
        var stream = client.GetStream();
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        var reader = new StreamReader(stream, new UTF8Encoding(false));
        _enabled = true;
        Send(new { @event = "ready" });
        var readerThread = new Thread(() =>
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                try { Commands.Enqueue(JsonDocument.Parse(line).RootElement.Clone()); }
                catch { }
            }
        }) { IsBackground = true, Name = "FeiSharp debug protocol reader" };
        readerThread.Start();
    }

    internal static void BeforeStatement(Parser parser, Token token, string functionName)
    {
        if (!_enabled || token.Type == TokenTypes.EndOfFile) return;
        ParserState state;
        if (!ParserStates.TryGetValue(parser, out state!))
        {
            int parentDepth = _currentParser == null ? 0 : ParserStates.GetOrCreateValue(_currentParser).Depth;
            state = new ParserState { Depth = parentDepth + (_currentParser == null ? 0 : 1) };
            ParserStates.Add(parser, state);
        }
        _currentParser = parser;
        _currentDepth = state.Depth;
        DrainCommands();
        while (!_started) { Thread.Sleep(5); DrainCommands(); }

        int sourceLine = token.Line - _sourceLineOffset;
        bool step = _mode == "stepIn" || (_mode == "next" && state.Depth <= _targetDepth) || (_mode == "stepOut" && state.Depth < _targetDepth);
        bool breakpoint = sourceLine > 0 && Breakpoints.TryGetValue(SourcePath, out var lines) && lines.Contains(sourceLine);
        if (token.Line <= _sourceLineOffset) return;
        if (_stopOnEntry || _pauseRequested || breakpoint || step)
        {
            string reason = breakpoint ? "breakpoint" : _pauseRequested ? "pause" : _stopOnEntry ? "entry" : "step";
            _stopOnEntry = false; _pauseRequested = false; _mode = "stopped";
            SendStopped(parser, token, functionName, reason);
            while (_mode == "stopped") { Thread.Sleep(5); DrainCommands(parser); }
        }
    }

    private static void DrainCommands(Parser? parser = null)
    {
        while (Commands.TryDequeue(out JsonElement command))
        {
            string name = command.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
            switch (name)
            {
                case "start": _stopOnEntry = !command.TryGetProperty("stopOnEntry", out var stop) || stop.GetBoolean(); _started = true; break;
                case "breakpoints":
                    string source = Path.GetFullPath(command.GetProperty("source").GetString() ?? SourcePath);
                    Breakpoints[source] = command.GetProperty("lines").EnumerateArray().Select(x => x.GetInt32()).ToHashSet();
                    break;
                case "pause": _pauseRequested = true; break;
                case "continue": _mode = "continue"; break;
                case "next": _targetDepth = _currentDepth; _mode = "next"; break;
                case "stepIn": _targetDepth = _currentDepth; _mode = "stepIn"; break;
                case "stepOut": _targetDepth = _currentDepth; _mode = "stepOut"; break;
                case "evaluate":
                    if (parser != null) Evaluate(parser, command);
                    break;
                case "disconnect": _enabled = false; _mode = "continue"; break;
            }
        }
    }

    private static void Evaluate(Parser parser, JsonElement command)
    {
        int id = command.GetProperty("id").GetInt32();
        try
        {
            object? result = parser.EvaluateDebuggerExpression(command.GetProperty("expression").GetString() ?? "");
            Send(new { @event = "evaluate", id, result = DebugValue(result), type = DebugTypeName(result) });
        }
        catch (System.Exception ex) { Send(new { @event = "evaluate", id, error = ex.Message }); }
    }

    private static void SendStopped(Parser parser, Token token, string functionName, string reason)
    {
        var variables = parser._variables
            .Where(x => !x.Key.StartsWith("__feisharp_main_source_marker__", StringComparison.Ordinal))
            .ToDictionary(x => x.Key, x => DebugValue(x.Value), StringComparer.OrdinalIgnoreCase);
        Send(new { @event = "stopped", reason, source = SourcePath, line = token.Line - _sourceLineOffset, column = token.Column,
            function = string.IsNullOrWhiteSpace(functionName) ? "<global>" : functionName, locals = variables, globals = variables });
    }

    private static object DebugValue(object? value)
    {
        if (value == null) return new { value = (string?)null, type = "null" };
        object printable = value switch
        {
            double number when double.IsNaN(number) || double.IsInfinity(number) => number.ToString(),
            float number when float.IsNaN(number) || float.IsInfinity(number) => number.ToString(),
            string or char or bool or byte or short or int or long or float or double or decimal => value,
            Parser.ClassInstance instance => $"{instance.ClassName} {{ … }}",
            _ => value.ToString() ?? value.GetType().Name
        };
        return new { value = printable, type = DebugTypeName(value) };
    }

    private static string DebugTypeName(object? value) => value switch
    {
        null => "null",
        Parser.ClassInstance instance => instance.ClassName,
        _ => value.GetType().Name
    };

    private static void Send(object message)
    {
        try { _writer?.WriteLine(JsonSerializer.Serialize(message)); } catch { _enabled = false; }
    }
}
