using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SakuraMod.TestRunner;

public sealed record RuntimePrerequisites(
    string GameRoot,
    string GameExecutable,
    string GameDataDirectory,
    string GameVersion,
    string RitsuPackageRoot,
    string RitsuVersion,
    string SakuraVersion,
    string GodotExecutable,
    string RealModsDirectory,
    string RealUserDataDirectory);

public static class RuntimePreflight
{
    public const string ExpectedGameVersion = "0.107.1";
    public const string ExpectedRitsuVersion = "0.5.18";

    public static RuntimePrerequisites Inspect(string repoRoot)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The pinned runtime harness currently supports Linux only.");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var gameRoot = Environment.GetEnvironmentVariable("STS2_PATH")
            ?? Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Slay the Spire 2");
        gameRoot = Path.GetFullPath(gameRoot);
        var gameExecutable = RequireFile(Path.Combine(gameRoot, "SlayTheSpire2"), "STS2 executable");
        var gameDataDirectory = RequireDirectory(
            Path.Combine(gameRoot, "data_sts2_linuxbsd_x86_64"),
            "STS2 managed data directory");
        RequireFile(Path.Combine(gameDataDirectory, "sts2.dll"), "STS2 managed assembly");

        var release = ReadJson<ReleaseInfo>(RequireFile(
            Path.Combine(gameRoot, "release_info.json"),
            "STS2 release metadata"));
        var gameVersion = NormalizeVersion(release.Version);
        RequireVersion("STS2", gameVersion, ExpectedGameVersion);

        var ritsuRoot = FindRitsuPackage(home);
        var ritsuManifest = ReadJson<ModManifest>(RequireFile(
            Path.Combine(ritsuRoot, "mod_manifest.json"),
            "RitsuLib manifest"));
        if (!string.Equals(ritsuManifest.Id, "STS2-RitsuLib", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected RitsuLib mod id '{ritsuManifest.Id}'.");
        }

        var ritsuVersion = NormalizeVersion(ritsuManifest.Version);
        RequireVersion("RitsuLib", ritsuVersion, ExpectedRitsuVersion);
        var compatibleRitsuAssembly = RequireFile(
            Path.Combine(ritsuRoot, "lib", ExpectedGameVersion, "STS2-RitsuLib.dll"),
            "RitsuLib compatibility assembly");
        var assemblyVersion = AssemblyName.GetAssemblyName(compatibleRitsuAssembly).Version;
        if (assemblyVersion is null)
        {
            throw new InvalidDataException("RitsuLib compatibility assembly has no managed version.");
        }

        var sakuraManifest = ReadJson<ModManifest>(RequireFile(
            Path.Combine(repoRoot, "SakuraMod.json"),
            "SakuraMod manifest"));
        if (!string.Equals(sakuraManifest.Id, "SakuraMod", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected SakuraMod id '{sakuraManifest.Id}'.");
        }

        RequireVersion("SakuraMod minimum game", NormalizeVersion(sakuraManifest.MinimumGameVersion), ExpectedGameVersion);
        var godotExecutable = RequireFile(GodotPckInspector.FindGodotPath(repoRoot), "Godot executable");
        var dataBase = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(home, ".local", "share");

        return new RuntimePrerequisites(
            gameRoot,
            gameExecutable,
            gameDataDirectory,
            gameVersion,
            ritsuRoot,
            ritsuVersion,
            NormalizeVersion(sakuraManifest.Version),
            godotExecutable,
            Path.Combine(gameRoot, "mods"),
            Path.Combine(dataBase, "SlayTheSpire2"));
    }

    private static string FindRitsuPackage(string home)
    {
        var configured = Environment.GetEnvironmentVariable("STS2_RITSULIB_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return RequireDirectory(Path.GetFullPath(configured), "configured RitsuLib package");
        }

        var workshopRoot = Path.Combine(
            home,
            ".local",
            "share",
            "Steam",
            "steamapps",
            "workshop",
            "content",
            "2868840");
        if (!Directory.Exists(workshopRoot))
        {
            throw new DirectoryNotFoundException($"Steam Workshop content directory was not found: {workshopRoot}");
        }

        foreach (var directory in Directory.EnumerateDirectories(workshopRoot).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory, "mod_manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                if (ReadJson<ModManifest>(manifestPath).Id == "STS2-RitsuLib")
                {
                    return directory;
                }
            }
            catch (JsonException)
            {
                // An unrelated Workshop item with malformed metadata is not a RitsuLib candidate.
            }
        }

        throw new DirectoryNotFoundException(
            $"RitsuLib was not found under {workshopRoot}. Set STS2_RITSULIB_PATH to its package root.");
    }

    private static T ReadJson<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream)
            ?? throw new InvalidDataException($"JSON document is empty: {path}");
    }

    private static string RequireFile(string path, string description)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0)
        {
            throw new FileNotFoundException($"{description} is missing or empty: {path}", path);
        }

        return file.FullName;
    }

    private static string RequireDirectory(string path, string description)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{description} was not found: {path}");
        }

        return Path.GetFullPath(path);
    }

    private static void RequireVersion(string component, string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{component} version mismatch: expected {expected}, found {actual}.");
        }
    }

    public static string NormalizeVersion(string value) => value.Trim().TrimStart('v');

    private sealed record ReleaseInfo(
        [property: JsonPropertyName("version")] string Version);

    private sealed record ModManifest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("min_game_version")] string MinimumGameVersion);
}
