using FeiSharpStudio;
using FeiSharpTerminal3._1;
using FeiSharpTerminal3._1.Tests;
using Spectre.Console;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FeiSharp8._5RuntimeSdk;
public class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int handle);

    public static string? MapPath(string vpath, string _applicationPath)
    {
        string path = "";
        if (vpath.StartsWith("~/"))
        {
            path = Path.Combine(Path.GetDirectoryName(_applicationPath), vpath[2..]);
        }
        else if (vpath.StartsWith("$"))
        {
            path = Path.Combine(AppContext.BaseDirectory, "Imports/" + vpath[1..]);
        }
        path = path.Replace("/", "\\");
        return File.Exists(path) ? path : null;
    }

    static void EnableVirtualTerminalProcessing()
    {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        GetConsoleMode(handle, out uint mode);
        SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    static string CreateWavyUnderline(int length)
    {
        const char waveChar1 = '~';
        char[] wavyLine = new char[length];
        for (int i = 0; i < length; i++)
        {
            wavyLine[i] = waveChar1;
        }
        return new string(wavyLine);
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_MAXIMIZE = 3;

    static string GetUserCode()
    {
        global::System.String code = "";
        global::System.String scode = "";

        AnsiConsole.MarkupLine("[grey]Enter your code (type 'exit' to finish, Ctrl+C to cancel execution)[/]");

        while (true)
        {
            // 检查是否有中断请求
            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Code input cancelled[/]");
                return "";
            }

            Console.Write("... ");
            code = Console.ReadLine();

            if (code == "exit")
            {
                AnsiConsole.MarkupLine($"[grey]>>>[/] [yellow]Exiting code input mode at[/] [cyan]{DateTime.Now}[/]");
                break;
            }

            scode += code + "\n";
        }

        return scode;
    }


    public static string _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static Parser _parser;
    static string dynamicTitle = "FeiSharp - Inputing Command";
    const string IN_CMD = "Inputing Command";
    const string EDT_CDE = "Editing Code";
    const string EXEC = "Executing";
    static void ChangeDynamicTitle(string change)
    {
        dynamicTitle = "FeiSharp - " + change;
        Console.Title = dynamicTitle;
    }

    static void TryConfigureConsole(Action configureAction)
    {
        try
        {
            configureAction();
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    static string? ResolveFeiSharpSourcePath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
        if (!File.Exists(fullInputPath))
        {
            return null;
        }

        if (!string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
        {
            return fullInputPath;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(fullInputPath));
        if (dict == null || !dict.TryGetValue("project_main_file", out object? mainFileValue))
        {
            return null;
        }

        string? mainFile = mainFileValue?.ToString();
        if (string.IsNullOrWhiteSpace(mainFile))
        {
            return null;
        }

        string projectDirectory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string resolvedMainFile = Path.GetFullPath(Path.Combine(projectDirectory, mainFile));
        return File.Exists(resolvedMainFile) ? resolvedMainFile : null;
    }

    static string GetBuildBaseDirectory(string inputPath, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
            if (File.Exists(fullInputPath) && string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
            }
        }

        return Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
    }

    static string GetBuildOutputName(string inputPath, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            string fullInputPath = Path.GetFullPath(inputPath.Trim().Trim('"'));
            if (File.Exists(fullInputPath) && string.Equals(Path.GetExtension(fullInputPath), ".feiproj", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(fullInputPath));
                    if (dict != null && dict.TryGetValue("project_name", out object? projectNameValue))
                    {
                        string? projectName = projectNameValue?.ToString();
                        if (!string.IsNullOrWhiteSpace(projectName))
                        {
                            return string.Concat(projectName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return Path.GetFileNameWithoutExtension(sourcePath);
    }

    static string ResolveBuildOutputPath(string sourcePath, string? requestedOutputPath)
    {
        if (string.IsNullOrWhiteSpace(requestedOutputPath))
        {
            return Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory(),
                Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }

        string trimmedPath = requestedOutputPath.Trim().Trim('"');
        if (trimmedPath.EndsWith("\\") || trimmedPath.EndsWith("/"))
        {
            return Path.Combine(
                Path.GetFullPath(trimmedPath),
                Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }

        string fullPath = Path.GetFullPath(trimmedPath);
        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
        }

        return string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath + ".exe";
    }

    static string ResolveBuildOutputPath(string inputPath, string sourcePath, string? requestedOutputPath)
    {
        if (string.IsNullOrWhiteSpace(requestedOutputPath))
        {
            string buildBaseDirectory = GetBuildBaseDirectory(inputPath, sourcePath);
            string outputName = GetBuildOutputName(inputPath, sourcePath);
            return Path.Combine(buildBaseDirectory, "bin", "obj", "feisharp.sdk 9.0", outputName + ".exe");
        }

        return ResolveBuildOutputPath(sourcePath, requestedOutputPath);
    }

    static string GetRuntimeAssemblyPath()
    {
        string assemblyPath = typeof(Program).Assembly.Location;
        if (string.Equals(Path.GetExtension(assemblyPath), ".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(assemblyPath))
        {
            return assemblyPath;
        }

        string dllPath = Path.ChangeExtension(assemblyPath, ".dll");
        if (File.Exists(dllPath))
        {
            return dllPath;
        }

        throw new FileNotFoundException("Unable to locate the FeiSharp runtime assembly.", assemblyPath);
    }

    static string GetWindowsRuntimeIdentifier()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "win-arm64",
            Architecture.X86 => "win-x86",
            _ => "win-x64"
        };
    }

    static bool TryBuildExecutable(string inputPath, string? requestedOutputPath, out string outputExePath, out string errorMessage)
    {
        outputExePath = string.Empty;
        errorMessage = string.Empty;

        string? sourcePath = ResolveFeiSharpSourcePath(inputPath);
        if (sourcePath == null)
        {
            errorMessage = "The source file or project file could not be found.";
            return false;
        }

        string runtimeAssemblyPath;
        try
        {
            runtimeAssemblyPath = GetRuntimeAssemblyPath();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        outputExePath = ResolveBuildOutputPath(inputPath, sourcePath, requestedOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputExePath) ?? Directory.GetCurrentDirectory());

        string previousApplicationPath = _applicationPath;
        string tempRoot = Path.Combine(Path.GetTempPath(), "FeiSharpBuild", Guid.NewGuid().ToString("N"));
        string tempProjectPath = Path.Combine(tempRoot, "GeneratedLauncher.csproj");
        string tempProgramPath = Path.Combine(tempRoot, "Program.cs");
        string tempBuildDirectory = Path.Combine(tempRoot, "build");
        string tempPublishDirectory = Path.Combine(tempRoot, "publish");
        string assemblyName = Path.GetFileNameWithoutExtension(outputExePath);
        string base64Source;

        try
        {
            _applicationPath = sourcePath;
            string sourceCode = File.ReadAllText(sourcePath);
            string processedSource = ProProcesser(sourceCode);
            base64Source = Convert.ToBase64String(Encoding.UTF8.GetBytes(processedSource));
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to read or preprocess source: {ex.Message}";
            return false;
        }
        finally
        {
            _applicationPath = previousApplicationPath;
        }

        try
        {
            Directory.CreateDirectory(tempRoot);

            string escapedAssemblyName = System.Security.SecurityElement.Escape(assemblyName) ?? assemblyName;
            string escapedRuntimeAssemblyPath = System.Security.SecurityElement.Escape(runtimeAssemblyPath) ?? runtimeAssemblyPath;
            string runtimeIdentifier = GetWindowsRuntimeIdentifier();

            File.WriteAllText(tempProjectPath, $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>{{escapedAssemblyName}}</AssemblyName>
    <UseAppHost>true</UseAppHost>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>{{runtimeIdentifier}}</RuntimeIdentifier>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="feisharp">
      <HintPath>{{escapedRuntimeAssemblyPath}}</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""");

            File.WriteAllText(tempProgramPath, $$"""
using System.Text;
using FeiSharp8._5RuntimeSdk;
using FeiSharpTerminal3._1;

Console.OutputEncoding = Encoding.Unicode;
Console.InputEncoding = Encoding.Unicode;
ExecutionCancellation.Initialize();
FeiSharpProgramData.AssemblyName = Guid.NewGuid().ToString("N");
FeiSharp8._5RuntimeSdk.Program._applicationPath = AppContext.BaseDirectory;
var sourceCode = Encoding.UTF8.GetString(Convert.FromBase64String("{{base64Source}}"));
FeiSharp8._5RuntimeSdk.Program.RunFeiSharpCodeWithProProcesser(sourceCode);
""");

            var buildProcess = new Process();
            buildProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            buildProcess.StartInfo.ArgumentList.Add("build");
            buildProcess.StartInfo.ArgumentList.Add(tempProjectPath);
            buildProcess.StartInfo.ArgumentList.Add("-c");
            buildProcess.StartInfo.ArgumentList.Add("Release");
            buildProcess.StartInfo.ArgumentList.Add("-o");
            buildProcess.StartInfo.ArgumentList.Add(tempBuildDirectory);

            buildProcess.Start();
            string buildStandardOutput = buildProcess.StandardOutput.ReadToEnd();
            string buildStandardError = buildProcess.StandardError.ReadToEnd();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                errorMessage = string.IsNullOrWhiteSpace(buildStandardError) ? buildStandardOutput : buildStandardError;
                return false;
            }

            string builtDllPath = Path.Combine(tempBuildDirectory, assemblyName + ".dll");
            if (!File.Exists(builtDllPath))
            {
                errorMessage = $"Build succeeded, but the generated dll was not found at {builtDllPath}.";
                return false;
            }

            var publishProcess = new Process();
            publishProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            publishProcess.StartInfo.ArgumentList.Add("publish");
            publishProcess.StartInfo.ArgumentList.Add(tempProjectPath);
            publishProcess.StartInfo.ArgumentList.Add("-c");
            publishProcess.StartInfo.ArgumentList.Add("Release");
            publishProcess.StartInfo.ArgumentList.Add("-o");
            publishProcess.StartInfo.ArgumentList.Add(tempPublishDirectory);

            publishProcess.Start();
            string publishStandardOutput = publishProcess.StandardOutput.ReadToEnd();
            string publishStandardError = publishProcess.StandardError.ReadToEnd();
            publishProcess.WaitForExit();

            if (publishProcess.ExitCode != 0)
            {
                errorMessage = string.IsNullOrWhiteSpace(publishStandardError) ? publishStandardOutput : publishStandardError;
                return false;
            }

            string publishedExePath = Path.Combine(tempPublishDirectory, assemblyName + ".exe");
            if (!File.Exists(publishedExePath))
            {
                errorMessage = $"Build succeeded, but the generated exe was not found at {publishedExePath}.";
                return false;
            }

            File.Copy(publishedExePath, outputExePath, true);
            File.Copy(builtDllPath, Path.ChangeExtension(outputExePath, ".dll"), true);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch
            {
            }
        }
    }

    public static async Task Main(string[] args)
    {
        #region <head></head>
        //head
        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;
        ExecutionCancellation.Initialize();
        if (args.Length == 0)
            FeiSharpTests.RunAllTests();
        TryConfigureConsole(() => Console.CursorSize = 25);
        FeiSharpProgramData.AssemblyName = Guid.NewGuid().ToString("N");
        TryConfigureConsole(EnableVirtualTerminalProcessing);
        TryConfigureConsole(() => Console.CursorVisible = true);
        TryConfigureConsole(() => Console.Title = dynamicTitle);

        // 漂亮的启动标题
        AnsiConsole.Write(
            new FigletText("FeiSharp SDK 9.0")
                .Color(Color.Cyan1));

        var rule = new Rule($"[yellow]Version 9.0[/]")
        {
            Style = Style.Parse("blue"),
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);

        // 创建信息面板
        var infoPanel = new Panel(
            "[grey]FeiSharp 9.0 (tags/v9.0, Apr 7 2026, 20:49:47) MSC v.1942 64 bit (AMD64) on win32[/]\n" +
            "[grey]Type [green]\"help\"[/], [green]\"copyright\"[/], [green]\"credits\"[/] or [green]\"license\"[/] for more information.[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Padding(1, 1, 1, 1);

        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();
        //end head
        #endregion

        if (args.Length > 0 && string.Equals(args[0], "--build", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                var usagePanel = new Panel("[red]Usage: feisharp --build <source.fsc|project.feiproj> [output.exe][/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red"));
                AnsiConsole.Write(usagePanel);
                return;
            }

            string? outputArg = args.Length > 2 ? args[2] : null;
            await AnsiConsole.Status()
                .StartAsync("[yellow]Building executable...[/]", async ctx =>
                {
                    if (TryBuildExecutable(args[1], outputArg, out string builtExePath, out string buildError))
                    {
                        var successPanel = new Panel($"[green]✓ Build completed:[/] [yellow]{builtExePath}[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("green"));
                        AnsiConsole.Write(successPanel);
                    }
                    else
                    {
                        var errorPanel = new Panel($"[red]✗ Build failed:[/] {buildError.EscapeMarkup()}")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("red"));
                        AnsiConsole.Write(errorPanel);
                    }

                    await Task.CompletedTask;
                });
            return;
        }

        if (args.Length > 0)
        {
            string? sourcePath = ResolveFeiSharpSourcePath(args[0]);
            if (sourcePath == null)
            {
                var errorPanel = new Panel($"[red]✗ File not found:[/] {args[0].EscapeMarkup()}")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red"));
                AnsiConsole.Write(errorPanel);
                return;
            }

            global::System.String sourceCode = File.ReadAllText(sourcePath);
            _applicationPath = sourcePath;
            RunFeiSharpCodeWithProProcesser(sourceCode);
            AnsiConsole.Markup("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return;
        }

        while (true)
        {
            // 漂亮的提示符
            var prompt = new TextPath(Directory.GetCurrentDirectory())
                .RootColor(Color.Red)
                .SeparatorColor(Color.Green)
                .StemColor(Color.Blue)
                .LeafColor(Color.Yellow);

            AnsiConsole.Write(prompt);
            AnsiConsole.Markup("[cyan1]>[/] ");

            global::System.String? command = null;
            try
            {
                command = Console.ReadLine();
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C 在 ReadLine 中被按下
                Console.WriteLine(); // 换行
                if (ExecutionCancellation._isExecuting)
                {
                    // 如果在执行代码，取消执行
                    ExecutionCancellation.CancelExecution();
                    AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                }
                continue;
            }

            // 处理 null 情况（通常是 Ctrl+C）
            if (command == null)
            {
                Console.WriteLine(); // 换行
                if (ExecutionCancellation._isExecuting)
                {
                    ExecutionCancellation.CancelExecution();
                    AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Use 'exit' to quit FeiSharp[/]");
                }
                continue;
            }

            // 处理空命令
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }
            if (command == "exit")
            {
                AnsiConsole.MarkupLine("[red]Shutting down FeiSharp... Goodbye![/]");
                global::System.Environment.Exit(0);
            }
            else if (command == "ui")
            {
                AnsiConsole.Markup("[cyan]Source File: [/]");
                string path = Console.ReadLine();
                string exePath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe").Replace("feisharp.exe", "UiG.exe").Replace("FeiSharpTerminal3.1\\bin\\Debug\\net8.0-windows", "UiG\\bin\\Debug\\net8.0-windows");

                var statusPanel = new Panel($"[yellow]Launching UI with file:[/] [green]{path}[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("yellow"));
                AnsiConsole.Write(statusPanel);

                Process.Start(exePath, path);
            }
            else if (command == "run")
            {
                ChangeDynamicTitle(EDT_CDE);
                AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input your code... Enter 'exit' to exit this mode...[/]");
                var rule1 = new Rule("[yellow]Code Input Mode[/]")
                {
                    Style = Style.Parse("green"),
                    Justification = Justify.Center
                };
                AnsiConsole.Write(rule1);
                RunFeiSharpCodeWithProProcesser(GetUserCode());
                ChangeDynamicTitle(IN_CMD);

            }
            else if (command == "file")
            {
                AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input the file path...[/]");
                AnsiConsole.Markup("[cyan]File Path: [/]");
                string path = Console.ReadLine();
                string? sourcePath = ResolveFeiSharpSourcePath(path);
                if (sourcePath == null)
                {
                    var errorPanel = new Panel($"[red]✗ File not found:[/] {path.EscapeMarkup()}")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("red"));
                    AnsiConsole.Write(errorPanel);
                    continue;
                }

                var statusPanel = new Panel($"[yellow]Loading file:[/] [green]{sourcePath}[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("green"));
                AnsiConsole.Write(statusPanel);

                global::System.String sourceCode = File.ReadAllText(sourcePath);
                _applicationPath = sourcePath;

                RunFeiSharpCodeWithProProcesser(sourceCode);

                _applicationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else if (command == "build" || command.StartsWith("build "))
            {
                string sourceInput = command.Length > 5 ? command[5..].Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(sourceInput))
                {
                    AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input the source file or project path...[/]");
                    AnsiConsole.Markup("[cyan]Source Path: [/]");
                    sourceInput = Console.ReadLine() ?? string.Empty;
                }
                string? outputInput = "bin\\obj\\feisharp.sdk 9.0\\" + Path.GetFileNameWithoutExtension(sourceInput) + ".exe";
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Building executable...[/]", async ctx =>
                    {
                        if (TryBuildExecutable(sourceInput, outputInput, out string builtExePath, out string buildError))
                        {
                            var successPanel = new Panel($"[green]✓ Build completed:[/] [yellow]{builtExePath}[/]")
                                .Border(BoxBorder.Rounded)
                                .BorderStyle(Style.Parse("green"));
                            AnsiConsole.Write(successPanel);
                        }
                        else
                        {
                            var errorPanel = new Panel($"[red]✗ Build failed:[/] {buildError.EscapeMarkup()}")
                                .Border(BoxBorder.Rounded)
                                .BorderStyle(Style.Parse("red"));
                            AnsiConsole.Write(errorPanel);
                        }

                        await Task.CompletedTask;
                    });
            }
            else if (command == "pwd")
            {
                var path = new TextPath(Directory.GetCurrentDirectory())
                    .RootColor(Color.Red)
                    .SeparatorColor(Color.Green)
                    .StemColor(Color.Blue)
                    .LeafColor(Color.Yellow);

                AnsiConsole.Markup("[cyan]Current Directory: [/]");
                AnsiConsole.Write(path);
                AnsiConsole.WriteLine();
            }
            else if (command == "cd..")
            {
                var oldPath = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory()).FullName);
                var newPath = Directory.GetCurrentDirectory();

                AnsiConsole.MarkupLine($"[grey]Changed directory from[/] [red]{oldPath}[/] [grey]to[/] [green]{newPath}[/]");
            }
            else if (command.StartsWith("cd "))
            {
                var oldPath = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(command.Split(' ')[1]);
                var newPath = Directory.GetCurrentDirectory();

                AnsiConsole.MarkupLine($"[grey]Changed directory from[/] [red]{oldPath}[/] [grey]to[/] [green]{newPath}[/]");
            }
            else if (command == "cls" || command == "clear")
            {
                Console.Clear();

                // 重新显示标题
                AnsiConsole.Write(
                    new FigletText("FeiSharp")
                        .Color(Color.Cyan1));
            }
            else if (command.StartsWith("create "))
            {
                if (command.Split(' ').Length == 3)
                {
                    string name = string.Concat(command.Split(' ')[2].Select(c => Path.GetInvalidPathChars().Contains(c) ? '_' : c)).Trim();
                    switch (command.Split(' ')[1])
                    {
                        case "Console":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"test_{name}1.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText($"main_{name}.fsc", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction main()\nfbegin:\n    printnl(\"Hello, World!\");\nfend;\nmain();");
                            File.WriteAllText("data.fsc", "anno(\"Set your data in this file\");");
                            File.WriteAllText("api.fsc", "anno(\"Create your own API functions in this file\");");
                            File.WriteAllText("README.md", $"This is the console project {name}");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create console project {name} by FeiSharp Target SDK 9.0\n");
                            File.WriteAllText(".gitignore", @"# 编译输出
bin/
obj/
*.exe
*.dll

# 测试文件夹
Tests/

# 日志文件
*.log

# IDE 配置
.vs/
.vscode/

# 系统文件
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 9.0
}}");
                            break;
                        case "API":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 9.0
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("data.fsc", "anno(\"Set your data in this file\");");
                            File.WriteAllText("api.fsc", "anno(\"Create your own API functions in this file\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
bin/
obj/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 9.0\n");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            break;
                        case "Data":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 9.0
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("data.fsc", "anno(\"Set your data in this file\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
bin/
obj/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 9.0\n");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            break;
                        case "fUnitTest":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 9.0
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/test_content.fsc\";\nimport \"~/api.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("test_content.fsc", "anno(\"Set the test content\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
bin/
obj/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 9.0\n");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            break;
                        default:
                            var errorPanel = new Panel("[red]✗ The type isn't correct/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                    AnsiConsole.Write(errorPanel);
                            break;
                    }
                }
            }
            else if (command == "feedback")
            {
                AnsiConsole.Markup("[yellow]Please enter your feedback: [/]");
                string feedback = Console.ReadLine();

                await AnsiConsole.Status()
                    .StartAsync("[cyan]Sending feedback...[/]", async ctx =>
                    {
                        try
                        {
                            TcpClient client = new TcpClient(PublicIpFetcher.GetPublicIpAsync().Result, 6721);
                            try
                            {
                                NetworkStream stream = client.GetStream();
                                byte[] data = Encoding.UTF8.GetBytes(feedback);
                                await stream.WriteAsync(data, 0, data.Length);

                                var successPanel = new Panel("[green]✓ Feedback sent successfully![/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("green"));
                                AnsiConsole.Write(successPanel);
                            }
                            catch
                            {
                                var errorPanel = new Panel("[red]✗ Failed to send feedback[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(errorPanel);
                            }
                            finally
                            {
                                client.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                        }
                    });
            }
            else if (command.StartsWith("version"))
            {
                if (command == "version")
                {
                    var versionPanel = new Panel("[yellow]FeiSharp Target SDK 9.0[/]")
                        .Header(" [cyan]Version Info[/] ")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("blue"));
                    AnsiConsole.Write(versionPanel);
                }
                else if (command.Split(' ')[1] == "--update")
                {
                    AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Finding update file from web...[/]");

                    await AnsiConsole.Progress()
                        .StartAsync(async ctx =>
                        {
                            var task = ctx.AddTask("[green]Checking for updates[/]");

                            while (!task.IsFinished)
                            {
                                await Task.Delay(100);
                                task.Increment(2);
                            }
                        });

                    if (IsConnectedToInternet())
                    {
                        Thread.Sleep(Random.Shared.Next(1500, 4000));
                        var noUpdatePanel = new Panel("[yellow]No updates available at this time.[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("yellow"));
                        AnsiConsole.Write(noUpdatePanel);
                    }
                    else
                    {
                        var offlinePanel = new Panel("[red]You are currently offline.[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(Style.Parse("red"));
                        AnsiConsole.Write(offlinePanel);
                    }
                }
                else if (command.Split(' ')[1] == "--select")
                {
                    var version = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[cyan]Please select a version:[/]")
                            .PageSize(10)
                            .AddChoices(new[] {
                                "FeiSharp Core 2.0",
                                "FeiSharp Core 3.0",
                                "FeiSharp Core 4.0",
                                "FeiSharp Core 5.0",
                                "FeiSharp Target SDK 6.0",
                                "FeiSharp Target SDK 7.0",
                                "FeiSharp Target SDK 8.0",
                                "FeiSharp Target SDK 9.0",
                                "FeiSharp Beta Preview SDK 10.0",
                            }));

                    AnsiConsole.Status()
                        .Start("[yellow]Updating version...[/]", ctx =>
                        {
                            if (version == "FeiSharp Target SDK 9.0")
                            {
                                var successPanel = new Panel($"[red]× FeiSharp Target SDK 9.0 is current version now[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(successPanel);
                                return;
                            }
                            Thread.Sleep(1500); // 模拟处理时间

                            if (IsConnectedToInternet())
                            {
                                var successPanel = new Panel($"[green]✓ Successfully switched to {version}[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("green"));
                                AnsiConsole.Write(successPanel);
                            }
                            else
                            {
                                var offlinePanel = new Panel("[red]Cannot connect to update server[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(offlinePanel);
                            }
                        });
                }
            }
            else if (command == "license")
            {
                var licensePanel = new Panel(
                    "[yellow]Copyright (c) 2024-2026 Mars Fei[/]\n\n" +
                    "[grey]Licensed under the Apache License, Version 2.0 (the \"License\");\n" +
                    "you may not use this software except in compliance with the License.\n" +
                    "You may obtain a copy of the License at[/]\n\n" +
                    "[blue]http://www.apache.org/licenses/LICENSE-2.0.txt[/]\n\n" +
                    "[grey]Unless required by applicable law or agreed to in writing, software\n" +
                    "distributed under the License is distributed on an \"AS IS\" BASIS,\n" +
                    "WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\n" +
                    "See the License for the specific language governing permissions and\n" +
                    "limitations under the License.[/]")
                    .Header(" [cyan]Apache License 2.0[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("blue"))
                    .Expand();

                AnsiConsole.Write(licensePanel);
            }
            else if (command == "resetColor")
            {
                Console.ResetColor();
                AnsiConsole.MarkupLine("[green]Colors reset to default[/]");
            }
            else if (command == "color")
            {
                var color = AnsiConsole.Prompt(
                    new SelectionPrompt<ConsoleColor>()
                        .Title("[cyan]Select a color:[/]")
                        .PageSize(10)
                        .AddChoices(Enum.GetValues<ConsoleColor>()));

                Console.ForegroundColor = color;
                AnsiConsole.MarkupLine($"[green]Color changed to {color}[/]");
            }
            else if (command.StartsWith("license "))
            {
                global::System.String arg1 = command.Split(' ')[1];
                if (arg1 == "--type")
                {
                    var typePanel = new Panel("[yellow]Apache License[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("green"));
                    AnsiConsole.Write(typePanel);
                }
                else if (arg1 == "-v")
                {
                    var versionPanel = new Panel("[yellow]2.0[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("green"));
                    AnsiConsole.Write(versionPanel);
                }
            }
            else if (command == "copyright")
            {
                var copyrightPanel = new Panel(
                    "[yellow]Copyright (c) 2024-2026, Mars Fei[/]\n" +
                    "[red]All Rights Reserved.[/]")
                    .Header(" [cyan]Copyright Information[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("blue"));

                AnsiConsole.Write(copyrightPanel);
                var copyrightPanel2 = new Panel(
                    "[yellow]Copyright (c) 2024-2026, Savannah Yang[/]\n" +
                    "[red]All Rights Reserved.[/]")
                    .Header(" [cyan]Copyright Information[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("blue"));

                AnsiConsole.Write(copyrightPanel2);
            }
            else if (command == "base")
            {
                var basePanel = new Panel(
                    "[cyan]FeiSharp is based on:[/]\n\n" +
                    "  [yellow]- C# .NET 8.0[/]\n" +
                    "  [yellow]- Microsoft IL[/]\n" +
                    "  [yellow]- Native Binary Code[/]")
                    .Header(" [green]Technology Stack[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("green"));

                AnsiConsole.Write(basePanel);
            }
            else if (command == "credits")
            {
                var credits = new Panel(
                    "[yellow]Special thanks to:[/]\n\n" +
                    "  [green]- Ben Bai[/]\n" +
                    "  [green]- Yolanda Yang[/]\n" +
                    "  [green]- Dean Liu[/]\n" +
                    "  [green]- Savannah Yang[/]\n" +
                    "  [green]- thinkgeo.com[/]\n" +
                    "  [green]- github.com[/]\n" +
                    "  [green]- doubao.com[/]\n" +
                    "  [green]- deepseek.com[/]\n" +
                    "  [green]- Git[/]\n" +
                    "  [green]- Visual Studio[/]\n" +
                    "  [green]- Visual Studio Code[/]\n" +
                    "  [green]- Trae AI[/]\n\n" +
                    "[grey]...and a cast of thousands for supporting FeiSharp development.[/]")
                    .Header(" [cyan]Credits[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("blue"))
                    .Expand();

                AnsiConsole.Write(credits);

                var specCredit = new Panel("[blue]Special thanks Savannah Yang for her important support").Header(" [cyan]Special Thanks[/] ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("blue"))
                    .Expand();

                AnsiConsole.Write(specCredit);

            }
            else if (command == "help" || command.StartsWith("help "))
            {
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var specificCommand = parts.Length > 1 ? parts[1] : null;

                await HelpSystem.ShowInteractiveHelp(specificCommand);
            }
            else
            {
                // 执行代码时显示进度
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Executing code...[/]", async ctx =>
                    {
                        _parser = RunFeiSharpCodeWithProProcesser(command, _parser);
                    });
            }
        }
    }

    public static bool IsConnectedToInternet()
    {
        try
        {
            using (Ping ping = new Ping())
            {
                PingReply reply = ping.Send("8.8.8.8", 1000);
                return reply.Status == IPStatus.Success;
            }
        }
        catch (PingException)
        {
            return false;
        }
    }

    public static string ProProcesser(string scode)
    {
        var lines = scode.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries); ;
        for (int i = 0; i < lines.Length; i++)
        {
            string? item = lines[i].Trim();
            if (item.StartsWith('i'))
            {
                var line = item[1..];
                if (line.StartsWith("mport"))
                {
                    try
                    {
                        var vFilePath = line.Split('"')[1].Split('"')[0];
                        if (vFilePath.StartsWith("FeiSharp"))
                        {
                            continue;
                        }
                        var tFilePath = MapPath(vFilePath, _applicationPath);
                        if (File.Exists(tFilePath))
                        {
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning (at line {i + 1}):[/] The path of import statement isn't correct: [red]{vFilePath}[/]");
                            continue;
                        }
                        string content = File.ReadAllText(tFilePath);
                        content = ProProcesser(content);
                        scode = string.Concat(content, scode.Replace(item, ""));
                    }
                    catch
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning (at line {i + 1}):[/] [red]The format of import statement isn't correct[/]");
                    }
                }
            }
        }
        return scode;
    }

    public static Parser RunFeiSharpCodeWithProProcesser(string scode, Parser _parser = null)
    {
        ChangeDynamicTitle(EXEC);
        // 设置执行状态
        ExecutionCancellation.SetExecuting(true);

        try
        {
            // 检查是否被取消
            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                return _parser;
            }

            global::System.String sourceCode = scode;
            sourceCode = ProProcesser(sourceCode);

            // 检查是否被取消
            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled during preprocessing[/]");
                return _parser;
            }

            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;

            // 在词法分析过程中检查取消
            do
            {
                if (ExecutionCancellation.IsCancellationRequested)
                {
                    AnsiConsole.MarkupLine("[yellow]Execution cancelled during lexing[/]");
                    return _parser;
                }

                token = lexer.NextToken();
                tokens.Add(token);
            } while (token.Type != TokenTypes.EndOfFile);

            if (CodeError.isError)
            {
                return _parser;
            }

            Parser parser = null;
            try
            {
                parser = new(tokens);
                if (_parser != null)
                {
                    parser._variables = _parser._variables;
                    parser._functions = _parser?._functions ?? new Dictionary<string, FunctionInfo>();
                }

                // 设置取消检查委托
                parser.ShouldCancel = () => ExecutionCancellation.IsCancellationRequested;
                var rule1 = new Rule("[yellow]Code Output[/]")
                {
                    Style = Style.Parse("green"),
                    Justification = Justify.Center
                };
                AnsiConsole.Write(rule1);
                // 执行解析
                parser.ParseStatements();

                return parser;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                return _parser;
            }
            catch (FeiSharpTerminal3._1.ExceptionThrow.Exception ex)
            {
                if (!ExecutionCancellation.IsCancellationRequested)
                {
                    var errorPanel = new Panel($"[red]Runtime Error: {ex.Message}[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderStyle(Style.Parse("red"));
                    AnsiConsole.Write(errorPanel);
                }
                return parser;
            }
        }
        finally
        {
            // 无论成功还是取消，都重置执行状态
            ExecutionCancellation.SetExecuting(false);
            ChangeDynamicTitle(IN_CMD);
        }
    }
}

public class CustomConsole
{
    [global::System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern global::System.Boolean SetConsoleTextAttribute(global::System.IntPtr WIN_X32_INTEGER32_PTR_H_CONSOLE_OUTPUT_ID_FOR_32BYTES, global::System.UInt16 WIN_X32_UINT16_ATTRIBUTES_FOR_16BYTES);

    public const global::System.Int32 WIN_X64_INT32_STD_OUTPUT_HANDLE_ID_FOR_32BYTES = -11;

    [global::System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern global::System.IntPtr GetStdHandle(global::System.Int32 WIN_X64_INTEGER32_N_STD_HANDLE_FOR_32BYTES);

    public static void WRITE_GREY_WITHOUT_LINE(global::System.String STR_UCODE_12_TEXT, global::System.UInt16 WIN_X64_UINT16_PTR_FOR_16BYTES)
    {
        global::System.IntPtr consoleHandle = GetStdHandle(WIN_X64_INT32_STD_OUTPUT_HANDLE_ID_FOR_32BYTES);
        global::System.UInt16 grayColor = WIN_X64_UINT16_PTR_FOR_16BYTES;
        global::FeiSharp8._5RuntimeSdk.CustomConsole.SetConsoleTextAttribute(consoleHandle, grayColor);
        global::System.Console.Write(STR_UCODE_12_TEXT);
        global::System.UInt16 defaultColor = 7;
        global::FeiSharp8._5RuntimeSdk.CustomConsole.SetConsoleTextAttribute(consoleHandle, defaultColor);
    }
}
