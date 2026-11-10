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
        result = TransformAsOperator(result);
        result = NormalizeCastTypeArguments(result);
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
        var importPlaceholders = new Dictionary<string, string>();
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
        // `as` is also the alias separator in an import directive. Protect
        // that complete directive before applying the legacy cast transform.
        input = Regex.Replace(input,
            @"\bimport\s+[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)+\s+as\s+[A-Za-z_]\w*\s*;?",
            m =>
            {
                string key = $"__IMPORT_{importPlaceholders.Count}__";
                importPlaceholders[key] = m.Value;
                return key;
            },
            RegexOptions.IgnoreCase);
        string pattern = @"(?<![.\w])\b([^,;()\[\]{}]*?)\s+as\s+(integer|string|boolean|number|double|float|int|str|bool|char)\b(?![.\w])";
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

                return $"cast({expr}, {typeName})";
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (var kv in stringPlaceholders)
        {
            input = input.Replace(kv.Key, kv.Value);
        }
        foreach (var kv in importPlaceholders)
        {
            input = input.Replace(kv.Key, kv.Value);
        }

        return input;
    }

    // `cast` is implemented by default_functions through ctype(), whose
    // runtime contract uses type-name strings. Keep the language surface
    // pleasant (`cast(value, integer)`) while supplying that internal form.
    private static string NormalizeCastTypeArguments(string input)
    {
        const string typePattern = "integer|string|boolean|number|double|float|int|str|bool|char";
        return Regex.Replace(input,
            $@"\bcast\s*\(\s*(?<value>[^,()]+?)\s*,\s*(?<type>{typePattern})\s*\)",
            match =>
            {
                string typeName = match.Groups["type"].Value;
                if (TypeAliasMap.TryGetValue(typeName, out string? mappedType))
                {
                    typeName = mappedType;
                }
                return $"cast({match.Groups["value"].Value.Trim()}, \"{typeName}\")";
            },
            RegexOptions.IgnoreCase);
    }

    private static bool IsReservedKeyword(string expr)
    {
        string[] reserved = { "if", "else", "while", "for", "return", "var", "func", "class" };
        return Array.Exists(reserved, k => k.Equals(expr, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Dictionary<string, string> TypeAliasMap = new(StringComparer.OrdinalIgnoreCase)
{
    { "int", "number" },
    { "integer", "number" },
    { "float", "number" },
    { "double", "number" },
    { "str", "string" },
    { "bool", "boolean" },
    { "char", "string" },
};
}
