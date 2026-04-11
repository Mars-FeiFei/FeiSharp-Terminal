using System.Text.Json.Serialization;
using FeiSharpTerminal3._1.Tests;

namespace FeiSharp8._5RuntimeSdk;

internal sealed class FeiSharpProjectFile
{
    [JsonPropertyName("project_main_file")]
    public string? ProjectMainFile { get; set; }

    [JsonPropertyName("target_platform")]
    public string? TargetPlatform { get; set; }

    [JsonPropertyName("trimmed_by_dotnet")]
    public bool? IsTrimmedByDotnet { get; set; }
    [JsonPropertyName("clean_build_directory")]
    public bool? IsCleanBuildDirectory { get; set; }
    [JsonPropertyName("only_copy_executable")]
    public bool? IsOnlyCopyExecutable { get; set; }

    [JsonPropertyName("project_name")]
    public string? ProjectName { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(FeiSharpProjectFile))]
[JsonSerializable(typeof(TestReportExporter.TestReportData))]
[JsonSerializable(typeof(List<FeiSharpTests.TestResult>))]
internal partial class FeiSharpJsonSerializerContext : JsonSerializerContext
{
}
