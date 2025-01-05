﻿using FeiSharpStudio.ClassInstance;
using FeiSharpStudio.UUID;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace FeiSharpStudio
{
    public class Parser
    {
        private Stopwatch Stopwatch { get; set; }
        private readonly List<Token> _tokens;
        private int _current;
        public Dictionary<string, object> _variables = new();
        public Dictionary<string, FunctionInfo> _functions = new();
        public event EventHandler<OutputEventArgs> OutputEvent;
        public Dictionary<string, object> _results = new();
        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
            _variables.Add("true", true);
            _variables.Add("false", false);
        }
        protected virtual void OnOutputEvent(OutputEventArgs e)
        {
            EventHandler<OutputEventArgs> handler = OutputEvent;
            handler?.Invoke(this, e);
        }
        public void ParseStatements(string funcName = "")
        {
            do
            {
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
                else if (MatchFunction(Peek().Value))
                {
                    RunFunction(Peek().Value);
                }
            } while (!IsAtEnd() && (Peek().Type == TokenTypes.Keyword || _functions.ContainsKey(Peek().Value) || _classInfos.ContainsKey(Peek().Value)));
        }
        bool isfileassembly = false;
        bool isjsonassembly = false;
        bool isnetassembly = false;
        private void ParseAnnotationStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            string context = EvaluateExpression(ParseExpression()).ToString();
            Debug.WriteLine("code annotation:" + context);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseImportStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            string assembly = EvaluateExpression(ParseExpression()).ToString();
            if (assembly == "file") { 
                isfileassembly = true;
            }
            else if(assembly == "json")
            {
                isjsonassembly = true;
            }
            else if (assembly == "net")
            {
                isnetassembly = true;
            }
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseReadStatement()
        {
            if (isfileassembly)
            {
                if (!MatchPunctuation("(")) throw new Exception("Expected '('");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                string path = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                try
                {
                    _variables.Add(varname, File.ReadAllText(path));
                }
                catch
                {
                    _variables[varname] = File.ReadAllText(path);
                }
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
            else
            {
                throw new Exception("the text \"read\" is not a context var function or readonlyclass name.(are you missing import assembly:file?)");
            }
        }
        private KeyValuePair<string,bool> Runclass(string name)
        {
            ClassInfo classInfo = _classInfos[name];
            string funcorvarname = "";
            bool isFunc = default;
            try {
                foreach (var item in classInfo._Vars)
                {
                    _variables.Add(item.Key, item.Value);
                }
            }
            catch
            {
                goto Parse;
            }
            Parse:
            if (classInfo != null) {
                if (Peek().Value == ".") {
                    Advance();
                    if (classInfo._FunctionInfo.ContainsKey(Peek().Value))
                    {
                        _functions.Add(Peek().Value, classInfo._FunctionInfo[Peek().Value]);
                        funcorvarname = Peek().Value;
                        isFunc = true;
                        RunFunction(Peek().Value);
                    }
                    else if (classInfo._Vars.ContainsKey(Peek().Value))
                    {
                        funcorvarname = Peek().Value;
                        isFunc = false;
                    }
                }
            }
            return new(funcorvarname,isFunc);
        }
        private void ParseClassStatement()
        {
            string className = Peek().Value;
            Advance();
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "}")
                {
                    indexC = i;
                    Advance();
                    break;
                }
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "{")
                {
                    Advance();
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
            classInfo = new(@return.Value,@return.Key,className);
            _classInfos.Add(className, classInfo);
        }
        internal Dictionary<string,ClassInfo> _classInfos = new Dictionary<string,ClassInfo>();
        private void ParseInvokeStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            string uuid = EvaluateExpression(ParseExpression()).ToString();
            if(uuid == UUIDData.AndUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                bool bool2 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.Add(varname, (object)(bool1 && bool2));
                }
                catch
                {
                    _variables[varname] = (object)(bool1 && bool2);
                }
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
            else if(uuid == UUIDData.OrUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                bool bool2 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.Add(varname, (object)(bool1 || bool2));
                }
                catch
                {
                    _variables[varname] = (object)(bool1 || bool2);
                }
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
            else if (uuid == UUIDData.NotUUID)
            {
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                string varname = EvaluateExpression(ParseExpression()).ToString();
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                bool bool1 = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
                try
                {
                    _variables.Add(varname, (object)(!bool1));
                }
                catch
                {
                    _variables[varname] = (object)(!bool1);
                }
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
        }
        private void ParseGetJsonFilePathStatement()
        {
            if (isjsonassembly)
            {
                if (!MatchPunctuation("(")) throw new Exception("Expected '('");
                string a = File.ReadAllText(EvaluateExpression(ParseExpression()).ToString());
                Console.WriteLine(a);
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
            else
            {
                throw new Exception("the text \"getJsonFromFilePath\" is not a context var function or readonlyclass name.(are you missing import assembly:json?)");
            }
        }
        private void ParseGetHtmlStatement()
        {
            if (isnetassembly)
            {
                if (!MatchPunctuation("(")) throw new Exception("Expected '('");
                string content = "";
                HttpClient client = new HttpClient();
                HttpResponseMessage response = client.GetAsync(EvaluateExpression(ParseExpression()).ToString()).Result;
                if (response.IsSuccessStatusCode)
                {
                    content = response.Content.ReadAsStringAsync().Result;
                }
                if (!MatchPunctuation(",")) throw new Exception("Expected ','");
                string a = EvaluateExpression(ParseExpression()).ToString();
                try
                {
                    _variables.Add(a, content);
                }
                catch
                {
                    _variables[a] = content;
                }
                if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
                if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            }
            else
            {
                throw new Exception("the text \"gethtml\" is not a context var function or readonlyclass name.(are you missing import assembly:net?)");
            }
        }
        private void ParseReturnStatement(string funcName)
        {
            _results.Add(funcName, EvaluateExpression(ParseExpression()));
            _variables.Add($"{funcName}:return", _results[funcName]);
        }

        private void ParseThrowStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Exception? ex = new();
            try
            {
                ex = (Exception)Activator.CreateInstance(Type.GetType("System." + Peek().Value));
            }
            catch
            {
                throw new Exception("This type isn't inherit Exception.");
            }
            throw (ex);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseDowhileStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "}")
                {
                    indexC = i;
                    break;
                }
                tokens.Add(_tokens[i]);
            }
            do
            {
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
                    _variables.Add(functionInfo.Parameter[i], actualParameters[i]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Exception("Parameters is not correct.");
                }
            }
            _variables = Run(functionInfo.FunctionBody, _variables, funcName);
        }
        private void RunFunction(string funcName,List<Token> tokens,List<string> args)
        {
            FunctionInfo functionInfo = new(funcName,args,tokens);
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
                    _variables.Add(functionInfo.Parameter[i], actualParameters[i]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Exception("Parameters is not correct.");
                }
            }
            _variables = Run(functionInfo.FunctionBody, _variables, funcName);
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
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "]")
                {
                    indexC = i;
                    break;
                }
                tokens.Add(_tokens[i]);
                Advance();
            }
            return tokens;
        }
        private void ParseWhileStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "}")
                {
                    indexC = i;
                    break;
                }
                tokens.Add(_tokens[i]);
            }
            while (a)
            {
                _variables = Run(tokens, _variables);
                _current = current;
                a = bool.Parse(EvaluateExpression(ParseExpression()).ToString());
            }
            _current = indexC;
        }
        private void ParseIfStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            int current = _current;
            string b = EvaluateExpression(ParseExpression()).ToString();
            bool a = bool.Parse(b);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            List<Token> tokens = new List<Token>();
            int indexC = 0;
            for (int i = _current + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "]")
                {
                    indexC = i;
                    break;
                }
                if (_tokens[i].Type == TokenTypes.Punctuation && _tokens[i].Value == "[")
                {
                    continue;
                }
                tokens.Add(_tokens[i]);
            }
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
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            string a = EvaluateExpression(ParseExpression()).ToString();
            if (a.Equals("syntax", StringComparison.OrdinalIgnoreCase))
            {
                OnOutputEvent(new OutputEventArgs("Syntax:\r\n1.keyword+(args);\r\nInvoke keyword with args.\r\nWarning: If keyword hasn't args,\r\nuse keyword+;\r\n2.Define var.\r\n(1)define:\r\ninit(varname,Type); Or var varname = value;\r\n(2)assignment:\r\nset(varname,value);\r\n3.Keywords Table.\r\n________________________________________________\r\n|keyword   |  args   |  do somwthings           |\r\n|paint        text     print the text           |\r\n|watchstart  varname   start stopwatch.         |\r\n|watchend     null     stop stopwatch           |\r\n|init    varname,Type  init var.                |\r\n|set    varname,value  set var.                 |\r\n|...          ....     ............             |\r\n|_______________________________________________|"));
            }
            else if (a.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                OnOutputEvent(new("https://github.com/Mars-FeiFei/FeiSharp \r\n It is copy to your clipboard."));
            }
            else
            {
                throw new Exception("Invalid string for \"helper\" keyword: " + a);
            }
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseABEStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            string a = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            double b = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            double c = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            double d = double.Parse(EvaluateExpression(ParseExpression()).ToString());
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            try
            {
                _variables.Add(a, (b + c + d) / 3);
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
                _variables.Add(name, Stopwatch.Elapsed.TotalSeconds);
            }
            catch
            {
                _variables[name] = Stopwatch.Elapsed.TotalSeconds;
            }
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        string name = "";
        private void ParseWatchStartStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Stopwatch = Stopwatch.StartNew();
            name = EvaluateExpression(ParseExpression()).ToString();
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseWaitStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Thread.Sleep(int.Parse(EvaluateExpression(ParseExpression()).ToString()) * 1000);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseStopStatement()
        {
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            OnOutputEvent(new OutputEventArgs("Application is stop..."));
            foreach (var item in _variables)
            {
                OnOutputEvent(new OutputEventArgs($"var {item.Key} = {item.Value} : {item.Value.GetType()}"));
            }
            OnOutputEvent(new OutputEventArgs($"{_variables.Count}" + " items of vars."));
        }
        private void ParseStartStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);
            Process.Start(a);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseExportStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            Expr b1 = ParseExpression();
            string a1 = (string)EvaluateExpression(b1);
            File.AppendAllText(a, a1);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
        }
        private void ParseRunStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr b = ParseExpression();
            string a = (string)EvaluateExpression(b);
            Run(File.ReadAllText(a));
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
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
            try
            {
                parser.ParseStatements();
            }
            catch (Exception ex)
            {
                OnOutputEvent(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            return;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            try
            {
                parser.ParseStatements();
            }
            catch (Exception ex)
            {
                OnOutputEvent(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            return parser._variables;
        }
        internal KeyValuePair<Dictionary<string,object>, Dictionary<string, FunctionInfo>> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, int op = 0)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            try
            {
                parser.ParseStatements();
            }
            catch (Exception ex)
            {
                OnOutputEvent(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            KeyValuePair<Dictionary<string, object>, Dictionary<string,FunctionInfo>> result = new(parser._variables,parser._functions);
            return result;
        }
        internal Dictionary<string, object> Run(IEnumerable<Token> tokens, Dictionary<string, object> _vars, string funcName)
        {
            List<Token> _tokens = new(tokens);
            Parser parser = new(_tokens);
            parser.OutputEvent = this.OutputEvent;
            parser._variables = _vars;
            try
            {
                parser.ParseStatements(funcName);
            }
            catch (Exception ex)
            {
                OnOutputEvent(new OutputEventArgs("Parsing error: " + ex.Message));
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
            try
            {
                parser.ParseStatements();
            }
            catch (Exception ex)
            {
                OnOutputEvent(new OutputEventArgs("Parsing error: " + ex.Message));
            }
            return parser._variables;
        }
        private void ParseSetStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr expr = GetVar();
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            string name = EvaluateExpression(ParseExpression()).ToString();
            Expr expr2 = new VarExpr(name);
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            _variables[((VarExpr)expr).Name] = ((VarExpr)expr2).Name;
        }
        private void ParseInitStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr expr = GetVar();
            if (!MatchPunctuation(",")) throw new Exception("Expected ','");
            Expr expr2 = GetType();
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            _variables.Add(((VarExpr)expr).Name, InitValue(((VarExpr)expr2).Name));
        }
        private object InitValue(string type)
        {
            Type t = Type.GetType("System." + type);
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
                throw new Exception("Expected variable name");
            }

            if (!MatchToken(TokenTypes.Operator, out string op) || op != "=")
            {
                throw new Exception("Expected '=' after variable name");
            }

            Expr expr = ParseExpression();

            if (!MatchPunctuation(";"))
            {
                throw new Exception("Expected ';' after variable declaration");
            }

            object value = EvaluateExpression(expr);
            _variables[varName] = value;
        }

        private PrintStmt ParsePrintStatement()
        {
            if (!MatchPunctuation("(")) throw new Exception("Expected '('");
            Expr expr = ParseExpression();
            if (!MatchPunctuation(")")) throw new Exception("Expected ')'");
            if (!MatchPunctuation(";")) throw new Exception("Expected ';'");
            return new PrintStmt(expr);
        }

        private Expr ParseExpression()
        {
            Expr expr = ParsePrimary();
            while (MatchOperator("+", "-", "*", "/", "|", "^", "<", ">", "=", "!"))
            {
                string op = Previous().Value;
                Expr right = ParsePrimary();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr ParsePrimary()
        {
            if (MatchToken(TokenTypes.Number))
            {
                return new ValueExpr(double.Parse(Previous().Value));
            }
            else if (MatchToken(TokenTypes.String))
            {
                return new StringExpr(Previous().Value);
            }
            else if (MatchToken(TokenTypes.Identifier))
            {
                string varName = Previous().Value;
                if (_variables.TryGetValue(varName, out object value))
                {
                    return new ValueExpr(value);
                }
                else if(_functions.ContainsKey(varName))
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
                    else {
                        return new ValueExpr(_variables[a.Key]);
                    }
                }
                throw new Exception($"Undefined variable: {varName}");
            }
            else if (MatchPunctuation("("))
            {
                Expr expr = ParseExpression();
                if (!MatchPunctuation(")"))
                {
                    throw new Exception("Expected ')' after expression");
                }
                return expr;
            }
            else if (MatchKeyword("true"))
            {
                string varName = Previous().Value;
                if (_variables.TryGetValue(varName, out object value))
                {
                    return new ValueExpr(value);
                }

                throw new Exception($"Undefined variable: {varName}");
            }
            else if (MatchKeyword("false"))
            {
                string varName = Previous().Value;
                if (_variables.TryGetValue(varName, out object value))
                {
                    return new ValueExpr(value);
                }

                throw new Exception($"Undefined variable: {varName}");
            }
            else
            {
                if(Previous().Type == TokenTypes.Keyword && Previous().Value == "true")
                {
                    _current--;
                    return new ValueExpr(true);
                }
                else if (Previous().Type == TokenTypes.Keyword && Previous().Value == "false")
                {
                    _current--;
                    return new ValueExpr(true);
                }
                else if (MatchPreviousToken(TokenTypes.Number))
                {
                    return new ValueExpr(double.Parse(Previous().Value));
                }
                else if (MatchPreviousToken(TokenTypes.String))
                {
                    return new StringExpr(Previous().Value);
                }
                else if (MatchPreviousToken(TokenTypes.Identifier))
                {
                    string varName = Previous().Value;
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
                    throw new Exception($"Undefined variable: {varName}");
                }
                else if (MatchPunctuation("("))
                {
                    Expr expr = ParseExpression();
                    if (!MatchPunctuation(")"))
                    {
                        throw new Exception("Expected ')' after expression");
                    }
                    return expr;
                }
            }
            throw new Exception("Unvalid token: " + Peek().Value);
        }
        private bool MatchPreviousToken(params TokenTypes[] types)
        {
            foreach (var type in types)
            {
                if (PreviousCheck(type))
                {
                    Advance();
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
            else {
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
            if (IsAtEnd()) throw new InvalidOperationException("No more tokens available.");
            return _tokens[_current];
        }
        private Token Previous()
        {
            if (_current == 0) throw new InvalidOperationException("No previous token available.");
            return _tokens[_current - 1];
        }
        private void EvaluatePrintStmt(PrintStmt stmt)
        {
            string text = EvaluateExpression(stmt.Expression).ToString();
            OnOutputEvent(new OutputEventArgs(text));
        }
        private object EvaluateExpression(Expr expr)
        {
            switch (expr)
            {
                case ValueExpr numExpr:
                    if (double.TryParse(numExpr.Value.ToString(), out double a))
                    {
                        return double.Parse(numExpr.Value.ToString());
                    }
                    else
                    {
                        return numExpr.Value;
                    }

                case BinaryExpr binExpr:
                    object left = EvaluateExpression(binExpr.Left);
                    object right = EvaluateExpression(binExpr.Right);
                    if (left is string && right is string)
                    {
                        return binExpr.Operator switch
                        {
                            "+" => (string)left + (string)right,
                            "-" => Regex.Replace((string)left, Regex.Escape((string)right), ""),
                            _ => throw new Exception("Unexpected operator: " + binExpr.Operator)
                        };
                    }
                    left = Convert.ToDouble(left.ToString());
                    right = Convert.ToDouble(right.ToString());
                    return binExpr.Operator switch
                    {
                        "+" => (double)left + (double)right,
                        "-" => (double)left - (double)right,
                        "*" => (double)left * (double)right,
                        "/" => (double)left / (double)right,
                        "^" => Math.Pow((double)left, (double)right),
                        "|" => Math.Pow((double)left, 1 / (double)right),
                        ">" => (double)left > (double)right,
                        "<" => (double)left < (double)right,
                        "=" => (double)left == (double)right,
                        "!" => (double)left != (double)right,
                        _ => throw new Exception("Unexpected operator: " + binExpr.Operator)
                    };
                case StringExpr stringExpr:
                    return stringExpr.Value;
                default:
                    throw new Exception("Unexpected expression type");
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
}
