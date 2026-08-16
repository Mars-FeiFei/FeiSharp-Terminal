using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
namespace FeiSharpTerminal3._1;
public class CodeTransformer
{
    public static string Transform(string input)
    {
        string result = ExpandCompoundAssignments(input);
        result = TransformConditionals(result);
        result = TransformAsOperator(result);
        return result;
    }

    private static string ExpandCompoundAssignments(string input)
    {
        string pattern = @"\b([a-zA-Z_]\w*)\s*([+\-*/%&|^]?=)\s*([^;]+);";
        return Regex.Replace(input, pattern, match =>
        {
            string varName = match.Groups[1].Value;
            string op = match.Groups[2].Value;
            string expr = match.Groups[3].Value.Trim();

            if (op == "=") return match.Value;

            string actualOp = op.TrimEnd('=');
            return $"{varName} = {varName} {actualOp} {expr};";
        });
    }
    private static string TransformAsOperator(string input)
    {
        var stringPlaceholders = new Dictionary<string, string>();
        int placeholderIndex = 0;
        input = Regex.Replace(input, @"""(?:[^""\\]|\\.)*""",
            m => {
                string key = $"__STRING_{placeholderIndex++}__";
                stringPlaceholders[key] = m.Value;
                return key;
            },
            RegexOptions.Singleline);
        input = Regex.Replace(input, @"'(?:[^'\\]|\\.)*'",
            m => {
                string key = $"__CHAR_{placeholderIndex++}__";
                stringPlaceholders[key] = m.Value;
                return key;
            },
            RegexOptions.Singleline);
        string pattern = @"(?<![.\w])\b([^,;()\[\]{}]*?)\s+as\s+([a-zA-Z_]\w*)\b(?![.\w])";
        input = Regex.Replace(input, pattern,
            m => {
                string expr = m.Groups[1].Value.Trim();
                string typeName = m.Groups[2].Value;
                if (TypeAliasMap.TryGetValue(typeName, out string? mappedType))
                {
                    typeName = mappedType;
                }
                if (string.IsNullOrEmpty(expr) || IsReservedKeyword(expr))
                {
                    return m.Value;
                }

                return $"cast({expr}, \"{typeName}\")";
            },
            RegexOptions.Singleline);
        foreach (var kv in stringPlaceholders)
        {
            input = input.Replace(kv.Key, kv.Value);
        }

        return input;
    }

    private static bool IsReservedKeyword(string expr)
    {
        string[] reserved = { "if", "else", "while", "for", "return", "var", "func", "class" };
        return Array.Exists(reserved, k => k.Equals(expr, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Dictionary<string, string> TypeAliasMap = new()
{
    { "int", "number" },
    { "integer", "number" },
    { "float", "number" },
    { "double", "number" },
    { "str", "string" },
    { "bool", "boolean" },
    { "char", "string" },
};
    private static string TransformConditionals(string input)
    {
        var lines = input.Split('\n');
        var result = new List<string>();
        var ifConditions = new Stack<string>();

        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i].Trim();

            var ifMatch = Regex.Match(line, @"^if\s*\(([^)]+)\)");
            if (ifMatch.Success)
            {
                string condition = ifMatch.Groups[1].Value;
                ifConditions.Push(condition);
                result.Add(lines[i]);
                i++;
                continue;
            }

            var elseIfMatch = Regex.Match(line, @"^else\s+if\s*\(([^)]+)\)");
            if (elseIfMatch.Success && ifConditions.Count > 0)
            {
                string currentCondition = ifConditions.Peek();
                string newCondition = elseIfMatch.Groups[1].Value;
                string combinedCondition = $"(({currentCondition}) = false & ({newCondition}))";
                string newLine = line.Replace(
                    elseIfMatch.Value,
                    $"if{combinedCondition}"
                );

                ifConditions.Pop();
                ifConditions.Push(combinedCondition);
                result.Add(newLine);
                i++;
                continue;
            }

            var elseMatch = Regex.Match(line, @"^else\s*(\{)");
            if (elseMatch.Success && ifConditions.Count > 0)
            {
                string currentCondition = ifConditions.Peek();

                string negatedCondition = $"({currentCondition}) = false";

                string newLine = line.Replace(
                    "else",
                    $"if({negatedCondition})"
                );

                ifConditions.Pop();
                result.Add(newLine);
                i++;
                continue;
            }

            result.Add(lines[i]);
            i++;
        }

        return string.Join("\n", result);
    }
}