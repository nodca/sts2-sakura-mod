using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace SakuraMod.TestRunner;

public sealed record ProtectedDirectoryFingerprint(
    string Path,
    string Sha256,
    long FileCount,
    long DirectoryCount,
    long TotalBytes);

public static class DirectoryFingerprinter
{
    private const int BufferSize = 128 * 1024;
    private const long ProgressByteInterval = 128L * 1024 * 1024;

    public static async Task<ProtectedDirectoryFingerprint> ComputeAsync(
        string root,
        CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var state = new FingerprintState(root);
        if (!Directory.Exists(root))
        {
            AppendToken(hash, "absent\0");
            return new ProtectedDirectoryFingerprint(root, Convert.ToHexStringLower(hash.GetHashAndReset()), 0, 0, 0);
        }

        await VisitDirectoryAsync(root, string.Empty, hash, state, cancellationToken);
        Console.WriteLine(
            $"[isolation] fingerprint complete: {root} ({state.FileCount} files, {state.TotalBytes} bytes)");
        return new ProtectedDirectoryFingerprint(
            root,
            Convert.ToHexStringLower(hash.GetHashAndReset()),
            state.FileCount,
            state.DirectoryCount,
            state.TotalBytes);
    }

    public static void RequireEqual(
        IReadOnlyList<ProtectedDirectoryFingerprint> before,
        IReadOnlyList<ProtectedDirectoryFingerprint> after)
    {
        if (before.Count != after.Count)
        {
            throw new InvalidOperationException("Protected-directory fingerprint set changed shape.");
        }

        for (var index = 0; index < before.Count; index++)
        {
            var expected = before[index];
            var actual = after[index];
            if (!string.Equals(expected.Path, actual.Path, StringComparison.Ordinal)
                || !string.Equals(expected.Sha256, actual.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Protected directory changed during the test: {expected.Path} " +
                    $"({expected.Sha256} -> {actual.Sha256}).");
            }
        }
    }

    private static async Task VisitDirectoryAsync(
        string absolutePath,
        string relativePath,
        IncrementalHash hash,
        FingerprintState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.DirectoryCount++;
        AppendToken(hash, $"D\0{Normalize(relativePath)}\0");
        var entries = new DirectoryInfo(absolutePath)
            .EnumerateFileSystemInfos()
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childRelative = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : Path.Combine(relativePath, entry.Name);
            if (entry.LinkTarget is not null)
            {
                AppendToken(hash, $"L\0{Normalize(childRelative)}\0{entry.LinkTarget}\0");
                continue;
            }

            if (entry is DirectoryInfo)
            {
                await VisitDirectoryAsync(entry.FullName, childRelative, hash, state, cancellationToken);
                continue;
            }

            var file = (FileInfo)entry;
            state.FileCount++;
            state.TotalBytes += file.Length;
            AppendToken(hash, $"F\0{Normalize(childRelative)}\0{file.Length}\0");
            if (file.Length > 0)
            {
                await AppendFileAsync(file.FullName, hash, cancellationToken);
            }
            state.ReportProgressIfNeeded();
        }
    }

    private static async Task AppendFileAsync(
        string path,
        IncrementalHash hash,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            while (true)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken);
                if (count == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, count);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void AppendToken(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    private sealed class FingerprintState(string root)
    {
        private long _lastReportedBytes;

        public long FileCount { get; set; }
        public long DirectoryCount { get; set; }
        public long TotalBytes { get; set; }

        public void ReportProgressIfNeeded()
        {
            if (TotalBytes - _lastReportedBytes < ProgressByteInterval && FileCount % 1000 != 0)
            {
                return;
            }

            _lastReportedBytes = TotalBytes;
            Console.WriteLine($"[isolation] fingerprinting {root}: {FileCount} files, {TotalBytes} bytes");
        }
    }
}
