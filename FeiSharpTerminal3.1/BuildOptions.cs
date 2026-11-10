using System.Runtime.InteropServices;

namespace FeiSharp8._5RuntimeSdk;

internal enum BuildPublishMode
{
    NativeAot,
    SelfContained,
    FrameworkDependent
}

internal sealed class BuildCommandOptions
{
    public required string InputPath { get; init; }
    public string? OutputPath { get; private set; }
    public string? Configuration { get; private set; }
    public string? RuntimeIdentifier { get; private set; }
    public BuildPublishMode? PublishMode { get; private set; }
    public bool? SingleFile { get; private set; }
    public bool? Trimmed { get; private set; }
    public bool? DebugSymbols { get; private set; }
    public bool? InvariantGlobalization { get; private set; }
    public bool? Clean { get; private set; }
    public bool? CopyAllArtifacts { get; private set; }
    public bool RunAfterBuild { get; private set; }

    public static bool TryParse(IReadOnlyList<string> arguments, out BuildCommandOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        string? input = null;
        string? legacyOutput = null;
        var parsed = new BuildCommandOptions { InputPath = string.Empty };

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (!argument.StartsWith('-'))
            {
                if (input == null) input = argument;
                else if (legacyOutput == null) legacyOutput = argument;
                else return Fail($"Unexpected argument '{argument}'.", out options, out error);
                continue;
            }

            string name = argument;
            string? inlineValue = null;
            int equalsIndex = argument.IndexOf('=');
            if (equalsIndex > 0)
            {
                name = argument[..equalsIndex];
                inlineValue = argument[(equalsIndex + 1)..];
            }

            string? ReadValue()
            {
                if (inlineValue != null) return inlineValue;
                if (index + 1 >= arguments.Count) return null;
                return arguments[++index];
            }

            switch (name.ToLowerInvariant())
            {
                case "-o": case "--output":
                    parsed.OutputPath = ReadValue();
                    if (string.IsNullOrWhiteSpace(parsed.OutputPath)) return Fail("--output requires a path.", out options, out error);
                    break;
                case "-c": case "--configuration":
                    parsed.Configuration = ReadValue();
                    if (string.Equals(parsed.Configuration, "debug", StringComparison.OrdinalIgnoreCase)) parsed.Configuration = "Debug";
                    else if (string.Equals(parsed.Configuration, "release", StringComparison.OrdinalIgnoreCase)) parsed.Configuration = "Release";
                    else return Fail("--configuration must be Debug or Release.", out options, out error);
                    break;
                case "-r": case "--runtime":
                    parsed.RuntimeIdentifier = ReadValue();
                    if (string.IsNullOrWhiteSpace(parsed.RuntimeIdentifier)) return Fail("--runtime requires a runtime identifier.", out options, out error);
                    break;
                case "--mode":
                    string? mode = ReadValue()?.ToLowerInvariant();
                    parsed.PublishMode = mode switch
                    {
                        "aot" or "native-aot" => BuildPublishMode.NativeAot,
                        "self-contained" => BuildPublishMode.SelfContained,
                        "framework-dependent" or "framework" => BuildPublishMode.FrameworkDependent,
                        _ => null
                    };
                    if (parsed.PublishMode == null) return Fail("--mode must be aot, self-contained, or framework-dependent.", out options, out error);
                    break;
                case "--aot": parsed.PublishMode = BuildPublishMode.NativeAot; break;
                case "--self-contained": parsed.PublishMode = BuildPublishMode.SelfContained; break;
                case "--framework-dependent": parsed.PublishMode = BuildPublishMode.FrameworkDependent; break;
                case "--single-file": parsed.SingleFile = true; break;
                case "--no-single-file": parsed.SingleFile = false; break;
                case "--trim": parsed.Trimmed = true; break;
                case "--no-trim": parsed.Trimmed = false; break;
                case "--debug-symbols": parsed.DebugSymbols = true; break;
                case "--no-debug-symbols": parsed.DebugSymbols = false; break;
                case "--invariant-globalization": parsed.InvariantGlobalization = true; break;
                case "--full-globalization": parsed.InvariantGlobalization = false; break;
                case "--clean": parsed.Clean = true; break;
                case "--no-clean": parsed.Clean = false; break;
                case "--artifacts": parsed.CopyAllArtifacts = true; break;
                case "--executable-only": parsed.CopyAllArtifacts = false; break;
                case "--run": parsed.RunAfterBuild = true; break;
                case "--no-run": parsed.RunAfterBuild = false; break;
                default: return Fail($"Unknown build option '{name}'.", out options, out error);
            }
        }

        if (string.IsNullOrWhiteSpace(input)) return Fail("A .fsc or .feiproj input path is required.", out options, out error);
        if (parsed.OutputPath != null && legacyOutput != null) return Fail("Specify the output path either positionally or with --output, not both.", out options, out error);
        parsed.OutputPath ??= legacyOutput;
        options = new BuildCommandOptions
        {
            InputPath = input,
            OutputPath = parsed.OutputPath,
            Configuration = parsed.Configuration,
            RuntimeIdentifier = parsed.RuntimeIdentifier,
            PublishMode = parsed.PublishMode,
            SingleFile = parsed.SingleFile,
            Trimmed = parsed.Trimmed,
            DebugSymbols = parsed.DebugSymbols,
            InvariantGlobalization = parsed.InvariantGlobalization,
            Clean = parsed.Clean,
            CopyAllArtifacts = parsed.CopyAllArtifacts,
            RunAfterBuild = parsed.RunAfterBuild
        };
        return true;
    }

    private static bool Fail(string message, out BuildCommandOptions? options, out string error)
    {
        options = null;
        error = message;
        return false;
    }
}

internal sealed record EffectiveBuildOptions(
    string Configuration,
    string RuntimeIdentifier,
    BuildPublishMode PublishMode,
    bool SingleFile,
    bool Trimmed,
    bool DebugSymbols,
    bool InvariantGlobalization,
    bool Clean,
    bool CopyAllArtifacts)
{
    public static EffectiveBuildOptions Create(BuildCommandOptions command, FeiSharpProjectFile? project)
    {
        BuildPublishMode projectMode = ParseMode(project?.PublishMode)
            ?? (project?.IsTrimmedByDotnet == true ? BuildPublishMode.FrameworkDependent : BuildPublishMode.NativeAot);
        BuildPublishMode mode = command.PublishMode ?? projectMode;
        return new(
            command.Configuration ?? project?.BuildConfiguration ?? "Release",
            command.RuntimeIdentifier ?? project?.TargetPlatform ?? DefaultRuntimeIdentifier(),
            mode,
            command.SingleFile ?? project?.PublishSingleFile ?? project?.IsTrimmedByDotnet ?? mode == BuildPublishMode.SelfContained,
            command.Trimmed ?? project?.PublishTrimmed ?? false,
            command.DebugSymbols ?? project?.DebugSymbols ?? false,
            command.InvariantGlobalization ?? project?.InvariantGlobalization ?? true,
            command.Clean ?? project?.IsCleanBuildDirectory ?? false,
            command.CopyAllArtifacts ?? !(project?.IsOnlyCopyExecutable ?? false));
    }

    private static BuildPublishMode? ParseMode(string? value) => value?.ToLowerInvariant() switch
    {
        "aot" or "native-aot" => BuildPublishMode.NativeAot,
        "self-contained" => BuildPublishMode.SelfContained,
        "framework-dependent" or "framework" => BuildPublishMode.FrameworkDependent,
        _ => null
    };

    private static string DefaultRuntimeIdentifier() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.Arm64 => "win-arm64",
        Architecture.X86 => "win-x86",
        _ => "win-x64"
    };
}
