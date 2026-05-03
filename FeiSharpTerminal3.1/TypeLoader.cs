using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.IO;
[RequiresDynamicCode("FeiSharp resolves CLR types dynamically. Native AOT requires any invoked types and members to be preserved explicitly.")]
[RequiresUnreferencedCode("FeiSharp resolves CLR types dynamically from arbitrary assemblies. Trimming cannot statically analyze these accesses.")]
public static class TypeLoader
{
    public static Type? LoadType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;
        Type? result = null;
        result = TryGetTypeSimple(typeName);
        if (result != null) return result;
        result = TryGetTypeFromLoadedAssemblies(typeName);
        if (result != null) return result;
        result = TryGetTypeFromCommonAssemblies(typeName);
        if (result != null) return result;
        result = TryGetTypeFromAllAssembliesInDirectory(typeName);
        if (result != null) return result;
        result = TryGetTypeFromAllReferencedAssemblies(typeName);
        return result;
    }
    private static readonly Dictionary<string, Type?> _typeCache = new Dictionary<string, Type?>();
    public static Type? LoadTypeCached(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;
        if (_typeCache.TryGetValue(typeName, out Type? cachedType))
            return cachedType;
        Type? type = LoadType(typeName);
        _typeCache[typeName] = type;
        return type;
    }
    #region 加载策略实现
    private static Type? TryGetTypeSimple(string typeName)
    {
        try
        {
            Type type = Type.GetType(typeName);
            if (type != null) return type;
            type = Type.GetType(typeName + ", " + GetAssemblyNameForType(typeName));
            if (type != null) return type;
        }
        catch
        {
        }
        return null;
    }
    private static Type? TryGetTypeFromLoadedAssemblies(string typeName)
    {
        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;
                    type = assembly.GetTypes()
                        .FirstOrDefault(t => t.FullName?.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true);
                    if (type != null) return type;
                }
                catch
                {
                    continue;
                }
            }
        }
        catch { }
        return null;
    }
    private static Type? TryGetTypeFromCommonAssemblies(string typeName)
    {
        string[] commonAssemblies = new[]
        {
            "mscorlib",
            "System",
            "System.Core",
            "System.Private.CoreLib",
            "System.Runtime",
            "netstandard",
            GetAssemblyNameForType(typeName)
        };
        if (IsNetCore())
        {
            commonAssemblies = commonAssemblies.Concat(new[]
            {
                "System.Diagnostics.Process",
                "System.Collections",
                "System.Linq",
                "System.Text.RegularExpressions",
                "System.IO.FileSystem",
                "System.Net.Primitives"
            }).ToArray();
        }
        commonAssemblies = commonAssemblies.Distinct().ToArray();
        foreach (var assemblyName in commonAssemblies)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyName)) continue;
                Assembly assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(typeName);
                if (type != null) return type;
                type = assembly.GetTypes()
                    .FirstOrDefault(t => t.FullName == typeName || t.Name == GetShortName(typeName));
                if (type != null) return type;
            }
            catch
            {
                continue;
            }
        }
        return null;
    }
    private static Type? TryGetTypeFromAllAssembliesInDirectory(string typeName)
    {
        try
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dllFiles = Directory.GetFiles(currentDir, "*.dll");
            foreach (string dllPath in dllFiles)
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllPath);
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    continue;
                }
            }
        }
        catch { }
        return null;
    }
    private static Type? TryGetTypeFromAllReferencedAssemblies(string typeName, HashSet<string>? visitedAssemblies = null)
    {
        try
        {
            if (visitedAssemblies == null)
                visitedAssemblies = new HashSet<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (visitedAssemblies.Contains(assembly.FullName))
                    continue;
                visitedAssemblies.Add(assembly.FullName);
                Type type = assembly.GetType(typeName);
                if (type != null) return type;
                foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
                {
                    if (visitedAssemblies.Contains(referencedAssemblyName.FullName))
                        continue;
                    try
                    {
                        Assembly referencedAssembly = Assembly.Load(referencedAssemblyName);
                        type = TryGetTypeFromAllReferencedAssemblies(typeName, visitedAssemblies);
                        if (type != null) return type;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return null;
    }
    #endregion
    #region 辅助方法
    private static string GetAssemblyNameForType(string typeName)
    {
        var typeAssemblyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.String"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Int32"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Boolean"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.DateTime"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Object"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Diagnostics.Process"] = IsNetCore() ? "System.Diagnostics.Process" : "System",
            ["System.Text.RegularExpressions.Regex"] = IsNetCore() ? "System.Text.RegularExpressions" : "System",
            ["System.Net.Http.HttpClient"] = IsNetCore() ? "System.Net.Http" : "System.Net.Http",
            ["System.IO.File"] = IsNetCore() ? "System.IO.FileSystem" : "mscorlib",
            ["System.Linq.Enumerable"] = IsNetCore() ? "System.Linq" : "System.Core",
            ["System.Xml.XmlDocument"] = IsNetCore() ? "System.Xml.ReaderWriter" : "System.Xml",
        };
        if (typeAssemblyMap.TryGetValue(typeName, out string assemblyName))
            return assemblyName;
        if (typeName.StartsWith("System.Collections.Generic"))
            return IsNetCore() ? "System.Collections" : "mscorlib";
        if (typeName.StartsWith("System.Data"))
            return IsNetCore() ? "System.Data.Common" : "System.Data";
        if (typeName.StartsWith("System.Drawing"))
            return IsNetCore() ? "System.Drawing.Common" : "System.Drawing";
        return null;
    }
    private static string GetShortName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName))
            return fullTypeName;
        int lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot > 0 && lastDot < fullTypeName.Length - 1)
            return fullTypeName.Substring(lastDot + 1);
        return fullTypeName;
    }
    private static bool IsNetCore()
    {
        try
        {
            return Type.GetType("System.Runtime.GCSettings") != null &&
                   Assembly.Load("System.Private.CoreLib") != null;
        }
        catch
        {
            return false;
        }
    }
    #endregion
}
