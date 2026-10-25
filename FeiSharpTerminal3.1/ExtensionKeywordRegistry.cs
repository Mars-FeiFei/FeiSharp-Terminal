using FeiSharpStudio;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace FeiSharpTerminal3._1;

public static class ExtensionKeywordRegistry
{
    private static readonly Dictionary<string, BaseKeyword> Keywords = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<AssemblyLoadContext> LoadContexts = [];
    private static readonly Regex KeywordNamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static bool TryGet(string name, out BaseKeyword keyword) => Keywords.TryGetValue(name, out keyword!);

    public static void Load(string importDirectory)
    {
        string extensionDirectory = Path.Combine(importDirectory, "extension");
        if (!Directory.Exists(extensionDirectory)) return;

        foreach (string filePath in Directory.EnumerateFiles(extensionDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileNameWithoutExtension(filePath).Equals(typeof(BaseKeyword).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var loadContext = new ExtensionLoadContext(filePath);
                Assembly assembly = loadContext.LoadFromAssemblyPath(filePath);
                foreach (Type type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(BaseKeyword).IsAssignableFrom(type)))
                {
                    if (Activator.CreateInstance(type) is not BaseKeyword keyword ||
                        !KeywordNamePattern.IsMatch(keyword.Name) ||
                        FeiSharpKeywords.KeywordMap.ContainsKey(keyword.Name) ||
                        !Keywords.TryAdd(keyword.Name, keyword))
                    {
                        Console.Error.WriteLine($"Ignoring extension keyword in {Path.GetFileName(filePath)} because its name is invalid or already in use.");
                        continue;
                    }

                    FeiSharpKeywords.KeywordMap.Add(keyword.Name, new Token(TokenTypes.Keyword, keyword.Name));
                }
                LoadContexts.Add(loadContext);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Could not load extension {Path.GetFileName(filePath)}: {exception.Message}");
            }
        }
    }

    private sealed class ExtensionLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ExtensionLoadContext(string pluginPath) : base(isCollectible: false) => _resolver = new AssemblyDependencyResolver(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (AssemblyName.ReferenceMatchesDefinition(assemblyName, typeof(BaseKeyword).Assembly.GetName()))
            {
                return typeof(BaseKeyword).Assembly;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }
    }
}
