using FeiSharpStudio;
using FeiSharpTerminal3._1;
using FeiSharpTerminal3._1.Tests;
using Spectre.Console;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace FeiSharp8._5RuntimeSdk;
public class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int handle);
    static string FEISHARP_IMPORT_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FeiSharpImportFiles");
    public static string? MapPath(string vpath, string _applicationPath)
    {
        string path = "";
        if (vpath.StartsWith("~/"))
        {
            path = Path.Combine(Path.GetDirectoryName(_applicationPath), vpath[2..]);
        }
        else if (vpath.StartsWith("$"))
        {
            path = Path.Combine(FEISHARP_IMPORT_PATH, vpath[1..]);
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
    static string? platform = "win-x64";
    static bool? isCleanBuildDirectory = false;
    static bool? isTrimmedByDotnet = false;
    static bool? isOnlyCopyExecutable = false;
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
        FeiSharpProjectFile? projectFile = JsonSerializer.Deserialize(
            File.ReadAllText(fullInputPath),
            FeiSharpJsonSerializerContext.Default.FeiSharpProjectFile);
        if (string.IsNullOrWhiteSpace(projectFile?.ProjectMainFile))
        {
            return null;
        }
        platform = !string.IsNullOrEmpty(projectFile.TargetPlatform) ? projectFile.TargetPlatform : platform;
        isCleanBuildDirectory = projectFile.IsCleanBuildDirectory ?? false;
        isTrimmedByDotnet = projectFile.IsTrimmedByDotnet ?? false;
        isOnlyCopyExecutable = projectFile.IsOnlyCopyExecutable ?? false;
        string? mainFile = projectFile.ProjectMainFile;
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
                    FeiSharpProjectFile? projectFile = JsonSerializer.Deserialize(
                        File.ReadAllText(fullInputPath),
                        FeiSharpJsonSerializerContext.Default.FeiSharpProjectFile);
                    if (!string.IsNullOrWhiteSpace(projectFile?.ProjectName))
                    {
                        string? projectName = projectFile.ProjectName;
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
            return Path.Combine(buildBaseDirectory, "bin", "obj", "feisharp.sdk 10.0", outputName + ".exe");
        }
        return ResolveBuildOutputPath(sourcePath, requestedOutputPath);
    }
    static string GetRuntimeAssemblyPath()
    {
        string assemblyName = typeof(Program).Assembly.GetName().Name ?? "feisharp";
        string bundledDllPath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        if (File.Exists(bundledDllPath))
        {
            return bundledDllPath;
        }
        throw new FileNotFoundException("Unable to locate the FeiSharp runtime assembly.", bundledDllPath);
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
            if ((bool)isCleanBuildDirectory && Directory.Exists(Path.GetDirectoryName(outputExePath)))
            {
                foreach (var item in Directory.GetFiles(Path.GetDirectoryName(outputExePath)))
                {
                    File.Delete(item);
                }
            }
            string argument = $"publish -c Release -r {platform} -p:PublishAot=true -p:OptimizationPreference=Size -p:DebugType=none -p:InvariantGlobalization=true -o " + tempBuildDirectory;
            if ((bool)isTrimmedByDotnet)
            {
                AnsiConsole.MarkupLine("[yellow]Warning: Use trimmed_by_dotnet must ensure the target machine has installed .NET 8.0[/]");
                argument = $"build -c Release -r {platform} -p:PublishSingleFile=true -p:OutputPath={tempBuildDirectory}";
            }
            var buildProcess = new Process();
            buildProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = argument,
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            buildProcess.Start();
            string buildStandardOutput = buildProcess.StandardOutput.ReadToEnd();
            string buildStandardError = buildProcess.StandardError.ReadToEnd();
            buildProcess.WaitForExit();
            if (buildProcess.ExitCode != 0)
            {
                errorMessage = string.IsNullOrWhiteSpace(buildStandardError) ? buildStandardOutput : buildStandardError;
                return false;
            }
            string publishedExePath = Path.Combine(tempBuildDirectory, assemblyName + ".exe");
            if (!File.Exists(publishedExePath))
            {
                errorMessage = $"Build succeeded, but the generated exe was not found at {publishedExePath}.";
                return false;
            }
            if ((bool)isOnlyCopyExecutable && (bool)isTrimmedByDotnet)
            {
                errorMessage = $"trimmed_by_dotnet and only_copy_executable cannot both be true";
                return false;
            }
            if ((bool)isOnlyCopyExecutable)
            {
                File.Copy(publishedExePath, outputExePath, true);
                return true;
            }
            foreach (string file in Directory.GetFiles(tempBuildDirectory))
            {
                File.Copy(file, Path.Combine(Path.GetDirectoryName(outputExePath), Path.GetFileName(file)), true);
            }
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

        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;
        ExecutionCancellation.Initialize();
        Directory.CreateDirectory(FEISHARP_IMPORT_PATH);
        if(Environment.GetEnvironmentVariable("FEISHARP_IMPORT_PATH", EnvironmentVariableTarget.User) == null)
            Environment.SetEnvironmentVariable("FEISHARP_IMPORT_PATH", FEISHARP_IMPORT_PATH, EnvironmentVariableTarget.User);
        if (args.Length == 0)
            FeiSharpTests.RunAllTests();
        TryConfigureConsole(() => Console.CursorSize = 25);
        FeiSharpProgramData.AssemblyName = Guid.NewGuid().ToString("N");
        TryConfigureConsole(EnableVirtualTerminalProcessing);
        TryConfigureConsole(() => Console.CursorVisible = true);
        TryConfigureConsole(() => Console.Title = dynamicTitle);


        AnsiConsole.Write(
            new FigletText("FEI# Target SDK 10.0")
                .Color(Color.Cyan1));

        var rule = new Rule($"[yellow]Version 10.0[/]")
        {
            Style = Style.Parse("blue"),
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);


        var infoPanel = new Panel(
            "[grey]FeiSharp 10.0 (tags/v10.0, Apr 7 2026, 20:49:47) MSC v.1942 64 bit (AMD64) on win32[/]\n" +
            "[grey]Type [green]\"help\"[/], [green]\"copyright\"[/], [green]\"credits\"[/] or [green]\"license\"[/] for more information.[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Padding(1, 1, 1, 1);

        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        #endregion

        if (args.Length > 0 && string.Equals(args[0], "build", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                var usagePanel = new Panel("[red]Usage: feisharp build <source.fsc|project.feiproj> [output.exe][/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red"));
                AnsiConsole.Write(usagePanel);
                return;
            }

            string? outputArg = "out\\feisharp-sdk-10.0-release\\" + (args.Length > 2 ? args[2] : null);
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

                Console.WriteLine();
                if (ExecutionCancellation._isExecuting)
                {

                    ExecutionCancellation.CancelExecution();
                    AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                }
                continue;
            }


            if (command == null)
            {
                Console.WriteLine();
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
                string currentProcessPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "feisharp.exe");
                string exePath = Path.ChangeExtension(currentProcessPath, ".exe").Replace("feisharp.exe", "UiG.exe").Replace("FeiSharpTerminal3.1\\bin\\Debug\\net8.0-windows", "UiG\\bin\\Debug\\net8.0-windows");

                var statusPanel = new Panel($"[yellow]Launching UI with file:[/] [green]{path}[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("yellow"));
                AnsiConsole.Write(statusPanel);

                Process.Start(exePath, path);
            }
            else if (command.StartsWith("open"))
            {
                string path = "";
                if (command == "open")
                {
                    path = Directory.GetCurrentDirectory();
                }
                else
                {
                    path = command.Split(' ')[1];
                }
                Task.Factory.StartNew(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "code.exe",
                        Arguments = $"--log=off \"{path}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                });
                if (Console.CursorTop > 0)
                {
                    int currentLeft = Console.CursorLeft;
                    int currentTop = Console.CursorTop;
                    Console.SetCursorPosition(0, currentTop - 1);
                }
                Thread.Sleep(500);

            }
            else if (command.StartsWith("explorer"))
            {
                string path = "";
                if (command == "explorer")
                {
                    path = Directory.GetCurrentDirectory();
                }
                else
                {
                    path = command.Split(' ')[1];
                }
                Task.Factory.StartNew(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                });
                Thread.Sleep(500);

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
                List<string> feiprojs = [];
                if (string.IsNullOrWhiteSpace(sourceInput))
                {
                    foreach (var item in Directory.GetFiles(Directory.GetCurrentDirectory()))
                    {
                        if (item.EndsWith(".feiproj"))
                        {
                            feiprojs.Add(item);
                        }
                    }
                    if (feiprojs.Count == 1)
                    {
                        sourceInput = feiprojs[0];
                    }
                    else
                    {
                        feiprojs.Add("Others");
                        sourceInput = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("What source file would you like?")
        .AddChoices(feiprojs.ToArray())
);
                    }
                    if (sourceInput == "Others")
                    {
                        AnsiConsole.MarkupLine("[cyan]>>>[/] [yellow]Input the source file or project path...[/]");
                        AnsiConsole.Markup("[cyan]Source Path: [/]");
                        sourceInput = Console.ReadLine() ?? string.Empty;
                    }
                }
                string? outputInput = "out\\feisharp-sdk-10.0-release\\" + Path.GetFileNameWithoutExtension(sourceInput) + ".exe";
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Building executable...[/]", async ctx =>
                    {
                        ChangeDynamicTitle("Generate Native Code");
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
                        ChangeDynamicTitle(IN_CMD);
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
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create console project {name} by FeiSharp Target SDK 10.0\n");
                            File.WriteAllText(".gitignore", @"# 编译输出
out/
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
    ""sdk_version"": 10.0
}}");
                            break;
                        case "API":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 10.0
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nimport \"~/api.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("data.fsc", "anno(\"Set your data in this file\");");
                            File.WriteAllText("api.fsc", "anno(\"Create your own API functions in this file\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 10.0\n");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            break;
                        case "Data":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""trimmed_by_dotnet"": true,
    ""only_copy_executable"": true,
    ""clean_build_directory"": true,
    ""sdk_version"": ""9.5 Beta"",
    ""contained_assembly""
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/data.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("data.fsc", "anno(\"Set your data in this file\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
out/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 10.0\n");
                            File.WriteAllText($"license.txt", "Your license file, for example: MIT");
                            break;
                        case "fUnitTest":
                            Directory.CreateDirectory(name);
                            Directory.SetCurrentDirectory(name);
                            File.WriteAllText($"{name}.feiproj", @$"{{
    ""project_name"": ""{name}"",
    ""project_main_file"": ""main_{name}.fsc"",
    ""sdk_version"": 10.0
}}");
                            File.WriteAllText($"test_{name}.fsc1", "import \"$prelude.fsc\";\nimport \"~/test_content.fsc\";\nimport \"~/api.fsc\";\nfunction test()\nfbegin:\n    print(\"Hello, World!\");\nfend;\ntest();");
                            File.WriteAllText("test_content.fsc", "anno(\"Set the test content\");");
                            File.WriteAllText("README.md", $"This is the api project {name}");
                            File.WriteAllText(".gitignore", @"
ouy/
*.exe
*.dll
Tests/
*.log
.vs/
.vscode/
.DS_Store
Thumbs.db
");
                            File.WriteAllText($"{name}.log", $"{DateTime.Now} Create api project {name} by FeiSharp Target SDK 10.0\n");
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
                    var versionPanel = new Panel("[yellow]FeiSharp Target SDK 10.0[/]")
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
                                "FeiSharp Target SDK 10.0 Release",
                            }));

                    AnsiConsole.Status()
                        .Start("[yellow]Updating version...[/]", ctx =>
                        {
                            if (version == "FeiSharp Target SDK 10.0 Release")
                            {
                                var successPanel = new Panel($"[red]× FeiSharp Target SDK 10.0 Release is current version now[/]")
                                    .Border(BoxBorder.Rounded)
                                    .BorderStyle(Style.Parse("red"));
                                AnsiConsole.Write(successPanel);
                                return;
                            }
                            Thread.Sleep(1500);

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

        ExecutionCancellation.SetExecuting(true);

        try
        {

            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled[/]");
                return _parser;
            }

            global::System.String sourceCode = scode;
            sourceCode = ProProcesser(sourceCode);


            if (ExecutionCancellation.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]Execution cancelled during preprocessing[/]");
                return _parser;
            }

            Lexer lexer = new(sourceCode);
            List<Token> tokens = [];
            Token token;


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


                parser.ShouldCancel = () => ExecutionCancellation.IsCancellationRequested;
                var rule1 = new Rule("[yellow]Code Output[/]")
                {
                    Style = Style.Parse("green"),
                    Justification = Justify.Center
                };
                AnsiConsole.Write(rule1);

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
