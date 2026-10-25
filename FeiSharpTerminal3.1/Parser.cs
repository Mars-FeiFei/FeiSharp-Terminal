using FeiSharp8._5RuntimeSdk;
using FeiSharpStudio.ClassInstance;
using FeiSharpStudio.UUID;
using FeiSharpTerminal3._1;
using FMusicPlayer;
using Spectre.Console;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static FeiSharpStudio.Parser;
using Exception = FeiSharpTerminal3._1.ExceptionThrow.Exception;
namespace FeiSharpStudio
{
    public class ScriptTitleExtractor
    {
        public static string ExtractEscapedTitleFromScript(string htmlContent)
        {
            string pattern = @"const\s+escapedTitle\s*=\s*'([^']*)'";
            var match = Regex.Match(htmlContent, pattern);

            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        public static string ProcessTitle(string htmlContent)
        {
            string escapedTitle = ExtractEscapedTitleFromScript(htmlContent);
            if (string.IsNullOrEmpty(escapedTitle))
                return null;
            string rawTitle = System.Net.WebUtility.HtmlDecode(escapedTitle);
            string encodedTitle = Uri.EscapeDataString(rawTitle);
            return encodedTitle;
        }
    }
    internal class FeiSharpSpecialValue
    {
        internal string Value { get; init; }
        internal FeiSharpSpecialValue(string value)
        {
            Value = value;
        }
    }
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Script engine uses reflection intentionally")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Script engine uses reflection intentionally")]
    public class Parser
    {
        class CSharpType<T>
        {
            internal T Value { get; private set; }
            internal Type Type { get; private set; }
            public CSharpType(T value)
            {
                Value = value;
                Type = value.GetType();
            }
        }
        private Stopwatch Stopwatch { get; set; }
        private sealed class AssignmentTarget
        {
            public required string Kind { get; init; }
            public required string Name { get; init; }
            public string? MemberName { get; init; }
            public ClassInstance? Instance { get; init; }
            public ClassInfo? ClassInfo { get; init; }
            public object? TargetObject { get; init; }
            public object? IndexKey { get; init; }
        }
        private List<Token> _tokens;
        private int _current;
        private bool _propagateFeiSharpExceptions;
        private bool _isParsingClassBody;
        private string? _classDeclarationName;
        private string? _currentExecutionClassName;
        private string? _currentFunctionName;
        private string? _currentNamespace;
        private HashSet<string> _usingNamespaces = new(StringComparer.OrdinalIgnoreCase);
        private object? _lastItValue;
        private Dictionary<string, VariableInfo> _variableInfos = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, object> _variables = new();
        public Dictionary<string, FunctionInfo> _functions = new();
        public event EventHandler<OutputEventArgs> OutputEvent;
        public Dictionary<string, object> _results = new();
        public List<string> strings = new List<string>();
        public Func<bool> ShouldCancel { get; set; }
        private const int CancelCheckInterval = 10;
        private void CheckCancellation()
        {
            if (ShouldCancel != null && ShouldCancel())
            {
                throw new OperationCanceledException("Execution cancelled by user");
            }
        }
        List<string> keys =
[
    "true",
    "false",
    "null",
    "positiveInf",
    "negativeInf",
    "buildVersion",
    "zeroInt",
    "emptyStr",
    "ten",
    "hundred",
    "thousand",
    "pi",
    "e",
    "tau",
    "_"
];
        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
            _variables.NewAdd("true", true);
            _variables.NewAdd("false", false);
            _variables.NewAdd("positiveInf", double.PositiveInfinity);
            _variables.NewAdd("negativeInf", double.NegativeInfinity);
            _variables.NewAdd("buildVersion", 10.0);
            _variables.NewAdd("zeroInt", 0);
            _variables.NewAdd("emptyStr", "");
            _variables.NewAdd("null", null);
            _variables.NewAdd("ten", 10.0);
            _variables.NewAdd("hundred", 100.0);
            _variables.NewAdd("thousand", 1000.0);
            _variables.NewAdd("pi", Math.PI);
            _variables.NewAdd("e", Math.E);
            _variables.NewAdd("tau", Math.Tau);
            _variables.NewAdd("_", new FeiSharpSpecialValue("Dircards"));
        }
        protected virtual void OnOutputEvent(OutputEventArgs e)
        {
            EventHandler<OutputEventArgs> handler = OutputEvent;
            handler?.Invoke(this, e);
        }
        public void ParseStatements(string funcName = "")
        {
            foreach (string reference in _usingNamespaces)
                EnableBuiltInNamespace(reference);
            int statementCount = 0;
            try
            {
                do
                {
                    if (IsAtEnd())
                    {
                        break;
                    }
                    if (Peek().Type == TokenTypes.Punctuation && Peek().Value == ";")
                    {
                        Advance();
                        continue;
                    }
                    statementCount++;
                    if (statementCount % CancelCheckInterval == 0)
                    {
                        CheckCancellation();
                    }
                    if (MatchKeyword(TokenKeywords._var))
                    {
                        ParseVariableDeclaration();
                    }
                    else if (MatchKeyword(TokenKeywords._const))
                    {
                        ParseVariableDeclaration(isConst: true);
                    }
                    else if (TryParseExtensionKeywordStatement())
                    {
                    }
                    else if (MatchKeyword(TokenKeywords.abs))
                    {
                        Parseabs();
                    }
                    else if (MatchKeyword(TokenKeywords.print))
                    {
                        PrintStmt printStmt = ParsePrintStatement();
                        EvaluatePrintStmt(printStmt);
                    }
                    else if (MatchKeyword(TokenKeywords.init))
                    {
                        ParseInitStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.set))
                    {
                        ParseSetStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.run))
                    {
                        ParseRunStatement();
                    }
                    else if (MatchWord(TokenKeywords.export))
                    {
                        ParseExportStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.start))
                    {
                        ParseStartStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.stop))
                    {
                        ParseStopStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.package))
                    {
                        ParsePackageStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.unpackage))
                    {
                        ParseUnPackageStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.wait))
                    {
                        ParseWaitStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.watchstart))
                    {
                        ParseWatchStartStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.watchend))
                    {
                        ParseWatchEndStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.abe))
                    {
                        ParseABEStatement();
                    }
                    else if (MatchKeyword("breakpoint"))
                    {
                        ParseBreakPointStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.helper))
                    {
                        ParseHelperStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._if))
                    {
                        ParseIfStatement();
                    }
                    else if (MatchKeyword("forEach"))
                    {
                        ParseForEachStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._for))
                    {
                        ParseForStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._while))
                    {
                        ParseWhileStatement();
                    }
                    else if (TryParseModifiedDeclaration())
                    {
                    }
                    else if (MatchKeyword(TokenKeywords.func))
                    {
                        ParseFunctionStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.dowhile))
                    {
                        ParseDowhileStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._throw))
                    {
                        ParseThrowStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._try))
                    {
                        ParseTryCatchStatement(funcName);
                    }
                    else if (MatchKeyword(TokenKeywords._return))
                    {
                        ParseReturnStatement(funcName);
                    }
                    else if (MatchKeyword(TokenKeywords.gethtml))
                    {
                        ParseGetHtmlStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.getVarsFromJsonFilePath))
                    {
                        ParseGetJsonFilePathStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readonlyclass))
                    {
                        ParseClassStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.invoke))
                    {
                        ParseInvokeStatement();
                    }
                    else if (Check(TokenTypes.Keyword) && Peek().Value == TokenKeywords.read && Peek(1).Value == "(")
                    {
                        Advance();
                        ParseReadStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.import))
                    {
                        ParseImportStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._namespace))
                    {
                        ParseNamespaceStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._using))
                    {
                        ParseUsingNamespaceStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.annotation))
                    {
                        ParseAnnotationStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.define))
                    {
                        ParseDefineStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readline))
                    {
                        ParseReadLineStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readkey))
                    {
                        ParseReadKeyStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.ctype))
                    {
                        ParseCTypeStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.cstr))
                    {
                        ParseCStRStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._astextbox))
                    {
                        ParseAstextboxStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.createData))
                    {
                        ParseCreateDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.addData))
                    {
                        ParseAddDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.delData))
                    {
                        ParseDelDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.replaceData))
                    {
                        ParseReplaceData();
                    }
                    else if (MatchKeyword(TokenKeywords.getData))
                    {
                        ParseGetData();
                    }
                    else if (MatchKeyword(TokenKeywords.saveDataChanges))
                    {
                        ParseSaveDataChange();
                    }
                    else if (MatchKeyword(TokenKeywords.invokeData))
                    {
                        ParseInvokeData();
                    }
                    else if (MatchKeyword(TokenKeywords.createInstance))
                    {
                        ParseInstance();
                    }
                    else if (MatchKeyword(TokenKeywords.setClassVar))
                    {
                        ParseSetClassVar();
                    }
                    else if (MatchKeyword(TokenKeywords.setBaseClass))
                    {
                        ParseSetBase();
                    }
                    else if (MatchKeyword(TokenKeywords.printMethod))
                    {
                        ParsePrintMethod();
                    }
                    else if (MatchKeyword(TokenKeywords.rand))
                    {
                        ParseRand();
                    }
                    else if (MatchKeyword(TokenKeywords.pow))
                    {
                        Parsepow();
                    }
                    else if (MatchKeyword(TokenKeywords.sin))
                    {
                        Parsesin();
                    }
                    else if (MatchKeyword(TokenKeywords.cos))
                    {
                        Parsecos();
                    }
                    else if (MatchKeyword("musicPlay"))
                    {
                        ParseMusicPlay();
                    }
                    else if (MatchKeyword("musicPause"))
                    {
                        ParseMusicPause();
                    }
                    else if (MatchKeyword("musicDownload"))
                    {
                        ParseMusicDownload();
                    }
                    else if (MatchKeyword(TokenKeywords.tan))
                    {
                        Parsetan();
                    }
                    else if (MatchKeyword(TokenKeywords.asin))
                    {
                        Parseasin();
                    }
                    else if (MatchKeyword(TokenKeywords.acos))
                    {
                        Parseacos();
                    }
                    else if (MatchKeyword(TokenKeywords.atan))
                    {
                        Parseatan();
                    }
                    else if (MatchKeyword(TokenKeywords.sqrt))
                    {
                        Parsesqrt();
                    }
                    else if (MatchKeyword(TokenKeywords.strfromindex))
                    {
                        Parsefromindex();
                    }
                    else if (MatchKeyword(TokenKeywords.getindex))
                    {
                        Parsegetindex();
                    }
                    else if (MatchKeyword(TokenKeywords.strlen))
                    {
                        Parsestrlen();
                    }
                    else if (MatchKeyword(TokenKeywords.strreplace))
                    {
                        Parsereplace();
                    }
                    else if (MatchKeyword(TokenKeywords.datalen))
                    {
                        Parsedatalen();
                    }
                    else if (MatchKeyword(TokenKeywords.now))
                    {
                        Parsenow();
                    }
                    else if (MatchKeyword(TokenKeywords.timeformat))
                    {
                        Parsetimeformat();
                    }
                    else if (MatchKeyword(TokenKeywords.printnl))
                    {
                        ParsePrintnlStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.substr))
                    {
                        Parsesubstr();
                    }
                    else if (MatchKeyword(TokenKeywords.eval))
                    {
                        Parseeval();
                    }
                    else if (MatchKeyword("musicStop"))
                    {
                        ParseMusicStop();
                    }
                    else if (MatchKeyword(TokenKeywords.osinfo))
                    {
                        Parseosinfo();
                    }
                    else if (MatchKeyword(TokenKeywords.sys))
                    {
                        Parsesys();
                    }
                    else if (MatchKeyword(TokenKeywords.getCurrentFilePath))
                    {
                        ParseGetCurrentFilePath();
                    }
                    else if (MatchKeyword(TokenKeywords.getCurrentFolderPath))
                    {
                        ParseGetCurrentFolderPath();
                    }
                    else if (MatchKeyword(TokenKeywords.mapPath))
                    {
                        ParseMapPath();
                    }
                    else if (MatchKeyword(TokenKeywords.appQuit))
                    {
                        ParseAppQuit();
                    }
                    else if (TryParseAssignmentStatement())
                    {
                    }
                    else if (MatchKeyword("pause"))
                    {
                        ParsePause();
                    }
                    else if (Peek().Type == TokenTypes.Identifier && Peek().Value == TokenKeywords.classInvoke)
                    {
                        Advance();
                        ParseClassInvoke();
                    }
                    else if (Peek().Type == TokenTypes.Identifier && Peek().Value == TokenKeywords.objectInvoke)
                    {
                        Advance();
                        ParseObjectInvoke();
                    }
                    else if (MatchFunction(Peek().Value))
                    {
                        RunFunction(Peek().Value);
                    }
                    else
                    {
                        Expr expr = ParseExpression();
                        object result = EvaluateExpression(expr);
                        RememberItCandidate(result);
                        ConsumeOptionalSemicolon();
                    }
                    if (_isQuit)
                    {
                        Environment.Exit(_n);
                    }
                } while (!IsAtEnd());
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(new OutputEventArgs("\n[yellow]Execution cancelled by user[/]"));
                return;
            }
            catch (FeiSharpTerminal3._1.ExceptionThrow.Exception e)
            {
                if (_propagateFeiSharpExceptions)
                {
                    throw;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.stringBuilder.ToString());
                Console.ResetColor();
                return;
            }
        }
        private bool TryParseExtensionKeywordStatement()
        {
            if (!Check(TokenTypes.Keyword) || !ExtensionKeywordRegistry.TryGet(Peek().Value, out BaseKeyword extensionKeyword))
            {
                return false;
            }

            Advance();
            extensionKeyword.Execute(new KeywordContext(
                () => ToExtensionToken(Peek()),
                () => ToExtensionToken(Advance()),
                (type, value) =>
                {
                    if (!Enum.TryParse<TokenTypes>(type.ToString(), out TokenTypes hostType) ||
                        !Check(hostType) ||
                        (value is not null && !Peek().Value.Equals(value, StringComparison.OrdinalIgnoreCase))) return false;
                    Advance();
                    return true;
                },
                () => EvaluateExpression(ParseExpression()),
                Run));
            return true;
        }

        private static KeywordToken ToExtensionToken(Token token) => new((KeywordTokenType)token.Type, token.Value);

        private void ParseMusicStop()
        {
            if (!MatchPunctuation("(") || !MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected '()'", "FS2003");
            ConsumeOptionalSemicolon();
            player?.StopMusic();
        }

        private void ParseMusicDownload()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Excepted '('", "FS2003");
            string name = EvaluateExpression(ParseExpression())?.ToString()
                ?? throw new Exception(_tokens, _current, "Music name cannot be null", "FS3002");
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            string path = EvaluateExpression(ParseExpression())?.ToString()
                ?? throw new Exception(_tokens, _current, "Download path cannot be null", "FS3002");
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            ConsumeOptionalSemicolon();
            player ??= new MusicPlayer("online");
            player.SwitchToOnlineMode();
            player.DownloadOnlineMusic(name, path).GetAwaiter().GetResult();
        }
        private MusicPlayer player;
        internal void CopyPlayerFrom(Parser source)
        {
            player = source.player;
        }
        private void ParseMusicPause()
        {
            if (!MatchPunctuation("(") || !MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected '()'", "FS2003");
            ConsumeOptionalSemicolon();
            player?.PauseMusic();
        }
        private void ParseMusicPlay()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Excepted '('", "FS2003");
            string name = EvaluateExpression(ParseExpression())?.ToString()
                ?? throw new Exception(_tokens, _current, "Music name/path cannot be null", "FS3002");
            string mode = "online";
            if (MatchPunctuation(","))
            {
                mode = EvaluateExpression(ParseExpression())?.ToString()
                    ?? throw new Exception(_tokens, _current, "Music mode cannot be null", "FS3002");
            }
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            ConsumeOptionalSemicolon();
            if (!mode.Equals("online", StringComparison.OrdinalIgnoreCase) && !mode.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(_tokens, _current, "Music mode must be 'online' or 'local'", "FS3002");
            }
            if (player == null || !player.GetCurrentMode().Equals(mode, StringComparison.OrdinalIgnoreCase))
            {
                player?.Dispose();
                player = new MusicPlayer(mode.ToLowerInvariant());
            }
            if (player.GetPlaybackState() == "Paused") player.ResumeMusic();
            else player.PlayMusic(name).GetAwaiter().GetResult();
        }
        private void ParseUnPackageStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Excepted '('", "FS2003");
            string pkgfile = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            string topath = EvaluateExpression(ParseExpression()).ToString();
            FilePkg.Unpack(pkgfile, topath);
            Advance();
            Advance();
        }

        private void ParsePackageStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Excepted '('", "FS2003");
            object[] strs = null;
            try
            {
                strs = (object[])EvaluateExpression(ParseExpression());
            }
            catch
            {
                throw new Exception(_tokens, _current, "Argument[0] type does not match, expected type: arr_base<string>", "FS3002");
            }
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            string topath = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            bool recursive = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
            FilePkg.Pack(string.IsNullOrEmpty(Path.GetDirectoryName(strs[0].ToString())) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(strs[0].ToString()), strs.Select(i => i.ToString()).ToArray() , topath, recursive);
            Advance();
            Advance();
        }

        private void ParseBreakPointStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Excepted '('", "FS2003");
            while (Peek().Value != ")" && Peek().Type != TokenTypes.Punctuation)
            {
                if (Peek().Value == "," && Peek().Type == TokenTypes.Punctuation)
                {
                    Advance();
                    continue;
                }
                string key = Peek().Value;
                Advance();
                if (!MatchPunctuation(":")) throw new Exception(_tokens, _current, "Excepted ':'", "FS2003");
                object thing = EvaluateExpression(ParseExpression());
                if (key == "condition")
                {
                    if ((bool)thing)
                    {
                        BreakPointCore();
                        return;
                    }
                }
                DoBreakPoint(key, thing);
                if (inf)
                {
                    inf = false;
                    break;
                }
                Advance();
            }
            BreakPointCore();
        }
        public string ToDeepJson(object obj, int maxDepth = 10, int currentDepth = 0)
        {
            if (obj == null) return "null";
            if (currentDepth >= maxDepth) return "\"...\"";
            Type type = obj.GetType();
            if (type.IsPrimitive || obj is string || obj is decimal || obj is DateTime ||
                obj is Guid || obj is Enum)
            {
                return JsonSerializer.Serialize(obj);
            }
            if (obj is IEnumerable enumerable && obj is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(ToDeepJson(item, maxDepth, currentDepth + 1));
                }
                return $"[{string.Join(",", items)}]";
            }
            var dict = new Dictionary<string, string>();
            var allFields = GetAllFields(type);
            foreach (var field in allFields)
            {
                if (IsCompilerGeneratedBackingField(field.Name)) continue;

                try
                {
                    var value = field.GetValue(obj);
                    string jsonValue = ToDeepJson(value, maxDepth, currentDepth + 1);
                    dict[field.Name] = jsonValue;
                }
                catch
                {
                    dict[field.Name] = "\"<error>\"";
                }
            }
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (IsCompilerGeneratedBackingField(prop.Name)) continue;
                if (prop.CanRead)
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        string jsonValue = ToDeepJson(value, maxDepth, currentDepth + 1);
                        dict[prop.Name] = jsonValue;
                    }
                    catch
                    {
                        dict[prop.Name] = "\"<error>\"";
                    }
                }
            }
            var jsonPairs = dict.Select(kv => $"\n    \"{kv.Key}\": {kv.Value}");
            return $"{{{string.Join(",", jsonPairs)}\n    }}";
        }

        private IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            if (type == null || type == typeof(object))
                return Enumerable.Empty<FieldInfo>();

            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var fields = type.GetFields(flags);
            return fields.Concat(GetAllFields(type.BaseType));
        }

        private bool IsCompilerGeneratedBackingField(string name)
        {
            return name.Contains("k__BackingField") ||
                   (name.Contains('<') && name.Contains('>'));
        }
        bool isOnce = false;
        bool inf = false;
        private void DoBreakPoint(string key, object thing)
        {
            switch (key)
            {
                case "watch":
                    HandleWatch(thing);
                    break;
                case "eval":
                    AnsiConsole.MarkupLine($"[yellow]Expression Result[/]: {thing}");
                    break;
                case "once":
                    if (isOnce)
                    {
                        inf = true;
                        return;
                    }
                    isOnce = true;
                    return;
            }
        }

        private void HandleWatch(object thing)
        {
            string[] watchNames = GetWatchNames(thing);
            if (watchNames == null || watchNames.Length == 0) return;

            foreach (string name in watchNames)
            {
                DisplayVariableValue(name);
            }
        }

        private string[] GetWatchNames(object thing)
        {
            if (thing is string singleString)
            {
                return new[] { singleString };
            }
            if (thing is object[] objArray)
            {
                try
                {
                    return objArray.Cast<string>().ToArray();
                }
                catch (InvalidCastException)
                {
                    AnsiConsole.MarkupLine("[red]Error: Watch list contains non-string values[/]");
                    return Array.Empty<string>();
                }
            }
            if (thing is string[] strArray)
            {
                return strArray;
            }
            if (thing is IEnumerable<string> stringEnumerable)
            {
                return stringEnumerable.ToArray();
            }

            AnsiConsole.MarkupLine("[red]Error: Invalid watch parameter type[/]");
            return Array.Empty<string>();
        }

        private void DisplayVariableValue(string variableName)
        {
            if (_variables.TryGetValue(variableName, out var value))
            {
                PrintVariable(variableName, value);
                return;
            }
            if (_functions.TryGetValue(variableName, out var fvalue))
            {
                PrintFunction(variableName, fvalue);
                return;
            }
            if (_classInfos.TryGetValue(variableName, out var cvalue))
            {
                PrintClass(variableName, cvalue);
                return;
            }
            AnsiConsole.MarkupLine($"[red]Warning: '{variableName}' not found[/]");
        }

        private void PrintVariable(string name, object value)
        {
            string valueStr;

            if (value is ClassInstance classInstance)
            {
                valueStr = ToDeepJson(classInstance);
                AnsiConsole.MarkupLine($"{name}[yellow](variable)[/] : {valueStr}");
            }
            else if (IsArrayOrList(value))
            {
                valueStr = string.Join(", ", (IEnumerable<object>)value);
                AnsiConsole.MarkupLine($"{name}[yellow](variable)[/] : [{valueStr}]");
            }
            else
            {
                valueStr = value?.ToString() ?? "null";
                AnsiConsole.MarkupLine($"{name}[yellow](variable)[/] : {valueStr}");
            }
        }

        private void PrintFunction(string name, object function)
        {
            AnsiConsole.Markup($"{name}[cyan](function)[/] : ");
            Console.WriteLine(ToDeepJson(function));
        }

        private void PrintClass(string name, object classInfo)
        {
            AnsiConsole.Markup($"{name}[green](class)[/] : ");
            Console.WriteLine(ToDeepJson(classInfo));
        }

        private bool IsArrayOrList(object obj)
        {
            if (obj == null) return false;

            Type type = obj.GetType();
            return type.IsArray ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        }
        private void BreakPointCore()
        {
            AnsiConsole.MarkupLine("All [yellow]variables & const[/]: ");
            foreach (var item in _variables)
            {
                AnsiConsole.MarkupLine($"[yellow]{item.Key}[/]: [cyan1]{item.Value.ToString().Replace("[", "[[").Replace("]", "]]")}[/]");
            }
            while (true)
            {
                AnsiConsole.MarkupLine("Enter [yellow]'get token'[/], [yellow]'exit'[/], or some code: ");
                string op = Console.ReadLine();
                if (op == "exit") break;
                else if (op == "get token")
                {
                    AnsiConsole.MarkupLine($"Current Token: [yellow]{Peek().Value}[/]([green]{Peek().Type}[/])");
                }
                else
                {
                    var a = Eval(op);
                    if (a == null)
                    {
                        AnsiConsole.MarkupLine("[red]Error[/]: Not a valid statement");
                    }
                    else
                    {
                        Console.WriteLine(a);
                    }
                }
            }
            AnsiConsole.MarkupLine("[green]breakpoint[/]: [yellow]End 'breakpoint' mode at " + DateTime.Now + "[/]");
        }
        bool _isQuit = false;
        int _n = 0;
        private List<string> _builtInTypesList = [
            "integer", "string", "bool", "extendObject", "object", "char", "objectReturned", "symbol", "double", "float", "error"
        ];
        private void ParsePause()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Console.WriteLine("Program pause...(Press any key to exit)");
            Console.ReadKey(true);
            Advance();
            Advance();
        }
        private void ParseAppQuit()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int a = int.Parse(EvaluateExpression(ParseExpression()).ToString());
            _isQuit = true;
            _n = a;
            Advance();
            Advance();
        }
        [RequiresDynamicCode("FeiSharp supports script-driven reflection over CLR static members.")]
        [RequiresUnreferencedCode("FeiSharp supports script-driven reflection over CLR static members.")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields, typeof(Console))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields, typeof(string))]
        private object ParseClassInvoke()
        {
            if (!MatchPunctuation(":")) throw new Exception(_tokens, _current, "Expected ':'", "FS2003");
            var className = Peek().Value;
            Advance();
            if (Peek().Value != "in") throw new Exception(_tokens, _current, "Expected 'in'", "FS2003");
            Advance();
            string space = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected '.'", "FS2003");
            var functionName = Peek().Value;
            Advance();
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            List<object> args = new List<object>();
            while (Peek().Value != ")")
            {
                if (Peek().Value != ",")
                {
                    args.Add(EvaluateExpression(ParseExpression()));
                }
            }
            Type[] paramTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? new Type[0];
            Type? type = TypeLoader.LoadType(space + "." + className);
            if (type == null)
            {
                var assemblies = new[]
        {
            typeof(Console).Assembly,
            typeof(string).Assembly,
            Assembly.GetExecutingAssembly(),
            Assembly.GetCallingAssembly()
        };
                foreach (var assembly in assemblies)
                {
                    type = assembly.GetType(space + "." + className);
                    if (type != null)
                        break;
                }
                if (type == null)
                    throw new Exception(_tokens, _current, "Type is not correct", "FS2003");
            }
            MethodInfo method = type.GetMethod(functionName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase, null, paramTypes, null);
            PropertyInfo property = null;
            FieldInfo field = null;
            if (method == null)
            {
                property = type.GetProperty(functionName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    field = type.GetField(functionName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (field == null)
                        throw new Exception(_tokens, _current, "Method, property or field is not correct", "FS2003");
                }
            }
            var k = method == null ? (property == null ? (args.Count == 1 ? ClassInvokeField(field, args[0]) : field.GetValue(null)) : (args.Count == 1 ? ClassInvokeProperty(property, args[0]) : property.GetValue(null))) : method.Invoke(null, args.ToArray());
            Advance();
            return k;
        }
        [RequiresDynamicCode("FeiSharp supports script-driven reflection over CLR instance members.")]
        [RequiresUnreferencedCode("FeiSharp supports script-driven reflection over CLR instance members.")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields, typeof(object))]
        private object ParseObjectInvoke()
        {
            if (!MatchPunctuation(":")) throw new Exception(_tokens, _current, "Expected ':'", "FS2003");
            var varName = Peek().Value;
            Advance();
            if (Peek().Value != "in") throw new Exception(_tokens, _current, "Expected 'in'", "FS2003");
            Advance();
            string space = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected '.'", "FS2003");
            var functionName = Peek().Value;
            Advance();
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            List<object> args = new List<object>();
            while (Peek().Value != ")")
            {
                if (Peek().Value != ",")
                {
                    args.Add(EvaluateExpression(ParseExpression()));
                }
            }
            var c = _variables[varName];
            Type? type = c.GetType();
            Type[] paramTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? new Type[0];
            MethodInfo method = type.GetMethod(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy, null, paramTypes, null);
            PropertyInfo property = null;
            FieldInfo field = null;
            if (method == null)
            {
                property = type.GetProperty(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                if (property == null)
                {
                    field = type.GetField(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                    if (field == null)
                        throw new Exception(_tokens, _current, "Method, property or field is not correct", "FS2003");
                }
            }
            var k = method == null ? (property == null ? (args.Count == 1 ? ObjectInvokeField(field, args[0], c) : field.GetValue(c)) : (args.Count == 1 ? ObjectInvokeProperty(property, args[0], c) : property.GetValue(c))) : method.Invoke(c, args.ToArray());
            Advance();
            return k;
        }
        private object ObjectInvokeProperty(PropertyInfo property, object value, object obj)
        {
            property.SetValue(obj, value);
            return null;
        }
        private object ObjectInvokeField(FieldInfo property, object value, object obj)
        {
            property.SetValue(obj, value);
            return null;
        }
        private object ClassInvokeProperty(PropertyInfo property, object value)
        {
            property.SetValue(null, value);
            return null;
        }
        private object ClassInvokeField(FieldInfo property, object value)
        {
            property.SetValue(null, value);
            return null;
        }
        private void ParseGetCurrentFilePath()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varName = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(varName, Program._applicationPath);
            Advance();
            Advance();
        }
        private void ParseGetCurrentFolderPath()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varName = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(varName, Path.GetDirectoryName(Program._applicationPath));
            Advance();
            Advance();
        }
        private void ParseMapPath()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varName = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string targetValue = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(varName, Program.MapPath(targetValue, Program._applicationPath));
            Advance();
            Advance();
        }
        private void Parsesys()
        {
            if (isSystemAssembly)
            {
                if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                string objects = Peek().Value;
                if (objects == "console")
                {
                    if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                    Advance();
                    string objects1 = Peek().Value;
                    if (objects1 == "errorLine")
                    {
                        if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                        Advance();
                        string objects2 = EvaluateExpression(ParseExpression()).ToString();
                        if (objects2 == "writeText")
                        {
                            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                            string text = Peek().Value;
                            Console.Error.Write(text);
                            Advance();
                        }
                        else if (objects2 == "writeLine")
                        {
                            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                            Advance();
                            string text = EvaluateExpression(ParseExpression()).ToString();
                            Console.Error.WriteLine(text);
                            Advance();
                        }
                    }
                    else if (objects1 == "commonLine")
                    {
                        if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                        Advance();
                        string objects2 = Peek().Value;
                        if (objects2 == "writeText")
                        {
                            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                            string text = EvaluateExpression(ParseExpression()).ToString();
                            Console.Write(text);
                            Advance();
                        }
                        else if (objects2 == "writeLine")
                        {
                            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                            string text = EvaluateExpression(ParseExpression()).ToString();
                            Console.WriteLine(text);
                            Advance();
                        }
                    }
                }
                else if (objects == "memory")
                {
                    if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                    Advance();
                    string objects1 = Peek().Value;
                    if (objects1 == "collect")
                    {
                        if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                        GC.Collect();
                        Advance();
                    }
                    else if (objects1 == "showMemoryTotalValue")
                    {
                        if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                        string varname = EvaluateExpression(ParseExpression()).ToString();
                        _variables.NewAdd(varname, GC.GetTotalMemory(false));
                        Advance();
                    }
                }
                else if (objects == "emit")
                {
                    if (!MatchPunctuation(".")) throw new Exception(_tokens, _current, "Expected Objects Name", "FS2003");
                    Advance();
                    string objects1 = Peek().Value;
                    if (objects1 == "typeof")
                    {
                        if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                        Advance();
                        string type = Peek().Value;
                        string res = "";
                        if (_builtInTypesList.Contains(type))
                        {
                            res =
                                $"FeiSharpAssembly_{GetCurrentAssemblyName()}, Version 1.0.0.0, InvariantCulture, type: {type}, in ./std/{type}.f";
                        }
                        Advance();
                        if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                        string varname = EvaluateExpression(ParseExpression()).ToString();
                        _variables.NewAdd(varname, res);
                    }
                    Advance();
                }
            }
            else
            {
                throw new Exception(_tokens, _current, "A error " + _current + " was detected as a static-object name, but the corresponding namespace was not applied: FeiSharp.System", "FS3001");
            }
        }
        private string GetCurrentAssemblyName()
        {
            return FeiSharpProgramData.AssemblyName;
        }
        private void Parseosinfo()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(strvarname, Environment.OSVersion.ToString());
            Advance();
            Advance();
        }
        private void Parseeval()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            Run(strvarname);
            Advance();
            Advance();
        }
        private void Parsesubstr()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            int x = int.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, strvarname.Substring(x));
            Advance();
            Advance();
        }
        private void Parsesqrt()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double malimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Pow(milimit, 1 / malimit));
            Advance();
            Advance();
        }
        private void ParsePrintnlStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            Console.WriteLine(varname);
            Advance();
            Advance();
        }
        private void Parsetimeformat()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string format = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(varname, DateTime.Now.ToString(format));
            Advance();
            Advance();
        }
        private void Parsepow()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double malimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Pow(milimit, malimit));
            Advance();
            Advance();
        }
        private void Parsesin()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Sin(milimit));
            Advance();
            Advance();
        }
        private void Parsecos()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Cos(milimit));
            Advance();
            Advance();
        }
        private void Parsetan()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Tan(milimit));
            Advance();
            Advance();
        }
        private void Parseasin()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Asin(milimit));
            Advance();
            Advance();
        }
        private void Parseacos()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Acos(milimit));
            Advance();
            Advance();
        }
        private void Parseatan()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Atan(milimit));
            Advance();
            Advance();
        }
        private void Parseabs()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, Math.Abs(milimit));
            Advance();
            Advance();
        }
        private void Parsefromindex()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, _variables[strvarname].ToString()[(int)milimit]);
            Advance();
            Advance();
        }
        private void Parsegetindex()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            char milimit = char.Parse(EvaluateExpression(ParseExpression()).ToString());
            _variables.NewAdd(varname, _variables[strvarname].ToString().IndexOf(milimit));
            Advance();
            Advance();
        }
        private void Parsenow()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(strvarname, DateTime.Now.ToString());
            Advance();
            Advance();
        }
        private void Parsereplace()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string target = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string replace = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(strvarname, _variables[strvarname].ToString().Replace(target, replace));
            Advance();
            Advance();
        }
        private void Parsestrlen()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string target = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(strvarname, target.Length);
            Advance();
            Advance();
        }
        private void Parsedatalen()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string strvarname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string target = EvaluateExpression(ParseExpression()).ToString();
            _variables.Add(strvarname, target.Split('{')[1].Split('}')[0].Split(',').Length);
            Advance();
            Advance();
        }
        private void ParseRand()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double milimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double malimit = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (milimit >= malimit)
            {
                throw new Exception(_tokens, _current, "rand: args is invalid", "FS3003");
            }
            else
            {
                _variables.NewAdd(varname, Random.Shared.Next((int)milimit, (int)malimit));
            }
            Advance();
            Advance();
        }
        bool isfileassembly = false;
        bool isjsonassembly = false;
        bool isnetassembly = false;
        Dictionary<string, string> modals = new Dictionary<string, string>();
        private void ParsePrintMethod()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string text = EvaluateExpression(ParseExpression()).ToString();
            if (_functions.ContainsKey(text))
            {
                if (strings.IndexOf(text) != -1)
                {
                    Console.WriteLine(new OutputEventArgs(strings[strings.IndexOf(text)]));
                }
                else
                {
                    Console.WriteLine(new OutputEventArgs("{Method:" + text + ",Guid:" + Guid.NewGuid().ToString() + "}"));
                    strings.Add("{Method:" + text + ",Guid:" + Guid.NewGuid().ToString() + "}");
                }
            }
            else
            {
                throw new Exception(_tokens, _current, $"the text \"{text}\" is not a function name.", "FS3002");
            }
            Advance();
            Advance();
        }
        private void ParseReplaceData()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            object value = EvaluateExpression(ParseExpression());
            var vari = _variables[name].ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            vari = vari.Replace(value.ToString() + ",", EvaluateExpression(ParseExpression()).ToString() + ",");
            _variables[name] = vari;
            Advance();
            Advance();
        }
        private void ParseSetBase()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            var nameinfo = _classInfos[name];
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string namebase = EvaluateExpression(ParseExpression()).ToString();
            var namebaseinfo = _classInfos[namebase];
            foreach (var i in namebaseinfo._Vars)
            {
                if (!nameinfo._Vars.ContainsKey(i.Key))
                {
                    nameinfo._Vars.Add(i.Key, i.Value);
                }
                else
                {
                    continue;
                }
            }
            foreach (var i in namebaseinfo._FunctionInfo)
            {
                if (!nameinfo._FunctionInfo.ContainsKey(i.Key))
                {
                    nameinfo._FunctionInfo.Add(i.Key, i.Value);
                }
                else
                {
                    continue;
                }
            }
            _classInfos[name] = nameinfo;
            Advance();
            Advance();
        }
        private void ParseSetClassVar()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string classname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Excepted ','", "FS2003");
            object value = EvaluateExpression(ParseExpression());
            _classInfos[classname]._Vars[varname] = value;
            Advance();
            Advance();
        }
        private void ParseInstance()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string t = EvaluateExpression(ParseExpression()).ToString();
            if (t == "system.string")
            {
                _variables.NewAdd(name, String.Empty);
            }
            else if (t == "system.double")
            {
                _variables.NewAdd(name, default(Double));
            }
            else if (t == "system.boolean")
            {
                _variables.NewAdd(name, default(Boolean));
            }
            else
            {
                throw new Exception(_tokens, _current, $"the text \"{t}\" is not a readonlyclass name.", "FS3002");
            }
            Advance();
            Advance();
        }
        private void ParseSaveDataChange()
        {
            Console.Write("This application want to write your file, do you agree it?(y/n)");
            var _ = Console.ReadKey();
            Console.WriteLine();
            if (_.Key == ConsoleKey.Y)
            {
            }
            else
            {
                throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
            }
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string path = EvaluateExpression(ParseExpression()).ToString();
            File.WriteAllText(path, _variables[name].ToString());
            Advance();
            Advance();
        }
        private void ParseInvokeData()
        {
            Console.Write("This application want to read your file, do you agree it?(y/n)");
            var _ = Console.ReadKey();
            Console.WriteLine();
            if (_.Key == ConsoleKey.Y)
            {
            }
            else
            {
                throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
            }
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string path = EvaluateExpression(ParseExpression()).ToString();
            if (Advance().Value != "to") throw new Exception(_tokens, _current, "Expected 'to' keyword", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(name, File.ReadAllText(path));
            Advance();
            Advance();
        }
        private void ParseGetData()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            int index = int.Parse(EvaluateExpression(ParseExpression()).ToString());
            var datas = _variables[name].ToString().Split('{')[1].Split("}")[0].Split(',');
            for (int i1 = 0; i1 < datas.Length; i1++)
            {
                if (i1 == index)
                {
                    _variables.NewAdd(varname, datas[i1]);
                }
            }
            Advance();
            Advance();
        }
        private void ParseCreateDataStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(name, "{}");
            Advance();
            Advance();
        }
        private void ParseAddDataStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            object value = EvaluateExpression(ParseExpression());
            var vari = _variables[name].ToString();
            vari = vari.Insert(vari.Length - 1, value.ToString() + ",");
            _variables[name] = vari;
            Advance();
            Advance();
        }
        private void ParseDelDataStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            object value = EvaluateExpression(ParseExpression());
            var vari = _variables[name].ToString();
            vari = vari.Replace(value.ToString() + ",", "");
            _variables[name] = vari;
            Advance();
            Advance();
        }
        private void ParseAstextboxStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string endValue = EvaluateExpression(ParseExpression()).ToString();
            string alltext = "";
            string readlinetxt = "";
            readlinetxt = Console.ReadLine();
            while (readlinetxt != endValue)
            {
                readlinetxt = Console.ReadLine();
                alltext += readlinetxt;
            }
            _variables.NewAdd(varname, alltext);
            Advance();
            Advance();
        }
        private void ParseCStRStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            object convertItem = EvaluateExpression(ParseExpression());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            _variables.NewAdd(varname, convertItem.ToString());
            Advance();
            Advance();
        }
        private void ParseCTypeStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            object convertItem = EvaluateExpression(ParseExpression());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string type = "string";
            type = EvaluateExpression(ParseExpression()).ToString();
            Type typeT = null;
            if (type == "number")
            {
                typeT = typeof(double);
            }
            else if (type == "string")
            {
                typeT = typeof(string);
            }
            else if (type == "boolean")
            {
                typeT = typeof(bool);
            }
            object convertedItem = Convert.ChangeType(convertItem, typeT);
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            string varname = EvaluateExpression(ParseExpression()).ToString();
            if (convertedItem is double)
            {
                _variables.NewAdd(varname, Convert.ToInt32(convertedItem));
            }
            else if (convertedItem is string)
            {
                _variables.NewAdd(varname, convertedItem.ToString());
            }
            else
            {
                _variables.NewAdd(varname, bool.Parse(convertedItem.ToString()));
            }
            Advance();
            Advance();
        }
        private void ParseReadLineStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (name == "_")
            {
                Console.ReadLine();
            }
            else
            {
                _variables.NewAdd(name, Console.ReadLine());
            }
            Advance();
            Advance();
        }
        private void ParseReadKeyStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (name == "_")
            {
                Console.ReadKey();
            }
            else
            {
                // Interactive programs read into the same variable repeatedly.
                // Dictionary assignment creates it on first use and updates it later.
                _variables[name] = Console.ReadKey().KeyChar.ToString();
            }
            Console.WriteLine();
            Advance();
            Advance();
        }
        private void ParseDefineStatement()
        {
            string context = EvaluateExpression(ParseExpression()).ToString();
            if (context == "macro")
            {
                try
                {
                    string modalName = EvaluateExpression(ParseExpression()).ToString();
                    string modalSet = EvaluateExpression(ParseExpression()).ToString();
                    modals.Add(modalName, modalSet);
                    _variables.NewAdd(modalName, modalSet);
                }
                catch
                {
                    Console.WriteLine("Enter STRING_OBJ('modalName' or 'modalSet') is not valid.");
                }
            }
            else if (context == "edit")
            {
                try
                {
                    string id = EvaluateExpression(ParseExpression()).ToString();
                    string value = EvaluateExpression(ParseExpression()).ToString();
                    modals[id] = value;
                    _variables[id] = value;
                }
                catch
                {
                    Console.WriteLine("Enter STRING_OBJ('id' or 'value') is not valid.");
                }
            }
            else if (context == "view")
            {
                if (modals.Count == 0)
                {
                    Console.WriteLine("MODALS_OBJS: It is empty.");
                }
                else
                {
                    foreach (var item in modals)
                    {
                        Console.WriteLine("[" + item.Key + ":" + item.Value + "]" + "\r\n");
                    }
                    Console.WriteLine(modals.Count + " modals in MODALS_OBJS.");
                }
            }
            else
            {
                Console.WriteLine(context + ": It is not a correct DEFINE_OBJ.");
                Advance();
            }
            Advance();
        }
        private void ParseAnnotationStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string context = EvaluateExpression(ParseExpression()).ToString();
            Debug.WriteLine("code annotation:" + context);
            Advance();
            Advance();
        }
        private bool isSystemAssembly = false;
        private void ParseNamespaceStatement()
        {
            bool isFileScope = CheckPunctuation("{");
            string declaredNamespace = isFileScope ? string.Empty : ParseQualifiedName("Expected namespace name");
            string? effectiveNamespace = isFileScope ? null : string.IsNullOrEmpty(_currentNamespace)
                ? declaredNamespace
                : $"{_currentNamespace}.{declaredNamespace}";

            // A file-scoped namespace owns every declaration that follows it.
            // This is especially useful in source files brought in by `import`.
            if (MatchPunctuation(";"))
            {
                _currentNamespace = effectiveNamespace;
                return;
            }

            List<Token> body = ParseScopedTokens("{", "}", "namespace");

            var namespaceParser = new Parser(body)
            {
                player = player,
                OutputEvent = OutputEvent,
                _variables = _variables,
                _variableInfos = _variableInfos,
                _functions = _functions,
                _classInfos = _classInfos,
                _results = _results,
                ShouldCancel = ShouldCancel,
                _propagateFeiSharpExceptions = _propagateFeiSharpExceptions,
                _currentNamespace = effectiveNamespace,
                _usingNamespaces = isFileScope ? new(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase)
            };
            foreach (string reference in namespaceParser._usingNamespaces)
                namespaceParser.EnableBuiltInNamespace(reference);
            namespaceParser.ParseStatements();

            _variables = namespaceParser._variables;
            _variableInfos = namespaceParser._variableInfos;
            _functions = namespaceParser._functions;
            _classInfos = namespaceParser._classInfos;
            _results = namespaceParser._results;
            player = namespaceParser.player;
        }

        private void ParseUsingNamespaceStatement()
        {
            string namespaceName = ParseQualifiedName("Expected namespace name after 'using'");
            _usingNamespaces.Add(namespaceName);
            EnableBuiltInNamespace(namespaceName);
            ConsumeOptionalSemicolon();
        }

        // Native statements predate the script-level namespace feature. Keep
        // them available through `using`, not through a string-valued import.
        private void EnableBuiltInNamespace(string namespaceName)
        {
            if (namespaceName.Equals("FeiSharp.IO", StringComparison.OrdinalIgnoreCase))
            {
                isfileassembly = true;
            }
            else if (namespaceName.Equals("FeiSharp.Text.Json", StringComparison.OrdinalIgnoreCase))
            {
                isjsonassembly = true;
            }
            else if (namespaceName.Equals("FeiSharp.Net", StringComparison.OrdinalIgnoreCase))
            {
                isnetassembly = true;
            }
            else if (namespaceName.Equals("FeiSharp.System", StringComparison.OrdinalIgnoreCase))
            {
                isSystemAssembly = true;
            }
        }

        private string ParseQualifiedName(string errorMessage)
        {
            if (!MatchMemberName(out string firstSegment))
            {
                throw new Exception(_tokens, _current, errorMessage, "FS2003");
            }

            var segments = new List<string> { firstSegment };
            while (MatchPunctuation("."))
            {
                if (!MatchMemberName(out string segment))
                {
                    throw new Exception(_tokens, _current, "Expected name after '.'", "FS2003");
                }
                segments.Add(segment);
            }
            return string.Join('.', segments);
        }

        private string QualifyDeclaredName(string name) => string.IsNullOrEmpty(_currentNamespace)
            ? name
            : $"{_currentNamespace}.{name}";

        private bool TryResolveFunctionName(string name, out string resolvedName, out FunctionInfo functionInfo)
        {
            if (!name.Contains('.') && !string.IsNullOrEmpty(_currentNamespace))
            {
                string localName = $"{_currentNamespace}.{name}";
                if (_functions.TryGetValue(localName, out functionInfo!))
                {
                    resolvedName = localName;
                    return true;
                }
            }

            if (_functions.TryGetValue(name, out functionInfo!))
            {
                resolvedName = name;
                return true;
            }

            if (!name.Contains('.'))
            {
                foreach (string importedNamespace in _usingNamespaces)
                {
                    string importedName = $"{importedNamespace}.{name}";
                    if (_functions.TryGetValue(importedName, out functionInfo!))
                    {
                        resolvedName = importedName;
                        return true;
                    }
                }
            }

            resolvedName = string.Empty;
            functionInfo = null!;
            return false;
        }

        private void ParseImportStatement()
        {
            string namespaceName = ParseQualifiedName("Expected namespace name after 'import'");
            _usingNamespaces.Add(namespaceName);
            EnableBuiltInNamespace(namespaceName);
            ConsumeOptionalSemicolon();
        }
        private void ParseReadStatement()
        {
            CheckCancellation();
            if (isfileassembly)
            {
                Console.Write("This application want to read your file, do you agree it?(y/n)");
                var _ = Console.ReadKey();
                Console.WriteLine();
                if (_.Key == ConsoleKey.Y)
                {
                }
                else
                {
                    throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
                }
                CheckCancellation();
                if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchKeyword("to")) throw new Exception(_tokens, _current, "Expected 'to' keyword", "FS2003");
                string path = EvaluateExpression(ParseExpression()).ToString();
                Advance();
                CheckCancellation();
                try
                {
                    _variables.NewAdd(varname, File.ReadAllText(path));
                }
                catch
                {
                    _variables[varname] = File.ReadAllText(path);
                }
                Advance();
            }
            else
            {
                throw new Exception(_tokens, _current, "A error " + _current + " was detected as a function name, but the corresponding namespace was not applied: FeiSharp.IO", "FS3001");
            }
        }
        private KeyValuePair<string, bool> Runclass(string name, Dictionary<string, ClassInfo> _classInfos = null)
        {
            if (_classInfos == null)
            {
                _classInfos = this._classInfos;
            }
            ClassInfo classInfo;
            try
            {
                classInfo = _classInfos[name];
            }
            catch
            {
                throw new Exception(_tokens, _current, "Variable, function or class not defined: " + name, FeiSharpErrorCodes.UndefinedSymbol);
            }
            string funcorvarname = "";
            bool isFunc = default;
            if (Peek().Value == "(")
            {
                classInfo = RunClassConstructor(name, classInfo);
            }
            foreach (var item in classInfo._Vars)
            {
                _variables[item.Key] = item.Value;
            }
            if (classInfo != null)
            {
                if (Peek().Value == ".")
                {
                    Advance();
                    if (classInfo._FunctionInfo.ContainsKey(Peek().Value))
                    {
                        funcorvarname = Peek().Value;
                        isFunc = true;
                        RunFunction(Peek().Value, classInfo._FunctionInfo[Peek().Value], classInfo);
                    }
                    else if (classInfo._Vars.ContainsKey(Peek().Value))
                    {
                        funcorvarname = Peek().Value;
                        isFunc = false;
                        Advance();
                    }
                }
            }
            return new(funcorvarname, isFunc);
        }
        private KeyValuePair<string, bool> RunClassInstance(string name, Dictionary<string, ClassInstance> instances = null)
        {
            ClassInstance classInfo;

            try
            {
                if (instances != null && instances.ContainsKey(name))
                    classInfo = instances[name];
                else if (_variables.ContainsKey(name) && _variables[name] is ClassInstance)
                    classInfo = _variables[name] as ClassInstance;
                else
                    throw new Exception(_tokens, _current, "类实例未定义: " + name, "FS3001");
            }
            catch
            {
                throw new Exception(_tokens, _current, "类实例未定义: " + name, "FS3001");
            }

            string funcorvarname = "";
            bool isFunc = false;
            object returnValue = null;

            var savedVariables = new Dictionary<string, object>(_variables);
            var savedResults = new Dictionary<string, object>(_results);

            var instanceVariablesAdded = new List<string>();
            foreach (var item in classInfo.Variables)
            {
                if (!_variables.ContainsKey(item.Key))
                {
                    _variables[item.Key] = item.Value;
                    instanceVariablesAdded.Add(item.Key);
                }
            }

            try
            {
                if (classInfo != null)
                {
                    if (Peek().Value == ".")
                    {
                        Advance();
                        if (classInfo.Functions.ContainsKey(Peek().Value))
                        {
                            funcorvarname = Peek().Value;
                            isFunc = true;
                            List<object> args = new List<object>();
                            Advance();

                            if (MatchPunctuation("("))
                            {
                                while (Peek().Value != ")")
                                {
                                    if (Peek().Value == ",")
                                    {
                                        Advance();
                                        continue;
                                    }
                                    args.Add(EvaluateExpression(ParseExpression()));
                                }
                                Advance();
                            }

                            returnValue = classInfo.CallMethod(funcorvarname, args, this);
                            if (returnValue != null)
                            {
                                _variables[$"{name}:return"] = returnValue;
                                _results[name] = returnValue;
                            }
                        }
                        else if (classInfo.Variables.ContainsKey(Peek().Value))
                        {
                            funcorvarname = Peek().Value;
                            isFunc = false;
                        }
                    }

                    foreach (var item in classInfo.Variables.Keys.ToList())
                    {
                        if (_variables.ContainsKey(item))
                        {
                            classInfo.Variables[item] = _variables[item];
                        }
                    }
                }
            }
            finally
            {

                _variables.Clear();
                foreach (var item in savedVariables)
                {
                    _variables[item.Key] = item.Value;
                }
                _results.Clear();
                foreach (var item in savedResults)
                {
                    _results[item.Key] = item.Value;
                }

                if (returnValue != null)
                {
                    _variables[$"{name}:last_return"] = returnValue;
                }
            }

            return new KeyValuePair<string, bool>(funcorvarname, isFunc);
        }
        private void ParseClassStatement()
        {
            string className = Peek().Value;
            Advance();
            string? baseClassName = null;
            if (MatchPunctuation(":"))
            {
                baseClassName = Peek().Value;
                Advance();
            }
            List<Token> tokens = ParseScopedTokens("{", "}", "class", "cbegin", "cend");
            ClassInfo classInfo = BuildClassInfo(className, baseClassName, tokens);
            _classInfos[className] = classInfo;
        }
        internal Dictionary<string, ClassInfo> _classInfos = new Dictionary<string, ClassInfo>();
        private void ParseInvokeStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string uuid = EvaluateExpression(ParseExpression()).ToString();
            if (uuid == UUIDData.AndUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                bool bool2 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.NewAdd(varname, (object)(bool1 && bool2));
                }
                catch
                {
                    _variables[varname] = (object)(bool1 && bool2);
                }
                Advance();
                Advance();
            }
            else if (uuid == UUIDData.OrUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                bool bool2 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.NewAdd(varname, (object)(bool1 || bool2));
                }
                catch
                {
                    _variables[varname] = (object)(bool1 || bool2);
                }
                Advance();
                Advance();
            }
            else if (uuid == UUIDData.NotUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.NewAdd(varname, (object)(!bool1));
                }
                catch
                {
                    _variables[varname] = (object)(!bool1);
                }
                Advance();
                Advance();
            }
        }
        private void ParseGetJsonFilePathStatement()
        {
            Console.Write("This application want to read your file, do you agree it?(y/n)");
            var _ = Console.ReadKey();
            Console.WriteLine();
            if (_.Key == ConsoleKey.Y)
            {
            }
            else
            {
                throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
            }
            if (isjsonassembly)
            {
                if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                string a = File.ReadAllText(EvaluateExpression(ParseExpression()).ToString());
                Console.WriteLine(a);
                Advance();
                Advance();
            }
            else
            {
                throw new Exception(_tokens, _current, "A error " + _current + " was detected as a function name, but the corresponding namespace was not applied: FeiSharp.Text.Json", "FS3001");
            }
        }
        private void ParseGetHtmlStatement()
        {
            CheckCancellation();
            if (isnetassembly)
            {
                if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                string url = EvaluateExpression(ParseExpression()).ToString();
                CheckCancellation();
                string content = "";
                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    try
                    {
                        HttpClient client = new HttpClient();
                        var task = client.GetAsync(url, cts.Token);
                        while (!task.IsCompleted)
                        {
                            if (ShouldCancel != null && ShouldCancel())
                            {
                                cts.Cancel();
                                throw new OperationCanceledException("Network request cancelled by user");
                            }
                            Thread.Sleep(100);
                        }
                        HttpResponseMessage response = task.Result;
                        if (response.IsSuccessStatusCode)
                        {
                            content = response.Content.ReadAsStringAsync().Result;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
                CheckCancellation();
                if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
                string a = EvaluateExpression(ParseExpression()).ToString();
                try
                {
                    _variables.NewAdd(a, content);
                }
                catch
                {
                    _variables[a] = content;
                }
                Advance();
                Advance();
            }
            else
            {
                throw new Exception(_tokens, _current, "A error " + _current + " was detected as a function name, but the corresponding namespace was not applied: FeiSharp.Net", "FS3001");
            }
        }
        private void ParseReturnStatement(string funcName)
        {
            if (string.IsNullOrWhiteSpace(funcName))
            {
                throw new Exception(_tokens, _current, "return can only be used inside a function or constructor.", "FS2003");
            }
            if (CheckPunctuation(";") || CheckPunctuation("}"))
            {
                _results[funcName] = null;
                _variables[$"{funcName}:return"] = null;
                return;
            }
            _results[funcName] = EvaluateExpression(ParseExpression());
            _variables[$"{funcName}:return"] = _results[funcName];
        }
        private object? GetFunctionReturnValueOrDefault(string funcName)
        {
            if (_variables.TryGetValue($"{funcName}:return", out object? value))
            {
                return value;
            }
            if (_results.TryGetValue(funcName, out value))
            {
                return value;
            }
            return null;
        }
        private void ParseThrowStatement()
        {
            string msg = EvaluateExpression(ParseExpression()).ToString();
            throw new Exception(_tokens, _current, msg, "FS4001", "UserException");
        }
        private bool TryParseModifiedDeclaration()
        {
            if (!Check(TokenTypes.Keyword) || !IsDeclarationModifier(Peek().Value))
            {
                return false;
            }
            if (Peek().Value.EndsWith("_readonly")) throw new Exception(_tokens, _current, "Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            MethodVisibility? explicitVisibility = null;
            bool isStatic = false;
            while (Check(TokenTypes.Keyword) && IsDeclarationModifier(Peek().Value))
            {
                string modifier = Advance().Value;
                if (modifier.Equals(TokenKeywords._private, StringComparison.OrdinalIgnoreCase))
                {
                    explicitVisibility = MergeVisibilityModifier(explicitVisibility, MethodVisibility.Private);
                }
                else if (modifier.Equals(TokenKeywords._protected, StringComparison.OrdinalIgnoreCase))
                {
                    explicitVisibility = MergeVisibilityModifier(explicitVisibility, MethodVisibility.Protected);
                }
                else if (modifier.Equals(TokenKeywords._public, StringComparison.OrdinalIgnoreCase))
                {
                    explicitVisibility = MergeVisibilityModifier(explicitVisibility, MethodVisibility.Public);
                }
                else if (modifier.Equals(TokenKeywords._static, StringComparison.OrdinalIgnoreCase))
                {
                    isStatic = true;
                }
            }

            if (MatchKeyword(TokenKeywords.func))
            {
                ValidateFunctionModifiers(explicitVisibility, isStatic);
                ParseFunctionStatement(explicitVisibility, isStatic);
                return true;
            }

            if (MatchKeyword(TokenKeywords._var))
            {
                ValidateVariableModifiers(explicitVisibility, isStatic);
                ParseVariableDeclaration(explicitVisibility, isStatic);
                return true;
            }

            if (MatchKeyword(TokenKeywords._const))
            {
                ValidateVariableModifiers(explicitVisibility, isStatic);
                ParseVariableDeclaration(explicitVisibility, isStatic, isConst: true);
                return true;
            }

            if (MatchKeyword(TokenKeywords.readonlyclass))
            {
                if (explicitVisibility != null)
                {
                    throw new Exception(_tokens, _current, $"'{explicitVisibility.Value.ToString().ToLowerInvariant()}' can only be used before function declarations.", "FS2003");
                }
                ParseClassStatement();
                return true;
            }

            throw new Exception(_tokens, _current, "Declaration modifiers must be followed by 'function', 'var', 'const' or 'class'.", "FS2003");
        }
        private static bool IsDeclarationModifier(string value)
        {
            return value.Equals(TokenKeywords._private, StringComparison.OrdinalIgnoreCase)
                || value.Equals(TokenKeywords._protected, StringComparison.OrdinalIgnoreCase)
                || value.Equals(TokenKeywords._public, StringComparison.OrdinalIgnoreCase)
                || value.Equals(TokenKeywords._static, StringComparison.OrdinalIgnoreCase)
                || value.Equals(TokenKeywords._abstract, StringComparison.OrdinalIgnoreCase);
        }
        private MethodVisibility MergeVisibilityModifier(MethodVisibility? currentVisibility, MethodVisibility nextVisibility)
        {
            if (currentVisibility != null && currentVisibility != nextVisibility)
            {
                throw new Exception(_tokens, _current, "Only one visibility modifier can be used on the same declaration.", "FS2003");
            }
            return nextVisibility;
        }
        private void ValidateFunctionModifiers(MethodVisibility? visibility, bool isStatic)
        {
            if (!_isParsingClassBody && visibility == MethodVisibility.Public)
            {
                throw new Exception(_tokens, _current, "'public' can only be used for class members.", "FS3001");
            }
            if (!_isParsingClassBody && visibility == MethodVisibility.Protected)
            {
                throw new Exception(_tokens, _current, "'protected' can only be used for class members.", "FS3001");
            }
            if (!_isParsingClassBody && isStatic)
            {
                throw new Exception(_tokens, _current, "'static' can only be used for class members.", "FS3001");
            }
        }
        private void ValidateVariableModifiers(MethodVisibility? visibility, bool isStatic)
        {
            if (!_isParsingClassBody && visibility == MethodVisibility.Public)
            {
                throw new Exception(_tokens, _current, "'public' can only be used for class members.", "FS3001");
            }
            if (!_isParsingClassBody && visibility == MethodVisibility.Protected)
            {
                throw new Exception(_tokens, _current, "'protected' can only be used for class members.", "FS3001");
            }
            if (!_isParsingClassBody && isStatic)
            {
                throw new Exception(_tokens, _current, "'static' can only be used for class members.", "FS3001");
            }
        }
        private void ParseVisibilityQualifiedFunctionStatement(MethodVisibility visibility)
        {
            if (!MatchKeyword(TokenKeywords.func))
            {
                throw new Exception(_tokens, _current, $"'{visibility.ToString().ToLowerInvariant()}' can only be used before function declarations.", "FS2003");
            }
            ValidateFunctionModifiers(visibility, false);
            ParseFunctionStatement(visibility, false);
        }
        private void ParseTryCatchStatement(string funcName)
        {
            List<Token> tryTokens = ParseScopedTokens("{", "}", "try");
            if (!MatchKeyword(TokenKeywords._catch))
            {
                throw new Exception(_tokens, _current, "Expected 'catch' after try block", "FS2003");
            }
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            if (!MatchToken(TokenTypes.Identifier, out string typeVar))
            {
                throw new Exception(_tokens, _current, "Expected error type variable name", "FS2003");
            }
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            List<Token> catchTokens = ParseScopedTokens("{", "}", "catch");
            try
            {
                ExecuteNestedBlock(tryTokens, funcName, _variables, CloneClassInfos(_classInfos));
            }
            catch (Exception ex)
            {
                _variables[typeVar] = new ClassInstance()
                {
                    Variables =
                    {
                        {"type", ex.ErrorType},
                        {"msg", ex.Message},
                        {"index", ex.Number }
                    },
                    VariableInfos =
                    {
                        {"type", new("type",MethodVisibility.Public, false, true, typeVar) },
                        {"msg", new("msg",MethodVisibility.Public, false, true, typeVar) },
                        {"index", new("index",MethodVisibility.Public, false, true, typeVar) }
                    },
                };
                ExecuteNestedBlock(catchTokens, funcName, _variables, CloneClassInfos(_classInfos));
            }
        }
        private void ParseForEachStatement()
        {
            if (!MatchPunctuation("("))
                throw new Exception(_tokens, _current, "Expected '(' after 'foreach'", "FS2003");
            if (!MatchKeyword(TokenKeywords._var))
                throw new Exception(_tokens, _current, "Expected 'var' in foreach", "FS2003");
            if (!MatchToken(TokenTypes.Identifier, out string itemName))
                throw new Exception(_tokens, _current, "Expected loop variable name", "FS2003");
            if (!MatchKeyword("in") && !MatchWord("in"))
                throw new Exception(_tokens, _current, "Expected 'in' keyword", "FS2003");
            object iterable = EvaluateExpression(ParseExpression());
            if (!MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected ')' after foreach header", "FS2003");
            if (!MatchPunctuation("{"))
                throw new Exception(_tokens, _current, "Expected '{' to start foreach body", "FS2003");
            int bodyStartIndex = _current;
            int braceDepth = 1;
            while (braceDepth > 0 && !IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Value == "{") braceDepth++;
                if (token.Value == "}") braceDepth--;
            }
            int bodyEndIndex = _current - 1;
            int afterBodyIndex = _current;
            bool hadPrevious = _variables.ContainsKey(itemName);
            object? previousValue = hadPrevious ? _variables[itemName] : null;
            int loopCount = 0;
            try
            {
                foreach (object item in GetIterableItems(iterable))
                {
                    loopCount++;
                    if (loopCount % CancelCheckInterval == 0)
                        CheckCancellation();

                    _variables[itemName] = item;
                    ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex, _currentFunctionName);
                    if (_isQuit)
                    {
                        Environment.Exit(_n);
                    }
                }
            }
            finally
            {
                if (hadPrevious)
                    _variables[itemName] = previousValue!;
                else
                    _variables.Remove(itemName);
            }
            _current = afterBodyIndex;
        }
        private IEnumerable<object> GetIterableItems(object iterable)
        {
            if (iterable == null)
                throw new Exception(_tokens, _current, "Cannot iterate over a null value", "FS3001");

            if (iterable is string str)
            {
                foreach (char c in str) yield return c;
                yield break;
            }

            if (iterable is IEnumerable enumerable)
            {
                foreach (object item in enumerable) yield return item;
                yield break;
            }

            throw new Exception(_tokens, _current,
                $"Type '{iterable.GetType().Name}' is not iterable", "FS3002");
        }
        private void ExecuteStatementsInRange(int startPos, int endPos, string? funcName = null)
        {
            int savedPosition = _current;
            string activeFunctionName = funcName ?? _currentFunctionName ?? "";

            try
            {
                _current = startPos;

                while (_current < endPos && !IsAtEnd())
                {
                    if (Peek().Type == TokenTypes.Punctuation && Peek().Value == ";")
                    {
                        Advance();
                        continue;
                    }

                    if (MatchKeyword(TokenKeywords._var))
                    {
                        ParseVariableDeclaration();
                    }
                    else if (MatchKeyword(TokenKeywords._const))
                    {
                        ParseVariableDeclaration(isConst: true);
                    }
                    else if (MatchKeyword(TokenKeywords.print))
                    {
                        PrintStmt printStmt = ParsePrintStatement();
                        EvaluatePrintStmt(printStmt);
                    }
                    else if (MatchKeyword(TokenKeywords.init))
                    {
                        ParseInitStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.set))
                    {
                        ParseSetStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.run))
                    {
                        ParseRunStatement();
                    }
                    else if (MatchWord(TokenKeywords.export))
                    {
                        ParseExportStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.start))
                    {
                        ParseStartStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.stop))
                    {
                        ParseStopStatement();
                    }
                    else if (MatchKeyword("forEach"))
                    {
                        ParseForEachStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.wait))
                    {
                        ParseWaitStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.watchstart))
                    {
                        ParseWatchStartStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.watchend))
                    {
                        ParseWatchEndStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.abe))
                    {
                        ParseABEStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.helper))
                    {
                        ParseHelperStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._if))
                    {
                        ParseIfStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._for))
                    {
                        ParseForStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._while))
                    {
                        ParseWhileStatement();
                    }
                    else if (TryParseModifiedDeclaration())
                    {
                    }
                    else if (MatchKeyword(TokenKeywords.func))
                    {
                        ParseFunctionStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.dowhile))
                    {
                        ParseDowhileStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._throw))
                    {
                        ParseThrowStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._try))
                    {
                        ParseTryCatchStatement(activeFunctionName);
                    }
                    else if (MatchKeyword(TokenKeywords._return))
                    {
                        ParseReturnStatement(activeFunctionName);
                    }
                    else if (MatchKeyword(TokenKeywords.gethtml))
                    {
                        ParseGetHtmlStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.getVarsFromJsonFilePath))
                    {
                        ParseGetJsonFilePathStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readonlyclass))
                    {
                        ParseClassStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.invoke))
                    {
                        ParseInvokeStatement();
                    }
                    else if (Check(TokenTypes.Keyword) && Peek().Value == TokenKeywords.read && Peek(1).Value == "(")
                    {
                        Advance();
                        ParseReadStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.import))
                    {
                        ParseImportStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.annotation))
                    {
                        ParseAnnotationStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.define))
                    {
                        ParseDefineStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readline))
                    {
                        ParseReadLineStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.readkey))
                    {
                        ParseReadKeyStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.ctype))
                    {
                        ParseCTypeStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.cstr))
                    {
                        ParseCStRStatement();
                    }
                    else if (MatchKeyword(TokenKeywords._astextbox))
                    {
                        ParseAstextboxStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.createData))
                    {
                        ParseCreateDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.addData))
                    {
                        ParseAddDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.delData))
                    {
                        ParseDelDataStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.replaceData))
                    {
                        ParseReplaceData();
                    }
                    else if (MatchKeyword(TokenKeywords.getData))
                    {
                        ParseGetData();
                    }
                    else if (MatchKeyword(TokenKeywords.saveDataChanges))
                    {
                        ParseSaveDataChange();
                    }
                    else if (MatchKeyword(TokenKeywords.invokeData))
                    {
                        ParseInvokeData();
                    }
                    else if (MatchKeyword(TokenKeywords.createInstance))
                    {
                        ParseInstance();
                    }
                    else if (MatchKeyword(TokenKeywords.setClassVar))
                    {
                        ParseSetClassVar();
                    }
                    else if (MatchKeyword(TokenKeywords.setBaseClass))
                    {
                        ParseSetBase();
                    }
                    else if (MatchKeyword(TokenKeywords.printMethod))
                    {
                        ParsePrintMethod();
                    }
                    else if (MatchKeyword(TokenKeywords.rand))
                    {
                        ParseRand();
                    }
                    else if (MatchKeyword(TokenKeywords.pow))
                    {
                        Parsepow();
                    }
                    else if (MatchKeyword(TokenKeywords.sin))
                    {
                        Parsesin();
                    }
                    else if (MatchKeyword(TokenKeywords.cos))
                    {
                        Parsecos();
                    }
                    else if (MatchKeyword(TokenKeywords.tan))
                    {
                        Parsetan();
                    }
                    else if (MatchKeyword(TokenKeywords.asin))
                    {
                        Parseasin();
                    }
                    else if (MatchKeyword(TokenKeywords.acos))
                    {
                        Parseacos();
                    }
                    else if (MatchKeyword(TokenKeywords.atan))
                    {
                        Parseatan();
                    }
                    else if (MatchKeyword(TokenKeywords.sqrt))
                    {
                        Parsesqrt();
                    }
                    else if (MatchKeyword(TokenKeywords.strfromindex))
                    {
                        Parsefromindex();
                    }
                    else if (MatchKeyword(TokenKeywords.getindex))
                    {
                        Parsegetindex();
                    }
                    else if (MatchKeyword(TokenKeywords.strlen))
                    {
                        Parsestrlen();
                    }
                    else if (MatchKeyword(TokenKeywords.strreplace))
                    {
                        Parsereplace();
                    }
                    else if (MatchKeyword(TokenKeywords.datalen))
                    {
                        Parsedatalen();
                    }
                    else if (MatchKeyword(TokenKeywords.now))
                    {
                        Parsenow();
                    }
                    else if (MatchKeyword(TokenKeywords.timeformat))
                    {
                        Parsetimeformat();
                    }
                    else if (MatchKeyword(TokenKeywords.printnl))
                    {
                        ParsePrintnlStatement();
                    }
                    else if (MatchKeyword(TokenKeywords.substr))
                    {
                        Parsesubstr();
                    }
                    else if (MatchKeyword(TokenKeywords.eval))
                    {
                        Parseeval();
                    }
                    else if (MatchKeyword(TokenKeywords.osinfo))
                    {
                        Parseosinfo();
                    }
                    else if (MatchKeyword(TokenKeywords.sys))
                    {
                        Parsesys();
                    }
                    else if (MatchKeyword(TokenKeywords.getCurrentFilePath))
                    {
                        ParseGetCurrentFilePath();
                    }
                    else if (MatchKeyword(TokenKeywords.getCurrentFolderPath))
                    {
                        ParseGetCurrentFolderPath();
                    }
                    else if (MatchKeyword(TokenKeywords.mapPath))
                    {
                        ParseMapPath();
                    }
                    else if (MatchKeyword(TokenKeywords.appQuit))
                    {
                        ParseAppQuit();
                    }
                    else if (TryParseAssignmentStatement())
                    {
                    }
                    else if (MatchKeyword("pause"))
                    {
                        ParsePause();
                    }
                    else if (Peek().Type == TokenTypes.Identifier && Peek().Value == TokenKeywords.classInvoke)
                    {
                        Advance();
                        ParseClassInvoke();
                    }
                    else if (Peek().Type == TokenTypes.Identifier && Peek().Value == TokenKeywords.objectInvoke)
                    {
                        Advance();
                        ParseObjectInvoke();
                    }
                    else if (MatchFunction(Peek().Value))
                    {
                        RunFunction(Peek().Value);
                        ConsumeOptionalSemicolon();
                    }
                    else if (_classInfos.ContainsKey(Peek().Value))
                    {
                        string className = Peek().Value;
                        Advance();
                        Runclass(className);
                        ConsumeOptionalSemicolon();
                    }
                    else
                    {
                        Expr expr = ParseExpression();
                        object result = EvaluateExpression(expr);
                        RememberItCandidate(result);
                        ConsumeOptionalSemicolon();
                    }

                    if (_isQuit)
                    {
                        Environment.Exit(_n);
                    }
                }
            }
            finally
            {
                _current = savedPosition;
            }
        }

        private void RememberItCandidate(object? value)
        {
            _lastItValue = value;
        }
        private void ParseDowhileStatement()
        {
            if (!MatchPunctuation("{"))
                throw new Exception(_tokens, _current, "Expected '{'", "FS2003");

            int bodyStartIndex = _current;
            int braceDepth = 1;
            while (braceDepth > 0 && !IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Value == "{") braceDepth++;
                if (token.Value == "}") braceDepth--;
            }
            int bodyEndIndex = _current - 1;

            if (!MatchKeyword(TokenKeywords._while))
                throw new Exception(_tokens, _current, "Expected 'while' after do block", "FS2003");

            if (!MatchPunctuation("("))
                throw new Exception(_tokens, _current, "Expected '('", "FS2003");

            int conditionStart = _current;
            Expr conditionExpr = ParseExpression();

            if (!MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected ')'", "FS2003");

            int loopCount = 0;
            bool condition;

            do
            {
                loopCount++;
                if (loopCount % CancelCheckInterval == 0)
                    CheckCancellation();

                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex, _currentFunctionName);

                _current = conditionStart;
                conditionExpr = ParseExpression();
                condition = bool.Parse(EvaluateExpression(conditionExpr).ToString());
                if (!MatchPunctuation(")"))
                    throw new Exception(_tokens, _current, "Expected ')'", "FS2003");

            } while (condition);
        }
        private void RunFunction(string funcName)
        {
            RunFunction(funcName, _functions[funcName], null);
        }
        private void RunFunction(string funcName, List<Token> tokens, List<string> args)
        {
            RunFunction(funcName, new FunctionInfo(funcName, args, tokens), null);
        }
        private bool MatchFunction(string funcName)
        {
            return _functions.ContainsKey(funcName);
        }
        private void ParseFunctionStatement(MethodVisibility? explicitVisibility = null, bool isStatic = false)
        {
            FunctionInfo functionInfo;
            if (!MatchMemberName(out string name))
            {
                throw new Exception(_tokens, _current, "Expected function name", "FS2003");
            }
            List<string> parameters = [];
            if (!MatchPunctuation("("))
            {
                throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            }
            while (Peek().Value != ")")
            {
                if (Peek().Value == ",")
                {
                    Advance();
                    continue;
                }
                else
                {
                    parameters.Add(Peek().Value);
                    Advance();
                }
            }
            Advance();
            List<Token> tokens = ParseScopedTokens("{", "}", "function", "fbegin", "fend");
            MethodVisibility visibility = explicitVisibility ?? (_isParsingClassBody ? MethodVisibility.Private : MethodVisibility.Private);
            string functionName = _isParsingClassBody ? name : QualifyDeclaredName(name);
            functionInfo = new(functionName, parameters, tokens, visibility, isStatic, _isParsingClassBody, _classDeclarationName);
            functionInfo.Namespace = _currentNamespace;
            functionInfo.UsingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            _functions[functionName] = functionInfo;
        }
        private void ParseWhileStatement()
        {
            if (!MatchPunctuation("("))
                throw new Exception(_tokens, _current, "Expected '('", "FS2003");

            int conditionStart = _current;
            Expr conditionExpr = ParseExpression();

            if (!MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected ')'", "FS2003");

            if (!MatchPunctuation("{"))
                throw new Exception(_tokens, _current, "Expected '{'", "FS2003");

            int bodyStartIndex = _current;
            int braceDepth = 1;
            while (braceDepth > 0 && !IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Value == "{") braceDepth++;
                if (token.Value == "}") braceDepth--;
            }
            int bodyEndIndex = _current - 1;
            int afterBodyIndex = _current;

            int loopCount = 0;
            bool condition = bool.Parse(EvaluateExpression(conditionExpr).ToString());

            while (condition)
            {
                loopCount++;
                if (loopCount % CancelCheckInterval == 0)
                    CheckCancellation();
                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex, _currentFunctionName);
                _current = conditionStart;
                conditionExpr = ParseExpression();
                condition = bool.Parse(EvaluateExpression(conditionExpr).ToString());
                if (!MatchPunctuation(")"))
                    throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            }
            _current = afterBodyIndex;
        }
        private void ParseForStatement()
        {
            if (!MatchPunctuation("("))
                throw new Exception(_tokens, _current, "Expected '('", "FS2003");

            int initializerStart = _current;
            int initializerEnd = FindClauseBoundary(";");
            _current = initializerEnd;
            if (!MatchPunctuation(";"))
                throw new Exception(_tokens, _current, "Expected ';' after for initializer", "FS2003");

            int conditionStart = _current;
            int conditionEnd = FindClauseBoundary(";");
            _current = conditionEnd;
            if (!MatchPunctuation(";"))
                throw new Exception(_tokens, _current, "Expected ';' after for condition", "FS2003");

            int iteratorStart = _current;
            int iteratorEnd = FindClauseBoundary(")");
            _current = iteratorEnd;
            if (!MatchPunctuation(")"))
                throw new Exception(_tokens, _current, "Expected ')' after for iterator", "FS2003");

            if (!MatchPunctuation("{"))
                throw new Exception(_tokens, _current, "Expected '{'", "FS2003");

            int bodyStartIndex = _current;
            int braceDepth = 1;
            while (braceDepth > 0 && !IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Value == "{") braceDepth++;
                if (token.Value == "}") braceDepth--;
            }
            int bodyEndIndex = _current - 1;
            int afterBodyIndex = _current;

            if (initializerStart < initializerEnd)
            {
                ExecuteStatementsInRange(initializerStart, initializerEnd, _currentFunctionName);
            }

            int loopCount = 0;
            while (EvaluateForCondition(conditionStart, conditionEnd))
            {
                loopCount++;
                if (loopCount % CancelCheckInterval == 0)
                    CheckCancellation();

                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex, _currentFunctionName);

                if (iteratorStart < iteratorEnd)
                {
                    ExecuteStatementsInRange(iteratorStart, iteratorEnd, _currentFunctionName);
                }
            }

            _current = afterBodyIndex;
        }
        private int FindClauseBoundary(string closingToken)
        {
            int depthParenthesis = 0;
            int depthBracket = 0;
            int depthBrace = 0;

            for (int i = _current; i < _tokens.Count; i++)
            {
                Token token = _tokens[i];
                if (token.Type == TokenTypes.EndOfFile)
                {
                    break;
                }
                if (token.Type != TokenTypes.Punctuation)
                {
                    continue;
                }

                switch (token.Value)
                {
                    case "(":
                        depthParenthesis++;
                        break;
                    case ")":
                        if (depthParenthesis == 0 && closingToken == ")")
                        {
                            return i;
                        }
                        depthParenthesis--;
                        break;
                    case "[":
                        depthBracket++;
                        break;
                    case "]":
                        depthBracket--;
                        break;
                    case "{":
                        depthBrace++;
                        break;
                    case "}":
                        depthBrace--;
                        break;
                    default:
                        if (depthParenthesis == 0 && depthBracket == 0 && depthBrace == 0 && token.Value == closingToken)
                        {
                            return i;
                        }
                        break;
                }
            }

            throw new Exception(_tokens, _current, $"Expected '{closingToken}'", "FS2003");
        }
        private bool EvaluateForCondition(int startPos, int endPos)
        {
            if (startPos >= endPos)
            {
                return true;
            }

            int savedPosition = _current;
            try
            {
                _current = startPos;
                Expr conditionExpr = ParseExpression();
                return bool.Parse(EvaluateExpression(conditionExpr).ToString());
            }
            finally
            {
                _current = savedPosition;
            }
        }
        private void ParseIfStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            List<Token> ifTokens = ParseScopedTokens("{", "}", "if", "ibegin", "iend");
            bool branchExecuted = false;
            if (a)
            {
                _variables = Run(ifTokens, _variables, _currentFunctionName ?? "", CloneClassInfos(_classInfos));
                branchExecuted = true;
            }

            while (MatchKeyword(TokenKeywords._else))
            {
                if (MatchKeyword(TokenKeywords._if))
                {
                    if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                    string elseIfResult = EvaluateExpression(ParseExpression()).ToString();
                    bool elseIfCondition = bool.Parse(elseIfResult);
                    if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
                    List<Token> elseIfTokens = ParseScopedTokens("{", "}", "else if", "ibegin", "iend");
                    if (!branchExecuted && elseIfCondition)
                    {
                        _variables = Run(elseIfTokens, _variables, _currentFunctionName ?? "", CloneClassInfos(_classInfos));
                        branchExecuted = true;
                    }
                    continue;
                }

                List<Token> elseTokens = ParseScopedTokens("{", "}", "else", "ibegin", "iend");
                if (!branchExecuted)
                {
                    _variables = Run(elseTokens, _variables, _currentFunctionName ?? "", CloneClassInfos(_classInfos));
                }
                break;
            }
        }
        private void ParseHelperStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string a = EvaluateExpression(ParseExpression()).ToString();
            if (a.Equals("syntax", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Syntax:\r\n1.keyword+(args);\r\nInvoke keyword with args.\r\nWarning: If keyword hasn't args,\r\nuse keyword+;\r\n2.Define var.\r\n(1)define:\r\ninit(varName,type); Or var varName = value;\r\n(2)assignment:\r\nset(varName,value);\r\n3.Keywords Table.\r\n________________________________________________\r\n|keyword   |  args   |  do somwthings           |\r\n|paint        text     print the text           |\r\n|watchStart  varName   start stopwatch.         |\r\n|watchEnd     null     stop stopwatch           |\r\n|init    varName,type  init var.                |\r\n|set    varName,value  set var.                 |\r\n|...          ....     ............             |\r\n|_______________________________________________|");
            }
            else if (a.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("https://mars-feifei.github.io/feitools.github.io/feisharp\r\n");
            }
            else
            {
                throw new Exception(_tokens, _current, "Invalid string for \"helper\" keyword: " + a, "FS3001");
            }
            Advance();
            Advance();
        }
        private void ParseABEStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string a = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double b = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double c = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            double d = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            Advance();
            Advance();
            try
            {
                _variables.NewAdd(a, (b + c + d) / 3);
            }
            catch
            {
                _variables[a] = (b + c + d) / 3;
            }
        }
        private void ParseWatchEndStatement()
        {
            Stopwatch.Stop();
            try
            {
                _variables.NewAdd(name, Stopwatch.Elapsed.TotalSeconds);
            }
            catch
            {
                _variables[name] = Stopwatch.Elapsed.TotalSeconds;
            }
            Advance();
        }
        string name = "";
        private void ParseWatchStartStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Stopwatch = Stopwatch.StartNew();
            name = EvaluateExpression(ParseExpression()).ToString();
            Advance();
            Advance();
        }
        private void ParseWaitStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int milliseconds = int.Parse(EvaluateExpression(ParseExpression()).ToString());
            Advance();
            Advance();
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < milliseconds)
            {
                if (sw.ElapsedMilliseconds % 10 == 0)
                {
                    CheckCancellation();
                }
                Thread.Sleep(1);
            }
            sw.Stop();
        }
        private void ParseStopStatement()
        {
            Advance();
            Console.WriteLine(new OutputEventArgs($"Application is stop...\r\n(Current token: \"{Peek().Value + "&" + Peek().Type}\", Previous token:\"{Previous().Value + "&" + Previous().Type}\")"));
            Console.WriteLine("variables:");
            foreach (var item in _variables)
            {
                Console.WriteLine(new OutputEventArgs($"var {item.Key} = {item.Value} : {item.Value.GetType()}"));
            }
            Console.WriteLine(new OutputEventArgs($"{_variables.Count}" + " items of vars."));
            Console.WriteLine("functions:");
            foreach (var item in _functions)
            {
                Console.WriteLine(new OutputEventArgs($"function {item.Key}, Parameters Length: {item.Value.Parameter.Count}, Tokens Length: {item.Value.FunctionBody.Count}"));
            }
            Console.WriteLine(new OutputEventArgs($"{_functions.Count}" + " items of functions."));
            Console.WriteLine("Enter any key to continue......");
            Console.ReadKey();
            Console.WriteLine();
        }
        private void ParseStartStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);

            Process.Start(a);
            Advance();
            Advance();
        }
        private void ParseExportStatement()
        {
            if (isfileassembly)
            {
                Console.Write("This application want to write your file, do you agree it?(y/n)");
                var _ = Console.ReadKey();
                Console.WriteLine();
                if (_.Key == ConsoleKey.Y)
                {
                }
                else
                {
                    throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
                }
                if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                Expr b = ParseExpression();
                string a = (string)EvaluateExpression(b);
                if (!MatchKeyword("to")) throw new Exception(_tokens, _current, "Expected 'to' keyword", "FS2003");
                Expr b1 = ParseExpression();
                string a1 = (string)EvaluateExpression(b1);
                File.WriteAllText(a1, a);
                Advance();
                Advance();
            }
            else
            {
                throw new Exception(_tokens, _current, "A error " + _current +
                                    " was detected as a function name, but the corresponding namespace was not applied: FeiSharp.Text.Json", "FS3001");
            }
        }
        private void ParseRunStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);
            Console.Write("This application want to read your file, do you agree it?(y/n)");
            var _ = Console.ReadKey();
            Console.WriteLine();
            if (_.Key == ConsoleKey.Y)
            {
            }
            else
            {
                throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
            }
            Run(File.ReadAllText(a));
            Advance();
            Advance();
        }
        internal void Run(string code)
        {
            string sourceCode = code;
            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;
            do
            {
                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);
            Parser parser = new(tokens);
            parser.player = this.player;
            parser._variableInfos = _variableInfos;
            parser._variables = _variables;
            parser._classInfos = _classInfos;
            parser._propagateFeiSharpExceptions = _propagateFeiSharpExceptions;
            parser._classDeclarationName = _classDeclarationName;
            parser._results = _results;
            parser._functions = _functions;
            parser._currentNamespace = _currentNamespace;
            parser._usingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            parser.ShouldCancel = this.ShouldCancel;
            try
            {
                parser.ParseStatements();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            this.player = parser.player;
            return;
        }
        internal object Eval(string code)
        {
            try
            {
                var vars = Run("var exev_val = " + code, '7');
                return vars["exev_val"];
            }
            catch
            {
                return null;
            }
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser._variableInfos = _variableInfos;
            parser._variables = _variables;
            parser._propagateFeiSharpExceptions = _propagateFeiSharpExceptions;
            parser._classInfos = _classInfos;
            parser._classDeclarationName = _classDeclarationName;
            parser._results = _results;
            parser._functions = _functions;
            parser._currentNamespace = _currentNamespace;
            parser._usingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            parser.ShouldCancel = this.ShouldCancel;
            try
            {
                parser.ParseStatements();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            _functions = parser._functions;
            _classInfos = parser._classInfos;
            _results = parser._results;
            this.player = parser.player;
            return parser._variables;
        }
        internal Dictionary<string, object> Run(string code, char aaa)
        {
            string sourceCode = code;
            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;
            do
            {
                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);
            return Run(tokens);
        }
        internal KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, int op = 0)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._functions = new(_functions);
            parser._currentNamespace = _currentNamespace;
            parser._usingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            parser._classInfos = CloneClassInfos(_classInfos);
            parser._results = _results;
            parser.ShouldCancel = this.ShouldCancel;
            parser._propagateFeiSharpExceptions = _propagateFeiSharpExceptions;
            parser._currentExecutionClassName = _currentExecutionClassName;
            parser._currentFunctionName = _currentFunctionName;
            try
            {
                parser.ParseStatements();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            _functions = parser._functions;
            _classInfos = parser._classInfos;
            _results = parser._results;
            this.player = parser.player;
            KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> result = new(parser._variables, parser._functions);
            return result;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, string funcName, Dictionary<string, ClassInfo> a)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._classInfos = CloneClassInfos(a);
            parser._functions = new(_functions);
            parser._currentNamespace = _currentNamespace;
            parser._usingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            parser._results = _results;
            parser.ShouldCancel = this.ShouldCancel;
            parser._propagateFeiSharpExceptions = _propagateFeiSharpExceptions;
            parser._currentExecutionClassName = _currentExecutionClassName;
            parser._currentFunctionName = funcName;
            try
            {
                parser.ParseStatements(funcName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            _functions = parser._functions;
            _classInfos = parser._classInfos;
            _results = parser._results;
            this.player = parser.player;
            return parser._variables;
        }
        private void ExecuteNestedBlock(IEnumerable<Token> tokens, string funcName, Dictionary<string, object> vars, Dictionary<string, ClassInfo> classInfos)
        {
            Parser parser = new(new List<Token>(tokens));
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser._variables = vars;
            parser._variableInfos = _variableInfos;
            parser._classInfos = CloneClassInfos(classInfos);
            parser._functions = new(_functions, StringComparer.OrdinalIgnoreCase);
            parser._currentNamespace = _currentNamespace;
            parser._usingNamespaces = new HashSet<string>(_usingNamespaces, StringComparer.OrdinalIgnoreCase);
            parser._results = _results;
            parser.ShouldCancel = this.ShouldCancel;
            parser._propagateFeiSharpExceptions = true;
            parser._currentExecutionClassName = _currentExecutionClassName;
            parser._currentFunctionName = funcName;
            parser.ParseStatements(funcName);
            _functions = parser._functions;
            _classInfos = parser._classInfos;
            _results = parser._results;
            _variables = parser._variables;
            _variableInfos = parser._variableInfos;
            this.player = parser.player;
        }
        internal Dictionary<string, object> Run(string code, int a)
        {
            string sourceCode = code;
            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;
            do
            {
                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);
            Parser parser = new(tokens);
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser.ShouldCancel = this.ShouldCancel;
            try
            {
                parser.ParseStatements();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("FeiSharp.Run.Eval.Error: Parsing error: " + ex.Message));
            }
            this.player = parser.player;
            return parser._variables;
        }
        private bool TryParseAssignmentStatement()
        {
            int savedPosition = _current;
            if (!TryParseAssignmentTarget(out AssignmentTarget? target))
            {
                _current = savedPosition;
                return false;
            }
            if (!MatchToken(TokenTypes.Operator, out string op) || op != "=")
            {
                _current = savedPosition;
                return false;
            }
            if (target != null && target.Kind == "variable")
            {
                string varName = target.Name;
                bool isReadOnlyClassMember = target.ClassInfo != null
                    && target.ClassInfo.VariableInfos.TryGetValue(varName, out var classInfo)
                    && classInfo.IsReadOnly;
                if ((_variableInfos.TryGetValue(varName, out var info) && info.IsReadOnly) || isReadOnlyClassMember || varName.EndsWith("_readonly") || keys.Contains(varName))
                {
                    throw new Exception(_tokens, _current,
                        $"Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
                }
            }
            if (target != null && target.Kind == "instance" && target.Instance != null && target.MemberName != null
                && GetVariableInfoOrThrow(target.Instance, target.MemberName).IsReadOnly)
            {
                throw new Exception(_tokens, _current, "Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            }
            if (target != null && target.Kind == "class" && target.ClassInfo != null && target.MemberName != null
                && GetVariableInfoOrThrow(target.ClassInfo, target.MemberName).IsReadOnly)
            {
                throw new Exception(_tokens, _current, "Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            }
            object value = ParseAssignmentValue(out string? assignedClassName);
            if (keys.Contains(target.Name)) throw new Exception(_tokens, _current, "Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            ApplyAssignment(target, value, assignedClassName);
            RememberItCandidate(value);
            ConsumeOptionalSemicolon();
            return true;
        }
        private bool TryParseAssignmentTarget(out AssignmentTarget? target)
        {
            target = null;
            int savedPosition = _current;
            if (MatchKeyword(TokenKeywords._this) || MatchKeyword(TokenKeywords._base))
            {
                bool isBase = Previous().Value.Equals(TokenKeywords._base, StringComparison.OrdinalIgnoreCase);
                ClassInfo context = GetAccessibleClassContext(isBase);
                if (!MatchPunctuation(".") || !MatchMemberName(out string memberName))
                {
                    _current = savedPosition;
                    return false;
                }
                if (!context._Vars.ContainsKey(memberName) && !context._FunctionInfo.ContainsKey(memberName))
                {
                    throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                }
                if (context._Vars.ContainsKey(memberName))
                {
                    VariableInfo variableInfo = GetVariableInfoOrThrow(context, memberName);
                    if (variableInfo.IsStatic)
                    {
                        throw new Exception(_tokens, _current, $"Static member '{memberName}' must be assigned on class '{context.Name}'.", "FS3001");
                    }
                    EnsureVariableAccessible(variableInfo, memberName, isBase);
                    target = new AssignmentTarget
                    {
                        Kind = "variable",
                        Name = memberName,
                        ClassInfo = context
                    };
                    if (MatchPunctuation("["))
                    {
                        object indexKey = EvaluateExpression(ParseExpression());
                        if (!MatchPunctuation("]"))
                        {
                            throw new Exception(_tokens, _current, "Expected ']'", "FS2003");
                        }
                        target = new AssignmentTarget
                        {
                            Kind = "index",
                            Name = memberName,
                            ClassInfo = context,
                            TargetObject = context._Vars[memberName],
                            IndexKey = indexKey
                        };
                    }
                    return true;
                }
                else
                {
                    FunctionInfo function = context._FunctionInfo[memberName];
                    if (function.IsStatic)
                    {
                        throw new Exception(_tokens, _current, $"Static member '{memberName}' must be assigned on class '{context.Name}'.", "FS3001");
                    }
                    target = new AssignmentTarget
                    {
                        Kind = "function",
                        Name = memberName,
                        ClassInfo = context,
                    };
                    return true;
                }
            }
            if (!MatchToken(TokenTypes.Identifier, out string name))
            {
                _current = savedPosition;
                return false;
            }
            if (MatchPunctuation("."))
            {
                if (!MatchMemberName(out string memberName))
                {
                    throw new Exception(_tokens, _current, "Expected member name after '.'", "FS2003");
                }
                if (_variables.TryGetValue(name, out object? value) && value is ClassInstance instance)
                {
                    if (MatchPunctuation("["))
                    {
                        VariableInfo variableInfo = GetVariableInfoOrThrow(instance, memberName);
                        if (variableInfo.IsStatic)
                        {
                            throw new Exception(_tokens, _current, $"Static member '{memberName}' must be accessed on class '{instance.ClassName}'.", "FS3001");
                        }
                        EnsureVariableAccessible(variableInfo, memberName);
                        object? memberValue = instance.GetVariable(memberName);
                        object indexKey = EvaluateExpression(ParseExpression());
                        if (!MatchPunctuation("]"))
                        {
                            throw new Exception(_tokens, _current, "Expected ']'", "FS2003");
                        }
                        target = new AssignmentTarget
                        {
                            Kind = "index",
                            Name = memberName,
                            Instance = instance,
                            TargetObject = memberValue,
                            IndexKey = indexKey
                        };
                        return true;
                    }
                    target = new AssignmentTarget
                    {
                        Kind = "instance",
                        Name = name,
                        MemberName = memberName,
                        Instance = instance
                    };
                    return true;
                }
                if (_classInfos.TryGetValue(name, out ClassInfo? classInfo))
                {
                    if (!classInfo._Vars.ContainsKey(memberName) && !classInfo._FunctionInfo.Keys.Contains(memberName))
                    {
                        throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                    }
                    if (classInfo._FunctionInfo.Keys.Contains(memberName)) return true;
                    VariableInfo variableInfo = GetVariableInfoOrThrow(classInfo, memberName);
                    if (!variableInfo.IsStatic)
                    {
                        throw new Exception(_tokens, _current, $"Member '{memberName}' is not static.", "FS3001");
                    }
                    EnsureVariableAccessible(variableInfo, memberName);
                    target = new AssignmentTarget
                    {
                        Kind = "class",
                        Name = name,
                        MemberName = memberName,
                        ClassInfo = classInfo
                    };
                    return true;
                }
                _current = savedPosition;
                return false;
            }
            if (_variables.TryGetValue(name, out object? container) && MatchPunctuation("["))
            {
                object indexKey = EvaluateExpression(ParseExpression());
                if (!MatchPunctuation("]"))
                {
                    throw new Exception(_tokens, _current, "Expected ']'", "FS2003");
                }
                target = new AssignmentTarget
                {
                    Kind = "index",
                    Name = name,
                    TargetObject = container,
                    IndexKey = indexKey
                };
                return true;
            }
            target = new AssignmentTarget
            {
                Kind = "variable",
                Name = name
            };
            return true;
        }
        private object ParseAssignmentValue(out string? assignedClassName)
        {
            int savedPosition = _current;
            if (TryParseClassInstantiationValue(out object? instanceValue, out string? className))
            {
                assignedClassName = className;
                return instanceValue!;
            }
            _current = savedPosition;
            assignedClassName = null;
            return EvaluateExpression(ParseExpression());
        }
        private bool TryParseClassInstantiationValue(out object? instanceValue, out string? className)
        {
            instanceValue = null;
            className = null;
            if (!Check(TokenTypes.Identifier))
            {
                return false;
            }
            string name = Peek().Value;
            if (!_classInfos.TryGetValue(name, out ClassInfo? classInfo))
            {
                return false;
            }
            int savedPosition = _current;
            Advance();
            if (!MatchPunctuation("("))
            {
                _current = savedPosition;
                return false;
            }
            List<object> args = [];
            while (!IsAtEnd() && Peek().Value != ")")
            {
                if (Peek().Value == ",")
                {
                    Advance();
                    continue;
                }
                args.Add(EvaluateExpression(ParseExpression()));
            }
            if (!MatchPunctuation(")"))
            {
                _current = savedPosition;
                return false;
            }
            instanceValue = CreateClassInstance(name, classInfo, args);
            className = name;
            return true;
        }
        private void ApplyAssignment(AssignmentTarget target, object value, string? assignedClassName)
        {
            switch (target.Kind)
            {
                case "variable":
                    AssignVariableValue(target.Name, value, assignedClassName);
                    if (target.ClassInfo != null && target.ClassInfo._Vars.ContainsKey(target.Name))
                    {
                        UpdateClassInfoVariable(target.ClassInfo.Name, target.Name, value);
                    }
                    break;
                case "function":
                    break;
                case "instance":
                    if (target.Instance == null || target.MemberName == null)
                    {
                        throw new Exception(_tokens, _current, "Invalid instance assignment target", "FS3001");
                    }
                    VariableInfo variableInfo = GetVariableInfoOrThrow(target.Instance, target.MemberName);
                    if (variableInfo.IsStatic)
                    {
                        throw new Exception(_tokens, _current, $"Static member '{target.MemberName}' must be assigned on class '{target.Instance.ClassName}'.", "FS3001");
                    }
                    target.Instance.SetVariable(target.MemberName, value);
                    break;
                case "class":
                    if (target.MemberName == null)
                    {
                        throw new Exception(_tokens, _current, "Invalid class assignment target", "FS3001");
                    }
                    UpdateClassInfoVariable(target.Name, target.MemberName, value);
                    _variables[target.MemberName] = value;
                    break;
                case "index":
                    if (target.TargetObject == null)
                    {
                        throw new Exception(_tokens, _current, "Invalid index assignment target", "FS3001");
                    }
                    SetIndexedValue(target.TargetObject, target.IndexKey, value);
                    break;
                default:
                    throw new Exception(_tokens, _current, "Unsupported assignment target", "FS3001");
            }
        }
        private void AssignVariableValue(string name, object value, string? assignedClassName)
        {
            if (!_variables.ContainsKey(name))
            {
                throw new Exception(_tokens, _current, "Undefined variable: " + name, "FS3001");
            }
            _variables[name] = value;
            string typeKey = name + ":type";
            if (!string.IsNullOrWhiteSpace(assignedClassName))
            {
                _variables[typeKey] = assignedClassName;
            }
            else if (_variables.ContainsKey(typeKey))
            {
                _variables.Remove(typeKey);
            }
        }
        private void UpdateClassInfoVariable(string className, string memberName, object value)
        {
            if (!_classInfos.TryGetValue(className, out ClassInfo? classInfo))
            {
                throw new Exception(_tokens, _current, $"Class not defined: {className}", "FS3001");
            }
            var vars = new Dictionary<string, object>(classInfo._Vars, StringComparer.OrdinalIgnoreCase)
            {
                [memberName] = value
            };
            _classInfos[className] = new ClassInfo(classInfo._FunctionInfo, vars, classInfo.Name, classInfo.ConstructorInfo, classInfo.BaseClassName, classInfo.VariableInfos);
        }
        private void ParseSetStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name;
            if (TryParseContextMemberReference(out name))
            {
            }
            else if (MatchToken(TokenTypes.Identifier, out string identifier))
            {
                name = identifier;
            }
            else
            {
                name = EvaluateExpression(ParseExpression()).ToString();
            }
            if (name.EndsWith("_readonly")) throw new Exception(_tokens, _current,
                    $"Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            if (_variableInfos.TryGetValue(name, out var info) && info.IsReadOnly)
            {
                throw new Exception(_tokens, _current,
                    $"Cannot modify readonly variable '{name}'", FeiSharpErrorCodes.ReadonlyViolation);
            }
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            object name1 = EvaluateExpression(ParseExpression());
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            if (_variables.TryGetValue(name, out object _))
            {
                _variables[name] = name1;
            }
            else
            {
                throw new Exception(_tokens, _current, "Undefined variable: " + name, "FS3001");
            }
            var a = _classInfos;
            foreach (var item in _classInfos)
            {
                if (item.Value._Vars.ContainsKey(name))
                {
                    var vars = item.Value._Vars;
                    vars[name] = name1;
                    ClassInfo classInfo = new(item.Value._FunctionInfo, vars, item.Key, item.Value.ConstructorInfo, item.Value.BaseClassName, item.Value.VariableInfos);
                    a[item.Key] = classInfo;
                }
            }
            _classInfos = a;
            ConsumeOptionalSemicolon();
        }
        private bool TryParseContextMemberReference(out string memberName)
        {
            memberName = string.Empty;
            int savedPosition = _current;
            bool isThis = MatchKeyword(TokenKeywords._this);
            bool isBase = !isThis && MatchKeyword(TokenKeywords._base);
            if ((!isThis && !isBase) || !MatchPunctuation(".") || !MatchToken(TokenTypes.Identifier, out memberName))
            {
                _current = savedPosition;
                memberName = string.Empty;
                return false;
            }
            GetAccessibleClassContext(isBase);
            return true;
        }
        private void ParseInitStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Expr expr = GetVar();
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            Expr expr2 = GetType();
            Advance();
            Advance();
            _variables.NewAdd(((VarExpr)expr).Name, InitValue(((VarExpr)expr2).Name));
        }
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, "System.String", "System.Runtime")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, "System.Int32", "System.Runtime")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, "System.Boolean", "System.Runtime")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, "System.Double", "System.Runtime")]
        [RequiresDynamicCode("FeiSharp creates CLR instances from runtime type names.")]
        [RequiresUnreferencedCode("FeiSharp creates CLR instances from runtime type names.")]
        [DynamicDependency("System.Void System.Object::.ctor()", typeof(object))]
        [DynamicDependency("System.Void System.String::.ctor(System.Char[])")]
        [DynamicDependency("System.Void System.Int32::.ctor(System.Int32)")]
        [DynamicDependency("System.Void System.Boolean::.ctor(System.Boolean)")]
        [DynamicDependency("System.Void System.Double::.ctor(System.Double)")]
        [DynamicDependency("System.Void System.Char::.ctor(System.Char)")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(string))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(int))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(double))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(bool))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(char))]
        private object InitValue(string type)
        {
            Type t = TypeLoader.LoadType("System." + type);
            return Activator.CreateInstance(t);
        }
        private Expr GetVar()
        {
            if (MatchIdentifierOrSoftKeyword(out string value, TokenKeywords._base))
            {
                return new VarExpr(value);
            }
            return null;
        }
        private Expr GetType()
        {
            if (MatchToken(TokenTypes.Type))
            {
                return new VarExpr(Previous().Value);
            }
            return null;
        }
        private void ParseVariableDeclaration(MethodVisibility? explicitVisibility = null, bool isStatic = false, bool isConst = false)
        {
            // Both spellings share the normal variable storage and metadata.  This
            // deliberately permits `var const name` and `const var name` as aliases.
            if (MatchKeyword(TokenKeywords._const))
            {
                isConst = true;
            }
            if (isConst)
            {
                MatchKeyword(TokenKeywords._var);
            }

            bool isReadOnly = isConst;
            if (MatchKeyword("readonly"))
            {
                isReadOnly = true;
            }
            if (!MatchIdentifierOrSoftKeyword(out string varName, TokenKeywords._base))
            {
                throw new Exception(_tokens, _current, "Expected variable name", "FS2003");
            }
            if (_isParsingClassBody || isReadOnly)
            {
                MethodVisibility visibility = explicitVisibility ?? MethodVisibility.Public;
                _variableInfos[varName] = new VariableInfo(
                    varName,
                    visibility,
                    isStatic,
                    _isParsingClassBody,
                    _isParsingClassBody ? _classDeclarationName : null)
                { IsReadOnly = isReadOnly };
            }
            if (!MatchToken(TokenTypes.Operator, out string op) || op != "=")
            {
                if (isReadOnly)
                {
                    throw new Exception(_tokens, _current,
                        $"Const variable '{varName}' must be initialized", "FS2003");
                }
                _variables[varName] = 0;
                ConsumeOptionalSemicolon();
                return;
            }
            int savedPosition = _current;
            if (TryParseClassInstantiation(varName, out object instanceValue))
            {
                _variables[varName] = instanceValue;
                ConsumeOptionalSemicolon();
                return;
            }
            _current = savedPosition;
            Expr expr = ParseExpression();
            object value = EvaluateExpression(expr);
            _variables[varName] = value;
            RememberItCandidate(value);
            ConsumeOptionalSemicolon();
        }

        private bool TryParseClassInstantiation(string varName, out object instanceValue)
        {
            instanceValue = null;
            if (!Check(TokenTypes.Identifier))
                return false;
            string className = Peek().Value;
            if (!_classInfos.TryGetValue(className, out ClassInfo classInfo))
                return false;
            int savedPosition = _current;
            Advance();
            if (!MatchPunctuation("("))
            {
                _current = savedPosition;
                return false;
            }
            List<object> args = new List<object>();
            while (!IsAtEnd() && Peek().Value != ")")
            {
                if (Peek().Value != ",")
                {
                    Expr argExpr = ParseExpression();
                    object argValue = EvaluateExpression(argExpr);
                    args.Add(argValue);
                }
                else
                {
                    Advance();
                }
            }
            if (!MatchPunctuation(")"))
            {
                _current = savedPosition;
                return false;
            }
            instanceValue = CreateClassInstance(className, classInfo, args);
            _variables[varName] = instanceValue;
            _variables[varName + ":type"] = className;

            return true;
        }

        private object CreateClassInstance(string className, ClassInfo classInfo, List<object> args)
        {
            var instanceScope = classInfo._Vars
                .Where(kvp =>
                    !classInfo.VariableInfos.TryGetValue(kvp.Key, out VariableInfo? info)
                    || !info.IsStatic)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            if (classInfo.ConstructorInfo != null)
            {
                var ctorArgs = new List<object>();
                for (int i = 0; i < Math.Min(args.Count, classInfo.ConstructorInfo.Parameter.Count); i++)
                {
                    ctorArgs.Add(args[i]);
                }
                RunConstructor(classInfo.ConstructorInfo, instanceScope, ctorArgs);
            }
            var instance = new ClassInstance
            {
                ClassName = className,
                Variables = instanceScope,
                Functions = classInfo._FunctionInfo,
                VariableInfos = classInfo.VariableInfos
            };

            return instance;
        }
        private void RunConstructor(FunctionInfo constructor, Dictionary<string, object> scope, List<object> args)
        {
            var savedVariables = new Dictionary<string, object>(_variables);
            var savedResults = new Dictionary<string, object>(_results);
            var savedExecutionClassName = _currentExecutionClassName;
            var savedFunctionName = _currentFunctionName;
            try
            {
                _currentExecutionClassName = constructor.DeclaringClassName;
                _currentFunctionName = constructor.Name;
                var tempVariables = new Dictionary<string, object>(scope);
                for (int i = 0; i < constructor.Parameter.Count && i < args.Count; i++)
                {
                    tempVariables[constructor.Parameter[i]] = args[i];
                }
                var result = Run(constructor.FunctionBody, tempVariables, constructor.Name, _classInfos);
                foreach (var kvp in result)
                {
                    scope[kvp.Key] = kvp.Value;
                }
            }
            finally
            {
                _currentExecutionClassName = savedExecutionClassName;
                _currentFunctionName = savedFunctionName;
                _variables = savedVariables;
                _results = savedResults;
            }
        }

        public class ClassInstance
        {
            public string ClassName { get; set; }
            public Dictionary<string, object> Variables { get; set; } = new();
            public Dictionary<string, FunctionInfo> Functions { get; set; } = new();
            public Dictionary<string, VariableInfo> VariableInfos { get; set; } = new();

            public object GetVariable(string name)
            {
                return Variables.TryGetValue(name, out var value) ? value : null;
            }

            public void SetVariable(string name, object value)
            {
                Variables[name] = value;
            }
            public object CallMethod(string methodName, List<object> args, Parser parser)
            {
                if (Functions.TryGetValue(methodName, out var function))
                {
                    if (function.IsStatic)
                    {
                        throw new Exception(parser._tokens, parser._current, $"Static member '{methodName}' must be called on class '{ClassName}'.", "FS3001");
                    }
                    if (!parser.CanAccessMethod(function))
                    {
                        throw new Exception(parser._tokens, parser._current, $"Method '{methodName}' is not accessible.", "FS3001");
                    }
                    var methodScope = new Dictionary<string, object>(Variables);
                    for (int i = 0; i < function.Parameter.Count && i < args.Count; i++)
                    {
                        methodScope[function.Parameter[i]] = args[i];
                    }
                    string? previousExecutionClassName = parser._currentExecutionClassName;
                    string? previousFunctionName = parser._currentFunctionName;
                    parser._currentExecutionClassName = ClassName;
                    parser._currentFunctionName = methodName;
                    var result = parser.Run(function.FunctionBody, methodScope, methodName, parser._classInfos);
                    parser._currentExecutionClassName = previousExecutionClassName;
                    parser._currentFunctionName = previousFunctionName;
                    foreach (var kvp in result)
                    {
                        Variables[kvp.Key] = kvp.Value;
                    }
                    if (parser._results.TryGetValue(methodName, out var returnValue))
                    {
                        return returnValue;
                    }
                }

                return null;
            }
        }
        private PrintStmt ParsePrintStatement()
        {
            if (!MatchPunctuation("("))
            {
                _current--;
                string text = EvaluateExpression(ParseExpression()).ToString();
                if (Peek().Value == "to")
                {
                    Console.Write("This application want to write your file, do you agree it?(y/n)");
                    var _ = Console.ReadKey();
                    Console.WriteLine();
                    if (_.Key == ConsoleKey.Y)
                    {
                    }
                    else
                    {
                        throw new Exception(_tokens, _current, "User do not agree this application.", "FS2003");
                    }
                    Advance();
                    string content = EvaluateExpression(ParseExpression()).ToString();
                    Advance();
                    File.WriteAllText(text, content);
                }
                else
                {
                    Console.WriteLine("No 'to' keyword.");
                }
            }
            Expr expr = ParseExpression();
            Advance();
            Advance();
            return new PrintStmt(expr);
        }
        private Expr ParseExpression(int minPrecedence = 0)
        {
            Expr expr = ParsePrimary();
            while (true)
            {
                if (IsAtEnd() || !IsOperator(Peek().Value))
                    break;
                string op = Peek().Value;
                int precedence = GetPrecedence(op);
                if (precedence < minPrecedence)
                    break;
                Advance();
                Expr right = ParseExpression(precedence + 1);
                expr = new BinaryExpr(expr, op, right);
            }
            if (minPrecedence == 0 && MatchKeyword(TokenKeywords.when))
            {
                Expr condition = ParseExpression();
                Expr? falseValue = null;
                if (MatchKeyword(TokenKeywords.otherwise))
                {
                    falseValue = ParseExpression();
                }
                expr = new ConditionalExpr(expr, condition, falseValue);
            }
            return expr;
        }
        private bool IsOperator(string token)
        {
            return token == "+" || token == "-" || token == "*" || token == "/" ||
                   token == "^" || token == ">" || token == "<" || token == "=" ||
                   token == "!=" || token == ">=" || token == "<=" ||
                   token == "||" || token == "&&" ||
                   token == "!" || token == "|" || token == "&" || token == "$" || token == "++" || token == "--";
        }

        private int GetPrecedence(string op)
        {
            return op switch
            {
                "||" => 1,
                "&&" => 2,
                "|" => 3,
                "&" => 4,
                "=" or "!=" => 5,
                ">" or "<" or ">=" or "<=" => 5,
                "^" => 6,
                "+" or "-" => 7,
                "*" or "/" => 8,
                "$" => 9,
                "++" or "--" => 10,
                _ => 0
            };
        }
        private bool TryParseQualifiedNamespaceFunctionCall(string firstSegment, out Expr? expression)
        {
            expression = null;
            int savedPosition = _current;
            if (!MatchPunctuation("."))
            {
                return false;
            }

            var segments = new List<string> { firstSegment };
            do
            {
                if (!MatchMemberName(out string segment))
                {
                    _current = savedPosition;
                    return false;
                }
                segments.Add(segment);
            }
            while (MatchPunctuation("."));

            if (!MatchPunctuation("("))
            {
                _current = savedPosition;
                return false;
            }

            string requestedName = string.Join('.', segments);
            if (!TryResolveFunctionName(requestedName, out string resolvedName, out FunctionInfo functionInfo))
            {
                _current = savedPosition;
                return false;
            }

            List<object> args = ParseArgumentValuesUntil(")");
            RunFunction(resolvedName, functionInfo, null, args);
            expression = ParseRuntimeAccess(GetFunctionReturnValueOrDefault(resolvedName));
            return true;
        }

        private Expr ParsePrimary()
        {
            if (Check(TokenTypes.Operator) && (Peek().Value == "++" || Peek().Value == "--"))
            {
                string op = Advance().Value;
                Expr operand = ParsePrimary();
                return new IncrementExpr(op, operand, true);
            }

            if (MatchOperator("!") || MatchOperator("-") || MatchOperator("+"))
            {
                string op = Previous().Value;
                Expr operand = ParsePrimary();
                return new UnaryExpr(op, operand);
            }
            string varName = "";
            if (MatchPunctuation("("))
            {
                Expr expr = ParseExpression(0);
                if (!MatchPunctuation(")"))
                {
                    throw new Exception(_tokens, _current, "Expected ')' after expression", "FS2003");
                }
                return expr;
            }
            if (MatchToken(TokenTypes.Number))
            {
                return new ValueExpr(double.Parse(Previous().Value));
            }
            else if (MatchPunctuation("["))
            {
                return new ValueExpr(ParseInlineArrayLiteral());
            }
            else if (MatchToken(TokenTypes.String))
            {
                return new ValueExpr(Previous().Value);
            }
            else if (MatchToken(TokenTypes.Character))
            {
                return new ValueExpr(Previous().Value[0]);
            }
            else if (MatchKeyword(TokenKeywords._this))
            {
                return ParseContextKeywordExpression(false);
            }
            else if (Check(TokenTypes.Keyword) && Peek().Value == TokenKeywords._base && Peek(1).Value == ".")
            {
                Advance();
                return ParseContextKeywordExpression(true);
            }
            else if (MatchIdentifierOrSoftKeyword(out string identifierOrSoftKeyword, TokenKeywords._base))
            {
                varName = identifierOrSoftKeyword;
                if (TryParseQualifiedNamespaceFunctionCall(varName, out Expr? qualifiedCall))
                {
                    return qualifiedCall;
                }
                if (Check(TokenTypes.Operator) && (Peek().Value == "++" || Peek().Value == "--"))
                {
                    string op = Advance().Value;
                    return new IncrementExpr(op, new VarExpr(varName), false);
                }

                if (Previous().Type == TokenTypes.Identifier && Previous().Value == "classinvoke")
                {
                    return new BinaryExpr(new ValueExpr(ParseClassInvoke()), "HAVE", null);
                }
                if (Previous().Type == TokenTypes.Identifier && Previous().Value == "objectinvoke")
                {
                    return new BinaryExpr(new ValueExpr(ParseObjectInvoke()), "OBJ", null);
                }
                if (Previous().Type == TokenTypes.Identifier && Previous().Value == "new")
                {
                    return ParseNewExpression();
                }
                if (_variables.TryGetValue(varName, out object value1))
                {
                    if (value1 is ClassInstance instance && MatchPunctuation("."))
                    {
                        if (!MatchMemberName(out string memberName))
                        {
                            throw new Exception(_tokens, _current, "Expected member name after '.'", "FS2003");
                        }
                        if (MatchPunctuation("("))
                        {
                            object result = instance.CallMethod(memberName, ParseArgumentValuesUntil(")"), this);
                            return ParseRuntimeAccess(result);
                        }
                        VariableInfo variableInfo = GetVariableInfoOrThrow(instance, memberName);
                        if (variableInfo.IsStatic)
                        {
                            throw new Exception(_tokens, _current, $"Static member '{memberName}' must be accessed on class '{instance.ClassName}'.", "FS3001");
                        }
                        EnsureVariableAccessible(variableInfo, memberName);
                        object memberValue = instance.GetVariable(memberName);
                        return ParseRuntimeAccess(memberValue);
                    }
                    if (varName == "_") throw new Exception(_tokens, _current, "Variable, function or class not defined: " + varName, "FS3001");
                    return ParseRuntimeAccess(value1);
                }
                if (_classInfos.ContainsKey(varName))
                {
                    if (CheckPunctuation("("))
                    {
                        ClassInfo classInfo = _classInfos[varName];
                        MatchPunctuation("(");
                        List<object> args = ParseArgumentValuesUntil(")");
                        return new ValueExpr(CreateClassInstance(varName, classInfo, args));
                    }
                    if (MatchPunctuation("."))
                    {
                        if (!MatchMemberName(out string memberName))
                        {
                            throw new Exception(_tokens, _current, "Expected member name after '.'", "FS2003");
                        }

                        ClassInfo classInfo = _classInfos[varName];
                        if (MatchPunctuation("("))
                        {
                            if (!classInfo._FunctionInfo.TryGetValue(memberName, out FunctionInfo? functionInfo))
                            {
                                throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                            }
                            if (!functionInfo.IsStatic)
                            {
                                throw new Exception(_tokens, _current, $"Member '{memberName}' is not static.", "FS3001");
                            }

                            List<object> args = [];
                            while (!IsAtEnd() && Peek().Value != ")")
                            {
                                if (Peek().Value == ",")
                                {
                                    Advance();
                                    continue;
                                }
                                args.Add(EvaluateExpression(ParseExpression()));
                            }
                            if (!MatchPunctuation(")"))
                            {
                                throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
                            }

                            RunFunction(memberName, functionInfo, classInfo, args);
                            return new ValueExpr(GetFunctionReturnValueOrDefault(memberName));
                        }

                        if (!classInfo._Vars.TryGetValue(memberName, out object? memberValue))
                        {
                            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                        }
                        VariableInfo variableInfo = GetVariableInfoOrThrow(classInfo, memberName);
                        if (!variableInfo.IsStatic)
                        {
                            throw new Exception(_tokens, _current, $"Member '{memberName}' is not static.", "FS3001");
                        }
                        EnsureVariableAccessible(variableInfo, memberName);
                        return ParseRuntimeAccess(memberValue);
                    }

                    var a = Runclass(varName);
                    if (a.Value)
                    {
                        return new ValueExpr(GetFunctionReturnValueOrDefault(a.Key));
                    }
                    if (!string.IsNullOrEmpty(a.Key) && _variables.TryGetValue(a.Key, out object? classValue))
                    {
                        return new ValueExpr(classValue);
                    }
                    throw new Exception(_tokens, _current, $"Undefined variable, function or class: {varName}", FeiSharpErrorCodes.UndefinedSymbol);
                }
                if (TryResolveFunctionName(varName, out string resolvedFunctionName, out FunctionInfo resolvedFunction))
                {
                    RunFunction(resolvedFunctionName, resolvedFunction, null);
                    return ParseRuntimeAccess(GetFunctionReturnValueOrDefault(resolvedFunctionName));
                }
                if (StandardLibrary.IsGlobalFunction(varName))
                {
                    return ParseStandardFunctionCall(varName);
                }
                if (StandardLibrary.IsClass(varName))
                {
                    return ParseStandardClassAccess(varName);
                }
                else
                {
                    var a = Runclass(varName);
                    if (a.Value)
                    {
                        return ParseRuntimeAccess(GetFunctionReturnValueOrDefault(a.Key));
                    }
                    if (!string.IsNullOrEmpty(a.Key) && _variables.TryGetValue(a.Key, out object? classValue))
                    {
                        return ParseRuntimeAccess(classValue);
                    }
                    throw new Exception(_tokens, _current, $"Undefined variable, function or class: {varName}", FeiSharpErrorCodes.UndefinedSymbol);
                }
            }
            else if (MatchPunctuation("("))
            {
                Expr expr = ParseExpression();
                if (!MatchPunctuation(")"))
                {
                    throw new Exception(_tokens, _current, "Expected ')' after expression", "FS2003");
                }
                return expr;
            }
            else if (MatchKeyword("true"))
            {
                return new ValueExpr(true);
            }
            else if (MatchKeyword("false"))
            {
                return new ValueExpr(false);
            }
            throw new Exception(_tokens, _current, "Unvalid token: " + Peek().Value, "FS2003");
        }
        private Expr ParseStandardFunctionCall(string functionName)
        {
            if (!MatchPunctuation("("))
            {
                throw new Exception(_tokens, _current, $"Expected '(' after standard function '{functionName}'", FeiSharpErrorCodes.MissingDelimiter);
            }
            List<object> args = ParseArgumentValuesUntil(")");
            try
            {
                return ParseRuntimeAccess(StandardLibrary.InvokeGlobal(functionName, args));
            }
            catch (StandardLibraryException ex)
            {
                throw new Exception(_tokens, _current, ex.Message, ex.ErrorCode);
            }
            catch (OverflowException ex)
            {
                throw new Exception(_tokens, _current, ex.Message, FeiSharpErrorCodes.InvalidArgument);
            }
        }
        private Expr ParseStandardClassAccess(string className)
        {
            EnsureStandardClassNamespace(className);
            if (!MatchPunctuation("."))
            {
                throw new Exception(_tokens, _current, $"Expected member access after standard class '{className}'", FeiSharpErrorCodes.MissingDelimiter);
            }
            if (!MatchMemberName(out string memberName))
            {
                throw new Exception(_tokens, _current, "Expected member name after '.'", FeiSharpErrorCodes.MissingDelimiter);
            }
            try
            {
                object? value = MatchPunctuation("(")
                    ? StandardLibrary.InvokeStatic(className, memberName, ParseArgumentValuesUntil(")"))
                    : StandardLibrary.GetStaticMember(className, memberName);
                return ParseRuntimeAccess(value);
            }
            catch (StandardLibraryException ex)
            {
                throw new Exception(_tokens, _current, ex.Message, ex.ErrorCode);
            }
            catch (OverflowException ex)
            {
                throw new Exception(_tokens, _current, ex.Message, FeiSharpErrorCodes.InvalidArgument);
            }
        }
        private void EnsureStandardClassNamespace(string className)
        {
            bool requiresIo = className.Equals("file", StringComparison.OrdinalIgnoreCase)
                || className.Equals("directory", StringComparison.OrdinalIgnoreCase)
                || className.Equals("path", StringComparison.OrdinalIgnoreCase);
            if (requiresIo && !isfileassembly)
            {
                throw new Exception(_tokens, _current,
                    $"Standard class '{className}' requires 'using FeiSharp.IO;'.",
                    FeiSharpErrorCodes.NamespaceNotImported);
            }
            if (className.Equals("http", StringComparison.OrdinalIgnoreCase) && !isnetassembly)
            {
                throw new Exception(_tokens, _current,
                    "Standard class 'Http' requires 'using FeiSharp.Net;'.",
                    FeiSharpErrorCodes.NamespaceNotImported);
            }
        }
        private Expr ParseContextKeywordExpression(bool useBase)
        {
            ClassInfo context = GetAccessibleClassContext(useBase);
            if (!MatchPunctuation("."))
            {
                return new ValueExpr(new ClassInstance
                {
                    ClassName = context.Name,
                    Variables = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase),
                    Functions = context._FunctionInfo,
                    VariableInfos = context.VariableInfos
                });
            }
            if (!MatchMemberName(out string memberName))
            {
                throw new Exception(_tokens, _current, "Expected member name after context keyword", "FS2003");
            }
            if (MatchPunctuation("("))
            {
                List<object> args = [];
                while (!IsAtEnd() && Peek().Value != ")")
                {
                    if (Peek().Value == ",")
                    {
                        Advance();
                        continue;
                    }
                    args.Add(EvaluateExpression(ParseExpression()));
                }
                if (!MatchPunctuation(")"))
                {
                    throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
                }
                return new ValueExpr(InvokeContextMethod(context, memberName, args, useBase));
            }
            if (!context._Vars.TryGetValue(memberName, out object? value))
            {
                throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
            }
            VariableInfo variableInfo = GetVariableInfoOrThrow(context, memberName);
            if (variableInfo.IsStatic)
            {
                throw new Exception(_tokens, _current, $"Static member '{memberName}' must be accessed on class '{context.Name}'.", "FS3001");
            }
            EnsureVariableAccessible(variableInfo, memberName, useBase);
            return new ValueExpr(value);
        }
        private ClassInfo GetAccessibleClassContext(bool useBase)
        {
            if (string.IsNullOrWhiteSpace(_currentExecutionClassName) || !_classInfos.TryGetValue(_currentExecutionClassName, out var currentClass))
            {
                throw new Exception(_tokens, _current, useBase ? "'base' can only be used inside a derived class member." : "'this' can only be used inside a class member.", "FS3001");
            }
            if (!useBase)
            {
                return currentClass;
            }
            if (string.IsNullOrWhiteSpace(currentClass.BaseClassName) || !_classInfos.TryGetValue(currentClass.BaseClassName, out var baseClass))
            {
                throw new Exception(_tokens, _current, "'base' requires an accessible base class.", "FS3001");
            }
            return baseClass;
        }
        private object InvokeContextMethod(ClassInfo classInfo, string methodName, List<object> args, bool useBase)
        {
            if (!classInfo._FunctionInfo.TryGetValue(methodName, out var functionInfo))
            {
                throw new Exception(_tokens, _current, $"Undefined method: {methodName}", "FS3001");
            }
            if (useBase && functionInfo.Visibility == MethodVisibility.Private)
            {
                throw new Exception(_tokens, _current, $"Base member '{methodName}' is not accessible.", "FS3001");
            }
            if (!useBase && !CanAccessMethod(functionInfo))
            {
                throw new Exception(_tokens, _current, $"Method '{methodName}' is not accessible.", "FS3001");
            }
            var methodScope = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < functionInfo.Parameter.Count && i < args.Count; i++)
            {
                methodScope[functionInfo.Parameter[i]] = args[i];
            }
            var result = Run(functionInfo.FunctionBody, methodScope, methodName, CloneClassInfos(_classInfos));
            foreach (var kvp in result)
            {
                _variables[kvp.Key] = kvp.Value;
            }
            return _results.TryGetValue(methodName, out var returnValue) ? returnValue : null;
        }
        internal bool CanAccessMethod(FunctionInfo functionInfo)
        {
            return !functionInfo.IsClassMember
                || functionInfo.Visibility == MethodVisibility.Public
                || string.Equals(_currentExecutionClassName, functionInfo.DeclaringClassName, StringComparison.OrdinalIgnoreCase)
                || (functionInfo.Visibility == MethodVisibility.Protected
                    && IsClassOrDerivedFrom(_currentExecutionClassName, functionInfo.DeclaringClassName));
        }
        internal bool CanAccessVariable(VariableInfo variableInfo)
        {
            return !variableInfo.IsClassMember
                || variableInfo.Visibility == MethodVisibility.Public
                || string.Equals(_currentExecutionClassName, variableInfo.DeclaringClassName, StringComparison.OrdinalIgnoreCase)
                || (variableInfo.Visibility == MethodVisibility.Protected
                    && IsClassOrDerivedFrom(_currentExecutionClassName, variableInfo.DeclaringClassName));
        }
        private VariableInfo GetVariableInfoOrThrow(ClassInfo classInfo, string memberName)
        {
            if (classInfo.VariableInfos.TryGetValue(memberName, out var variableInfo))
            {
                return variableInfo;
            }
            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
        }
        private VariableInfo GetVariableInfoOrThrow(ClassInstance instance, string memberName)
        {
            if (instance.VariableInfos.TryGetValue(memberName, out var variableInfo))
            {
                return variableInfo;
            }
            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
        }
        private void EnsureVariableAccessible(VariableInfo variableInfo, string memberName, bool useBase = false)
        {
            if (useBase && variableInfo.Visibility == MethodVisibility.Private)
            {
                throw new Exception(_tokens, _current, $"Base member '{memberName}' is not accessible.", "FS3001");
            }
            if (!useBase && !CanAccessVariable(variableInfo))
            {
                throw new Exception(_tokens, _current, $"Member '{memberName}' is not accessible.", "FS3001");
            }
        }
        private bool IsClassOrDerivedFrom(string? className, string? expectedBaseClassName)
        {
            if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(expectedBaseClassName))
            {
                return false;
            }

            string? currentClassName = className;
            while (!string.IsNullOrWhiteSpace(currentClassName) && _classInfos.TryGetValue(currentClassName, out var currentClass))
            {
                if (string.Equals(currentClass.Name, expectedBaseClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                currentClassName = currentClass.BaseClassName;
            }

            return false;
        }
        private bool MatchPreviousToken(params TokenTypes[] types)
        {
            foreach (var type in types)
            {
                if (PreviousCheck(type))
                {
                    return true;
                }
            }
            return false;
        }
        private bool PreviousCheck(TokenTypes type)
        {
            return !IsAtEnd() && Previous().Type == type;
        }
        private bool MatchToken(params TokenTypes[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }
        private bool MatchToken(TokenTypes type, out string value)
        {
            if (Check(type))
            {
                value = Peek().Value;
                Advance();
                return true;
            }
            value = null;
            return false;
        }
        private bool MatchKeyword(string keyword)
        {
            if (Check(TokenTypes.Keyword) && Peek().Value == keyword)
            {
                Advance();
                return true;
            }
            return false;
        }
        private bool MatchWord(string keyword)
        {
            if (Check(TokenTypes.Keyword) && Peek().Value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                return true;
            }
            if (Check(TokenTypes.Identifier) && Peek().Value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                return true;
            }
            return false;
        }
        private bool MatchMemberName(out string value)
        {
            if (MatchToken(TokenTypes.Identifier, out value) || MatchToken(TokenTypes.Keyword, out value))
            {
                return true;
            }
            value = string.Empty;
            return false;
        }
        private bool MatchIdentifierOrSoftKeyword(out string value, params string[] softKeywords)
        {
            if (MatchToken(TokenTypes.Identifier, out value))
            {
                return true;
            }
            if (Check(TokenTypes.Keyword) && softKeywords.Contains(Peek().Value, StringComparer.OrdinalIgnoreCase))
            {
                value = Advance().Value;
                return true;
            }
            value = string.Empty;
            return false;
        }
        private bool MatchPunctuation(string punctuation)
        {
            if (Check(TokenTypes.Punctuation) && Peek().Value == punctuation)
            {
                Advance();
                return true;
            }
            return false;
        }
        private Token Peek(int offset)
        {
            int index = _current + offset;
            if (index >= _tokens.Count)
            {
                return _tokens[^1];
            }
            return _tokens[index];
        }
        private bool CheckPunctuation(string punctuation)
        {
            return !IsAtEnd() && Peek().Type == TokenTypes.Punctuation && Peek().Value == punctuation;
        }
        private bool CheckOperator(string op)
        {
            return !IsAtEnd() && Peek().Type == TokenTypes.Operator && Peek().Value == op;
        }
        private void ConsumeOptionalSemicolon()
        {
            if (!IsAtEnd() && Peek().Type == TokenTypes.Punctuation && Peek().Value == ";")
            {
                Advance();
            }
        }
        private Dictionary<string, ClassInfo> CloneClassInfos(Dictionary<string, ClassInfo> source)
        {
            return source.ToDictionary(
                item => item.Key,
                item => new ClassInfo(item.Value._FunctionInfo, item.Value._Vars, item.Value.Name, item.Value.ConstructorInfo, item.Value.BaseClassName, item.Value.VariableInfos),
                StringComparer.OrdinalIgnoreCase);
        }
        private List<Token> ParseScopedTokens(string openToken, string closeToken, string blockName, string? legacyOpenToken = null, string? legacyCloseToken = null)
        {
            if (MatchPunctuation(openToken))
            {
                return ReadBraceBlock(closeToken, blockName);
            }
            if (!string.IsNullOrEmpty(legacyOpenToken) && Peek().Value == legacyOpenToken)
            {
                Advance();
                if (Peek().Value == ":")
                {
                    Advance();
                }
                return ReadLegacyBlock(legacyOpenToken, legacyCloseToken!, blockName);
            }
            throw new Exception(_tokens, _current, $"Expected '{openToken}' to start {blockName} block", "FS2003");
        }
        private List<Token> ReadBraceBlock(string closeToken, string blockName)
        {
            List<Token> tokens = new();
            int depth = 1;
            while (!IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Type == TokenTypes.Punctuation && token.Value == "{")
                {
                    depth++;
                    tokens.Add(token);
                    continue;
                }
                if (token.Type == TokenTypes.Punctuation && token.Value == closeToken)
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (!IsAtEnd() && Peek().Value == ";")
                        {
                            Advance();
                        }
                        return tokens;
                    }
                    tokens.Add(token);
                    continue;
                }
                tokens.Add(token);
            }
            throw new Exception(_tokens, _current, $"Unterminated {blockName} block", "FS2003");
        }
        private List<Token> ReadLegacyBlock(string legacyOpenToken, string legacyCloseToken, string blockName)
        {
            List<Token> tokens = new();
            int depth = 1;
            while (!IsAtEnd())
            {
                Token token = Peek();
                Advance();
                if (token.Value == legacyOpenToken)
                {
                    depth++;
                    if (Peek().Value == ":")
                    {
                        Advance();
                    }
                    continue;
                }
                if (token.Value == legacyCloseToken)
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (!IsAtEnd() && Peek().Value == ";")
                        {
                            Advance();
                        }
                        return tokens;
                    }
                    continue;
                }
                tokens.Add(token);
            }
            throw new Exception(_tokens, _current, $"Unterminated {blockName} block", "FS2003");
        }
        private ClassInfo BuildClassInfo(string className, string? baseClassName, List<Token> classBodyTokens)
        {
            Dictionary<string, object> baseVars = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FunctionInfo> baseFunctions = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, VariableInfo> baseVariableInfos = new(StringComparer.OrdinalIgnoreCase);
            FunctionInfo? constructorInfo = null;
            if (!string.IsNullOrWhiteSpace(baseClassName))
            {
                if (!_classInfos.TryGetValue(baseClassName, out var baseClassInfo))
                {
                    throw new Exception(_tokens, _current, $"Base class not defined: {baseClassName}", "FS3001");
                }
                baseVars = new(baseClassInfo._Vars, StringComparer.OrdinalIgnoreCase);
                baseVariableInfos = new(baseClassInfo.VariableInfos, StringComparer.OrdinalIgnoreCase);
                baseFunctions = new(
                    baseClassInfo._FunctionInfo
                        .Where(kvp => kvp.Value.Visibility != MethodVisibility.Private),
                    StringComparer.OrdinalIgnoreCase);
                constructorInfo = baseClassInfo.ConstructorInfo;
            }
            var outerFunctionNames = new HashSet<string>(_functions.Keys, StringComparer.OrdinalIgnoreCase);
            Parser parser = new(RemoveConstructorTokens(className, classBodyTokens));
            parser.player = this.player;
            parser.OutputEvent = this.OutputEvent;
            parser.ShouldCancel = this.ShouldCancel;
            parser._isParsingClassBody = true;
            parser._classDeclarationName = className;
            parser._variables = new(baseVars, StringComparer.OrdinalIgnoreCase);
            parser._variableInfos = new(baseVariableInfos, StringComparer.OrdinalIgnoreCase);
            parser._functions = new(_functions, StringComparer.OrdinalIgnoreCase);
            parser._classInfos = CloneClassInfos(_classInfos);
            parser.ParseStatements();
            this.player = parser.player;
            var memberFunctions = new Dictionary<string, FunctionInfo>(baseFunctions, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parser._functions)
            {
                if (!outerFunctionNames.Contains(kvp.Key))
                {
                    memberFunctions[kvp.Key] = kvp.Value;
                }
            }
            constructorInfo = ParseConstructorInfo(className, classBodyTokens) ?? constructorInfo;
            return new ClassInfo(memberFunctions, parser._variables, className, constructorInfo, baseClassName, parser._variableInfos);
        }
        private FunctionInfo? ParseConstructorInfo(string className, List<Token> classBodyTokens)
        {
            for (int i = 0; i < classBodyTokens.Count; i++)
            {
                var token = classBodyTokens[i];
                bool isConstructor = token.Type == TokenTypes.Identifier
                    && (token.Value.Equals("constructor", StringComparison.OrdinalIgnoreCase)
                    || token.Value.Equals(className, StringComparison.OrdinalIgnoreCase));
                if (!isConstructor)
                {
                    continue;
                }
                if (i + 1 >= classBodyTokens.Count || classBodyTokens[i + 1].Value != "(")
                {
                    continue;
                }
                i += 2;
                List<string> parameters = [];
                while (i < classBodyTokens.Count && classBodyTokens[i].Value != ")")
                {
                    if (classBodyTokens[i].Type == TokenTypes.Identifier)
                    {
                        parameters.Add(classBodyTokens[i].Value);
                    }
                    i++;
                }
                if (i >= classBodyTokens.Count || classBodyTokens[i].Value != ")")
                {
                    throw new Exception(_tokens, _current, $"Invalid constructor declaration in class '{className}'", "FS2003");
                }
                i++;
                if (i >= classBodyTokens.Count || classBodyTokens[i].Value != "{")
                {
                    throw new Exception(_tokens, _current, $"Constructor in class '{className}' must use '{{}}' block", "FS2003");
                }
                i++;
                List<Token> bodyTokens = [];
                int depth = 1;
                for (; i < classBodyTokens.Count; i++)
                {
                    var currentToken = classBodyTokens[i];
                    if (currentToken.Value == "{")
                    {
                        depth++;
                        bodyTokens.Add(currentToken);
                        continue;
                    }
                    if (currentToken.Value == "}")
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new FunctionInfo($"{className}:ctor", parameters, bodyTokens, MethodVisibility.Public, false, true, className);
                        }
                        bodyTokens.Add(currentToken);
                        continue;
                    }
                    bodyTokens.Add(currentToken);
                }
                throw new Exception(_tokens, _current, $"Unterminated constructor block in class '{className}'", "FS2003");
            }
            return null;
        }
        private List<Token> RemoveConstructorTokens(string className, List<Token> classBodyTokens)
        {
            List<Token> filtered = [];
            for (int i = 0; i < classBodyTokens.Count; i++)
            {
                var token = classBodyTokens[i];
                bool isConstructor = token.Type == TokenTypes.Identifier
                    && (token.Value.Equals("constructor", StringComparison.OrdinalIgnoreCase)
                    || token.Value.Equals(className, StringComparison.OrdinalIgnoreCase));
                if (!isConstructor || i + 1 >= classBodyTokens.Count || classBodyTokens[i + 1].Value != "(")
                {
                    filtered.Add(token);
                    continue;
                }
                i += 2;
                while (i < classBodyTokens.Count && classBodyTokens[i].Value != ")")
                {
                    i++;
                }
                if (i >= classBodyTokens.Count || i + 1 >= classBodyTokens.Count || classBodyTokens[i + 1].Value != "{")
                {
                    throw new Exception(_tokens, _current, $"Invalid constructor declaration in class '{className}'", "FS2003");
                }
                i += 2;
                int depth = 1;
                for (; i < classBodyTokens.Count; i++)
                {
                    if (classBodyTokens[i].Value == "{")
                    {
                        depth++;
                        continue;
                    }
                    if (classBodyTokens[i].Value == "}")
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                }
            }
            return filtered;
        }
        private ClassInfo RunClassConstructor(string className, ClassInfo classInfo)
        {
            if (classInfo.ConstructorInfo == null)
            {
                SkipInvocationArguments();
                return classInfo;
            }
            var ctorScope = new Dictionary<string, object>(classInfo._Vars, StringComparer.OrdinalIgnoreCase);
            RunFunction(classInfo.ConstructorInfo.Name, classInfo.ConstructorInfo, classInfo, ctorScope);
            ClassInfo updated = new(classInfo._FunctionInfo, ctorScope, className, classInfo.ConstructorInfo, classInfo.BaseClassName, classInfo.VariableInfos);
            _classInfos[className] = updated;
            return updated;
        }
        private void SkipInvocationArguments()
        {
            if (!MatchPunctuation("("))
            {
                return;
            }
            int depth = 1;
            while (!IsAtEnd() && depth > 0)
            {
                Token token = Peek();
                Advance();
                if (token.Value == "(") depth++;
                if (token.Value == ")") depth--;
            }
        }
        private void RunFunction(string funcName, FunctionInfo functionInfo, ClassInfo? classContext, Dictionary<string, object>? targetVariables = null)
        {
            List<object> actualParameters = new();
            if (Peek().Value == "(")
            {
                Advance();
            }
            else
            {
                Advance();
                if (!MatchPunctuation("("))
                {
                    throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                }
            }
            while (Peek().Value != ")" && Peek().Value != ";")
            {
                if (Peek().Value == ",")
                {
                    Advance();
                    continue;
                }
                actualParameters.Add(EvaluateExpression(ParseExpression()));
            }
            if (!MatchPunctuation(")"))
            {
                throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            }
            RunFunction(funcName, functionInfo, classContext, actualParameters, targetVariables);
        }
        private void RunFunction(string funcName, FunctionInfo functionInfo, ClassInfo? classContext, List<object> actualParameters, Dictionary<string, object>? targetVariables = null)
        {
            var scope = targetVariables ?? _variables;
            string? previousExecutionClassName = _currentExecutionClassName;
            string? previousFunctionName = _currentFunctionName;
            string? previousNamespace = _currentNamespace;
            var previousUsingNamespaces = _usingNamespaces;
            var savedParameters = new Dictionary<string, (bool existed, object? value)>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < functionInfo.Parameter.Count; i++)
            {
                try
                {
                    string parameterName = functionInfo.Parameter[i];
                    savedParameters[parameterName] = scope.TryGetValue(parameterName, out object? existingValue)
                        ? (true, existingValue)
                        : (false, null);
                    scope[parameterName] = actualParameters[i];
                }
                catch
                {
                    throw new Exception(_tokens, _current, "Parameters is not correct.", "FS3003");
                }
            }
            var functionClasses = CloneClassInfos(_classInfos);
            if (classContext != null)
            {
                functionClasses[classContext.Name] = new ClassInfo(classContext._FunctionInfo, scope, classContext.Name, classContext.ConstructorInfo, classContext.BaseClassName, classContext.VariableInfos);
            }
            var previousFunctions = _functions;
            _currentExecutionClassName = classContext?.Name ?? previousExecutionClassName;
            _currentFunctionName = funcName;
            _currentNamespace = functionInfo.UsingNamespaces != null ? functionInfo.Namespace : GetNamespaceOfSymbol(funcName) ?? previousNamespace;
            if (functionInfo.UsingNamespaces != null)
                _usingNamespaces = new HashSet<string>(functionInfo.UsingNamespaces, StringComparer.OrdinalIgnoreCase);
            if (classContext != null)
            {
                _functions = new(previousFunctions, StringComparer.OrdinalIgnoreCase);
                foreach (var memberFunction in classContext._FunctionInfo)
                {
                    _functions[memberFunction.Key] = memberFunction.Value;
                }
            }
            try
            {
                _variables = Run(functionInfo.FunctionBody, scope, funcName, functionClasses);
            }
            finally
            {
                foreach (var savedParameter in savedParameters)
                {
                    if (savedParameter.Value.existed)
                    {
                        scope[savedParameter.Key] = savedParameter.Value.value!;
                    }
                    else
                    {
                        scope.Remove(savedParameter.Key);
                    }
                }

                if (classContext != null)
                {
                    _functions = previousFunctions;
                }
                _currentExecutionClassName = previousExecutionClassName;
                _currentFunctionName = previousFunctionName;
                _currentNamespace = previousNamespace;
                _usingNamespaces = previousUsingNamespaces;
                if (classContext != null)
                {
                    _classInfos[classContext.Name] = new ClassInfo(classContext._FunctionInfo, _variables, classContext.Name, classContext.ConstructorInfo, classContext.BaseClassName, classContext.VariableInfos);
                }
            }
        }

        private static string? GetNamespaceOfSymbol(string symbolName)
        {
            int separator = symbolName.LastIndexOf('.');
            return separator > 0 ? symbolName[..separator] : null;
        }
        private bool MatchOperator(params string[] operators)
        {
            if (Check(TokenTypes.Operator) && operators.Contains(Peek().Value))
            {
                Advance();
                return true;
            }
            return false;
        }
        private bool Check(TokenTypes type)
        {
            return !IsAtEnd() && Peek().Type == type;
        }
        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return Previous();
        }
        private bool IsAtEnd()
        {
            return _current >= _tokens.Count || _tokens[_current].Type == TokenTypes.EndOfFile;
        }
        private Token Peek()
        {
            if (IsAtEnd()) throw new Exception(_tokens, _current, "No more tokens available.", "FS2003");
            return _tokens[_current];
        }
        private Token Previous()
        {
            if (_current == 0) throw new Exception(_tokens, _current, "No previous token available.", "FS2003");
            return _tokens[_current - 1];
        }
        private void EvaluatePrintStmt(PrintStmt stmt)
        {
            string text = EvaluateExpression(stmt.Expression)?.ToString() ?? "";
            if (text == "$(meidufei)")
            {
                var mds = new FeiSharpTerminal3.MeDuFeiAnimation();
                mds.Run();
            }
            else if (text.StartsWith("$(variable:") && text.EndsWith(")"))
            {
                text = _variables[text.Split("$(variable:")[1].Split(")")[0]].ToString();
            }
            Console.Write(text);
        }
        private object EvaluateUnaryExpr(UnaryExpr unaryExpr)
        {
            object operand = EvaluateExpression(unaryExpr.Operand);

            if (unaryExpr.Operator == "-")
            {
                return operand switch
                {
                    double d => -d,
                    int i => -i,
                    float f => -f,
                    long l => -l,
                    decimal dec => -dec,
                    _ => throw new Exception(_tokens, _current,
                        $"Cannot apply unary '-' to type {operand.GetType()}", "FS2003")
                };
            }
            else if (unaryExpr.Operator == "!")
            {
                return operand switch
                {
                    bool b => !b,
                    _ => throw new Exception(_tokens, _current,
                        $"Cannot apply unary '!' to type {operand.GetType()}", "FS2003")
                };
            }
            else if (unaryExpr.Operator == "+")
            {
                return operand;
            }
            throw new Exception(_tokens, _current,
                $"Unknown unary operator: {unaryExpr.Operator}", "FS2003");
        }
        private object EvaluateIncrementExpr(IncrementExpr incExpr)
        {
            object value = EvaluateExpression(incExpr.Operand);
            string varName = null;
            if (incExpr.Operand is VarExpr varExpr)
            {
                varName = varExpr.Name;
                if (varName.EndsWith("_readonly") || keys.Contains(varName)) throw new Exception(_tokens, _current, "Cannot set or cover a const", FeiSharpErrorCodes.ReadonlyViolation);
            }
            else
            {
                throw new Exception(_tokens, _current,
                    "Increment/decrement operator can only be applied to variables", "FS2003");
            }
            if (_variableInfos.TryGetValue(varName, out var info) && info.IsReadOnly)
            {
                throw new Exception(_tokens, _current,
                    $"Cannot modify readonly variable '{varName}'", FeiSharpErrorCodes.ReadonlyViolation);
            }
            if (!_variables.ContainsKey(varName))
            {
                throw new Exception(_tokens, _current,
                    $"Undefined variable: {varName}", FeiSharpErrorCodes.UndefinedSymbol);
            }
            double numericValue;
            try
            {
                numericValue = Convert.ToDouble(value);
            }
            catch
            {
                throw new Exception(_tokens, _current,
                    $"Cannot increment/decrement non-numeric value: {value.GetType()}", FeiSharpErrorCodes.TypeMismatch);
            }
            double newValue = incExpr.Operator == "++" ? numericValue + 1 : numericValue - 1;
            _variables[varName] = newValue;
            return incExpr.IsPrefix ? newValue : value;
        }

        private object EvaluateExpression(Expr expr)
        {
            switch (expr)
            {
                case RuntimeValueExpr runtimeValueExpr:
                    return runtimeValueExpr.Value;
                case ValueExpr numExpr:
                    return EvaluateValueExpr(numExpr);
                case VarExpr varExpr:
                    return EvaluateVarExpr(varExpr);
                case BinaryExpr binExpr:
                    return EvaluateBinaryExpr(binExpr);
                case ConditionalExpr conditionalExpr:
                    return EvaluateConditionalExpr(conditionalExpr);
                case IncrementExpr incExpr:
                    return EvaluateIncrementExpr(incExpr);
                case StringExpr stringExpr:
                    return EvaluateStringExpr(stringExpr);
                case UnaryExpr unaryExpr:
                    return EvaluateUnaryExpr(unaryExpr);

                default:
                    throw new Exception(_tokens, _current, "Unexpected expression type", "FS2003");
            }
        }
        private object? EvaluateConditionalExpr(ConditionalExpr conditionalExpr)
        {
            object? condition = EvaluateExpression(conditionalExpr.Condition);
            if (condition is not bool matches)
            {
                throw new Exception(_tokens, _current, "The condition after 'when' must be a boolean value", "FS2003");
            }
            return matches
                ? EvaluateExpression(conditionalExpr.TrueValue)
                : conditionalExpr.FalseValue is null ? null : EvaluateExpression(conditionalExpr.FalseValue);
        }
        private object EvaluateVarExpr(VarExpr varExpr)
        {
            return _variables[varExpr.Name];
        }
        private object EvaluateValueExpr(ValueExpr numExpr)
        {
            if (numExpr.Value == null)
            {
                return null;
            }
            if (numExpr.Value is double d)
                return d;
            if (numExpr.Value is int i)
                return (double)i;
            if (numExpr.Value is float f)
                return (double)f;
            if (numExpr.Value is string str)
            {
                str = Regex.Replace(str, @"\$\(unicode:([0-9A-Fa-f]{4,5})\)",
                    m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
                for (int a = 0; a < str.Length; a++)
                {
                    if (str[a] == '\\')
                    {
                        bool isEscapedBackslash = a > 0 && str[a - 1] == '\\';
                        bool hasNextChar = a + 1 < str.Length;
                        bool isValidEscape = hasNextChar && (str[a + 1] == '\\' || str[a + 1] == 'n' || str[a + 1] == 't');
                        if (!isEscapedBackslash && !isValidEscape)
                        {
                            throw new Exception(_tokens, _current, "FS1002: '\\' syntax is error, it must be 'n', 't', or another '\\' after '\\'", "FS1002");
                        }
                    }
                }
                return str.Replace("$(newline)", "\n")
                          .Replace("$(tab)", "    ")
                          .Replace("\\n", "\n")
                          .Replace("\\t", "\t")
                          .Replace("\\\\", "\\");
            }
            if (numExpr.Value is char c)
            {
                string result = Regex.Replace(c.ToString(), @"\$\(unicode:([0-9A-Fa-f]{4,5})\)",
                    m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
                return result.Replace("\\\\", "\\").Replace("\\t", "\t").Replace("\\n", "\n");
            }
            if (numExpr.Value is bool b)
                return b;

            if (double.TryParse(numExpr.Value.ToString(), out double parsed))
                return parsed;

            return numExpr.Value;
        }
        private Expr ParseNewExpression()
        {
            int savedPosition = _current;
            if (TryParseCollectionCreation(out object? created))
            {
                return new ValueExpr(created);
            }
            _current = savedPosition;
            return ParseLegacyNewExpression();
        }
        [RequiresDynamicCode("FeiSharp creates CLR instances from runtime type names.")]
        [RequiresUnreferencedCode("FeiSharp creates CLR instances from runtime type names.")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Console))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(string))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(int))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(double))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(bool))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(char))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Type))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Assembly))]
        [DynamicDependency("System.Type System.Type::GetType(System.String)")]
        [DynamicDependency("System.Reflection.Assembly System.Reflection.Assembly::GetExecutingAssembly()")]
        [DynamicDependency("System.Reflection.Assembly System.Reflection.Assembly::GetCallingAssembly()")]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runtime type creation for script engine")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Runtime type creation for script engine")]
        private Expr ParseLegacyNewExpression()
        {
            string className = Advance().Value;
            if (!MatchKeyword("in")) throw new Exception(_tokens, _current, "Expected 'in' keyword", "FS2003");
            string space = Peek().Value;
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            List<object> args = new();
            while (Peek().Value != ")")
            {
                Advance();
                if (Peek().Value != "," && Peek().Value != ")")
                {
                    args.Add(Peek().Value);
                }
            }
            Type? type = TypeLoader.LoadType(space + "." + className);
            if (type == null)
            {
                var assemblies = new[]
                {
                    typeof(Console).Assembly,
                    typeof(string).Assembly,
                    Assembly.GetExecutingAssembly(),
                    Assembly.GetCallingAssembly()
                };
                foreach (var assembly in assemblies)
                {
                    type = assembly.GetType(space + "." + className);
                    if (type != null)
                    {
                        break;
                    }
                }
                if (type == null)
                {
                    throw new Exception(_tokens, _current, "Type is not correct", "FS2003");
                }
            }
            var created = SmartActivator.CreateInstance(type, args.ToArray());
            return new ValueExpr(created);
        }
        private bool TryParseCollectionCreation(out object? created)
        {
            created = null;
            if (!Check(TokenTypes.Identifier))
            {
                return false;
            }
            string typeName = Peek().Value;
            if (typeName.Equals("List", StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                Type elementType = ParseGenericTypeArguments().SingleOrDefault() ?? typeof(object);
                Type concreteType = typeof(List<>).MakeGenericType(elementType);
                IList list = (IList)SmartActivator.CreateInstance(concreteType, ParseOptionalArgumentList().ToArray());
                if (MatchPunctuation("{"))
                {
                    foreach (object item in ParseBraceSeparatedExpressions("}"))
                    {
                        list.Add(ConvertValueForType(item, elementType));
                    }
                }
                created = list;
                return true;
            }
            if (typeName.Equals("Dictionary", StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                List<Type> genericTypes = ParseGenericTypeArguments();
                Type keyType = genericTypes.Count > 0 ? genericTypes[0] : typeof(object);
                Type valueType = genericTypes.Count > 1 ? genericTypes[1] : typeof(object);
                Type concreteType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                IDictionary dictionary = (IDictionary)SmartActivator.CreateInstance(concreteType, ParseOptionalArgumentList().ToArray());
                if (MatchPunctuation("{"))
                {
                    ParseDictionaryInitializer(dictionary, keyType, valueType);
                }
                created = dictionary;
                return true;
            }
            if (Check(TokenTypes.Identifier) && Peek(1).Type == TokenTypes.Punctuation && Peek(1).Value == "[")
            {
                Type elementType = ResolveTypeName(Peek().Value);
                Advance();
                MatchPunctuation("[");
                int? size = null;
                if (!MatchPunctuation("]"))
                {
                    size = Convert.ToInt32(EvaluateExpression(ParseExpression()));
                    if (!MatchPunctuation("]"))
                    {
                        throw new Exception(_tokens, _current, "Expected ']'", "FS2003");
                    }
                }
                List<object> items = [];
                if (MatchPunctuation("{"))
                {
                    items.AddRange(ParseBraceSeparatedExpressions("}"));
                }
                if (size.HasValue && items.Count == 0)
                {
                    created = Array.CreateInstance(elementType, size.Value);
                    return true;
                }
                Array array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    array.SetValue(ConvertValueForType(items[i], elementType), i);
                }
                created = array;
                return true;
            }
            return false;
        }
        private List<Type> ParseGenericTypeArguments()
        {
            List<Type> result = [];
            if (!CheckOperator("<"))
            {
                return result;
            }
            Advance();
            while (!IsAtEnd())
            {
                if (!MatchToken(TokenTypes.Identifier, out string typeName))
                {
                    throw new Exception(_tokens, _current, "Expected generic type name", "FS2003");
                }
                result.Add(ResolveTypeName(typeName));
                if (CheckOperator(">"))
                {
                    Advance();
                    break;
                }
                if (!MatchPunctuation(","))
                {
                    throw new Exception(_tokens, _current, "Expected ',' or '>'", "FS2003");
                }
            }
            return result;
        }
        private List<object> ParseOptionalArgumentList()
        {
            List<object> args = [];
            if (!MatchPunctuation("("))
            {
                return args;
            }
            while (!IsAtEnd() && !CheckPunctuation(")"))
            {
                if (MatchPunctuation(","))
                {
                    continue;
                }
                args.Add(EvaluateExpression(ParseExpression()));
            }
            if (!MatchPunctuation(")"))
            {
                throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            }
            return args;
        }
        private List<object> ParseBraceSeparatedExpressions(string closingToken)
        {
            List<object> values = [];
            while (!IsAtEnd() && !CheckPunctuation(closingToken))
            {
                if (MatchPunctuation(","))
                {
                    continue;
                }
                values.Add(EvaluateExpression(ParseExpression()));
            }
            if (!MatchPunctuation(closingToken))
            {
                throw new Exception(_tokens, _current, $"Expected '{closingToken}'", "FS2003");
            }
            return values;
        }
        private object ParseInlineArrayLiteral()
        {
            List<object> values = ParseBraceSeparatedExpressions("]");
            return values.ToArray();
        }
        private void ParseDictionaryInitializer(IDictionary dictionary, Type keyType, Type valueType)
        {
            while (!IsAtEnd() && !CheckPunctuation("}"))
            {
                if (MatchPunctuation(","))
                {
                    continue;
                }
                if (!MatchPunctuation("{"))
                {
                    throw new Exception(_tokens, _current, "Expected '{' for dictionary entry", "FS2003");
                }
                object key = EvaluateExpression(ParseExpression());
                if (!MatchPunctuation(","))
                {
                    throw new Exception(_tokens, _current, "Expected ',' between dictionary key and value", "FS2003");
                }
                object value = EvaluateExpression(ParseExpression());
                if (!MatchPunctuation("}"))
                {
                    throw new Exception(_tokens, _current, "Expected '}' after dictionary entry", "FS2003");
                }
                dictionary.Add(ConvertValueForType(key, keyType), ConvertValueForType(value, valueType));
            }
            if (!MatchPunctuation("}"))
            {
                throw new Exception(_tokens, _current, "Expected '}'", "FS2003");
            }
        }
        private Expr ParseRuntimeAccess(object? value)
        {
            object? currentValue = value;
            while (!IsAtEnd())
            {
                if (MatchPunctuation("["))
                {
                    object key = EvaluateExpression(ParseExpression());
                    if (!MatchPunctuation("]"))
                    {
                        throw new Exception(_tokens, _current, "Expected ']'", "FS2003");
                    }
                    currentValue = GetIndexedValue(currentValue, key);
                    continue;
                }
                if (MatchPunctuation("."))
                {
                    if (!MatchMemberName(out string memberName))
                    {
                        throw new Exception(_tokens, _current, "Expected member name after '.'", "FS2003");
                    }
                    if (currentValue is ClassInstance classInstance)
                    {
                        if (MatchPunctuation("("))
                        {
                            currentValue = classInstance.CallMethod(memberName, ParseArgumentValuesUntil(")"), this);
                        }
                        else
                        {
                            currentValue = classInstance.GetVariable(memberName);
                        }
                        continue;
                    }
                    if (MatchPunctuation("("))
                    {
                        currentValue = InvokeRuntimeMember(currentValue, memberName, ParseArgumentValuesUntil(")"));
                    }
                    else
                    {
                        currentValue = GetRuntimeMember(currentValue, memberName);
                    }
                    continue;
                }
                break;
            }
            return new RuntimeValueExpr(currentValue);
        }
        private List<object> ParseArgumentValuesUntil(string closingToken)
        {
            List<object> args = [];
            while (!IsAtEnd() && !CheckPunctuation(closingToken))
            {
                if (MatchPunctuation(","))
                {
                    continue;
                }
                args.Add(EvaluateExpression(ParseExpression()));
            }
            if (!MatchPunctuation(closingToken))
            {
                throw new Exception(_tokens, _current, $"Expected '{closingToken}'", "FS2003");
            }
            return args;
        }
        private object? GetIndexedValue(object? target, object key)
        {
            if (target == null)
            {
                throw new Exception(_tokens, _current, "Cannot index a null value", FeiSharpErrorCodes.NullReference);
            }
            try
            {
                if (target is string str)
                {
                    return str[Convert.ToInt32(key)];
                }
                if (target is Array array)
                {
                    return array.GetValue(Convert.ToInt32(key));
                }
                if (target is IList list)
                {
                    return list[Convert.ToInt32(key)];
                }
                if (target is IDictionary dictionary)
                {
                    return dictionary[key];
                }
            }
            catch (System.Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or KeyNotFoundException)
            {
                throw new Exception(_tokens, _current, "Index or key is out of range", FeiSharpErrorCodes.IndexOutOfRange);
            }
            throw new Exception(_tokens, _current, $"Type '{target.GetType().Name}' does not support index access.", "FS3002");
        }
        private void SetIndexedValue(object target, object? key, object value)
        {
            if (target is Array array)
            {
                array.SetValue(ConvertValueForType(value, array.GetType().GetElementType() ?? typeof(object)), Convert.ToInt32(key));
                return;
            }
            if (target is IList list)
            {
                Type elementType = target.GetType().IsGenericType ? target.GetType().GetGenericArguments()[0] : typeof(object);
                list[Convert.ToInt32(key)] = ConvertValueForType(value, elementType);
                return;
            }
            if (target is IDictionary dictionary)
            {
                Type[] genericArguments = target.GetType().IsGenericType ? target.GetType().GetGenericArguments() : [typeof(object), typeof(object)];
                object convertedKey = ConvertValueForType(key, genericArguments[0])!;
                object convertedValue = ConvertValueForType(value, genericArguments[1])!;
                dictionary[convertedKey] = convertedValue;
                return;
            }
            throw new Exception(_tokens, _current, $"Type '{target.GetType().Name}' does not support indexed assignment.", "FS3001");
        }
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Array))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ICollection))]
        [DynamicDependency("System.Int32 System.Array::get_Length()")]
        [DynamicDependency("System.Int32 System.Collections.ICollection::get_Count()")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields, typeof(object))]
        private object? GetRuntimeMember([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] object? target, string memberName)
        {
            if (target == null)
            {
                throw new Exception(_tokens, _current, $"Cannot access member '{memberName}' on null.", FeiSharpErrorCodes.NullReference);
            }
            if (memberName.Equals("Count", StringComparison.OrdinalIgnoreCase))
            {
                if (target is ICollection collection)
                {
                    return collection.Count;
                }
                if (target is Array array)
                {
                    return array.Length;
                }
            }
            Type type = target.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null)
            {
                return property.GetValue(target);
            }
            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                return field.GetValue(target);
            }
            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", FeiSharpErrorCodes.UndefinedSymbol);
        }
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Type))]
        [DynamicDependency("System.Reflection.MethodInfo System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])")]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runtime reflection for script engine")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Runtime reflection for script engine")]
        private object? InvokeRuntimeMember([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] object? target, string memberName, List<object> args)
        {
            if (target == null)
            {
                throw new Exception(_tokens, _current, $"Cannot invoke member '{memberName}' on null.", FeiSharpErrorCodes.NullReference);
            }
            Type type = target.GetType();
            MethodInfo? method = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(m => CanBindArguments(m.GetParameters(), args));
            if (method == null)
            {
                throw new Exception(_tokens, _current, $"Undefined method: {memberName}", FeiSharpErrorCodes.UndefinedSymbol);
            }
            object?[] boundArgs = ConvertArgumentsToTarget(args, method);
            return method.Invoke(target, boundArgs);
        }
        private bool CanBindArguments(ParameterInfo[] parameters, List<object> args)
        {
            if (parameters.Length != args.Count)
            {
                return false;
            }
            for (int i = 0; i < parameters.Length; i++)
            {
                object arg = args[i];
                Type parameterType = parameters[i].ParameterType;
                if (arg == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        return false;
                    }
                    continue;
                }
                if (parameterType.IsInstanceOfType(arg))
                {
                    continue;
                }
                try
                {
                    ConvertValueForType(arg, parameterType);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }
        private object?[] ConvertArgumentsToTarget(List<object> args, MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object?[] converted = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                converted[i] = ConvertValueForType(args[i], parameters[i].ParameterType);
            }
            return converted;
        }
        private object? ConvertValueForType(object? value, Type targetType)
        {
            if (targetType == typeof(object))
            {
                return value;
            }
            if (value == null)
            {
                return null;
            }
            Type sourceType = value.GetType();
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }
            Type effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effectiveTargetType.IsEnum)
            {
                return Enum.Parse(effectiveTargetType, value.ToString()!, true);
            }
            if (sourceType == typeof(string) && effectiveTargetType == typeof(char[]))
            {
                return ((string)value).ToCharArray();
            }
            if (effectiveTargetType == typeof(string))
            {
                return value.ToString();
            }
            return Convert.ChangeType(value, effectiveTargetType);
        }
        private Type ResolveTypeName(string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "bool" or "boolean" => typeof(bool),
                "byte" => typeof(byte),
                "char" => typeof(char),
                "decimal" => typeof(decimal),
                "double" => typeof(double),
                "float" or "single" => typeof(float),
                "int" or "int32" => typeof(int),
                "long" or "int64" => typeof(long),
                "object" => typeof(object),
                "short" or "int16" => typeof(short),
                "string" => typeof(string),
                _ => TypeLoader.LoadType(typeName)
                    ?? TypeLoader.LoadType("System." + typeName)
                    ?? throw new Exception(_tokens, _current, $"Type is not correct: {typeName}", "FS2003")
            };
        }

        private object EvaluateBinaryExpr(BinaryExpr binExpr)
        {
            object? left = EvaluateExpression(binExpr.Left);
            object? right = EvaluateExpression(binExpr.Right);

            if (binExpr.Operator == "+" && (left is null || right is null))
            {
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");
            }

            if (left is null || right is null)
            {
                throw new Exception(_tokens, _current,
                    $"Cannot apply operator '{binExpr.Operator}' to null value.", FeiSharpErrorCodes.NullReference);
            }

            if ((left is string or char) && (right is string or char))
            {
                return EvaluateStringBinary(left, right, binExpr.Operator);
            }

            if (binExpr.Operator == "+" && (left is string or char || right is string or char))
            {
                return left.ToString() + right.ToString();
            }

            if (left is bool && right is bool)
            {
                return EvaluateBoolBinary((bool)left, (bool)right, binExpr.Operator);
            }

            if (left.GetType() != right.GetType())
            {
                if (!TryUnifyTypes(ref left, ref right))
                {
                    throw new Exception(_tokens, _current,
                        $"ITC: Unable to accurately determine the type or the conversion attempt failed", FeiSharpErrorCodes.TypeMismatch);
                }
            }

            if (binExpr.Operator == "*")
            {
                if ((left is string or char && right is double) ||
                    (left is double && right is string or char))
                {
                    string str = (left is string or char) ? left.ToString() : right.ToString();
                    int count = (int)((left is double) ? (double)left : (double)right);
                    return string.Concat(Enumerable.Repeat(str, count));
                }
            }

            return EvaluateNumericBinary(left, right, binExpr.Operator);
        }

        private object EvaluateStringBinary(object left, object right, string op)
        {
            string leftStr = left.ToString();
            string rightStr = right.ToString();

            return op switch
            {
                "+" => leftStr + rightStr,
                "-" => Regex.Replace(leftStr, Regex.Escape(rightStr), ""),
                "/" => leftStr.Split(rightStr).Length - 1,
                "=" => leftStr == rightStr,
                "!=" => leftStr != rightStr,
                _ => throw new Exception(_tokens, _current,
                    $"Cannot using operator '{op}' to connect string_obj and string_obj.", "FS2003")
            };
        }

        private object EvaluateBoolBinary(bool left, bool right, string op)
        {
            return op switch
            {
                "&&" => left && right,
                "||" => left || right,
                "&" => left && right,
                "|" => left || right,
                "!=" => left != right,
                "=" => left == right,
                _ => throw new Exception(_tokens, _current,
                    $"Cannot using operator '{op}' to connect bool_obj and bool_obj.", "FS2003")
            };
        }

        private bool TryUnifyTypes(ref object left, ref object right)
        {
            Type leftType = left.GetType();
            Type rightType = right.GetType();
            object originalRight = right;
            object originalLeft = left;

            try
            {
                right = Convert.ChangeType(right, leftType);
                return true;
            }
            catch { }

            try
            {
                left = Convert.ChangeType(left, rightType);
                right = originalRight;
                return true;
            }
            catch { }

            return false;
        }
        private Dictionary<string, object> RunWithoutNewParser(List<Token> tokens, Dictionary<string, object> variables)
        {
            var savedTokens = _tokens;
            var savedPosition = _current;
            var savedVariables = new Dictionary<string, object>(_variables);
            var savedFunctions = new Dictionary<string, FunctionInfo>(_functions);
            var savedClassInfos = CloneClassInfos(_classInfos);
            var savedResults = new Dictionary<string, object>(_results);

            try
            {
                _tokens = tokens;
                _current = 0;
                _variables = new Dictionary<string, object>(variables);
                _functions = new Dictionary<string, FunctionInfo>(_functions);
                _classInfos = CloneClassInfos(_classInfos);
                _results = new Dictionary<string, object>();

                ParseStatements();

                return new Dictionary<string, object>(_variables);
            }
            finally
            {
                foreach (var kv in _variables)
                {
                    savedVariables[kv.Key] = kv.Value;
                }

                _tokens = savedTokens;
                _current = savedPosition;
                _variables = savedVariables;
                _functions = savedFunctions;
                _classInfos = savedClassInfos;
                _results = savedResults;
            }
        }
        private object EvaluateNumericBinary(object left, object right, string op)
        {
            double leftNum, rightNum;

            try
            {
                leftNum = Convert.ToDouble(left);
                rightNum = Convert.ToDouble(right);
            }
            catch
            {
                throw new Exception(_tokens, _current,
                    $"Cannot convert {left.GetType()} or {right.GetType()} to number for operator '{op}'", FeiSharpErrorCodes.TypeMismatch);
            }

            return op switch
            {
                "+" => leftNum + rightNum,
                "-" => leftNum - rightNum,
                "*" => leftNum * rightNum,
                "/" => leftNum / rightNum,
                "^" => (int)leftNum ^ (int)rightNum,
                ">" => leftNum > rightNum,
                "<" => leftNum < rightNum,
                ">=" => leftNum >= rightNum,
                "<=" => leftNum <= rightNum,
                "=" => leftNum == rightNum,
                "&" => (int)leftNum & (int)rightNum,
                "|" => (int)leftNum | (int)rightNum,
                "!=" => leftNum != rightNum,
                "$" => Math.Pow(leftNum, rightNum),
                _ => throw new Exception(_tokens, _current,
                    $"ITC: Cannot using operator '{op}' to connect numeric values.", "FS2003")
            };
        }
        private int _counter;
        public string TransformEvalExpressions(string code)
        {
            _counter = 0;
            return ProcessEvalExpressions(code);
        }
        private string ProcessEvalExpressions(string code)
        {
            string pattern = @"\$\(eval:\s*([^()]*(?:\([^()]*\)[^()]*)*)\)";
            var match = Regex.Match(code, pattern);
            if (!match.Success)
                return code;
            string fullMatch = match.Value;
            string expression = match.Groups[1].Value.Trim();
            expression = ProcessEvalExpressions(expression);
            string guid = Guid.NewGuid().ToString("N");
            string lettersOnly = new string(Array.FindAll(guid.ToCharArray(), char.IsLetter));
            string tempVar = $"tmpEval{lettersOnly}{GetLetterFromNumber(_counter)}";
            _counter++;
            string replacement = tempVar;
            string declaration = $"var {tempVar} = {expression};";
            string result = ReplaceAndAddDeclaration(code, fullMatch, replacement, declaration);
            return ProcessEvalExpressions(result);
        }
        private string GetLetterFromNumber(int number)
        {
            if (number < 26)
                return ((char)('A' + number)).ToString();
            else
                return GetLetterFromNumber(number / 26 - 1) + GetLetterFromNumber(number % 26);
        }
        private string ReplaceAndAddDeclaration(string code, string target, string replacement, string declaration)
        {
            var lines = code.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var result = new StringBuilder();
            bool declarationAdded = false;
            foreach (var line in lines)
            {
                if (!declarationAdded && line.Contains(target))
                {
                    result.AppendLine(declaration);
                    string newLine = line.Replace(target, replacement);
                    newLine = FixStringConcatenation(newLine);
                    result.AppendLine(newLine);
                    declarationAdded = true;
                }
                else
                {
                    result.AppendLine(line);
                }
            }
            return result.ToString().TrimEnd();
        }

        private string FixStringConcatenation(string line)
        {
            line = Regex.Replace(line, "\"\"\\s*\\+\\s*", "");
            line = Regex.Replace(line, "\\s*\\+\\s*\"\"", "");
            line = Regex.Replace(line, "^\\s*\\+\\s*", "");
            line = Regex.Replace(line, "\\s*\\+\\s*$", "");
            line = Regex.Replace(line, "\\+\\s*\\+", "+");
            return line;
        }
        private object EvaluateStringExpr(StringExpr stringExpr)
        {
            stringExpr.Value = Regex.Replace(stringExpr.Value, @"\$\(unicode:([0-9A-Fa-f]{4,5})\)",
                m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
            string res = TransformEvalExpressions(stringExpr.Value.Replace("$(newline)", "\n")
                                  .Replace("$(tab)", "    ")
                                  .Replace("\\n", "\n")
                                  .Replace("\\t", "\t")
                                  .Replace("\\\\", "\\"));
            return res;
        }
    }
    [RequiresDynamicCode("FeiSharp constructs CLR instances from runtime-discovered constructors.")]
    [RequiresUnreferencedCode("FeiSharp constructs CLR instances from runtime-discovered constructors.")]
    public static class SmartActivator
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, "System.String", "System.Runtime")]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic activation is intentional for script engine")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Dynamic activation is intentional for script engine")]
        public static object CreateInstance(
            Type type,
            object[] args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));


            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);


            if (args == null || args.Length == 0)
            {
                var ctor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
                if (ctor != null)
                    return ctor.Invoke(null);


                if (type.IsValueType)
                    return Activator.CreateInstance(type);

                throw new MissingMethodException($"No parameterless constructor found for type {type}");
            }


            var exactMatch = FindExactMatchConstructor(constructors, args);
            if (exactMatch != null)
                return exactMatch.Invoke(args);


            var convertibleMatch = FindConvertibleMatchConstructor(constructors, args);
            if (convertibleMatch != null)
            {
                var convertedArgs = ConvertArguments(convertibleMatch, args);
                return convertibleMatch.Invoke(convertedArgs);
            }
            if (type == typeof(string))
                return HandleStringCreation(args);

            throw new MissingMethodException($"Cannot find matching constructor for type {type}");
        }


        private static ConstructorInfo FindExactMatchConstructor(
            ConstructorInfo[] constructors, object[] args)
        {
            var argTypes = args.Select(a => a?.GetType()).ToArray();

            return constructors.FirstOrDefault(ctor =>
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != args.Length) return false;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    var arg = args[i];


                    if (arg == null)
                    {
                        if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                            return false;
                    }
                    else if (arg.GetType() != paramType)
                    {
                        return false;
                    }
                }
                return true;
            });
        }


        private static ConstructorInfo FindConvertibleMatchConstructor(
            ConstructorInfo[] constructors, object[] args)
        {
            var candidates = constructors
                .Where(ctor => ctor.GetParameters().Length == args.Length)
                .ToList();


            var scoredCandidates = candidates.Select(ctor =>
            {
                var parameters = ctor.GetParameters();
                int score = 0;
                bool allConvertible = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    var arg = args[i];

                    if (arg == null)
                    {

                        if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                        {
                            allConvertible = false;
                            break;
                        }

                        score += 1;
                    }
                    else
                    {
                        var argType = arg.GetType();

                        if (paramType == argType)
                        {

                            score += 100;
                        }
                        else if (paramType.IsAssignableFrom(argType))
                        {

                            score += 50;
                        }
                        else if (CanConvert(argType, paramType))
                        {

                            score += 10;
                        }
                        else
                        {
                            allConvertible = false;
                            break;
                        }
                    }
                }

                return new { Constructor = ctor, Score = allConvertible ? score : -1 };
            })
            .Where(x => x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ToList();

            return scoredCandidates.FirstOrDefault()?.Constructor;
        }


        private static bool CanConvert(Type fromType, Type toType)
        {
            if (fromType == toType) return true;
            if (toType.IsAssignableFrom(fromType)) return true;


            try
            {

                if (fromType == typeof(string))
                {

                    if (toType == typeof(ReadOnlySpan<char>) ||
                        toType == typeof(Span<char>) ||
                        toType == typeof(char[]) ||
                        toType == typeof(IEnumerable<char>))
                        return true;
                }

                var testValue = fromType.IsValueType ? Activator.CreateInstance(fromType) : "";
                Convert.ChangeType(testValue, toType);
                return true;
            }
            catch
            {
                return false;
            }
        }


        private static object[] ConvertArguments(ConstructorInfo constructor, object[] args)
        {
            var parameters = constructor.GetParameters();
            var convertedArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var arg = args[i];

                if (arg == null)
                {
                    convertedArgs[i] = null;
                }
                else if (paramType == arg.GetType())
                {
                    convertedArgs[i] = arg;
                }
                else
                {

                    convertedArgs[i] = ConvertValue(arg, paramType);
                }
            }

            return convertedArgs;
        }


        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            var sourceType = value.GetType();

            if (sourceType == typeof(string) && targetType == typeof(char[]))
            {
                return ((string)value).ToCharArray();
            }


            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {

                if (targetType.IsAssignableFrom(sourceType))
                    return value;

                throw new InvalidCastException($"Cannot convert {sourceType} to {targetType}");
            }
        }


        private static object HandleStringCreation(object[] args)
        {
            if (args == null || args.Length == 0)
                return string.Empty;

            var arg = args[0];


            return arg switch
            {
                string s => s,

                char[] chars => new string(chars),


                char c when args.Length >= 2 && args[1] is int count => new string(c, count),


                _ => arg?.ToString() ?? string.Empty
            };
        }
    }
}
