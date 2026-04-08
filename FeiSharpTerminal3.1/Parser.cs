using FeiSharp8._5RuntimeSdk;
using FeiSharpStudio.ClassInstance;
using FeiSharpStudio.UUID;
using FeiSharpTerminal3._1;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
        private List<Token> _tokens;
        private int _current;
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
            _variables.NewAdd("null", null);
            _variables.NewAdd("positiveInf", double.PositiveInfinity);
            _variables.NewAdd("negativeInf", double.NegativeInfinity);
            _variables.NewAdd("buildVersion", "8.0");
            _variables.NewAdd("zeroInt", 0);
            _variables.NewAdd("emptyStr", "");
            _variables.NewAdd("ten", 10);
            _variables.NewAdd("hundred", 100);
            _variables.NewAdd("thousand", 1000);
            _variables.NewAdd("pi", Math.PI);
            _variables.NewAdd("e", Math.E);
            _variables.NewAdd("tau", Math.Tau);
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
                    else if (MatchKeyword(TokenKeywords._while))
                    {
                        ParseWhileStatement();
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
                    else if (MatchKeyword(TokenKeywords.read))
                    {
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
                        if (_classInfos.ContainsKey(Peek().Value))
                            Runclass(Peek().Value);
                        else
                        {
                            Advance();
                        }
                    }
                    if (_isQuit)
                    {
                        Environment.Exit(_n);
                    }
                    _variables.NewAdd("it", _variables.Where(kvp => kvp.Key != "it")
    .LastOrDefault()
    .Value);
                } while (!IsAtEnd());
            }
            catch (OperationCanceledException)
            {
                // 用户取消执行，干净地退出
                Console.WriteLine(new OutputEventArgs("\n[yellow]Execution cancelled by user[/]"));
                return;
            }
            catch (FeiSharpTerminal3._1.ExceptionThrow.Exception e){
                Console.Write("");
                return;
            }
        }
        bool _isQuit = false;
        int _n = 0;

        private List<string> _builtInTypesList = [
            "integer", "string", "bool", "extendObject", "object", "char", "objectReturned", "symbol", "double", "float", "error"
        ];
        private void ParseAppQuit()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int a = int.Parse(EvaluateExpression(ParseExpression()).ToString());
            _isQuit = true;
            _n = a;
            Advance();
            Advance();
        }
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
            typeof(Console).Assembly,           // System.Console 程序集
            typeof(string).Assembly,             // mscorlib/CoreLib
            Assembly.GetExecutingAssembly(),      // 当前程序集
            Assembly.GetCallingAssembly()         // 调用程序集
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
                if(property == null)
                {
                    field = type.GetField(functionName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if(field == null)
                        throw new Exception(_tokens, _current, "Method, property or field is not correct", "FS2003");
                }
            }
            var k = method == null ? (property == null ? (args.Count == 1 ?  ClassInvokeField(field, args[0]) : field.GetValue(null)) : (args.Count == 1 ? ClassInvokeProperty(property, args[0]) : property.GetValue(null))) : method.Invoke(null, args.ToArray());
            Advance();
            return k;
        }
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
            MethodInfo method = type.GetMethod(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance  | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy, null, paramTypes, null);
            PropertyInfo property = null;
            FieldInfo field = null;

            if (method == null)
            {
                property = type.GetProperty(functionName, BindingFlags.Public |  BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                if (property == null)
                {
                    field = type.GetField(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                    if (field == null)
                        throw new Exception(_tokens, _current, "Method, property or field is not correct", "FS2003");
                }
            }
            var k = method == null ? (property == null ? (args.Count == 1 ? ObjectInvokeField(field, args[0],c) : field.GetValue(c)) : (args.Count == 1 ? ObjectInvokeProperty(property, args[0],c) : property.GetValue(c))) : method.Invoke(c, args.ToArray());
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

        private KeyValuePair<string, bool> Runclass(string name)
        {
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
            try
            {
                foreach (var item in classInfo._Vars)
                {
                    _variables.NewAdd(item.Key, item.Value);
                }
            }
            catch
            {
                goto Parse;
            }
        Parse:
            if (classInfo != null)
            {
                if (Peek().Value == ".")
                {
                    Advance();
                    if (classInfo._FunctionInfo.ContainsKey(Peek().Value))
                    {
                        _functions.Add(Peek().Value, classInfo._FunctionInfo[Peek().Value]);
                        funcorvarname = Peek().Value;
                        isFunc = true;
                        RunFunction(Peek().Value);
                        Advance();
                    }
                    else if (classInfo._Vars.ContainsKey(Peek().Value))
                    {
                        funcorvarname = Peek().Value;
                        isFunc = false;
                    }
                }
            }
            return new(funcorvarname, isFunc);
        }
        private void ParseClassStatement()
        {
            string className = Peek().Value;
            Advance();
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            int doub = 0;
            bool doubb = false;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "cend")
                {
                    if (doub != 0)
                    {
                        doub--;
                        continue;
                    }
                    indexC = i;
                    break;
                }
                if (_tokens[i].Value == "cbegin")
                {
                    if (doubb)
                    {
                        doub++;
                        continue;
                    }
                    doubb = true;
                    continue;
                }
                tokens.Add(_tokens[i]);
                Advance();

            }
            Advance();
            ClassInfo classInfo;
            Parser parser = new(tokens);
            parser.OutputEvent += (s, e) => Console.WriteLine();
            var @return = parser.Run(tokens, new(), 0);
            classInfo = new(@return.Value, @return.Key, className);
            _classInfos.Add(className, classInfo);
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
                    // 设置网络请求超时
                    cts.CancelAfter(TimeSpan.FromSeconds(30));

                    try
                    {
                        HttpClient client = new HttpClient();
                        var task = client.GetAsync(url, cts.Token);

                        // 等待任务完成或取消
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
            _results.Add(funcName, EvaluateExpression(ParseExpression()));
            _variables.NewAdd($"{funcName}:return", _results[funcName]);
        }

        private void ParseThrowStatement()
        {
            string msg = EvaluateExpression(ParseExpression()).ToString();
            Console.WriteLine("FeiSharp.Exception went throw at throw ststement, " + msg + "");
            Console.Write("Do you want to continue and skip it?(y/n)");
            var a = Console.ReadKey();
            Console.WriteLine();
            if (a.Key == ConsoleKey.N)
            {
                throw new Exception(_tokens, _current, "This application is stop......", "FS2003");
            }
            Advance();
        }
        private void ParseDowhileStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            Advance();

            List<Token> tokens = new List<Token>();
            int indexC = 0;
            int doub = 0;
            bool doubb = false;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "dend")
                {
                    if (doub != 0)
                    {
                        doub--;
                        continue;
                    }
                    indexC = i;
                    break;
                }
                if (_tokens[i].Value == "dbegin")
                {
                    if (doubb)
                    {
                        doub++;
                        continue;
                    }
                    doubb = true;
                    continue;
                }
                tokens.Add(_tokens[i]);
                Advance();
            }

            int loopCount = 0;
            do
            {
                // 检查无限循环
                loopCount++;
                if (loopCount % 1000 == 0)
                {
                    CheckCancellation();
                }

                _variables = Run(tokens, _variables);
                _current = current;
                a = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
            } while (a);
            _current = indexC;
        }
        private void RunFunction(string funcName)
        {
            FunctionInfo functionInfo = _functions[funcName];
            List<object> actualParameters = new();
            Advance();
            while (Peek().Value != ")" && Peek().Value != ";")
            {
                if (Peek().Value == "," || Peek().Value == "(")
                {
                    Advance();
                    continue;
                }
                else
                {
                    actualParameters.Add(EvaluateExpression(ParseExpression()));
                    Advance();
                }
            }
            for (int i = 0; i < functionInfo.Parameter.Count; i++)
            {
                try
                {
                    _variables.NewAdd(functionInfo.Parameter[i], actualParameters[i]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Exception(_tokens, _current, "Parameters is not correct.", "FS3003");
                }
            }
            _variables = Run(functionInfo.FunctionBody, _variables, funcName, _classInfos);
        }
        private void RunFunction(string funcName, List<Token> tokens, List<string> args)
        {
            FunctionInfo functionInfo = new(funcName, args, tokens);
            List<object> actualParameters = new();
            Advance();
            while (Peek().Value != ")" && Peek().Value != ";")
            {
                if (Peek().Value == "," || Peek().Value == "(")
                {
                    Advance();
                    continue;
                }
                else
                {
                    actualParameters.Add(EvaluateExpression(ParseExpression()));
                    Advance();
                }
            }
            for (int i = 0; i < functionInfo.Parameter.Count; i++)
            {
                try
                {
                    _variables.NewAdd(functionInfo.Parameter[i], actualParameters[i]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Exception(_tokens, _current, "Parameters is not correct.", "FS3003");
                }
            }
            _variables = Run(functionInfo.FunctionBody, _variables, funcName, _classInfos);
        }
        private bool MatchFunction(string funcName)
        {
            return _functions.ContainsKey(funcName);
        }
        private void ParseFunctionStatement()
        {
            FunctionInfo functionInfo;
            string name = "";
            name = Peek().Value;
            List<string> parameters = [];
            Advance();
            while (Peek().Value != ")")
            {
                if (Peek().Value == "," || Peek().Value == "(")
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
            List<Token> tokens = ParseTokens();
            functionInfo = new(name, parameters, tokens);
            _functions.Add(name, functionInfo);
            Advance();
            Advance();
        }
        private List<Token> ParseTokens()
        {
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            int doub = 0;
            bool doubb = false;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "fend")
                {
                    if (doub != 0)
                    {
                        doub--;
                        continue;
                    }
                    indexC = i;
                    break;
                }
                if (_tokens[i].Value == "fbegin")
                {
                    if (doubb)
                    {
                        doub++;
                        continue;
                    }
                    doubb = true;
                    continue;
                }
                tokens.Add(_tokens[i]);
                Advance();
            }
            return tokens;
        }
        private void ParseWhileStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            Advance();
            Advance();

            List<Token> tokens = new List<Token>();
            int indexC = 0;
            int doub = 0;
            bool doubb = false;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "wend")
                {
                    if (doub != 0)
                    {
                        doub--;
                        continue;
                    }
                    indexC = i;
                    break;
                }
                if (_tokens[i].Value == "wbegin")
                {
                    if (doubb)
                    {
                        doub++;
                        continue;
                    }
                    doubb = true;
                    continue;
                }
                tokens.Add(_tokens[i]);
                Advance();
            }
            Advance();

            int loopCount = 0;
            while (a)
            {
                // 检查无限循环
                loopCount++;
                if (loopCount % 1000 == 0)
                {
                    CheckCancellation();
                }

                _variables = Run(tokens, _variables);
                _current = current;
                a = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
            }
            _current = indexC;
        }
        private void ParseIfStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            Advance();
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            int doub = 0;
            bool doubb = false;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "iend")
                {
                    if (doub != 0)
                    {
                        doub--;
                        continue;
                    }
                    indexC = i;
                    break;
                }
                if (_tokens[i].Value == "ibegin")
                {
                    if (doubb)
                    {
                        doub++;
                        continue;
                    }
                    doubb = true;
                    continue;
                }
                tokens.Add(_tokens[i]);
                Advance();

            }
            Advance();
            if (a)
            {
                _variables = Run(tokens, _variables);
                _current = current;
                a = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
            }
            _current = indexC;
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
            //Console Edition
            Console.WriteLine("Enter any key to continue......");
            Console.ReadKey();
            Console.WriteLine();
        }
        private void ParseStartStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);
            Process.Start(new ProcessStartInfo() { FileName = a, CreateNoWindow = false, UseShellExecute = false });
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
            return parser._variables;
        }
        internal KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, int op = 0)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
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
            KeyValuePair<Dictionary<string, object>, Dictionary<string, FunctionInfo>> result = new(parser._variables, parser._functions);
            return result;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, string funcName, Dictionary<string, ClassInfo> a)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            parser._classInfos = a;
            parser.ShouldCancel = this.ShouldCancel;
            try
            {
                parser.ParseStatements(funcName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            return parser._variables;
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
        private void ParseSetStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
            string name = EvaluateExpression(ParseExpression()).ToString();
            if (_variables.Count == 14 && _variables.ContainsKey(name))
            {
                throw new Exception(_tokens, _current, "Cannot set or cover a const", "FS3001");
            }
            if (!MatchPunctuation(",")) throw new Exception(_tokens, _current, "Expected ','", "FS2003");
            object name1 = EvaluateExpression(ParseExpression());
            Advance();
            Advance();
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
                    ClassInfo classInfo = new(item.Value._FunctionInfo, vars, item.Key);
                    a[item.Key] = classInfo;
                }
            }
            _classInfos = a;
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
                if (_variables.Count == 14 && _variables.ContainsKey(varName))
                {
                    throw new Exception(_tokens, _current, "Cannot set or cover a const", "FS3001");
                }
                _variables[varName] = _variables["null"];
                Advance();
                return;
            }
            else
            {
                if (_variables.Count == 14 && _variables.ContainsKey(varName))
                {
                    throw new Exception(_tokens, _current, "Cannot set or cover a const", "FS3001");
                }
                Expr expr = ParseExpression();
                Advance();

                object value = EvaluateExpression(expr);
                _variables[varName] = value;
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

        private Expr ParseExpression()
        {
            Expr expr = ParsePrimary();
            if(expr is BinaryExpr expr2 && expr2.Operator == "HAVE")
            {
                return expr;
            }
            else if (expr is BinaryExpr expr3 && expr3.Operator == "NEW")
            {
                return expr;
            }
            else if (expr is BinaryExpr expr4 && expr4.Operator == "OBJ")
            {
                return expr;
            }
            while (MatchOperator("+", "-", "*", "/", "|", "^", "<", ">", "=", "!", "|", "&", "$"))
            {
                string op = Previous().Value;
                Expr right = ParsePrimary();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr ParsePrimary()
        {
            string varName = "";
            if (MatchToken(TokenTypes.Number))
            {
                return new ValueExpr(double.Parse(Previous().Value));
            }
            else if (MatchToken(TokenTypes.String))
            {
                return new ValueExpr(Previous().Value);
            }
            else if (MatchToken(TokenTypes.Character))
            {
                return new ValueExpr(Previous().Value[0]);
            }
            else if (MatchToken(TokenTypes.Identifier))
            {
                varName = Previous().Value;
                if(Previous().Type == TokenTypes.Identifier && Previous().Value == "classinvoke")
                {
                    return new BinaryExpr(new ValueExpr(ParseClassInvoke()), "HAVE", null);
                }
                if (Previous().Type == TokenTypes.Identifier && Previous().Value == "objectinvoke")
                {
                    return new BinaryExpr(new ValueExpr(ParseObjectInvoke()), "OBJ", null);
                }
                if (Previous().Type == TokenTypes.Identifier && Previous().Value == "new")
                {
                    string className = Advance().Value;
                    if (!MatchKeyword("in")) throw new Exception(_tokens, _current, "Expected 'in' keyword", "FS2003");
                    string space = Peek().Value;
                    if (!MatchPunctuation("(")) throw new Exception(_tokens, _current, "Expected '('", "FS2003");
                    List<object> args = new List<object>();
                    int index = 0;
                    while (Peek().Value != ")")
                    {
                        Advance();
                        if (Peek().Value != "," && Peek().Value != ")")
                        {
                            args.Add(Peek().Value);
                        }
                    }
                    Type[] paramTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? new Type[0];
                    Type? type = TypeLoader.LoadType(space + "." + className);
                    if (type == null)
                    {
                        var assemblies = new[]
                {
            typeof(Console).Assembly,           // System.Console 程序集
            typeof(string).Assembly,             // mscorlib/CoreLib
            Assembly.GetExecutingAssembly(),      // 当前程序集
            Assembly.GetCallingAssembly()         // 调用程序集
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
                    var k = SmartActivator.CreateInstance(type, args.ToArray());
                    return new BinaryExpr(new ValueExpr(k), "NEW", new ValueExpr(k.GetType()));
                }
                if (_classInfos.ContainsKey(varName))
                {
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
                else
                {
                    
                    if (_variables.TryGetValue(varName, out object value))
                    {
                        return new ValueExpr(value);
                    }
                    else if (_functions.ContainsKey(varName))
                    {
                        RunFunction(varName);
                        return new ValueExpr(_variables[$"{varName}:return"]);
                    }
                    else
                    {
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
                    throw new Exception(_tokens, _current, $"Undefined variable: {varName}", "FS3001");
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
                varName = Previous().Value;
                if (_variables.TryGetValue(varName, out object value))
                {
                    return new ValueExpr(value);
                }

                throw new Exception(_tokens, _current, $"Undefined variable: {varName}", "FS3001");
            }
            else if (MatchKeyword("false"))
            {
                varName = Previous().Value;
                if (_variables.TryGetValue(varName, out object value))
                {
                    return new ValueExpr(value);
                }
                throw new Exception(_tokens, _current, $"Undefined variable: {varName}", "FS3001");
            }
            else
            {
                varName = _tokens[_current - 3].Value;
                if (Previous().Type == TokenTypes.Keyword && Previous().Value == "true")
                {
                    _current--;
                    return new ValueExpr(true);
                }
                else if (Previous().Type == TokenTypes.Keyword && Previous().Value == "false")
                {
                    _current--;
                    return new ValueExpr(false);
                }
                else if (MatchPreviousToken(TokenTypes.Number))
                {
                    return new ValueExpr(double.Parse(Previous().Value));
                }
                else if (MatchPreviousToken(TokenTypes.String))
                {
                    return new StringExpr(Previous().Value);
                }
                else if (MatchPreviousToken(TokenTypes.Character))
                {
                    return new ValueExpr(Previous().Value[0]);
                }
                else if (MatchPreviousToken(TokenTypes.Identifier))
                {
                    string varName2 = Previous().Value;
                    if (_variables.TryGetValue(varName2, out object value))
                    {
                        return new ValueExpr(value);
                    }
                    else if (_functions.ContainsKey(varName2))
                    {
                        RunFunction(varName2);
                        return new ValueExpr(_variables[$"{varName2}:return"]);
                    }
                    else
                    {
                        var a = Runclass(varName2);
                        if (a.Value)
                        {
                            return new ValueExpr(_variables[$"{a.Key}:return"]);
                        }
                        else
                        {
                            return new ValueExpr(_variables[a.Key]);
                        }
                    }
                    throw new Exception(_tokens, _current, $"Undefined variable: {varName2}", "FS3001");
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
            }
            throw new Exception(_tokens, _current, "Unvalid token: " + Peek().Value, "FS2003");
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
        private bool MatchPunctuation(string punctuation)
        {
            if (Check(TokenTypes.Punctuation) && Peek().Value == punctuation)
            {
                Advance();
                return true;
            }
            else
            {
                Advance();
                if (Check(TokenTypes.Punctuation) && Peek().Value == punctuation)
                {
                    return true;
                }
            }
            return false;
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
            return _current >= _tokens.Count;
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
            if(expr is BinaryExpr expr2 && expr2.Operator == "HAVE")
            {
                return (expr2.Left as ValueExpr).Value;
            }
            else if (expr is BinaryExpr expr3 && expr3.Operator == "NEW")
            {
                return (expr3.Left as ValueExpr).Value;
            }
            else if (expr is BinaryExpr expr4 && expr4.Operator == "OBJ")
            {
                return (expr4.Left as ValueExpr).Value;
            }
            switch (expr)
            {
                case ValueExpr numExpr:
                    if (double.TryParse(numExpr.Value.ToString(), out double a) && (numExpr.Value.GetType().Name == "Double" || numExpr.Value.GetType().Name == "Int32"))
                    {
                        return double.Parse(numExpr.Value.ToString());
                    }
                    else
                    {
                        if (numExpr.Value is string)
                        {
                            numExpr.Value = Regex.Replace(numExpr.Value.ToString(), @"\$\(unicode:([0-9A-Fa-f]{4,5})\)", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16))); ;
                            return numExpr.Value.ToString().Replace("$(newline)", "\n").Replace("$(tab)", "    ").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
                        }
                        else if(numExpr.Value is char)
                        {
                            return Regex.Replace(numExpr.Value.ToString(), @"\$\(unicode:([0-9A-Fa-f]{4,5})\)", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16))).Replace("\\\\", "\\").Replace("\\t", "\t").Replace("\\n", "\n");
                        }
                        else
                        {
                            return bool.Parse(numExpr.Value.ToString());
                        }
                    }

                case BinaryExpr binExpr:
                    object left = EvaluateExpression(binExpr.Left);
                    object right = EvaluateExpression(binExpr.Right);
                    if (left.GetType() != right.GetType())
                    {
                        object oldRight = right;
                        try
                        {
                            right = Convert.ChangeType(right, left.GetType());
                        }
                        catch
                        {
                            right = null;
                        }
                        if (right == null)
                        {
                            try
                            {
                                left = Convert.ChangeType(left, oldRight.GetType());
                            }
                            catch
                            {
                                left = null;
                            }
                            if (left == null)
                                throw new Exception(_tokens, _current, "ITC: Unable to accurately determine the type or the conversion attempt failed", "FS3001");
                            else
                                right = oldRight;
                        }
                        if(binExpr.Operator == "*")
                        {
                            if (left is string or char || right is double)
                            {
                                return string.Concat(Enumerable.Repeat(left.ToString(), (int)right));
                               
                            }
                            else if (left is double || right is string or char)
                            {
                                return string.Concat(Enumerable.Repeat(right.ToString(), (int)left));
                            }
                        }
                    }
                    if (left is string or char && right is string or char)
                    {
                        return binExpr.Operator switch
                        {
                            "+" => left.ToString() + right.ToString(),
                            "-" => Regex.Replace((string)left, Regex.Escape((string)right), ""),
                            "/" => (left.ToString()).Split(right.ToString()).Length - 1,
                            "=" => left.ToString() == right.ToString(),
                            "!" => left.ToString() != right.ToString(),
                            _ => throw new Exception(_tokens, _current, $"Cannot using operator '{binExpr.Operator}' to connect {left.GetType().ToString().ToLower()}_obj and {right.GetType().ToString().ToLower()}_obj.", "FS2003")
                        };
                    }
                    else if (right is bool && left is bool)
                    {
                        return binExpr.Operator switch
                        {
                            "&" => (bool)left && (bool)right,
                            "|" => (bool)left || (bool)right,
                            "!" => (bool)left != (bool)right,
                            "^" => !(bool)left && !(bool)right,
                            "%" => !(bool)left || !(bool)right,
                            _ => throw new Exception(_tokens, _current, $"Cannot using operator '{binExpr.Operator}' to connect {left.GetType().ToString().ToLower()}_obj and {right.GetType().ToString().ToLower()}_obj.", "FS2003")
                        };
                    }
                    if (left.GetType() != right.GetType())
                    {
                        throw new Exception(_tokens, _current, $"Cannot using operator '{binExpr.Operator}' to connect {left.GetType().ToString().ToLower()}_obj and {right.GetType().ToString().ToLower()}_obj.", "FS2003");
                    }
                    left = Convert.ToDouble(left.ToString());
                    right = Convert.ToDouble(right.ToString());
                    return binExpr.Operator switch
                    {
                        "+" => (double)left + (double)right,
                        "-" => (double)left - (double)right,
                        "*" => (double)left * (double)right,
                        "/" => (double)left / (double)right,
                        "^" => (int)left ^ (int)right,
                        ">" => (double)left > (double)right,
                        "<" => (double)left < (double)right,
                        "=" => (double)left == (double)right,
                        "&" => (int)left & (int)right,
                        "|" => (int)left | (int)right,
                        "!" => (double)left != (double)right,
                        "$" => Math.Pow((double)left, (double)right),
                        _ => throw new Exception(_tokens, _current, $"Cannot using operator '{binExpr.Operator}' to connect {left.GetType().ToString().ToLower()}_obj and {right.GetType().ToString().ToLower()}_obj" +
                        $").", "FS2003")
                    };
                case StringExpr stringExpr:
                    stringExpr.Value = Regex.Replace(stringExpr.Value, @"\$\(unicode:([0-9A-Fa-f]{4,5})\)", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
                    return stringExpr.Value.Replace("$(newline)", "\n").Replace("$(tab)", "    ").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\"); ;
                default:
                    throw new Exception(_tokens, _current, "Unexpected expression type", "FS2003");
            }
            string RepeatZeros(int a)
            {
                string result = "";
                for (int i = 0; i < a; i++)
                {
                    result += "0";
                }
                return result;
            }
        }
    }

    public class ConsoleTextReader
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadConsoleOutput(
            IntPtr hConsoleOutput,
            [Out] CHAR_INFO[] lpBuffer,
            COORD dwBufferSize,
            COORD dwBufferCoord,
            ref SMALL_RECT lpReadRegion);

        const int STD_OUTPUT_HANDLE = -11;

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct CHAR_INFO
        {
            [FieldOffset(0)] public char UnicodeChar;
            [FieldOffset(0)] public byte AsciiChar;
            [FieldOffset(2)] public short Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        public static string GetConsoleText()
        {
            IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
            if (hConsole == IntPtr.Zero)
                return string.Empty;

            CONSOLE_SCREEN_BUFFER_INFO csbi;
            if (!GetConsoleScreenBufferInfo(hConsole, out csbi))
                return string.Empty;

            int width = csbi.dwSize.X;
            int height = csbi.dwSize.Y;

            CHAR_INFO[] buffer = new CHAR_INFO[width * height];
            SMALL_RECT rect = new SMALL_RECT()
            {
                Left = 0,
                Top = 0,
                Right = (short)(width - 1),
                Bottom = (short)(height - 1)
            };

            COORD size = new COORD()
            {
                X = (short)width,
                Y = (short)height
            };

            COORD pos = new COORD()
            {
                X = 0,
                Y = 0
            };

            if (!ReadConsoleOutput(hConsole, buffer, size, pos, ref rect))
                return string.Empty;

            StringBuilder sb = new StringBuilder(width * height);
            for (int i = 0; i < buffer.Length; i++)
            {
                sb.Append(buffer[i].UnicodeChar);
            }

            return sb.ToString();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleScreenBufferInfo(
            IntPtr hConsoleOutput,
            out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }
    }
public static class SmartActivator
    {
        public static object CreateInstance(Type type, object[] args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            // 获取所有公共构造函数
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            // 如果没有参数，尝试无参构造函数
            if (args == null || args.Length == 0)
            {
                var ctor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
                if (ctor != null)
                    return ctor.Invoke(null);

                // 尝试创建值类型的默认实例
                if (type.IsValueType)
                    return Activator.CreateInstance(type);

                throw new MissingMethodException($"No parameterless constructor found for type {type}");
            }

            // 1. 首先尝试精确匹配
            var exactMatch = FindExactMatchConstructor(constructors, args);
            if (exactMatch != null)
                return exactMatch.Invoke(args);

            // 2. 尝试参数数量匹配且可转换
            var convertibleMatch = FindConvertibleMatchConstructor(constructors, args);
            if (convertibleMatch != null)
            {
                var convertedArgs = ConvertArguments(convertibleMatch, args);
                return convertibleMatch.Invoke(convertedArgs);
            }

            // 3. 特殊处理已知类型（如 string）
            if (type == typeof(string))
                return HandleStringCreation(args);

            throw new MissingMethodException($"Cannot find matching constructor for type {type}");
        }

        // 精确匹配（默认行为）
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

                    // 精确类型匹配（考虑 null）
                    if (arg == null)
                    {
                        if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                            return false; // 不能将 null 赋给非可空值类型
                    }
                    else if (arg.GetType() != paramType)
                    {
                        return false;
                    }
                }
                return true;
            });
        }

        // 可转换匹配（参数数量相同，类型可转换）
        private static ConstructorInfo FindConvertibleMatchConstructor(
            ConstructorInfo[] constructors, object[] args)
        {
            var candidates = constructors
                .Where(ctor => ctor.GetParameters().Length == args.Length)
                .ToList();

            // 评分系统：找到最匹配的构造函数
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
                        // null 可以赋给引用类型或可空值类型
                        if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                        {
                            allConvertible = false;
                            break;
                        }
                        // null 匹配：较低分数
                        score += 1;
                    }
                    else
                    {
                        var argType = arg.GetType();

                        if (paramType == argType)
                        {
                            // 精确匹配：最高分
                            score += 100;
                        }
                        else if (paramType.IsAssignableFrom(argType))
                        {
                            // 直接赋值兼容：高分
                            score += 50;
                        }
                        else if (CanConvert(argType, paramType))
                        {
                            // 需要类型转换：低分
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

        // 检查是否可以转换
        private static bool CanConvert(Type fromType, Type toType)
        {
            if (fromType == toType) return true;
            if (toType.IsAssignableFrom(fromType)) return true;

            // 内置类型转换检查
            try
            {
                // 尝试使用 TypeDescriptor 或 Convert
                if (fromType == typeof(string))
                {
                    // 字符串可以转换为很多类型
                    if (toType == typeof(ReadOnlySpan<char>) ||
                        toType == typeof(Span<char>) ||
                        toType == typeof(char[]) ||
                        toType == typeof(IEnumerable<char>))
                        return true;
                }
                // 使用 Convert.ChangeType 测试
                var testValue = fromType.IsValueType ? Activator.CreateInstance(fromType) : "";
                Convert.ChangeType(testValue, toType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 转换参数以匹配构造函数
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
                    // 执行类型转换
                    convertedArgs[i] = ConvertValue(arg, paramType);
                }
            }

            return convertedArgs;
        }

        // 值转换逻辑
        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            var sourceType = value.GetType();

            if (sourceType == typeof(string) && targetType == typeof(char[]))
            {
                return ((string)value).ToCharArray();
            }

            // 尝试使用 Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                // 如果失败，尝试其他转换方式
                if (targetType.IsAssignableFrom(sourceType))
                    return value;

                throw new InvalidCastException($"Cannot convert {sourceType} to {targetType}");
            }
        }

        // 专门处理字符串创建
        private static object HandleStringCreation(object[] args)
        {
            if (args == null || args.Length == 0)
                return string.Empty;

            var arg = args[0];

            // 处理常见的字符串构造函数场景
            return arg switch
            {
                string s => s,  // 直接返回字符串

                char[] chars => new string(chars),

                // char, int 的情况：new string('a', 5)
                char c when args.Length >= 2 && args[1] is int count => new string(c, count),

                // 其他类型转换为字符串
                _ => arg?.ToString() ?? string.Empty
            };
        }
    }
}
