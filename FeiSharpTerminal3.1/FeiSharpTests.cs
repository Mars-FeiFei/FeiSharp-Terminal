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
        ("'it' Keyword And Addition Test", () => {
            RunFeiSharpCode(@"
var x = 1 + 2;
var y = it + 1;",
                new ExpectedVariable("x", 3),
                new ExpectedVariable("y", 4));
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

        // 创建一个堆叠条形图来显示比例
        var chart = new BarChart()
            .Width(60)
            .Label("[yellow]Distribution Of Test Results[/]")
            .CenterLabel();

        // 添加通过和失败作为两个独立的条，或者使用堆叠效果
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
