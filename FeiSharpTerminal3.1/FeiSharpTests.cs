using BenchmarkDotNet.Attributes;
using FeiSharpStudio;
using Spectre.Console;
using System.Diagnostics;
namespace FeiSharpTerminal3._1.Tests;
[ShortRunJob]
[MemoryDiagnoser]
[RankColumn]
public class QuickBenchmarkAttribute : Attribute
{
}
[QuickBenchmark]
public static class FeiSharpTests
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;
    private static readonly List<TestResult> _results = new();
    public class TestResult
    {
        public int TestNumber { get; set; }
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
    static (string Name, Action Test)[] tests = new (string Name, Action Test)[]
    {
        ("Static Class Method Statement Test", () => {
            RunFeiSharpCode(@"
class console {
    public static function log(txt) {
        printNl(txt);
    }
}
console.log(""Hello"");
var result = 1;",
                new ExpectedVariable("result", 1));
        }),
        ("Generic Class And Function Syntax Test", () => {
            RunFeiSharpCode(@"
class pair<t1, t2> {
    var first = null;
    var second = null;
    constructor(firstValue, secondValue) {
        this.first = firstValue;
        this.second = secondValue;
    }
    public function getFirst() { return this.first; }
}
function identity<t>(value) { return value; }
var item = new pair<int, string>(7, ""ok"");
var result = identity<int>(item.getFirst());",
                new ExpectedVariable("result", 7));
        }),
        ("Lambda Delegate And Event Test", () => {
            RunFeiSharpCode(@"
var offset = 3;
var add = x => x + offset;
var sum = (x, y) => x + y;
var explicitDelegate = delegate(value) => value * 2;
var r1 = add(4);
var r2 = sum(2, 5);
var r3 = explicitDelegate(6);
var parenthesized = (a) => a + 1;
var block = (a) => { return a + 2; };
var r5 = parenthesized.invoke(4);
var r6 = block(4);
event changed;
changed += value => value + 10;
var r4 = changed(5);
event removable;
var handler = (value) => value + 20;
removable += handler;
var r7 = removable.invoke(1);
removable -= handler;
var r8 = removable(1);",
                new ExpectedVariable("r1", 7),
                new ExpectedVariable("r2", 7),
                new ExpectedVariable("r3", 12),
                new ExpectedVariable("r5", 5),
                new ExpectedVariable("r6", 6),
                new ExpectedVariable("r4", 15),
                new ExpectedVariable("r7", 21),
                new ExpectedVariable("r8", null));
        }),
        ("Single Line Control Body Test", () => {
            RunFeiSharpCode(@"
var number = 0;
while (number < 3) number += 1;
for (var index = 0; index < 3; index += 1) number += index;
foreach (var item in [1, 2]) number += item;
if (number == 11) number = 12; else number = 0;
var result = number;",
                new ExpectedVariable("result", 12));
        }),
        ("Recursive Function With Single Line Return Test", () => {
            RunFeiSharpCode(@"
function factorial(value) {
    if (value = 1) return 1;
    return value * factorial(value - 1);
}
var result = factorial(4);",
                new ExpectedVariable("result", 24));
        }),
        ("Import Class Alias Test", () => {
            RunFeiSharpCode(@"
namespace Demo.Models {
    class Counter {
        var value = 0;
        constructor(seed) { value = seed; }
        public function getValue() { return value; }
    }
}
import Demo.Models.Counter as Tally;
var counter = new Tally(42);
var result = counter.getValue();",
                new ExpectedVariable("result", 42));
        }),
        ("Import Alias Instance Method And Static Field Test", () => {
            RunFeiSharpCode(@"
namespace Data {
    class dataSource {
        private static var data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        public function getDataS(index, password) {
            if (password == ""feifeigan1234567890"") {
                return data[index];
            }
        }
    }
}
import Data.dataSource as d;
var result = d.getDataS(1, ""feifeigan1234567890"");",
                new ExpectedVariable("result", 2));
        }),
        ("Import Alias Does Not Transform To Cast Test", () => {
            const string source = "import Demo.Models.Counter as Tally;";
            if (CodeTransformer.Transform(source) != source)
                throw new AssertionException("Import aliases must not be transformed into cast expressions");
        }),
        ("As Cast Requires A Type Name Test", () => {
            if (CodeTransformer.Transform("print(\"1\" as a + 2);") != "print(\"1\" as a + 2);")
                throw new AssertionException("Only supported type names may follow 'as'");
            if (CodeTransformer.Transform("var value = \"1\" as integer;") != "var value = cast(\"1\", \"number\");")
                throw new AssertionException("The as cast syntax must normalize integer to the runtime number type");
            if (CodeTransformer.Transform("var value = cast(\"1\", integer);") != "var value = cast(\"1\", \"number\");")
                throw new AssertionException("cast must accept an unquoted type name");
        }),
        ("Character Literal Lexing Test", () => {
            AssertTokenSequence("'a'", new[]
            {
                new Token(TokenTypes.Character, "a"),
                new Token(TokenTypes.EndOfFile, "")
            });
        }),
        ("Modifier Keyword Lexing Test", () => {
            AssertTokenSequence("static abstract protected", new[]
            {
                new Token(TokenTypes.Keyword, "static"),
                new Token(TokenTypes.Keyword, "abstract"),
                new Token(TokenTypes.Keyword, "protected"),
                new Token(TokenTypes.EndOfFile, "")
            });
        }),
        ("Combined Operator Lexing Test", () => {
            AssertTokenSequence("== != && || <= >=", new[]
            {
                new Token(TokenTypes.Operator, "=="),
                new Token(TokenTypes.Operator, "!="),
                new Token(TokenTypes.Operator, "&&"),
                new Token(TokenTypes.Operator, "||"),
                new Token(TokenTypes.Operator, "<="),
                new Token(TokenTypes.Operator, ">="),
                new Token(TokenTypes.EndOfFile, "")
            });
        }),
        ("When Otherwise Expression Test", () => {
            RunFeiSharpCode(@"
var selected = ""yes"" when 3 > 2 otherwise ""no"";
var fallback = ""yes"" when false otherwise ""no"";
var absent = 10 when false;",
                new ExpectedVariable("selected", "yes"),
                new ExpectedVariable("fallback", "no"),
                new ExpectedVariable("absent", null));
        }),
        ("Multiplication Test", () => {
            RunFeiSharpCode(@"
var x = 3 * 4 * 5;",
                new ExpectedVariable("x", 60));
        }),
        ("Unary Not And New Comparison Operators Test", () => {
            RunFeiSharpCode(@"
var neq = 1 != 2;
var le = 2 <= 2;
var ge = 3 >= 2;
var andv = true && !false;
var orv = false || false;",
                new ExpectedVariable("neq", true),
                new ExpectedVariable("le", true),
                new ExpectedVariable("ge", true),
                new ExpectedVariable("andv", true),
                new ExpectedVariable("orv", false));
        }),
        ("Power Test", () => {
            RunFeiSharpCode(@"
var x = 0;
oldPow(""x"", 2, 3);",
                new ExpectedVariable("x", 8));
        }),
        ("Brace Function Scope Test", () => {
            RunFeiSharpCode(@"
function inc(v) {
    return v + 1;
}
function useOther() {
    return inc(4);
}
var result = useOther();",
                new ExpectedVariable("result", 5));
        }),
        ("Brace If Test", () => {
            RunFeiSharpCode(@"
var x = 0;
if(true) {
    x = 7;
}",
                new ExpectedVariable("x", 7));
        }),
        ("Class Inheritance Constructor Field Test", () => {
            RunFeiSharpCode(@"
class baseCounter {
    var baseValue = 2;
    public function readBase() {
        return baseValue;
    }
}
class childCounter : baseCounter {
    var value = 0;
    constructor(seed) {
        value = seed + baseValue;
    }
    public function getValue() {
        return value;
    }
}
var a = new childCounter(5);
var result = a.getValue();",
                new ExpectedVariable("result", 7));
        }),
        ("This Keyword Member Access Test", () => {
            RunFeiSharpCode(@"
class counterType {
    var value = 1;
    public function add(delta) {
        this.value = this.value + delta;
        return this.value;
    }
    public function read() {
        return this.value;
    }
}
var counter = new counterType();
var result = counter.add(4);
var result2 = counter.read();",
                new ExpectedVariable("result", 5),
                new ExpectedVariable("result2", 5));
        }),
        ("Base Keyword Public Method Test", () => {
            RunFeiSharpCode(@"
class baseType {
    public function seed() {
        return 3;
    }
}
class childType : baseType {
    public function calc() {
        return base.seed() + 4;
    }
}
var child = new childType();
var result = child.calc();",
                new ExpectedVariable("result", 7));
        }),
        ("Protected Method Inheritance Test", () => {
            RunFeiSharpCode(@"
class protectedBase {
    protected function seed() {
        return 3;
    }
}
class protectedChild : protectedBase {
    public function calc() {
        return base.seed() + 4;
    }
}
var child = new protectedChild();
var result = child.calc();",
                new ExpectedVariable("result", 7));
        }),
        ("Protected Member Access Rejected Test", () => {
            RunFeiSharpCode(@"
class demoType {
    protected function secret() {
        return 9;
    }
    public function expose() {
        return this.secret();
    }
}
var demo = new demoType();
var ok = demo.expose();
try {
    var bad = demo.secret();
} catch(ex) {
    var errType = ex.type;
    var errNo = ex.index;
}",
                new ExpectedVariable("ok", 9),
                new ExpectedVariable("errType", "SemanticError"),
                new ExpectedVariable("errNo", "FS3001"));
        }),
        ("Static Outside Class Rejected Test", () => {
            RunFeiSharpCode(@"
try {
    static function addOne(v) {
        return v + 1;
    }
} catch(ex) {
    var errType = ex.type;
    var errNo = ex.index;
}",
                new ExpectedVariable("errType", "SemanticError"),
                new ExpectedVariable("errNo", "FS3001"));
        }),
        ("Static Var Outside Class Rejected Test", () => {
            RunFeiSharpCode(@"
try {
    static var value = 1;
} catch(ex) {
    var errType = ex.type;
    var errNo = ex.index;
}",
                new ExpectedVariable("errType", "SemanticError"),
                new ExpectedVariable("errNo", "FS3001"));
        }),
        ("Static Class Method Invocation Test", () => {
            RunFeiSharpCode(@"
class console {
    public static function log(txt) {
        printNl(txt);
        return txt;
    }
}
var result = console.log(""Hello"");",
                new ExpectedVariable("result", "Hello"));
        }),
        
        ("Static Class Keyword-Like Method Invocation Test", () => {
            RunFeiSharpCode(@"
class console {
    public static var str = """";
    public static function write() {
        return str;
    }
}
console.str = ""hello"";
var result = console.write();",
                new ExpectedVariable("result", "hello"));
        }),
        ("Try Catch Error Metadata Test", () => {
            RunFeiSharpCode(@"
try {
    throw ""boom"";
} catch(ex) {
    var errType = ex.type;
    var errDesc = ex.msg;
    var errNo = ex.index;
}",
                new ExpectedVariable("errType", "UserException"),
                new ExpectedVariable("errDesc", "boom"),
                new ExpectedVariable("errNo", "FS4001"));
        }),
        ("Public Outside Class Rejected Test", () => {
            RunFeiSharpCode(@"
try {
    public function nope() {
        return 1;
    }
} catch(ex) {
    var errType = ex.type;
    var errNo = ex.index;
}",
                new ExpectedVariable("errType", "SemanticError"),
                new ExpectedVariable("errNo", "FS3001"));
        }),
        ("Variable Modifier Access Test", () => {
            RunFeiSharpCode(@"
class demoType {
    public var open = 3;
    protected var shield = 4;
    private var secret = 5;
    static var shared = 6;
    public function exposeSecret() {
        return this.secret;
    }
    public function exposeShield() {
        return this.shield;
    }
}
var demo = new demoType();
var a = demo.open;
var b = demoType.shared;
var c = demo.exposeSecret();
try {
    var bad0 = demo.shared;
} catch(ex) {
    var errType0 = ex.type;
    var errNo0 = ex.index;
}
try {
    var bad1 = demo.secret;
} catch(ex) {
    var errType1 = ex.type;
    var errNo1 = ex.index;
}
try {
    var bad2 = demo.shield;
} catch(ex) {
    var errType2 = ex.type;
    var errNo2 = ex.index;
}",
                new ExpectedVariable("a", 3),
                new ExpectedVariable("b", 6),
                new ExpectedVariable("c", 5),
                new ExpectedVariable("errType0", "SemanticError"),
                new ExpectedVariable("errNo0", "FS3001"),
                new ExpectedVariable("errType1", "SemanticError"),
                new ExpectedVariable("errNo1", "FS3001"),
                new ExpectedVariable("errType2", "SemanticError"),
                new ExpectedVariable("errNo2", "FS3001"));
        }),
        ("Const Declaration And Class Access Test", () => {
            RunFeiSharpCode(@"
const answer = 42;
var copied = answer;
try {
    answer = 0;
} catch(ex) {
    var constError = ex.type;
}
class configType {
    public const limit = 9;
    private const secret = 2;
    public function reveal() {
        return this.secret;
    }
}
var config = new configType();
var exposedLimit = config.limit;
var exposedSecret = config.reveal();
try {
    config.limit = 0;
} catch(ex) {
    var memberConstError = ex.type;
}
",
                new ExpectedVariable("copied", 42),
                new ExpectedVariable("constError", "SemanticError"),
                new ExpectedVariable("exposedLimit", 9),
                new ExpectedVariable("exposedSecret", 2),
                new ExpectedVariable("memberConstError", "SemanticError"));
        }),
        ("Assignment Syntax Test", () => {
            RunFeiSharpCode(@"
class assignmentBase {
    var seed = 1;
    public function setSeed(v) {
        base.seed = v;
        return base.seed;
    }
}
class assignmentChild : assignmentBase {
    var value = 0;
    constructor(seed) {
        this.value = seed;
    }
    public function assign(v) {
        this.value = v;
        return this.value;
    }
}
class assignmentConfig {
    static var defaultValue = 2;
}
assignmentConfig.defaultValue = 7;
var item = new assignmentChild(0);
var r1 = item.assign(5);
var r2 = item.setSeed(9);
item.value = 11;
item = new assignmentChild(3);
var r3 = item.value;
var r4 = assignmentConfig.defaultValue;",
                new ExpectedVariable("r1", 5),
                new ExpectedVariable("r2", 9),
                new ExpectedVariable("r3", 3),
                new ExpectedVariable("r4", 7));
        }),
        ("String Runtime Access Test", () => {
            RunFeiSharpCode(@"
var text = ""Hello"";
var c1 = text[1];
var len = text.Length;
var part = text.Substring(1, 3);
var idx = text.IndexOf(""ll"");
var chained = ""123"".replace('1', '2').replace(""22"", ""33"");
var chainedLength = ""123"".replace('1', '2').Length;
var upper = ""  FeiSharp  "".trim().toUpper();
var contains = ""FeiSharp"".contains(""Sharp"");
var prefix = ""FeiSharp"".startsWith(""Fei"");
var suffix = ""FeiSharp"".endsWith(""Sharp"");
var character = ""FeiSharp"".charAt(3);
var reversed = ""abc"".reverse();
var repeated = ""ab"".repeat(3);",
                new ExpectedVariable("c1", 'e'),
                new ExpectedVariable("len", 5),
                new ExpectedVariable("part", "ell"),
                new ExpectedVariable("idx", 2),
                new ExpectedVariable("chained", "333"),
                new ExpectedVariable("chainedLength", 3),
                new ExpectedVariable("upper", "FEISHARP"),
                new ExpectedVariable("contains", true),
                new ExpectedVariable("prefix", true),
                new ExpectedVariable("suffix", true),
                new ExpectedVariable("character", 'S'),
                new ExpectedVariable("reversed", "cba"),
                new ExpectedVariable("repeated", "ababab"));
        }),
        ("Backslash String Literal Print Test", () => {
            RunFeiSharpCode(@"
printNl(""\\\\"");
var ok = 1;",
                new ExpectedVariable("ok", 1));
        }),
        ("Array List Dictionary Syntax Test", () => {
            RunFeiSharpCode(@"
var arr = [1, 2, 3];
var typedArr = new int[] { 4, 5, 6 };
var list = new list<int> { 10, 20 };
list.add(30);
list[1] = 25;
var dict = new dictionary<string, int> { { ""a"", 1 }, { ""b"", 2 } };
dict.add(""c"", 3);
dict[""b""] = 20;
var r1 = arr[1];
var r2 = typedArr[2];
var r3 = list[1];
var r4 = list.count;
var r5 = dict[""b""];
var r6 = dict.count;
var r7 = dict.containsKey(""c"");",
                new ExpectedVariable("r1", 2),
                new ExpectedVariable("r2", 6),
                new ExpectedVariable("r3", 25),
                new ExpectedVariable("r4", 3),
                new ExpectedVariable("r5", 20),
                new ExpectedVariable("r6", 3),
                new ExpectedVariable("r7", true));
        }),
        ("Runtime List Mutation Test", () => {
            RunFeiSharpCode(@"
var values = new list<int> { 1, 3 };
values.insert(1, 2);
values.add(4);
values.removeAt(0);
var first = values[0];
var count = values.count;",
                new ExpectedVariable("first", 2),
                new ExpectedVariable("count", 3));
        }),
        ("Universal Value Member Syntax Test", () => {
            RunFeiSharpCode(@"
function addOne(value) { return value + 1; }
var functionIdentifier = addOne.getFunctionIdentifier();
var functionIsClass = addOne.isClass();
var functionIsFunction = addOne.isFunction();
class MetaType {
    public static var b = 100;
}
var classIdentifier = MetaType.getClassIdentifier();
var classIsClass = MetaType.isClass();
var classHasB = MetaType.getAllMembers().contains(""b"");
var numberIsClass = 1.isClass();
var numberHasToString = 1.getAllMembers().contains(""ToString"");
var integerValue = new int[] { 1 };
var integerIsClass = integerValue[0].isClass();
var booleanIsClass = true.isClass();
var booleanHasToString = true.getAllMembers().contains(""ToString"");
var characterIsClass = 'x'.isClass();
var stringIsClass = ""value"".isClass();
var stringHasLength = ""value"".getAllMembers().contains(""Length"");
var stringUpper = ""value"".toUpper();",
                new ExpectedVariable("functionIdentifier", "addOne"),
                new ExpectedVariable("functionIsClass", false),
                new ExpectedVariable("functionIsFunction", true),
                new ExpectedVariable("classIdentifier", "MetaType"),
                new ExpectedVariable("classIsClass", true),
                new ExpectedVariable("classHasB", true),
                new ExpectedVariable("numberIsClass", false),
                new ExpectedVariable("numberHasToString", true),
                new ExpectedVariable("integerIsClass", false),
                new ExpectedVariable("booleanIsClass", false),
                new ExpectedVariable("booleanHasToString", true),
                new ExpectedVariable("characterIsClass", false),
                new ExpectedVariable("stringIsClass", false),
                new ExpectedVariable("stringHasLength", true),
                new ExpectedVariable("stringUpper", "VALUE"));
        }),
        ("Standard API Functions And Math Class Test", () => {
            RunFeiSharpCode(@"
var textLength = len(""FeiSharp"");
var arrayLength = len([1, 2, 3]);
var emptyRange = range(3, 3);
var emptyCount = emptyRange.Count;
var ascending = range(1, 6, 2);
var descending = range(5, 0, -2);
var type1 = typeOf(ascending);
var type2 = typeOf(null);
var nullCheck = isNull(null);
var r1 = ascending[2];
var r2 = descending[2];
var absValue = math.abs(-4);
var clampValue = math.clamp(12, 0, 10);
var rounded = math.round(3.14159, 2);
var root = math.sqrt(81);
var piValue = math.pi;",
                new ExpectedVariable("textLength", 8),
                new ExpectedVariable("arrayLength", 3),
                new ExpectedVariable("emptyCount", 0),
                new ExpectedVariable("type1", "list"),
                new ExpectedVariable("type2", "null"),
                new ExpectedVariable("nullCheck", true),
                new ExpectedVariable("r1", 5),
                new ExpectedVariable("r2", 1),
                new ExpectedVariable("absValue", 4),
                new ExpectedVariable("clampValue", 10),
                new ExpectedVariable("rounded", 3.14),
                new ExpectedVariable("root", 9),
                new ExpectedVariable("piValue", Math.PI));
        }),
        ("Detailed FS Error Codes Test", () => {
            AssertFeiSharpErrorCode("var value = len 1;", "FS2004");
            AssertFeiSharpErrorCode("var value = missingName;", "FS3004");
            AssertFeiSharpErrorCode("var value = math.unknown(1);", "FS3004");
            AssertFeiSharpErrorCode("var value = range(0, 5, 0);", "FS3005");
            AssertFeiSharpErrorCode("var value = len(12);", "FS3006");
            AssertFeiSharpErrorCode("var values = [1]; var value = values[3];", "FS3007");
            AssertFeiSharpErrorCode("var target = null; var value = target.Length;", "FS3008");
            AssertFeiSharpErrorCode("const answer = 42; answer = 0;", "FS3009");
        }),
        ("Lower Camel Naming Convention Test", () => {
            RunFeiSharpCode(@"
class sampleCounter {
    public function getValue() {
        return 7;
    }
}
var counter = new sampleCounter();
var result = counter.getValue();
var upperMath = Math.ABS(-1);
var upperGlobal = TYPEOF(upperMath);
class SampleType {
    public function GetValue() { return 8; }
}
var mixedCase = new SampleType();
var mixedResult = mixedCase.getvalue();",
                new ExpectedVariable("result", 7),
                new ExpectedVariable("upperMath", 1),
                new ExpectedVariable("upperGlobal", "number"),
                new ExpectedVariable("mixedResult", 8));
        }),
        ("File Directory And Path API Test", TestFileApis),
        ("HTTP API Test", TestHttpApis),
        ("JavaScript JSON Interop Test", () => {
            RunFeiSharpCode(@"
var encoded = js.stringify([1, ""ok""]);
var parsed = js.parse(""[42, true]"");
var value = parsed[0];
var enabled = parsed[1];
var answer = js.eval(""1 + 2"");",
                new ExpectedVariable("encoded", "[1,\"ok\"]"),
                new ExpectedVariable("value", 42),
                new ExpectedVariable("enabled", true),
                new ExpectedVariable("answer", 3));
        }),
        ("IO And Network API Error Test", () => {
            AssertFeiSharpErrorCode("var value = file.exists(\"missing.txt\");", "FS3010");
            AssertFeiSharpErrorCode("var value = http.get(\"http://127.0.0.1/\");", "FS3010");
            AssertFeiSharpErrorCode("using FeiSharp.IO; var value = file.readText(\"__missing_feisharp_file__\");", "FS5001");
            AssertFeiSharpErrorCode("using FeiSharp.Net; var value = http.get(\"not-a-url\");", "FS5002");
        }),
        ("Sibling Namespace Sharing Test", TestSiblingNamespaceSharing),
        ("While Array Index And Stop Condition Test", () => {
            RunFeiSharpCode(@"
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
}",
                new ExpectedVariable("sum", 6),
                new ExpectedVariable("i", 3),
                new ExpectedVariable("count", 3),
                new ExpectedVariable("guard", 10));
        }),
        ("For Syntax Test", () => {
            RunFeiSharpCode(@"
var sum = 0;
for(var i = 1; i < 6; i = i + 1) {
    sum = sum + i;
}
var guard = 10;
for(; false; ) {
    guard = 99;
}",
                new ExpectedVariable("sum", 15),
                new ExpectedVariable("guard", 10));
        }),
        ("For Speed Test(5000 times)", () => {
            RunFeiSharpCode(@"
var a = 1;
for(var i = 1; i < 5001; i = i + 1){}",new ExpectedVariable("a", 1));
        }),
        ("Double For Speed Test(100 x 100 times)", () => {
            RunFeiSharpCode(@"
var nestSum = 0;
for(var a = 0; a < 100; a = a + 1) {
    for(var b = 0; b < 100; b = b + 1) {
        nestSum = nestSum + 1;
    }
}",new ExpectedVariable("nestSum", 10000));
        }),
    };
    public static void RunAllTests()
    {
        Start:
        ResetTestRunState();
        AnsiConsole.Write(
            new FigletText("FeiSharp Tests")
                .Color(Color.Cyan1));
        var rule = new Rule("[yellow]Test Results Details[/]")
        {
            Style = Style.Parse("blue"),
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[yellow]#[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Test Name[/]"))
            .AddColumn(new TableColumn("[yellow]Status[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Duration[/]").Centered());
        var totalStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < tests.Length; i++)
        {
            var test = tests[i];
            var testStopwatch = Stopwatch.StartNew();
            try
            {
                test.Test();
                testStopwatch.Stop();
                _passedTests++;
                table.AddRow(
                    (i + 1).ToString(),
                    test.Name.EscapeMarkup(),
                    "[green]PASS[/]",
                    $"{testStopwatch.ElapsedMilliseconds}ms");
                _results.Add(new TestResult
                {
                    TestNumber = i + 1,
                    TestName = test.Name,
                    Passed = true,
                    Duration = testStopwatch.Elapsed
                });
            }
            catch (Exception ex)
            {
                testStopwatch.Stop();
                _failedTests++;
                table.AddRow(
                    (i + 1).ToString(),
                    test.Name.EscapeMarkup(),
                    "[red]FAIL[/]",
                    $"{testStopwatch.ElapsedMilliseconds}ms");
                _results.Add(new TestResult
                {
                    TestNumber = i + 1,
                    TestName = test.Name,
                    Passed = false,
                    ErrorMessage = ex.Message,
                    Duration = testStopwatch.Elapsed
                });
            }
        }
        totalStopwatch.Stop();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        ShowTestRatioChart();
        var summaryPanel = new Panel(
           $"[bold]Total: {tests.Length} | " +
           $"[green]Passed: {_passedTests}[/] | " +
           $"[red]Failed: {_failedTests}[/] | " +
           $"[green]Pass Rate: {Math.Round((double)_passedTests / tests.Length * 100, 2)}%[/] | " +
           $"[red]Fail Rate: {Math.Round((double)_failedTests / tests.Length * 100, 2)}%[/][/]")
           .Border(BoxBorder.Rounded)
           .BorderStyle(_failedTests == 0 ? Style.Parse("green") : Style.Parse("red"))
           .Padding(1, 1, 1, 1);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(summaryPanel);
        if (_failedTests > 0)
        {
            ShowFailedTestsDetails();
        }
        while (true)
        {
            var choice = AnsiConsole.Prompt(
     new SelectionPrompt<string>()
         .Title("[blue]What do you want to do?[/]")
         .PageSize(10)
         .AddChoices(new[]
         {
            "[green]1.[/] [yellow]Rerun All Tests[/]",
            "[green]2.[/] [yellow]Export Test Report And Open[/]",
            "[green]3.[/] [yellow]Only Export Test Report[/]",
            "[green]4.[/] [yellow]View Reports History[/]",
            "[green]5.[/] [yellow]Open Folder in File Explorer[/]",
            "[green]6.[/] [yellow]Clear All Files[/]",
            "[green]7.[/] [yellow]Go to FeiSharp Terminal[/]",
         }));

            var cleanChoice = choice.Replace("[green]1.[/] [yellow]", "")
                                    .Replace("[green]2.[/] [yellow]", "")
                                    .Replace("[green]3.[/] [yellow]", "")
                                    .Replace("[green]4.[/] [yellow]", "")
                                    .Replace("[green]5.[/] [yellow]", "")
                                    .Replace("[green]6.[/] [yellow]", "")
                                    .Replace("[green]7.[/] [yellow]", "")
                                    .Replace("[/]", "");

            switch (cleanChoice.Trim())
            {
                case "Rerun All Tests":
                    goto Start;
                case "Export Test Report And Open":
                    ExportTestReport(true, totalStopwatch);
                    break;
                case "Only Export Test Report":
                    ExportTestReport(false, totalStopwatch);
                    break;
                case "View Reports History":
                    TestReportExporter.ShowReportHistory();
                    break;
                case "Open Folder in File Explorer":
                    Process.Start("explorer.exe", TestReportExporter.ReportsDirectory);
                    break;
                case "Clear All Files":
                    var files = Directory.GetFiles(TestReportExporter.ReportsDirectory);
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                    break;
                case "Go to FeiSharp Terminal":
                    return;
            }
        }

    }
    private static void ResetTestRunState()
    {
        _passedTests = 0;
        _failedTests = 0;
        _results.Clear();
    }
    private static void ExportTestReport(bool isOpen, Stopwatch sw)
    {
        TestReportExporter.ExportTestReport(_results, _passedTests, _failedTests, sw.Elapsed, isOpen);
    }
    private static void ShowTestRatioChart()
    {
        if (_passedTests == 0 && _failedTests == 0) return;

        var total = _passedTests + _failedTests;
        var passPercentage = total > 0 ? (double)_passedTests / total * 100 : 0;
        var failPercentage = total > 0 ? (double)_failedTests / total * 100 : 0;


        var chart = new BarChart()
            .Width(60)
            .Label("[yellow]Distribution Of Test Results[/]")
            .CenterLabel();


        if (_passedTests > 0)
        {
            chart.AddItem("Passed", _passedTests, Color.Green);
        }
        if (_failedTests > 0)
        {
            chart.AddItem("Failed", _failedTests, Color.Red);
        }

        AnsiConsole.Write(chart);
    }
    private static void ShowFailedTestsDetails()
    {
        var failedTests = _results.Where(r => !r.Passed).ToList();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Failed Tests Details:[/]");
        foreach (var test in failedTests)
        {
            var detailPanel = new Panel(
                $"[red]Test #{test.TestNumber}: {test.TestName}[/]\n" +
                $"[yellow]Error:[/] {test.ErrorMessage?.EscapeMarkup()}")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("red"))
                .Padding(1, 1, 1, 1);
            AnsiConsole.Write(detailPanel);
        }
    }
    public class ExpectedVariable
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public string? Type { get; set; }

        public ExpectedVariable(string name, object value, string? type = null)
        {
            Name = name;
            Value = value;
            Type = type;
        }
    }
    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }
    static void RunFeiSharpCode(string code, params ExpectedVariable[] expectedVariables)
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
        foreach (var expected in expectedVariables)
        {
            AssertVariableExists(parser, expected.Name);
            AssertVariableValue(parser, expected.Name, expected.Value);
            if (expected.Type != null)
            {
                AssertVariableType(parser, expected.Name, expected.Type);
            }
        }
    }
    static void AssertVariableExists(Parser parser, string name)
    {
        if (!parser._variables.ContainsKey(name))
        {
            throw new AssertionException($"Variable '{name}' is nonexistent");
        }
    }
    static void AssertVariableValue(Parser parser, string name, object expectedValue)
    {
        var actualValue = parser._variables[name];
        var actualStr = actualValue?.ToString() ?? "null";
        var expectedStr = expectedValue?.ToString() ?? "null";
        if (actualStr != expectedStr)
        {
            throw new AssertionException(
                $"The actual value of variable '{name}' doesn't match the excepted value\n" +
                $"  Excepted Value: {expectedStr}\n" +
                $"  Actual Value: {actualStr}");
        }
    }
    static void AssertVariableType(Parser parser, string name, string expectedType)
    {
        var actualValue = parser._variables[name];
        var actualType = actualValue?.GetType().Name ?? "null";
        if (actualType != expectedType)
        {
            throw new AssertionException(
                $"The type of variable '{name}' is unmatched\n" +
                $"  Excepted Type: {expectedType}\n" +
                $"  Actual Type: {actualType}");
        }
    }
    static void AssertTokenSequence(string code, IReadOnlyList<Token> expectedTokens)
    {
        Lexer lexer = new(code);
        for (int i = 0; i < expectedTokens.Count; i++)
        {
            Token actual = lexer.NextToken();
            Token expected = expectedTokens[i];
            if (actual.Type != expected.Type || actual.Value != expected.Value)
            {
                throw new AssertionException(
                    $"Token mismatch at position {i}\n" +
                    $"  Expected: {expected.Type} '{expected.Value}'\n" +
                    $"  Actual: {actual.Type} '{actual.Value}'");
            }
        }
    }

    static void TestSiblingNamespaceSharing()
    {
        string root = Path.Combine(Path.GetTempPath(), $"FeiSharpNamespaceTests_{Guid.NewGuid():N}");
        string previousApplicationPath = FeiSharp8._5RuntimeSdk.Program._applicationPath;
        Directory.CreateDirectory(root);
        try
        {
            string libraryPath = Path.Combine(root, "b.fsc");
            string entryPath = Path.Combine(root, "c.fsc");
            File.WriteAllText(libraryPath, "namespace A.B; function twice(value) { return value * 2; }");
            File.WriteAllText(Path.Combine(root, "ordinary.fsc"), "var shouldNotLoad = 1;");
            string entrySource = "import A.B; var shortResult = twice(21); var qualifiedResult = A.B.twice(3);";
            File.WriteAllText(entryPath, entrySource);

            FeiSharp8._5RuntimeSdk.Program._applicationPath = entryPath;
            string processedSource = FeiSharp8._5RuntimeSdk.Program.ProProcesser(entrySource);
            Lexer lexer = new(processedSource);
            List<Token> tokens = [];
            Token token;
            do
            {
                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);

            Parser parser = new(tokens);
            parser.ParseStatements();
            AssertVariableValue(parser, "shortResult", 42);
            AssertVariableValue(parser, "qualifiedResult", 6);
            if (parser._variables.ContainsKey("shouldNotLoad"))
            {
                throw new AssertionException("A sibling file without a namespace was loaded automatically");
            }
        }
        finally
        {
            FeiSharp8._5RuntimeSdk.Program._applicationPath = previousApplicationPath;
            Directory.Delete(root, true);
        }
    }

    static void TestFileApis()
    {
        string root = Path.Combine(Path.GetTempPath(), $"FeiSharpApiTests_{Guid.NewGuid():N}");
        string scriptPath = root.Replace('\\', '/');
        try
        {
            RunFeiSharpCode($@"
using FeiSharp.IO;
var root = ""{scriptPath}"";
var createdPath = directory.create(root);
var directoryExists = directory.exists(root);
var first = path.combine(root, ""first.txt"");
var copied = path.combine(root, ""copied.txt"");
var moved = path.combine(root, ""moved.txt"");
file.writeText(first, ""hello"");
file.appendText(first, "" world"");
var content = file.readText(first);
file.copy(first, copied);
file.move(copied, moved);
var fileExists = file.exists(moved);
var files = directory.getFiles(root, ""*.txt"");
var fileCount = len(files);
var fileName = path.getFileName(moved);
var extension = path.getExtension(moved);
var changed = path.changeExtension(moved, "".log"");
var changedExtension = path.getExtension(changed);
var currentDirectory = directory.current;
var temporaryPath = path.tempPath;
var separator = path.separator;
file.delete(first);
file.delete(moved);
directory.delete(root);
var removed = !directory.exists(root);",
                new ExpectedVariable("directoryExists", true),
                new ExpectedVariable("content", "hello world"),
                new ExpectedVariable("fileExists", true),
                new ExpectedVariable("fileCount", 2),
                new ExpectedVariable("fileName", "moved.txt"),
                new ExpectedVariable("extension", ".txt"),
                new ExpectedVariable("changedExtension", ".log"),
                new ExpectedVariable("removed", true));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    static void TestHttpApis()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        Task server = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    using System.Net.Sockets.TcpClient client = await listener.AcceptTcpClientAsync();
                    using System.Net.Sockets.NetworkStream stream = client.GetStream();
                    byte[] request = new byte[4096];
                    await stream.ReadAsync(request);
                    byte[] response = System.Text.Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
                    await stream.WriteAsync(response);
                }
            }
            catch (System.Net.Sockets.SocketException) when (!listener.Server.IsBound)
            {
            }
        });

        try
        {
            RunFeiSharpCode($@"
using FeiSharp.Net;
var body = http.get(""http://127.0.0.1:{port}/get"");
var status = http.getStatus(""http://127.0.0.1:{port}/status"");
var posted = http.post(""http://127.0.0.1:{port}/post"", ""payload"", ""text/plain"");
var jsonBody = http.getJson(""http://127.0.0.1:{port}/json"");
var jsonPayload = js.stringify([1]);
var postedJson = http.postJson(""http://127.0.0.1:{port}/post-json"", jsonPayload);
var requested = http.request(""http://127.0.0.1:{port}/request"", ""PUT"", ""payload"", ""text/plain"");",
                new ExpectedVariable("body", "ok"),
                new ExpectedVariable("status", 200),
                new ExpectedVariable("posted", "ok"),
                new ExpectedVariable("jsonBody", "ok"),
                new ExpectedVariable("postedJson", "ok"),
                new ExpectedVariable("requested", "ok"));
            server.GetAwaiter().GetResult();
        }
        finally
        {
            listener.Stop();
        }
    }

    static void AssertFeiSharpErrorCode(string code, string expectedCode)
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
        typeof(Parser)
            .GetField("_propagateFeiSharpExceptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(parser, true);

        try
        {
            parser.ParseStatements();
        }
        catch (System.Exception ex)
        {
            string? actualCode = ex.GetType().GetProperty("Number")?.GetValue(ex)?.ToString();
            if (actualCode == expectedCode)
            {
                return;
            }
            throw new AssertionException($"Expected error code '{expectedCode}', actual '{actualCode ?? ex.GetType().Name}'.");
        }

        throw new AssertionException($"Expected error code '{expectedCode}', but the code completed successfully.");
    }
}
