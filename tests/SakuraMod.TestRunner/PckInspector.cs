using System.Text.Json.Serialization;
using System.Text.Json;
using System.Xml.Linq;

namespace SakuraMod.TestRunner;

public sealed record PckInspectionResult(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pack_path")] string? PackPath,
    [property: JsonPropertyName("paths")] IReadOnlyList<string> Paths,
    [property: JsonPropertyName("added_paths")] IReadOnlyList<string> AddedPaths,
    [property: JsonPropertyName("failure")] string? Failure);

public interface IPckInspector
{
    Task<PckInspectionResult> InspectAsync(
        string pckPath,
        string outputPath,
        string logPath,
        CancellationToken cancellationToken = default);
}

public sealed class GodotPckInspector(string godotPath, string projectPath) : IPckInspector
{
    public async Task<PckInspectionResult> InspectAsync(
        string pckPath,
        string outputPath,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunLoggedAsync(
            godotPath,
            [
                "--headless",
                "--path", projectPath,
                "--script", "res://main.gd",
                "--",
                "--pack", Path.GetFullPath(pckPath),
                "--output", Path.GetFullPath(outputPath)
            ],
            projectPath,
            logPath,
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"PCK inspector exited with code {result.ExitCode}. See {logPath}.");
        }

        await using var stream = File.OpenRead(outputPath);
        var inspection = await JsonSerializer.DeserializeAsync<PckInspectionResult>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("PCK inspector returned an empty result.");
        if (inspection.SchemaVersion != 1 || inspection.Status != "PASS")
        {
            throw new InvalidDataException($"PCK inspector failed: {inspection.Failure ?? "unknown failure"}");
        }

        return inspection;
    }

    public static string FindGodotPath(string repoRoot)
    {
        var environmentPath = Environment.GetEnvironmentVariable("GODOT_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath) && File.Exists(environmentPath))
        {
            return Path.GetFullPath(environmentPath);
        }

        var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
        if (File.Exists(propsPath))
        {
            var value = XDocument.Load(propsPath)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "GodotPath")
                ?.Value
                .Trim();
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
            {
                return Path.GetFullPath(value);
            }
        }

        throw new FileNotFoundException("GodotPath is not configured. Set GODOT_PATH or Directory.Build.props.");
    }
}
