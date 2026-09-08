using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SakuraMod.SakuraModCode.Telemetry;

internal interface ITelemetryQuarantineStore
{
    bool Contains(string batchJson);
    void Save(string batchJson, string reason);
}

internal sealed class FileTelemetryQuarantineStore(Func<string> directory) : ITelemetryQuarantineStore
{
    internal const string UserPath = "user://sakuramod_telemetry_quarantine";
    internal const long MaxStorageBytes = 64 * 1024 * 1024;
    private readonly object _gate = new();

    public bool Contains(string batchJson) => Contains(Path.Combine(directory(), FileName(batchJson)), batchJson);

    private static string FileName(string batchJson) =>
        $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(batchJson)))}.json";

    private static bool Contains(string path, string batchJson)
    {
        if (!File.Exists(path))
            return false;
        using var saved = JsonDocument.Parse(File.ReadAllBytes(path));
        if (!saved.RootElement.TryGetProperty("batch", out var batch) || batch.GetRawText() != batchJson)
            throw new IOException("Existing quarantine record does not match its content hash.");
        return true;
    }

    public void Save(string batchJson, string reason)
    {
        lock (_gate)
        {
            var root = directory();
            if (OperatingSystem.IsWindows())
                Directory.CreateDirectory(root);
            else
                Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            // Multiple game processes may share user://. A busy lock fails the
            // send so RitsuLib retains its queue instead of racing the quota.
            using var quotaLock = new FileStream(Path.Combine(root, ".lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var destination = Path.Combine(root, FileName(batchJson));
            if (Contains(destination, batchJson))
                return;

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("schema", "sakuramod.telemetry.quarantine.v1");
                writer.WriteString("quarantined_at", DateTimeOffset.UtcNow);
                writer.WriteString("reason", reason);
                writer.WritePropertyName("batch");
                writer.WriteRawValue(batchJson);
                writer.WriteEndObject();
            }
            var bytes = buffer.ToArray();
            var used = Directory.EnumerateFiles(root).Sum(path => new FileInfo(path).Length);
            if (used + bytes.Length > MaxStorageBytes)
                throw new IOException("Telemetry quarantine reached its 64 MiB capacity; export or remove saved records before retrying.");

            var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var file = new FileStream(temporary, options))
                {
                    file.Write(bytes);
                    file.Flush(flushToDisk: true);
                }
                File.Move(temporary, destination);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
