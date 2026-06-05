using FeiSharp8._5RuntimeSdk;
using FeiSharpStudio.ClassInstance;
using FeiSharpStudio.UUID;
using FeiSharpTerminal3._1;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Exception = FeiSharpTerminal3._1.ExceptionThrow.Exception;
namespace FeiSharpStudio
{
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
        private object? _lastItValue;
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
            _variables.NewAdd("ten", 10);
            _variables.NewAdd("hundred", 100);
            _variables.NewAdd("thousand", 1000);
            _variables.NewAdd("pi", Math.PI);
            _variables.NewAdd("e", Math.E);
            _variables.NewAdd("tau", Math.Tau);
            _lastItValue = Math.Tau;
        }
        protected virtual void OnOutputEvent(OutputEventArgs e)
        {
            EventHandler<OutputEventArgs> handler = OutputEvent;
            handler?.Invoke(this, e);
        }
        public void ParseStatements(string funcName = "")
        {
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
                    else if (MatchKeyword(TokenKeywords.export))
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
                    else if (MatchKeyword(TokenKeywords._private))
                    {
                        ParseVisibilityQualifiedFunctionStatement(MethodVisibility.Private);
                    }
                    else if (MatchKeyword(TokenKeywords._public))
                    {
                        ParseVisibilityQualifiedFunctionStatement(MethodVisibility.Public);
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
            if (Advance().Value != "as") throw new Exception(_tokens, _current, "Expected 'as' keyword", "FS2003");
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
            string type = EvaluateExpression(ParseExpression()).ToString();
            Type typeT = null;
            if (type == "FeiSharp.System.Data.double")
            {
                typeT = typeof(double);
            }
            else if (type == "FeiSharp.System.Data.string")
            {
                typeT = typeof(string);
            }
            else if (type == "FeiSharp.System.Data.boolean")
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
                _variables
                .Add(name, Console.ReadKey().KeyChar.ToString());
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
        private void ParseImportStatement()
        {
            string assembly = EvaluateExpression(ParseExpression()).ToString();
            if (assembly == "FeiSharp.IO")
            {
                isfileassembly = true;
            }
            else if (assembly == "FeiSharp.Text.Json")
            {
                isjsonassembly = true;
            }
            else if (assembly == "FeiSharp.Net")
            {
                isnetassembly = true;
            }
            else if (assembly == "FeiSharp.Text" || assembly == "FeiSharp" || assembly == "FeiSharp.DataCollection" || assembly == "FeiSharp.Objects")
            {
            }
            else if (assembly == "FeiSharp.System")
            {
                isSystemAssembly = true;
            }
            else
            {
                throw new Exception(_tokens, _current, "import: invalid args[0]: not a namespace", "FS3002");
            }
            Advance();
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
                if (!MatchKeyword("as")) throw new Exception(_tokens, _current, "Expected 'as' keyword", "FS2003");
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
            if(_classInfos == null)
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
                throw new Exception(_tokens, _current, "Variable, function or class not defined: " + name, "FS3001");
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

            // 保存当前变量状态
            var savedVariables = new Dictionary<string, object>(_variables);
            var savedResults = new Dictionary<string, object>(_results);

            // 临时添加实例变量到作用域
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
                                Advance(); // 消耗 ')'
                            }

                            // 调用方法并获取返回值
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
                            // 变量已经添加到 _variables 中了，不需要额外处理
                        }
                    }

                    // 将实例变量中可能被修改的值同步回 classInfo
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
                // ============================================
                // 恢复原始变量（关键部分）
                // ============================================

                // 1. 恢复所有原始变量
                _variables.Clear();
                foreach (var item in savedVariables)
                {
                    _variables[item.Key] = item.Value;
                }

                // 2. 恢复结果字典
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
            _results[funcName] = EvaluateExpression(ParseExpression());
            _variables[$"{funcName}:return"] = _results[funcName];
        }
        private void ParseThrowStatement()
        {
            string msg = EvaluateExpression(ParseExpression()).ToString();
            throw new Exception(_tokens, _current, msg, "FS4001", "UserException");
        }
        private void ParseVisibilityQualifiedFunctionStatement(MethodVisibility visibility)
        {
            if (!MatchKeyword(TokenKeywords.func))
            {
                throw new Exception(_tokens, _current, $"'{visibility.ToString().ToLowerInvariant()}' can only be used before function declarations.", "FS2003");
            }
            if (!_isParsingClassBody && visibility == MethodVisibility.Public)
            {
                throw new Exception(_tokens, _current, "'public' can only be used for class members.", "FS3001");
            }
            ParseFunctionStatement(visibility);
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
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            if (!MatchToken(TokenTypes.Identifier, out string describeVar))
            {
                throw new Exception(_tokens, _current, "Expected error description variable name", "FS2003");
            }
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            if (!MatchToken(TokenTypes.Identifier, out string numberVar))
            {
                throw new Exception(_tokens, _current, "Expected error number variable name", "FS2003");
            }
            if (!MatchPunctuation(")")) throw new Exception(_tokens, _current, "Expected ')'", "FS2003");
            List<Token> catchTokens = ParseScopedTokens("{", "}", "catch");
            try
            {
                ExecuteNestedBlock(tryTokens, funcName, _variables, CloneClassInfos(_classInfos));
            }
            catch (Exception ex)
            {
                _variables[typeVar] = ex.ErrorType;
                _variables[describeVar] = ex.Description;
                _variables[numberVar] = ex.Number;
                ExecuteNestedBlock(catchTokens, funcName, _variables, CloneClassInfos(_classInfos));
            }
        }
        private void ExecuteStatementsInRange(int startPos, int endPos)
        {
            int savedPosition = _current;

            try
            {
                _current = startPos;

                while (_current < endPos && !IsAtEnd())
                {
                    // 跳过分号
                    if (Peek().Type == TokenTypes.Punctuation && Peek().Value == ";")
                    {
                        Advance();
                        continue;
                    }

                    // 所有关键字都在这
                    if (MatchKeyword(TokenKeywords._var))
                    {
                        ParseVariableDeclaration();
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
                    else if (MatchKeyword(TokenKeywords.export))
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
                    else if (MatchKeyword(TokenKeywords._private))
                    {
                        ParseVisibilityQualifiedFunctionStatement(MethodVisibility.Private);
                    }
                    else if (MatchKeyword(TokenKeywords._public))
                    {
                        ParseVisibilityQualifiedFunctionStatement(MethodVisibility.Public);
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
                        ParseTryCatchStatement("");
                    }
                    else if (MatchKeyword(TokenKeywords._return))
                    {
                        ParseReturnStatement("");
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
                    }
                    else if (_classInfos.ContainsKey(Peek().Value))
                    {
                        string className = Peek().Value;
                        Advance();
                        Runclass(className);
                    }
                    else
                    {
                        // 表达式语句
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

                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex);

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
        private void ParseFunctionStatement(MethodVisibility? explicitVisibility = null)
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
            functionInfo = new(name, parameters, tokens, visibility, _isParsingClassBody, _classDeclarationName);
            _functions[name] = functionInfo;
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
                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex);
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
                ExecuteStatementsInRange(initializerStart, initializerEnd);
            }

            int loopCount = 0;
            while (EvaluateForCondition(conditionStart, conditionEnd))
            {
                loopCount++;
                if (loopCount % CancelCheckInterval == 0)
                    CheckCancellation();

                ExecuteStatementsInRange(bodyStartIndex, bodyEndIndex);

                if (iteratorStart < iteratorEnd)
                {
                    ExecuteStatementsInRange(iteratorStart, iteratorEnd);
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
            List<Token> tokens = ParseScopedTokens("{", "}", "if", "ibegin", "iend");
            if (a)
            {
                _variables = Run(tokens, _variables);
            }
        }
        private void ParseHelperStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string a = EvaluateExpression(ParseExpression()).ToString();
            if (a.Equals("syntax", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Syntax:\r\n1.keyword+(args);\r\nInvoke keyword with args.\r\nWarning: If keyword hasn't args,\r\nuse keyword+;\r\n2.Define var.\r\n(1)define:\r\ninit(varname,Type); Or var varname = value;\r\n(2)assignment:\r\nset(varname,value);\r\n3.Keywords Table.\r\n________________________________________________\r\n|keyword   |  args   |  do somwthings           |\r\n|paint        text     print the text           |\r\n|watchstart  varname   start stopwatch.         |\r\n|watchend     null     stop stopwatch           |\r\n|init    varname,Type  init var.                |\r\n|set    varname,value  set var.                 |\r\n|...          ....     ............             |\r\n|_______________________________________________|");
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
                if (!MatchKeyword("as")) throw new Exception(_tokens, _current, "Expected 'as' keyword", "FS2003");
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
            parser._functions = _functions;
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
            return;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._functions = new(_functions);
            parser._classInfos = CloneClassInfos(_classInfos);
            parser._results = _results;
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
            return parser._variables;
        }
        internal KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, int op = 0)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._functions = new(_functions);
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
            KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> result = new(parser._variables, parser._functions);
            return result;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, string funcName, Dictionary<string, ClassInfo> a)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._classInfos = CloneClassInfos(a);
            parser._functions = new(_functions);
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
            return parser._variables;
        }
        private void ExecuteNestedBlock(IEnumerable<Token> tokens, string funcName, Dictionary<string, object> vars, Dictionary<string, ClassInfo> classInfos)
        {
            Parser parser = new(new List<Token>(tokens));
            parser.OutputEvent = this.OutputEvent;
            parser._variables = vars;
            parser._classInfos = CloneClassInfos(classInfos);
            parser._functions = new(_functions, StringComparer.OrdinalIgnoreCase);
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
            object value = ParseAssignmentValue(out string? assignedClassName);
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
                if (!context._Vars.ContainsKey(memberName))
                {
                    throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                }
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
                    object? memberValue = instance.GetVariable(memberName);
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
                    if (!classInfo._Vars.ContainsKey(memberName))
                    {
                        throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                    }
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
                case "instance":
                    if (target.Instance == null || target.MemberName == null)
                    {
                        throw new Exception(_tokens, _current, "Invalid instance assignment target", "FS3001");
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
            _classInfos[className] = new ClassInfo(classInfo._FunctionInfo, vars, classInfo.Name, classInfo.ConstructorInfo, classInfo.BaseClassName);
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
                    ClassInfo classInfo = new(item.Value._FunctionInfo, vars, item.Key, item.Value.ConstructorInfo, item.Value.BaseClassName);
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
        [RequiresDynamicCode("FeiSharp creates CLR instances from runtime type names.")]
        [RequiresUnreferencedCode("FeiSharp creates CLR instances from runtime type names.")]
        private object InitValue(string type)
        {
            Type t = TypeLoader.LoadType("System." + type);
            return Activator.CreateInstance(t);
        }
        private Expr GetVar()
        {
            if (MatchToken(TokenTypes.Identifier))
            {
                return new VarExpr(Previous().Value);
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
        private void ParseVariableDeclaration()
        {
            if (!MatchToken(TokenTypes.Identifier, out string varName))
            {
                throw new Exception(_tokens, _current, "Expected variable name", "FS2003");
            }
            if (!MatchToken(TokenTypes.Operator, out string op) || op != "=")
            {
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
            var instanceScope = new Dictionary<string, object>(classInfo._Vars, StringComparer.OrdinalIgnoreCase);
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
                Functions = classInfo._FunctionInfo
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
                if (Peek().Value == "as")
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
                    Console.WriteLine("No 'as' keyword.");
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
            return expr;
        }
        private bool IsOperator(string token)
        {
            return token == "+" || token == "-" || token == "*" || token == "/" ||
                   token == "^" || token == ">" || token == "<" || token == "=" ||
                   token == "!" || token == "|" || token == "&" || token == "$";
        }

        private int GetPrecedence(string op)
        {
            return op switch
            {
                "|" => 1,
                "&" => 2,
                "=" or "!" => 3,
                ">" or "<" => 3,
                "^" => 4,
                "+" or "-" => 5,
                "*" or "/" => 6,
                "$" => 7,
                _ => 0
            };
        }
        private Expr ParsePrimary()
        {
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
            else if (MatchKeyword(TokenKeywords._base))
            {
                return ParseContextKeywordExpression(true);
            }
            else if (MatchToken(TokenTypes.Identifier))
            {
                varName = Previous().Value;
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
                        object memberValue = instance.GetVariable(memberName);
                        return ParseRuntimeAccess(memberValue);
                    }
                    return ParseRuntimeAccess(value1);
                }
                if (_classInfos.ContainsKey(varName))
                {
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

                            RunFunction(memberName, functionInfo, classInfo);
                            return new ValueExpr(_variables[$"{memberName}:return"]);
                        }

                        if (!classInfo._Vars.TryGetValue(memberName, out object? memberValue))
                        {
                            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
                        }
                        return ParseRuntimeAccess(memberValue);
                    }

                    var a = Runclass(varName);
                    if (a.Value)
                    {
                        return new ValueExpr(_variables[$"{a.Key}:return"]);
                    }
                    else
                    {
                        return new ValueExpr(_variables[a.Key]);
                    }
                }
                if (_functions.ContainsKey(varName))
                {
                    RunFunction(varName);
                    return ParseRuntimeAccess(_variables[$"{varName}:return"]);
                }
                else
                {
                    var a = Runclass(varName);
                    if (a.Value)
                    {
                        return ParseRuntimeAccess(_variables[$"{a.Key}:return"]);
                    }
                    else
                    {
                        return ParseRuntimeAccess(_variables[a.Key]);
                    }
                }
                throw new Exception(_tokens, _current, $"Undefined variable: {varName}", "FS3001");
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
        private Expr ParseContextKeywordExpression(bool useBase)
        {
            ClassInfo context = GetAccessibleClassContext(useBase);
            if (!MatchPunctuation("."))
            {
                return new ValueExpr(new ClassInstance
                {
                    ClassName = context.Name,
                    Variables = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase),
                    Functions = context._FunctionInfo
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
            if (useBase && functionInfo.Visibility != MethodVisibility.Public)
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
                || string.Equals(_currentExecutionClassName, functionInfo.DeclaringClassName, StringComparison.OrdinalIgnoreCase);
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
        private bool MatchMemberName(out string value)
        {
            if (MatchToken(TokenTypes.Identifier, out value) || MatchToken(TokenTypes.Keyword, out value))
            {
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
                item => new ClassInfo(item.Value._FunctionInfo, item.Value._Vars, item.Value.Name, item.Value.ConstructorInfo, item.Value.BaseClassName),
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
            FunctionInfo? constructorInfo = null;
            if (!string.IsNullOrWhiteSpace(baseClassName))
            {
                if (!_classInfos.TryGetValue(baseClassName, out var baseClassInfo))
                {
                    throw new Exception(_tokens, _current, $"Base class not defined: {baseClassName}", "FS3001");
                }
                baseVars = new(baseClassInfo._Vars, StringComparer.OrdinalIgnoreCase);
                baseFunctions = new(
                    baseClassInfo._FunctionInfo
                        .Where(kvp => kvp.Value.Visibility == MethodVisibility.Public),
                    StringComparer.OrdinalIgnoreCase);
                constructorInfo = baseClassInfo.ConstructorInfo;
            }
            var outerFunctionNames = new HashSet<string>(_functions.Keys, StringComparer.OrdinalIgnoreCase);
            Parser parser = new(RemoveConstructorTokens(className, classBodyTokens));
            parser.OutputEvent = this.OutputEvent;
            parser.ShouldCancel = this.ShouldCancel;
            parser._isParsingClassBody = true;
            parser._classDeclarationName = className;
            parser._variables = new(baseVars, StringComparer.OrdinalIgnoreCase);
            parser._functions = new(_functions, StringComparer.OrdinalIgnoreCase);
            parser._classInfos = CloneClassInfos(_classInfos);
            parser.ParseStatements();
            var memberFunctions = new Dictionary<string, FunctionInfo>(baseFunctions, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parser._functions)
            {
                if (!outerFunctionNames.Contains(kvp.Key))
                {
                    memberFunctions[kvp.Key] = kvp.Value;
                }
            }
            constructorInfo = ParseConstructorInfo(className, classBodyTokens) ?? constructorInfo;
            return new ClassInfo(memberFunctions, parser._variables, className, constructorInfo, baseClassName);
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
                            return new FunctionInfo($"{className}:ctor", parameters, bodyTokens, MethodVisibility.Public, true, className);
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
            ClassInfo updated = new(classInfo._FunctionInfo, ctorScope, className, classInfo.ConstructorInfo, classInfo.BaseClassName);
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
            var scope = targetVariables ?? _variables;
            string? previousExecutionClassName = _currentExecutionClassName;
            string? previousFunctionName = _currentFunctionName;
            for (int i = 0; i < functionInfo.Parameter.Count; i++)
            {
                try
                {
                    scope[functionInfo.Parameter[i]] = actualParameters[i];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Exception(_tokens, _current, "Parameters is not correct.", "FS3003");
                }
            }
            var functionClasses = CloneClassInfos(_classInfos);
            if (classContext != null)
            {
                functionClasses[classContext.Name] = new ClassInfo(classContext._FunctionInfo, scope, classContext.Name, classContext.ConstructorInfo, classContext.BaseClassName);
            }
            var previousFunctions = _functions;
            _currentExecutionClassName = classContext?.Name ?? previousExecutionClassName;
            _currentFunctionName = funcName;
            if (classContext != null)
            {
                _functions = new(previousFunctions, StringComparer.OrdinalIgnoreCase);
                foreach (var memberFunction in classContext._FunctionInfo)
                {
                    _functions[memberFunction.Key] = memberFunction.Value;
                }
            }
            _variables = Run(functionInfo.FunctionBody, scope, funcName, functionClasses);
            if (classContext != null)
            {
                _functions = previousFunctions;
            }
            _currentExecutionClassName = previousExecutionClassName;
            _currentFunctionName = previousFunctionName;
            if (classContext != null)
            {
                _classInfos[classContext.Name] = new ClassInfo(classContext._FunctionInfo, _variables, classContext.Name, classContext.ConstructorInfo, classContext.BaseClassName);
            }
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
            string text = EvaluateExpression(stmt.Expression).ToString();
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
        private object EvaluateExpression(Expr expr)
        {
            switch (expr)
            {
                case ValueExpr numExpr:
                    return EvaluateValueExpr(numExpr);

                case BinaryExpr binExpr:
                    return EvaluateBinaryExpr(binExpr);

                case StringExpr stringExpr:
                    return EvaluateStringExpr(stringExpr);

                default:
                    throw new Exception(_tokens, _current, "Unexpected expression type", "FS2003");
            }
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
                for(int a = 0; a < str.Length; a++)
                {
                    if (str[a] == '\\')
                    {
                        if (!(str[a + 1] == '\\' || str[a + 1] == 'n' || str[a + 1] == 't' || str[a - 1] == '\\'))
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
            return new ValueExpr(currentValue);
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
                throw new Exception(_tokens, _current, "Cannot index a null value", "FS3001");
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
            catch(IndexOutOfRangeException)
            {
                throw new Exception(_tokens, _current, "Index out of range", "FS3001");
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
        private object? GetRuntimeMember(object? target, string memberName)
        {
            if (target == null)
            {
                throw new Exception(_tokens, _current, $"Cannot access member '{memberName}' on null.", "FS3001");
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
            throw new Exception(_tokens, _current, $"Undefined member: {memberName}", "FS3001");
        }
        private object? InvokeRuntimeMember(object? target, string memberName, List<object> args)
        {
            if (target == null)
            {
                throw new Exception(_tokens, _current, $"Cannot invoke member '{memberName}' on null.", "FS3001");
            }
            Type type = target.GetType();
            MethodInfo? method = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(m => CanBindArguments(m.GetParameters(), args));
            if (method == null)
            {
                throw new Exception(_tokens, _current, $"Undefined method: {memberName}", "FS3001");
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
            object left = EvaluateExpression(binExpr.Left);
            object right = EvaluateExpression(binExpr.Right);

            // 快速路径：字符串和字符操作
            if (left is string or char && right is string or char)
            {
                return EvaluateStringBinary(left, right, binExpr.Operator);
            }

            // 快速路径：布尔操作
            if (left is bool && right is bool)
            {
                return EvaluateBoolBinary((bool)left, (bool)right, binExpr.Operator);
            }

            // 类型统一和转换
            if (left.GetType() != right.GetType())
            {
                if (!TryUnifyTypes(ref left, ref right))
                {
                    throw new Exception(_tokens, _current,
                        $"ITC: Unable to accurately determine the type or the conversion attempt failed", "FS3001");
                }
            }

            // 字符串乘法特殊处理
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

            // 数值运算（优化后的快速路径）
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
                "!" => leftStr != rightStr,
                _ => throw new Exception(_tokens, _current,
                    $"Cannot using operator '{op}' to connect string_obj and string_obj.", "FS2003")
            };
        }

        private object EvaluateBoolBinary(bool left, bool right, string op)
        {
            return op switch
            {
                "&" => left && right,
                "|" => left || right,
                "!" => left != right,
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

            // 尝试将 right 转换为 left 的类型
            try
            {
                right = Convert.ChangeType(right, leftType);
                return true;
            }
            catch { }

            // 尝试将 left 转换为 right 的类型
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
                // 临时替换为新的 tokens 和变量
                _tokens = tokens;
                _current = 0;
                _variables = new Dictionary<string, object>(variables);
                _functions = new Dictionary<string, FunctionInfo>(_functions);
                _classInfos = CloneClassInfos(_classInfos);
                _results = new Dictionary<string, object>();

                // 执行语句
                ParseStatements();

                // 返回执行后的变量
                return new Dictionary<string, object>(_variables);
            }
            finally
            {
                // 合并变量更改（循环体内的变量修改需要保留）
                foreach (var kv in _variables)
                {
                    savedVariables[kv.Key] = kv.Value;
                }

                // 恢复状态
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
            // 快速转换为数值（避免 ToString()）
            double leftNum, rightNum;

            try
            {
                leftNum = Convert.ToDouble(left);
                rightNum = Convert.ToDouble(right);
            }
            catch
            {
                throw new Exception(_tokens, _current,
                    $"Cannot convert {left.GetType()} or {right.GetType()} to number for operator '{op}'", "FS2003");
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
                "=" => leftNum == rightNum,
                "&" => (int)leftNum & (int)rightNum,
                "|" => (int)leftNum | (int)rightNum,
                "!" => leftNum != rightNum,
                "$" => Math.Pow(leftNum, rightNum),
                _ => throw new Exception(_tokens, _current,
                    $"ICS: Cannot using operator '{op}' to connect numeric values.", "FS2003")
            };
        }

        private object EvaluateStringExpr(StringExpr stringExpr)
        {
            stringExpr.Value = Regex.Replace(stringExpr.Value, @"\$\(unicode:([0-9A-Fa-f]{4,5})\)",
                m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));

            return stringExpr.Value.Replace("$(newline)", "\n")
                                  .Replace("$(tab)", "    ")
                                  .Replace("\\n", "\n")
                                  .Replace("\\t", "\t")
                                  .Replace("\\\\", "\\");
        }
    }

    [RequiresDynamicCode("FeiSharp constructs CLR instances from runtime-discovered constructors.")]
    [RequiresUnreferencedCode("FeiSharp constructs CLR instances from runtime-discovered constructors.")]
    public static class SmartActivator
    {
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
