using System.Text.RegularExpressions;
namespace FeiSharp8._5RuntimeSdk;
internal static class FeiSharpCodeAssistant
{
    private sealed record Template(string Name, string[] Keywords, string Code);

    private static readonly Template[] Templates =
    [
        new("Print text", ["print", "output", "hello", "display", "输出", "打印", "显示"], "print(\"Hello, World!\");"),
        new("Read input", ["input", "read", "readline", "ask", "user input", "输入", "读取"], "readLine(\"name\");\nprint(name);"),
        new("Variable", ["variable", "var", "value", "declare", "definition", "变量", "声明"], "var value = 0;\nprint(value);"),
        new("Constant", ["constant", "const", "readonly", "fixed", "常量"], "const maxCount = 10;\nprint(maxCount);"),
        new("Function", ["function", "method", "procedure", "fn", "call", "函数", "方法"], "function add(a, b)\n{\n    return a + b;\n}\n\nprint(add(1, 2));"),
        new("Return value", ["return", "result", "value from function"], "function getValue()\n{\n    return 42;\n}"),
        new("If statement", ["if", "condition", "conditional", "branch", "otherwise", "条件", "判断"], "if (condition)\n{\n    print(\"true\");\n}\nelse\n{\n    print(\"false\");\n}"),
        new("For loop", ["for", "loop", "iterate", "counter", "repeat", "循环", "遍历"], "for (var i = 0; i < 10; i++)\n{\n    print(i);\n}"),
        new("While loop", ["while", "loop", "repeat while", "until", "当", "循环"], "var i = 0;\nwhile (i < 10)\n{\n    print(i);\n    i++;\n}"),
        new("ForEach loop", ["foreach", "for each", "collection loop", "items", "遍历集合"], "var items = [1, 2, 3];\nforEach (item in items)\n{\n    print(item);\n}"),
        new("Switch statement", ["switch", "case", "match", "select", "匹配", "分支"], "switch (value)\n{\n    case 1:\n        print(\"one\");\n        break;\n    otherwise:\n        print(\"other\");\n}"),
        new("Class", ["class", "object", "type", "model", "instance", "类", "对象"], "class Person\n{\n    var name = \"FeiSharp\";\n\n    function greet()\n    {\n        print(name);\n    }\n}"),
        new("Lambda", ["lambda", "arrow", "delegate", "anonymous", "callback", "匿名函数", "委托"], "var f = (a) => a + 1;\nprint(f.invoke(1));"),
        new("Event", ["event", "handler", "subscribe", "unsubscribe", "callback", "事件", "事件处理"], "event onChanged;\n\nfunction handleChanged(value)\n{\n    print(value);\n}\n\nonChanged += handleChanged;"),
        new("List", ["list", "array", "collection", "sequence", "items", "列表", "数组"], "var items = [1, 2, 3];\nitems.Add(4);\nprint(items.Count);"),
        new("Dictionary", ["dictionary", "map", "key value", "lookup", "object map"], "var users = {\"admin\": \"FeiSharp\"};\nprint(users[\"admin\"]);"),
        new("Try catch", ["try", "catch", "exception", "error", "handle failure", "异常", "错误处理"], "try\n{\n    // code that may fail\n}\ncatch (ex)\n{\n    print(ex);\n}"),
        new("Import", ["import", "include", "module", "library", "namespace"], "import \"helpers.fsc\";\n\nhelpers.run();"),
        new("File read", ["file", "read file", "load text", "open file", "text file"], "var text = File.ReadAllText(\"data.txt\");\nprint(text);"),
        new("File write", ["write file", "save text", "create file", "file output"], "File.WriteAllText(\"data.txt\", \"Hello from FeiSharp\");"),
        new("String operations", ["string", "text", "replace", "substring", "uppercase"], "var text = \"FeiSharp\";\nvar result = text.toUpper();\nprint(result);"),
        new("Math", ["math", "number", "calculate", "random", "round"], "var value = math.round(3.14);\nprint(value);"),
        new("Main function", ["main", "entry point", "program start", "application", "入口", "主函数"], "function main()\n{\n    print(\"FeiSharp application started\");\n}\n\nmain();")
    ];

    public static string Generate(string request, out string templateName)
    {
        string normalizedRequest = request.Trim();
        if (normalizedRequest.Length == 0)
        {
            templateName = "Print text";
            return Templates[0].Code;
        }

        if (TryGenerateStructured(normalizedRequest, out templateName, out string structuredCode))
        {
            return structuredCode;
        }

        Template best = Templates
            .Select(template => (Template: template, Score: Score(normalizedRequest, template)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Template.Name, StringComparer.OrdinalIgnoreCase)
            .First().Template;

        templateName = best.Name;
        return best.Code;
    }

    private static bool TryGenerateStructured(string request, out string templateName, out string code)
    {
        string normalized = request.ToLowerInvariant();
        bool mentionsArray = ContainsAny(normalized, "array", "list", "collection", "数组", "列表");
        bool mentionsTraversal = ContainsAny(normalized, "for", "foreach", "iterate", "loop", "遍历", "循环");
        bool mentionsPrint = ContainsAny(normalized, "print", "output", "display", "打印", "输出", "显示");
        List<string> values = ExtractValues(request);

        if (mentionsArray && mentionsTraversal && mentionsPrint && values.Count > 0)
        {
            string formattedValues = string.Join(", ", values);
            templateName = "Array traversal with output";
            code = $"var items = [{formattedValues}];\nfor (var i = 0; i < items.Count; i++)\n{{\n    print(items[i]);\n}}";
            return true;
        }

        if (mentionsArray && mentionsTraversal &&
            ContainsAny(normalized, "sum", "total", "add together", "求和", "总和") && values.Count > 0)
        {
            string formattedValues = string.Join(", ", values);
            templateName = "Array traversal with sum";
            code = $"var items = [{formattedValues}];\nvar total = 0;\nfor (var i = 0; i < items.Count; i++)\n{{\n    total += items[i];\n}}\nprint(total);";
            return true;
        }

        if (ContainsAny(normalized, "function", "method", "函数", "方法") &&
            ContainsAny(normalized, "add", "sum", "plus", "相加", "求和"))
        {
            templateName = "Function with arithmetic";
            code = "function add(a, b)\n{\n    return a + b;\n}\n\nprint(add(1, 2));";
            return true;
        }

        templateName = string.Empty;
        code = string.Empty;
        return false;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static List<string> ExtractValues(string request)
    {
        Match bracketMatch = Regex.Match(request, @"[\[【](?<values>[^\]】]+)[\]】]");
        string source = bracketMatch.Success
            ? bracketMatch.Groups["values"].Value
            : Regex.Match(request, @"-?\d+(?:\s*[,，、]\s*-?\d+)+").Value;

        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        return Regex.Matches(source, @"-?\d+(?:\.\d+)?")
            .Select(match => match.Value)
            .ToList();
    }

    public static bool IsStandaloneRequest(string request)
    {
        string normalized = request.Trim();
        return Templates.Any(template =>
            template.Keywords.Any(keyword => string.Equals(keyword, normalized, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(template.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static int Score(string request, Template template)
    {
        string[] requestWords = Words(request.ToLowerInvariant());
        int score = 0;

        foreach (string keyword in template.Keywords)
        {
            int bestKeywordScore = 0;
            foreach (string keywordWord in Words(keyword.ToLowerInvariant()))
            {
                foreach (string requestWord in requestWords)
                {
                    if (requestWord == keywordWord)
                    {
                        bestKeywordScore = Math.Max(bestKeywordScore, 140 + keywordWord.Length);
                    }
                    else if (requestWord.Contains(keywordWord, StringComparison.Ordinal) ||
                             keywordWord.Contains(requestWord, StringComparison.Ordinal))
                    {
                        bestKeywordScore = Math.Max(bestKeywordScore, 90 + Math.Min(requestWord.Length, keywordWord.Length));
                    }
                    else
                    {
                        int distance = MatrixDistance(requestWord, keywordWord);
                        int limit = Math.Max(1, Math.Min(requestWord.Length, keywordWord.Length) / 2);
                        if (distance <= limit)
                        {
                            bestKeywordScore = Math.Max(bestKeywordScore, 70 - distance * 10);
                        }
                    }
                }
            }

            score += bestKeywordScore;
        }

        score += Math.Max(0, 30 - MatrixDistance(request.ToLowerInvariant(), template.Name.ToLowerInvariant()));
        return score;
    }

    private static string[] Words(string value) =>
        Regex.Matches(value, "[a-z0-9_]+|[\u4e00-\u9fff]+", RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .ToArray();

    private static int MatrixDistance(string left, string right)
    {
        int[,] matrix = new int[left.Length + 1, right.Length + 1];
        for (int row = 0; row <= left.Length; row++) matrix[row, 0] = row;
        for (int column = 0; column <= right.Length; column++) matrix[0, column] = column;

        for (int row = 1; row <= left.Length; row++)
        {
            for (int column = 1; column <= right.Length; column++)
            {
                int substitution = matrix[row - 1, column - 1] +
                    (left[row - 1] == right[column - 1] ? 0 : 1);
                matrix[row, column] = Math.Min(
                    Math.Min(matrix[row - 1, column] + 1, matrix[row, column - 1] + 1),
                    substitution);
            }
        }

        return matrix[left.Length, right.Length];
    }
}
