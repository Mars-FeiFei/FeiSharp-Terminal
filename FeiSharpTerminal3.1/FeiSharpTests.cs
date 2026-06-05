using FeiSharpStudio;
using Spectre.Console;
using System.Diagnostics;
namespace FeiSharpTerminal3._1.Tests;
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
        ("Character Literal Lexing Test", () => {
            AssertTokenSequence("'a'", new[]
            {
                new Token(TokenTypes.Character, "a"),
                new Token(TokenTypes.EndOfFile, "")
            });
        }),
        ("Multiplication Test", () => {
            RunFeiSharpCode(@"
var x = 3 * 4 * 5;",
                new ExpectedVariable("x", 60));
        }),
        ("Power Test", () => {
            RunFeiSharpCode(@"
var x = 0;
oldpow(""x"", 2, 3);",
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
class Base {
    var baseValue = 2;
    public function readBase() {
        return baseValue;
    }
}
class Child : Base {
    var value = 0;
    constructor(seed) {
        value = seed + baseValue;
    }
    public function getValue() {
        return value;
    }
}
var a = Child(5);
var result = a.getValue();",
                new ExpectedVariable("result", 7));
        }),
        ("This Keyword Member Access Test", () => {
            RunFeiSharpCode(@"
class Counter {
    var value = 1;
    public function add(delta) {
        this.value = this.value + delta;
        return this.value;
    }
    public function read() {
        return this.value;
    }
}
var counter = Counter();
var result = counter.add(4);
var result2 = counter.read();",
                new ExpectedVariable("result", 5),
                new ExpectedVariable("result2", 5));
        }),
        ("Base Keyword Public Method Test", () => {
            RunFeiSharpCode(@"
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
var result = child.calc();",
                new ExpectedVariable("result", 7));
        }),
        ("Try Catch Error Metadata Test", () => {
            RunFeiSharpCode(@"
try {
    throw ""boom"";
} catch(type, describe, number) {
    var errType = type;
    var errDesc = describe;
    var errNo = number;
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
} catch(type, describe, number) {
    var errType = type;
    var errNo = number;
}",
                new ExpectedVariable("errType", "SemanticError"),
                new ExpectedVariable("errNo", "FS3001"));
        }),
        ("Assignment Syntax Test", () => {
            RunFeiSharpCode(@"
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
var r4 = Config.defaultValue;",
                new ExpectedVariable("r1", 5),
                new ExpectedVariable("r2", 9),
                new ExpectedVariable("r3", 3),
                new ExpectedVariable("r4", 7));
        }),
        ("Array List Dictionary Syntax Test", () => {
            RunFeiSharpCode(@"
var arr = [1, 2, 3];
var typedArr = new int[] { 4, 5, 6 };
var list = new List<int> { 10, 20 };
list.Add(30);
list[1] = 25;
var dict = new Dictionary<string, int> { { ""a"", 1 }, { ""b"", 2 } };
dict.Add(""c"", 3);
dict[""b""] = 20;
var r1 = arr[1];
var r2 = typedArr[2];
var r3 = list[1];
var r4 = list.Count;
var r5 = dict[""b""];
var r6 = dict.Count;",
                new ExpectedVariable("r1", 2),
                new ExpectedVariable("r2", 6),
                new ExpectedVariable("r3", 25),
                new ExpectedVariable("r4", 3),
                new ExpectedVariable("r5", 20),
                new ExpectedVariable("r6", 3));
        }),
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
        ("Double For Speed Test(5000 times)", () => {
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
}
