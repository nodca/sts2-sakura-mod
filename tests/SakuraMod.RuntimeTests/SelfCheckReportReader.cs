using System.IO.Compression;
using System.Text.RegularExpressions;

namespace SakuraMod.RuntimeTests;

internal sealed record SelfCheckSnapshot(
    string ZipPath,
    string ReportPath,
    int SakuraFailures,
    int CharacterAssetFailures,
    int LocalizationFailures,
    bool FrameworkActive,
    bool FrameworkInitialized,
    bool HarmonyDumpPassed);

internal static partial class SelfCheckReportReader
{
    private const int MaximumReportCharacters = 2 * 1024 * 1024;

    public static SelfCheckSnapshot ReadLatest(string directory)
    {
        var zipPath = Directory.EnumerateFiles(directory, "ritsulib_self_check_*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new FileNotFoundException($"RitsuLib self-check zip was not produced in {directory}.");
        using var archive = ZipFile.OpenRead(zipPath);
        var reportEntry = archive.GetEntry("self_check_report.log")
            ?? throw new InvalidDataException($"Self-check archive has no self_check_report.log: {zipPath}");
        if (reportEntry.Length > MaximumReportCharacters)
        {
            throw new InvalidDataException($"Self-check report exceeds {MaximumReportCharacters} bytes.");
        }

        using var reader = new StreamReader(reportEntry.Open());
        var report = reader.ReadToEnd();
        var sakuraFailures = ParseCount(report, SakuraSummaryRegex(), "SakuraMod per-mod summary");
        var characterFailures = ParseCount(report, CharacterAssetRegex(), "character asset summary");
        var localizationFailures = ParseCount(report, LocalizationRegex(), "localization summary");
        return new SelfCheckSnapshot(
            zipPath,
            "self_check_report.log",
            sakuraFailures,
            characterFailures,
            localizationFailures,
            report.Contains("Framework Active: True", StringComparison.Ordinal),
            report.Contains("Framework Initialized: True", StringComparison.Ordinal),
            report.Contains("Harmony Dump: PASS", StringComparison.Ordinal));
    }

    private static int ParseCount(string report, Regex regex, string description)
    {
        var match = regex.Match(report);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var count))
        {
            throw new InvalidDataException($"Could not parse {description} from RitsuLib self-check report.");
        }

        return count;
    }

    [GeneratedRegex(@"^- SakuraMod:.*\bFAIL=(\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SakuraSummaryRegex();

    [GeneratedRegex(@"^Character Asset Runtime Check: FAIL=(\d+),", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CharacterAssetRegex();

    [GeneratedRegex(@"^Localization/Entry Runtime Check: FAIL=(\d+),", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LocalizationRegex();
}
