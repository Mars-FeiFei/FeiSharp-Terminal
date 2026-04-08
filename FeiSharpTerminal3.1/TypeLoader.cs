using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;

public static class TypeLoader
{
    /// <summary>
    /// 万能类型加载器 - 通过多种策略尝试加载任何类型
    /// </summary>
    /// <param name="typeName">类型全名（如 "System.Diagnostics.Process"）</param>
    /// <returns>找到的Type对象，如果找不到则返回null</returns>
    public static Type LoadType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        Type result = null;

        // 策略1: 直接使用 Type.GetType (最简单快速)
        result = TryGetTypeSimple(typeName);
        if (result != null) return result;

        // 策略2: 遍历所有已加载的程序集
        result = TryGetTypeFromLoadedAssemblies(typeName);
        if (result != null) return result;

        // 策略3: 尝试常见程序集名称
        result = TryGetTypeFromCommonAssemblies(typeName);
        if (result != null) return result;

        // 策略4: 尝试从当前目录的所有DLL加载
        result = TryGetTypeFromAllAssembliesInDirectory(typeName);
        if (result != null) return result;

        // 策略5: 递归搜索所有可访问的程序集
        result = TryGetTypeFromAllReferencedAssemblies(typeName);

        return result;
    }

    /// <summary>
    /// 万能类型加载器（带缓存，推荐使用）
    /// </summary>
    private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

    public static Type LoadTypeCached(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (_typeCache.TryGetValue(typeName, out Type cachedType))
            return cachedType;

        Type type = LoadType(typeName);
        _typeCache[typeName] = type;
        return type;
    }

    #region 加载策略实现

    private static Type TryGetTypeSimple(string typeName)
    {
        try
        {
            // 直接使用 Type.GetType
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // 尝试带程序集限定的名称
            type = Type.GetType(typeName + ", " + GetAssemblyNameForType(typeName));
            if (type != null) return type;
        }
        catch
        {
            // 忽略异常，继续尝试其他方法
        }
        return null;
    }

    private static Type TryGetTypeFromLoadedAssemblies(string typeName)
    {
        try
        {
            // 获取所有已加载的程序集
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;

                    // 尝试忽略大小写
                    type = assembly.GetTypes()
                        .FirstOrDefault(t => t.FullName?.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true);
                    if (type != null) return type;
                }
                catch
                {
                    continue; // 跳过有问题的程序集
                }
            }
        }
        catch { }
        return null;
    }

    private static Type TryGetTypeFromCommonAssemblies(string typeName)
    {
        // 常见的基础程序集列表
        string[] commonAssemblies = new[]
        {
            "mscorlib",
            "System",
            "System.Core",
            "System.Private.CoreLib",
            "System.Runtime",
            "netstandard",
            GetAssemblyNameForType(typeName) // 尝试特定类型的程序集
        };

        // 添加 .NET Core / .NET 5+ 的常见程序集
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

        // 去重
        commonAssemblies = commonAssemblies.Distinct().ToArray();

        foreach (var assemblyName in commonAssemblies)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyName)) continue;

                Assembly assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(typeName);
                if (type != null) return type;

                // 尝试从程序集中搜索
                type = assembly.GetTypes()
                    .FirstOrDefault(t => t.FullName == typeName || t.Name == GetShortName(typeName));
                if (type != null) return type;
            }
            catch
            {
                continue; // 尝试下一个程序集
            }
        }
        return null;
    }

    private static Type TryGetTypeFromAllAssembliesInDirectory(string typeName)
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

    private static Type TryGetTypeFromAllReferencedAssemblies(string typeName, HashSet<string> visitedAssemblies = null)
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

                // 检查当前程序集
                Type type = assembly.GetType(typeName);
                if (type != null) return type;

                // 递归检查引用的程序集
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
        // 类型到程序集的映射
        var typeAssemblyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 常用类型映射
            ["System.String"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Int32"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Boolean"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.DateTime"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",
            ["System.Object"] = IsNetCore() ? "System.Private.CoreLib" : "mscorlib",

            // 特定类型映射
            ["System.Diagnostics.Process"] = IsNetCore() ? "System.Diagnostics.Process" : "System",
            ["System.Text.RegularExpressions.Regex"] = IsNetCore() ? "System.Text.RegularExpressions" : "System",
            ["System.Net.Http.HttpClient"] = IsNetCore() ? "System.Net.Http" : "System.Net.Http",
            ["System.IO.File"] = IsNetCore() ? "System.IO.FileSystem" : "mscorlib",
            ["System.Linq.Enumerable"] = IsNetCore() ? "System.Linq" : "System.Core",
            ["System.Xml.XmlDocument"] = IsNetCore() ? "System.Xml.ReaderWriter" : "System.Xml",
        };

        if (typeAssemblyMap.TryGetValue(typeName, out string assemblyName))
            return assemblyName;

        // 从命名空间推测程序集
        if (typeName.StartsWith("System.Collections.Generic"))
            return IsNetCore() ? "System.Collections" : "mscorlib";

        if (typeName.StartsWith("System.Data"))
            return IsNetCore() ? "System.Data.Common" : "System.Data";

        if (typeName.StartsWith("System.Drawing"))
            return IsNetCore() ? "System.Drawing.Common" : "System.Drawing";

        // 默认返回空，让系统自动查找
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
            // 检测是否是 .NET Core / .NET 5+
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