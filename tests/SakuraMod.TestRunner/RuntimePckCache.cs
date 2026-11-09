using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

internal sealed class RuntimePckCache(string repoRoot, string godotPath)
{
    private readonly string _cacheDirectory = Path.Combine(repoRoot, "artifacts", "tests", ".pck-cache");
    private const string PckName = "SakuraMod.pck";
    private const string MetadataName = "entry.json";

    private sealed record Entry(string InputKey, string PckHash);

    public async Task<string?> GetInputKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The BasicExport preset packages SakuraMod and Godot's generated resources.
            var hashes = new List<string> { "runtime-pck-v1", godotPath, await HashFileAsync(godotPath, cancellationToken) };
            foreach (var name in new[] { "SakuraMod", "SakuraModCode", ".godot/imported", ".godot/exported" })
            {
                var fingerprint = await DirectoryFingerprinter.ComputeAsync(
                    Path.Combine(repoRoot, name), cancellationToken, rejectLinks: true);
                hashes.Add(fingerprint.Sha256);
            }

            foreach (var name in new[]
                     {
                         "project.godot", "export_presets.cfg", "SakuraMod.csproj", "Directory.Build.props",
                         "Directory.Build.targets", "Sts2PathDiscovery.props", "global.json",
                         ".godot/uid_cache.bin", ".godot/global_script_class_cache.cfg"
                     })
            {
                var path = Path.Combine(repoRoot, name);
                hashes.Add(File.Exists(path) ? await HashFileAsync(path, cancellationToken) : "absent");
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', hashes))));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[package] PCK cache inputs unavailable: {exception.Message}");
            return null;
        }
    }

    public async Task<bool> TryStageAsync(string inputKey, string stagePck, CancellationToken cancellationToken)
    {
        try
        {
            var metadataPath = Path.Combine(_cacheDirectory, MetadataName);
            if (!File.Exists(metadataPath))
                return false;

            var entry = await SakuraTestProtocol.ReadAsync<Entry>(metadataPath, cancellationToken);
            if (entry.InputKey != inputKey)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(stagePck)!);
            File.Copy(Path.Combine(_cacheDirectory, PckName), stagePck);
            if (string.Equals(await HashFileAsync(stagePck, cancellationToken), entry.PckHash, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"[package] PCK cache entry unavailable: {exception.Message}");
        }

        File.Delete(stagePck);
        return false;
    }

    public async Task StoreAsync(string inputKey, string stagePck, string verifiedHash, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(_cacheDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            File.Copy(stagePck, temporary);
            // Validate the copied bytes on every hit, including when another runner replaces the slot.
            File.Move(temporary, Path.Combine(_cacheDirectory, PckName), overwrite: true);
            await SakuraTestProtocol.WriteAtomicAsync(
                Path.Combine(_cacheDirectory, MetadataName), new Entry(inputKey, verifiedHash), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[package] Could not update PCK cache: {exception.Message}");
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
