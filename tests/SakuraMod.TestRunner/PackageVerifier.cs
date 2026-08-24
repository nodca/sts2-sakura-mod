using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SakuraMod.TestRunner;

public sealed record PackageVerificationRequest(
    string RunId,
    string PackageDirectory,
    bool StageWasAbsent,
    DateTimeOffset StartedAtUtc,
    string ExpectedModId,
    string ExpectedGameVersion,
    string ExpectedDependencyId,
    string InventoryOutputPath,
    string InspectorLogPath);

public sealed record PackageFileResult(
    string Name,
    long Size,
    string Sha256,
    DateTimeOffset LastWriteTimeUtc);

public sealed record PackageVerificationResult(
    int SchemaVersion,
    string RunId,
    string Status,
    string ModId,
    string ManifestVersion,
    string MinimumGameVersion,
    string AssemblyName,
    string AssemblyVersion,
    IReadOnlyList<PackageFileResult> Files,
    int PckPathCount,
    string PckInventoryPath);

public sealed class PackageVerifier(IPckInspector pckInspector)
{
    private static readonly string[] RequiredFileNames =
    [
        "SakuraMod.dll",
        "SakuraMod.json",
        "SakuraMod.pck",
        "music/another_me.ogg",
        "voices/dream_wand.ogg",
        "voices/stabilize.ogg"
    ];
    private const string KeroCompanionImportPath =
        "res://SakuraMod/images/charui/combat/kero_companion.png.import";
    private const string KeroCompanionTexturePrefix =
        "res://.godot/imported/kero_companion.png-";

    public async Task<PackageVerificationResult> VerifyAsync(
        PackageVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.StageWasAbsent)
        {
            throw new InvalidOperationException("Package stage existed before the current invocation.");
        }

        ValidatePackageDirectory(request.PackageDirectory);
        var files = RequiredFileNames.Select(name => RequireFreshRegularFile(request.PackageDirectory, name)).ToArray();
        var manifest = await ReadManifestAsync(files.Single(file => file.Name == "SakuraMod.json").FullName, cancellationToken);
        ValidateManifest(manifest, request);

        var assembly = AssemblyName.GetAssemblyName(files.Single(file => file.Name == "SakuraMod.dll").FullName);
        if (!string.Equals(assembly.Name, request.ExpectedModId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Assembly name '{assembly.Name}' does not match mod id '{request.ExpectedModId}'.");
        }

        var inspection = await pckInspector.InspectAsync(
            files.Single(file => file.Name == "SakuraMod.pck").FullName,
            request.InventoryOutputPath,
            request.InspectorLogPath,
            cancellationToken);
        ValidatePckPaths(inspection.AddedPaths);

        var fileResults = new List<PackageFileResult>(files.Length);
        foreach (var file in files)
        {
            await using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            fileResults.Add(new PackageFileResult(
                file.Name,
                file.Length,
                Convert.ToHexStringLower(hash),
                file.LastWriteTimeUtc));
        }

        return new PackageVerificationResult(
            1,
            request.RunId,
            "PASS",
            manifest.Id,
            manifest.Version,
            manifest.MinimumGameVersion,
            assembly.Name!,
            assembly.Version?.ToString() ?? "unknown",
            fileResults,
            inspection.AddedPaths.Count,
            request.InventoryOutputPath);
    }

    public static void ValidatePckPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            throw new InvalidDataException("PCK inventory is empty.");
        }

        foreach (var path in paths)
        {
            var allowed = path.StartsWith("res://SakuraMod/", StringComparison.Ordinal)
                || path.StartsWith("res://.godot/imported/", StringComparison.Ordinal)
                || path.StartsWith("res://.godot/exported/", StringComparison.Ordinal)
                || path is "res://.godot/global_script_class_cache.cfg"
                    or "res://.godot/uid_cache.bin"
                    or "res://project.binary";
            if (!allowed)
            {
                throw new InvalidDataException($"PCK contains a path outside the production allowlist: {path}");
            }

            var normalized = path.ToLowerInvariant();
            if (normalized.Contains("runtimetests", StringComparison.Ordinal)
                || normalized.Contains("test-only", StringComparison.Ordinal)
                || normalized.Contains("/tests/", StringComparison.Ordinal)
                || normalized.Contains("fixture", StringComparison.Ordinal)
                || normalized.EndsWith(".cs", StringComparison.Ordinal)
                || normalized.EndsWith(".csproj", StringComparison.Ordinal)
                || normalized.EndsWith(".gd", StringComparison.Ordinal)
                || normalized.EndsWith(".dll", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"PCK contains a forbidden test or script path: {path}");
            }
        }

        if (!paths.Contains(KeroCompanionImportPath, StringComparer.Ordinal)
            || !paths.Any(path =>
                path.StartsWith(KeroCompanionTexturePrefix, StringComparison.Ordinal)
                && path.EndsWith(".ctex", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("PCK does not contain the Kero combat companion import and texture.");
        }
    }

    private static void ValidatePackageDirectory(string packageDirectory)
    {
        foreach (var path in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(packageDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
            var allowed = RequiredFileNames.Contains(relativePath, StringComparer.Ordinal)
                || relativePath == "SakuraMod.pdb"
                || relativePath.StartsWith("music/", StringComparison.Ordinal)
                || relativePath.StartsWith("voices/", StringComparison.Ordinal);
            if (!allowed)
            {
                throw new InvalidDataException($"Package contains an unexpected file: {relativePath}");
            }
        }
    }

    private static FileInfo RequireFreshRegularFile(string packageDirectory, string name)
    {
        var path = Path.Combine(packageDirectory, name);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0)
        {
            throw new FileNotFoundException($"Required package file is missing or empty: {path}", path);
        }

        if (file.LinkTarget is not null)
        {
            throw new InvalidDataException($"Required package file must not be a symbolic link: {path}");
        }

        return file;
    }

    private static async Task<ModManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ModManifest>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Mod manifest is empty.");
    }

    private static void ValidateManifest(ModManifest manifest, PackageVerificationRequest request)
    {
        if (manifest.Id != request.ExpectedModId || !manifest.HasDll || !manifest.HasPck)
        {
            throw new InvalidDataException("Manifest id or DLL/PCK capability flags do not match the package contract.");
        }

        if (!TryParseVersion(manifest.Version, out _)
            || manifest.MinimumGameVersion != request.ExpectedGameVersion
            || !TryParseVersion(manifest.MinimumGameVersion, out _))
        {
            throw new InvalidDataException("Manifest version or minimum game version is invalid.");
        }

        var dependency = manifest.Dependencies.SingleOrDefault(value => value.Id == request.ExpectedDependencyId);
        if (dependency is null || !TryParseVersion(dependency.MinimumVersion, out _))
        {
            throw new InvalidDataException($"Manifest dependency '{request.ExpectedDependencyId}' is missing or invalid.");
        }
    }

    private static bool TryParseVersion(string value, out Version? version) =>
        Version.TryParse(value.Trim().TrimStart('v'), out version);

    private sealed record ModManifest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("min_game_version")] string MinimumGameVersion,
        [property: JsonPropertyName("has_pck")] bool HasPck,
        [property: JsonPropertyName("has_dll")] bool HasDll,
        [property: JsonPropertyName("dependencies")] IReadOnlyList<ModDependency> Dependencies);

    private sealed record ModDependency(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("min_version")] string MinimumVersion);
}
