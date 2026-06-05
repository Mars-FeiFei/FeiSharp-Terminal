using FeiSharpStudio;
using FeiSharpTerminal3._1.ExceptionThrow;

static Parser RunCode(string code)
{
    Lexer lexer = new(code);
    List<Token> tokens = [];
    Token token;
    do
    {
        token = lexer.NextToken();
        tokens.Add(token);
    } while (token.Type != TokenTypes.EndOfFile);

    Parser parser = new(tokens);
    parser.ParseStatements();
    return parser;
}

static string CaptureRunOutput(string code)
{
    var originalOut = Console.Out;
    using var writer = new StringWriter();
    Console.SetOut(writer);
    try
    {
        RunCode(code);
    }
    finally
    {
        Console.SetOut(originalOut);
    }
    return writer.ToString();
}

static void AssertVariable(Parser parser, string name, object expected)
{
    if (!parser._variables.TryGetValue(name, out object? actual))
    {
        throw new InvalidOperationException($"Variable '{name}' not found.");
    }

    if (!string.Equals(actual?.ToString(), expected.ToString(), StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Variable '{name}' expected '{expected}' but got '{actual}'.");
    }
}

static void RunCase(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"PASS: {name}");
    }
    catch (System.Exception ex)
    {
        Console.WriteLine($"FAIL: {name}");
        Console.WriteLine(ex);
        Environment.ExitCode = 1;
    }
}

RunCase("this keyword", () =>
{
    Parser parser = RunCode("""
class Counter {
    var value = 1;
    public function add(delta) {
        set(this.value, this.value + delta);
        return this.value;
    }
    public function read() {
        return this.value;
    }
}
var counter = Counter();
var result = counter.add(4);
var result2 = counter.read();
""");

    AssertVariable(parser, "result", 5);
    AssertVariable(parser, "result2", 5);
});

RunCase("base keyword", () =>
{
    Parser parser = RunCode("""
class Base {
    public function seed() {
        return 3;
    }
}
class Child : Base {
    public function calc() {
        return base.seed() + 4;
    }
}
var child = Child();
var result = child.calc();
""");

    AssertVariable(parser, "result", 7);
});

RunCase("try catch metadata", () =>
{
    Parser parser = RunCode("""
try {
    throw "boom";
} catch(type, describe, number) {
    var errType = type;
    var errDesc = describe;
    var errNo = number;
}
""");

    AssertVariable(parser, "errType", "UserException");
    AssertVariable(parser, "errDesc", "boom");
    AssertVariable(parser, "errNo", "FS4001");
});

RunCase("public outside class rejected", () =>
{
    Parser parser = RunCode("""
try {
    public function nope() {
        return 1;
    }
} catch(type, describe, number) {
    var errType = type;
    var errNo = number;
}
""");

    AssertVariable(parser, "errType", "SemanticError");
    AssertVariable(parser, "errNo", "FS3001");
});

RunCase("private outside class allowed", () =>
{
    Parser parser = RunCode("""
private function addOne(v) {
    return v + 1;
}
var result = addOne(4);
""");

    AssertVariable(parser, "result", 5);
});

RunCase("nested function call in return expression", () =>
{
    Parser parser = RunCode("""
function inc(v) {
    return v + 1;
}
function useOther() {
    return inc(4);
}
var result = useOther();
""");

    AssertVariable(parser, "result", 5);
});

RunCase("syntax error type is unified", () =>
{
    var output = CaptureRunOutput("""
return inc(4);
""");

    if (!output.Contains("[SyntaxError]", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected SyntaxError output but got '{output}'.");
    }
});

RunCase("private member access rejected", () =>
{
    Parser parser = RunCode("""
class Demo {
    private function secret() {
        return 9;
    }
    public function expose() {
        return this.secret();
    }
}
var demo = Demo();
var ok = demo.expose();
try {
    var bad = demo.secret();
} catch(type, describe, number) {
    var errType = type;
    var errNo = number;
}
""");

    AssertVariable(parser, "ok", 9);
    AssertVariable(parser, "errType", "SemanticError");
    AssertVariable(parser, "errNo", "FS3001");
});

RunCase("assignment syntax", () =>
{
    Parser parser = RunCode("""
class Base {
    var seed = 1;
    public function setSeed(v) {
        base.seed = v;
        return base.seed;
    }
}
class Child : Base {
    var value = 0;
    constructor(seed) {
        this.value = seed;
    }
    public function assign(v) {
        this.value = v;
        return this.value;
    }
}
class Config {
    var defaultValue = 2;
}
Config.defaultValue = 7;
var item = Child(0);
var r1 = item.assign(5);
var r2 = item.setSeed(9);
item.value = 11;
item = Child(3);
var r3 = item.value;
var r4 = Config.defaultValue;
""");

    AssertVariable(parser, "r1", 5);
    AssertVariable(parser, "r2", 9);
    AssertVariable(parser, "r3", 3);
    AssertVariable(parser, "r4", 7);
});

RunCase("array list dictionary syntax", () =>
{
    Parser parser = RunCode("""
var arr = [1, 2, 3];
var typedArr = new int[] { 4, 5, 6 };
var list = new List<int> { 10, 20 };
list.Add(30);
list[1] = 25;
var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
dict.Add("c", 3);
dict["b"] = 20;
var r1 = arr[1];
var r2 = typedArr[2];
var r3 = list[1];
var r4 = list.Count;
var r5 = dict["b"];
var r6 = dict.Count;
""");

    AssertVariable(parser, "r1", 2);
    AssertVariable(parser, "r2", 6);
    AssertVariable(parser, "r3", 25);
    AssertVariable(parser, "r4", 3);
    AssertVariable(parser, "r5", 20);
    AssertVariable(parser, "r6", 3);
});

RunCase("while array index syntax and stop condition", () =>
{
    Parser parser = RunCode("""
var arr = [1, 2, 3];
var i = 0;
var count = 0;
var sum = 0;
while(i < 3) {
    sum = sum + arr[i];
    i = i + 1;
    count = count + 1;
}
var guard = 10;
while(i < 3) {
    guard = 99;
}
""");

    AssertVariable(parser, "sum", 6);
    AssertVariable(parser, "i", 3);
    AssertVariable(parser, "count", 3);
    AssertVariable(parser, "guard", 10);
});

RunCase("for syntax and sum", () =>
{
    Parser parser = RunCode("""
var sum = 0;
for(var i = 1; i < 6; i = i + 1) {
    sum = sum + i;
}
var guard = 10;
for(; false; ) {
    guard = 99;
}
""");

    AssertVariable(parser, "sum", 15);
    AssertVariable(parser, "guard", 10);
});

if (Environment.ExitCode == 0)
{
    Console.WriteLine("ALL VERIFICATIONS PASSED");
}
