using System.Collections;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Jint;
using Microsoft.Playwright;
using FeiSharpTerminal3._1;

namespace FeiSharpStudio;

/// <summary>
/// Small, expression-oriented standard library for values that every script
/// needs. Script declarations are resolved before this library, so users can
/// still replace a built-in function or class deliberately.
/// </summary>
internal static class StandardLibrary
{
    private static readonly HashSet<string> GlobalFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "len",
        "typeOf",
        "isNull",
        "range"
    };

    internal static bool IsGlobalFunction(string name) => GlobalFunctions.Contains(name);

    internal static bool IsClass(string name) => name.Equals("math", StringComparison.OrdinalIgnoreCase)
        || name.Equals("file", StringComparison.OrdinalIgnoreCase)
        || name.Equals("directory", StringComparison.OrdinalIgnoreCase)
        || name.Equals("path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("http", StringComparison.OrdinalIgnoreCase)
        || name.Equals("js", StringComparison.OrdinalIgnoreCase)
        || name.Equals("web", StringComparison.OrdinalIgnoreCase);

    internal static object? InvokeGlobal(string name, IReadOnlyList<object?> args)
    {
        if (name.Equals("len", StringComparison.OrdinalIgnoreCase))
        {
            RequireArgumentCount(name, args, 1);
            return GetLength(args[0]);
        }
        if (name.Equals("typeOf", StringComparison.OrdinalIgnoreCase))
        {
            RequireArgumentCount(name, args, 1);
            return GetTypeName(args[0]);
        }
        if (name.Equals("isNull", StringComparison.OrdinalIgnoreCase))
        {
            RequireArgumentCount(name, args, 1);
            return args[0] is null;
        }
        if (name.Equals("range", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count is < 1 or > 3)
                throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, "range expects 1 to 3 arguments.");

            int start = args.Count == 1 ? 0 : ToInt(args[0], "range");
            int end = ToInt(args.Count == 1 ? args[0] : args[1], "range");
            int step = args.Count == 3 ? ToInt(args[2], "range") : start <= end ? 1 : -1;
            if (step == 0)
                throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, "range step cannot be zero.");

            var values = new List<int>();
            if (step > 0)
            {
                for (int value = start; value < end; value = checked(value + step))
                    values.Add(value);
            }
            else
            {
                for (int value = start; value > end; value = checked(value + step))
                    values.Add(value);
            }
            return values;
        }
        throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Unknown standard function: {name}.");
    }

    internal static object? GetStaticMember(string className, string memberName)
    {
        string key = $"{className}.{memberName}".ToLowerInvariant();
        return key switch
        {
            "math.pi" => Math.PI,
            "math.e" => Math.E,
            "math.tau" => Math.Tau,
            "directory.current" => Directory.GetCurrentDirectory(),
            "path.separator" => Path.DirectorySeparatorChar.ToString(),
            "path.temppath" => Path.GetTempPath(),
            _ => throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static member: {className}.{memberName}.")
        };
    }

    internal static object InvokeStatic(string className, string memberName, IReadOnlyList<object?> args)
    {
        try
        {
            className = className.ToLowerInvariant();
            if (className == "math")
                return InvokeMath(memberName, args);
            if (className == "file")
                return InvokeFile(memberName, args);
            if (className == "directory")
                return InvokeDirectory(memberName, args);
            if (className == "path")
                return InvokePath(memberName, args);
            if (className == "http")
                return InvokeHttp(memberName, args);
            if (className == "js")
                return InvokeJavaScript(memberName, args);
            if (className == "web")
                return InvokeWeb(memberName, args);
            throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Unknown standard class: {className}.");
        }
        catch (StandardLibraryException)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.NetworkTimeout, $"Network request timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.NetworkRequestFailed, $"Network request failed: {ex.Message}");
        }
        catch (System.Exception ex) when (className == "http")
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.NetworkRequestFailed, $"Network request failed: {ex.Message}");
        }
        catch (JsonException ex) when (className == "js")
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, $"js JSON operation failed: {ex.Message}");
        }
        catch (System.Exception ex) when (className == "js")
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, $"js operation failed: {ex.Message}");
        }
        catch (System.Exception ex) when (className == "web")
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.NetworkRequestFailed, $"web operation failed: {ex.Message}");
        }
        catch (System.Exception ex) when (className is "file" or "directory" or "path")
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.FileOperationFailed, $"File operation failed: {ex.Message}");
        }
    }

    private static object InvokeMath(string memberName, IReadOnlyList<object?> args)
    {
        string qualifiedName = $"math.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "abs":
                RequireArgumentCount(qualifiedName, args, 1);
                return Math.Abs(ToDouble(args[0], qualifiedName));
            case "sqrt":
                RequireArgumentCount(qualifiedName, args, 1);
                return Math.Sqrt(ToDouble(args[0], qualifiedName));
            case "pow":
                RequireArgumentCount(qualifiedName, args, 2);
                return Math.Pow(ToDouble(args[0], qualifiedName), ToDouble(args[1], qualifiedName));
            case "min":
                RequireArgumentCount(qualifiedName, args, 2);
                return Math.Min(ToDouble(args[0], qualifiedName), ToDouble(args[1], qualifiedName));
            case "max":
                RequireArgumentCount(qualifiedName, args, 2);
                return Math.Max(ToDouble(args[0], qualifiedName), ToDouble(args[1], qualifiedName));
            case "clamp":
                RequireArgumentCount(qualifiedName, args, 3);
                return Math.Clamp(ToDouble(args[0], qualifiedName), ToDouble(args[1], qualifiedName), ToDouble(args[2], qualifiedName));
            case "round":
                if (args.Count is < 1 or > 2)
                    throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, $"{qualifiedName} expects 1 or 2 arguments.");
                return args.Count == 1
                    ? Math.Round(ToDouble(args[0], qualifiedName))
                    : Math.Round(ToDouble(args[0], qualifiedName), ToInt(args[1], qualifiedName));
            case "floor":
                RequireArgumentCount(qualifiedName, args, 1);
                return Math.Floor(ToDouble(args[0], qualifiedName));
            case "ceiling":
                RequireArgumentCount(qualifiedName, args, 1);
                return Math.Ceiling(ToDouble(args[0], qualifiedName));
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {qualifiedName}.");
        }
    }

    private static object InvokeFile(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"file.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "exists":
                RequireArgumentCount(apiName, args, 1);
                return File.Exists(ToText(args[0], apiName));
            case "readtext":
                RequireArgumentCount(apiName, args, 1);
                return File.ReadAllText(ToText(args[0], apiName));
            case "writetext":
                RequireArgumentCount(apiName, args, 2);
                File.WriteAllText(ToText(args[0], apiName), ToText(args[1], apiName));
                return true;
            case "appendtext":
                RequireArgumentCount(apiName, args, 2);
                File.AppendAllText(ToText(args[0], apiName), ToText(args[1], apiName));
                return true;
            case "delete":
                RequireArgumentCount(apiName, args, 1);
                File.Delete(ToText(args[0], apiName));
                return true;
            case "copy":
                RequireArgumentRange(apiName, args, 2, 3);
                File.Copy(ToText(args[0], apiName), ToText(args[1], apiName), args.Count == 3 && ToBool(args[2], apiName));
                return true;
            case "move":
                RequireArgumentRange(apiName, args, 2, 3);
                File.Move(ToText(args[0], apiName), ToText(args[1], apiName), args.Count == 3 && ToBool(args[2], apiName));
                return true;
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static object InvokeDirectory(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"directory.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "exists":
                RequireArgumentCount(apiName, args, 1);
                return Directory.Exists(ToText(args[0], apiName));
            case "create":
                RequireArgumentCount(apiName, args, 1);
                return Directory.CreateDirectory(ToText(args[0], apiName)).FullName;
            case "delete":
                RequireArgumentRange(apiName, args, 1, 2);
                Directory.Delete(ToText(args[0], apiName), args.Count == 2 && ToBool(args[1], apiName));
                return true;
            case "getfiles":
                RequireArgumentRange(apiName, args, 1, 2);
                return Directory.GetFiles(ToText(args[0], apiName), args.Count == 2 ? ToText(args[1], apiName) : "*");
            case "getdirectories":
                RequireArgumentRange(apiName, args, 1, 2);
                return Directory.GetDirectories(ToText(args[0], apiName), args.Count == 2 ? ToText(args[1], apiName) : "*");
            case "setcurrent":
                RequireArgumentCount(apiName, args, 1);
                Directory.SetCurrentDirectory(ToText(args[0], apiName));
                return Directory.GetCurrentDirectory();
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static object InvokePath(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"path.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "combine":
                RequireArgumentRange(apiName, args, 2, int.MaxValue);
                return Path.Combine(args.Select(arg => ToText(arg, apiName)).ToArray());
            case "getfilename":
                RequireArgumentCount(apiName, args, 1);
                return Path.GetFileName(ToText(args[0], apiName));
            case "getextension":
                RequireArgumentCount(apiName, args, 1);
                return Path.GetExtension(ToText(args[0], apiName));
            case "getdirectoryname":
                RequireArgumentCount(apiName, args, 1);
                return Path.GetDirectoryName(ToText(args[0], apiName)) ?? "";
            case "getfullpath":
                RequireArgumentCount(apiName, args, 1);
                return Path.GetFullPath(ToText(args[0], apiName));
            case "changeextension":
                RequireArgumentCount(apiName, args, 2);
                return Path.ChangeExtension(ToText(args[0], apiName), ToText(args[1], apiName)) ?? "";
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static object InvokeHttp(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"http.{memberName}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        switch (memberName.ToLowerInvariant())
        {
            case "get":
                RequireArgumentCount(apiName, args, 1);
                return client.GetStringAsync(ToText(args[0], apiName)).GetAwaiter().GetResult();
            case "getstatus":
                RequireArgumentCount(apiName, args, 1);
                using (HttpResponseMessage response = client.GetAsync(ToText(args[0], apiName)).GetAwaiter().GetResult())
                    return (double)(int)response.StatusCode;
            case "getjson":
                RequireArgumentCount(apiName, args, 1);
                using (HttpResponseMessage jsonResponse = client.GetAsync(ToText(args[0], apiName)).GetAwaiter().GetResult())
                {
                    jsonResponse.EnsureSuccessStatusCode();
                    return jsonResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            case "post":
                RequireArgumentRange(apiName, args, 2, 3);
                using (var content = new StringContent(ToText(args[1], apiName), Encoding.UTF8, args.Count == 3 ? ToText(args[2], apiName) : "application/json"))
            using (HttpResponseMessage response = client.PostAsync(ToText(args[0], apiName), content).GetAwaiter().GetResult())
                    return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            case "postjson":
                RequireArgumentCount(apiName, args, 2);
                using (var jsonContent = new StringContent(ToText(args[1], apiName), Encoding.UTF8, "application/json"))
                using (HttpResponseMessage jsonPostResponse = client.PostAsync(ToText(args[0], apiName), jsonContent).GetAwaiter().GetResult())
                {
                    jsonPostResponse.EnsureSuccessStatusCode();
                    return jsonPostResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            case "request":
                RequireArgumentRange(apiName, args, 2, 4);
                string method = ToText(args[1], apiName).ToUpperInvariant();
                using (var request = new HttpRequestMessage(new HttpMethod(method), ToText(args[0], apiName)))
                {
                    if (args.Count >= 3 && args[2] is not null)
                        request.Content = new StringContent(ToText(args[2], apiName), Encoding.UTF8,
                            args.Count == 4 ? ToText(args[3], apiName) : "application/json");
                    using HttpResponseMessage requestResponse = client.SendAsync(request).GetAwaiter().GetResult();
                    return requestResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static object? InvokeJavaScript(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"js.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "stringify":
                RequireArgumentCount(apiName, args, 1);
                return JsonSerializer.Serialize(args[0]);
            case "eval":
                RequireArgumentCount(apiName, args, 1);
                return new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(1)))
                    .Evaluate(ToText(args[0], apiName)).ToObject();
            case "parse":
                RequireArgumentCount(apiName, args, 1);
                using (JsonDocument document = JsonDocument.Parse(ToText(args[0], apiName)))
                    return ConvertJsonElement(document.RootElement);
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out long integer) => (double)integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null
    };

    private static IPlaywright? _playwright;
    private static IBrowser? _browser;
    private static IPage? _page;

    private static object? InvokeWeb(string memberName, IReadOnlyList<object?> args)
    {
        string apiName = $"web.{memberName}";
        switch (memberName.ToLowerInvariant())
        {
            case "open":
                RequireArgumentRange(apiName, args, 1, 2);
                EnsureBrowser(args.Count == 2 ? ToBool(args[1], apiName) : false);
                return Page().GotoAsync(ToText(args[0], apiName)).GetAwaiter().GetResult()?.Url;
            case "navigate":
                RequireArgumentCount(apiName, args, 1);
                return Page().GotoAsync(ToText(args[0], apiName)).GetAwaiter().GetResult()?.Url;
            case "click":
                RequireArgumentCount(apiName, args, 1);
                Page().Locator(ToText(args[0], apiName)).ClickAsync().GetAwaiter().GetResult();
                return true;
            case "fill":
                RequireArgumentCount(apiName, args, 2);
                Page().Locator(ToText(args[0], apiName)).FillAsync(ToText(args[1], apiName)).GetAwaiter().GetResult();
                return true;
            case "text":
                RequireArgumentCount(apiName, args, 1);
                return Page().Locator(ToText(args[0], apiName)).InnerTextAsync().GetAwaiter().GetResult();
            case "html":
                RequireArgumentCount(apiName, args, 1);
                return Page().Locator(ToText(args[0], apiName)).InnerHTMLAsync().GetAwaiter().GetResult();
            case "eval":
                RequireArgumentCount(apiName, args, 1);
                return Page().EvaluateAsync<object>(ToText(args[0], apiName)).GetAwaiter().GetResult();
            case "screenshot":
                RequireArgumentCount(apiName, args, 1);
                Page().ScreenshotAsync(new PageScreenshotOptions { Path = ToText(args[0], apiName), FullPage = true }).GetAwaiter().GetResult();
                return true;
            case "close":
                RequireArgumentCount(apiName, args, 0);
                CloseBrowser();
                return true;
            default:
                throw new StandardLibraryException(FeiSharpErrorCodes.UndefinedSymbol, $"Undefined static method: {apiName}.");
        }
    }

    private static IPage Page()
    {
        if (_page is null)
            EnsureBrowser(false);
        return _page!;
    }

    private static void EnsureBrowser(bool headless)
    {
        if (_page is not null)
            return;
        _playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
        _browser = _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = headless
        }).GetAwaiter().GetResult();
        _page = _browser.NewPageAsync().GetAwaiter().GetResult();
    }

    private static void CloseBrowser()
    {
        _page?.CloseAsync().GetAwaiter().GetResult();
        _browser?.CloseAsync().GetAwaiter().GetResult();
        _playwright?.Dispose();
        _page = null;
        _browser = null;
        _playwright = null;
    }

    private static int GetLength(object? value) => value switch
    {
        null => 0,
        string text => text.Length,
        Array array => array.Length,
        ICollection collection => collection.Count,
        _ => throw new StandardLibraryException(FeiSharpErrorCodes.TypeMismatch, $"len does not support values of type '{value.GetType().Name}'.")
    };

    private static string GetTypeName(object? value) => value switch
    {
        null => "null",
        string => "string",
        char => "char",
        bool => "boolean",
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
        Array => "array",
        IDictionary => "dictionary",
        IList => "list",
        _ => value.GetType().Name
    };

    private static int ToInt(object? value, string apiName)
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.TypeMismatch, $"{apiName} expects an integer argument.");
        }
    }

    private static double ToDouble(object? value, string apiName)
    {
        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            throw new StandardLibraryException(FeiSharpErrorCodes.TypeMismatch, $"{apiName} expects a numeric argument.");
        }
    }

    private static string ToText(object? value, string apiName)
    {
        if (value is null)
            throw new StandardLibraryException(FeiSharpErrorCodes.TypeMismatch, $"{apiName} expects a non-null string argument.");
        return value.ToString()!;
    }

    private static bool ToBool(object? value, string apiName)
    {
        if (value is bool boolean)
            return boolean;
        if (bool.TryParse(value?.ToString(), out bool parsed))
            return parsed;
        throw new StandardLibraryException(FeiSharpErrorCodes.TypeMismatch, $"{apiName} expects a boolean argument.");
    }

    private static void RequireArgumentCount(string apiName, IReadOnlyList<object?> args, int expected)
    {
        if (args.Count != expected)
            throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, $"{apiName} expects {expected} argument(s), but received {args.Count}.");
    }

    private static void RequireArgumentRange(string apiName, IReadOnlyList<object?> args, int minimum, int maximum)
    {
        if (args.Count < minimum || args.Count > maximum)
            throw new StandardLibraryException(FeiSharpErrorCodes.InvalidArgument, $"{apiName} expects {minimum} to {maximum} argument(s), but received {args.Count}.");
    }
}

internal sealed class StandardLibraryException : System.Exception
{
    internal string ErrorCode { get; }

    internal StandardLibraryException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
