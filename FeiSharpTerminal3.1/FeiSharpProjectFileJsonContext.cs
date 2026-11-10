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
    [JsonPropertyName("main_function_run")]
    public bool? IsMainFunctionRun { get; set; }
    [JsonPropertyName("only_copy_executable")]
    public bool? IsOnlyCopyExecutable { get; set; }
    [JsonPropertyName("project_name")]
    public string? ProjectName { get; set; }
    [JsonPropertyName("dependencies")]
    public List<Dependency>? Dependencies { get; set; }
    [JsonPropertyName("build_configuration")]
    public string? BuildConfiguration { get; set; }
    [JsonPropertyName("publish_mode")]
    public string? PublishMode { get; set; }
    [JsonPropertyName("single_file")]
    public bool? PublishSingleFile { get; set; }
    [JsonPropertyName("trimmed")]
    public bool? PublishTrimmed { get; set; }
    [JsonPropertyName("debug_symbols")]
    public bool? DebugSymbols { get; set; }
    [JsonPropertyName("invariant_globalization")]
    public bool? InvariantGlobalization { get; set; }
    [JsonPropertyName("output_path")]
    public string? OutputPath { get; set; }
}
public class Dependency
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("src_url")]
    public string? SrcUrl { get; set; }
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
