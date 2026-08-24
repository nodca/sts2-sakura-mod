using System.Text.Json;
using System.Net;

namespace SakuraMod.TestProtocol;

public static class SakuraTestProtocol
{
    public const int CurrentSchemaVersion = 2;
    public const string RequestEnvironmentVariable = "SAKURAMOD_TEST_REQUEST";

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    public static async Task WriteAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The protocol path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 64 * 1024,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The protocol path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 64 * 1024,
                       FileOptions.SequentialScan))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Protocol document is empty: {path}");
    }

    public static T Read<T>(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Protocol document is empty: {path}");
    }

    public static async Task AppendCheckpointAsync(
        string path,
        SakuraTestCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The checkpoint path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(checkpoint, JsonOptions).ReplaceLineEndings(string.Empty);
        await File.AppendAllTextAsync(path, json + Environment.NewLine, cancellationToken);
    }

    public static void AppendCheckpoint(string path, SakuraTestCheckpoint checkpoint)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The checkpoint path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var compactOptions = new JsonSerializerOptions(JsonOptions) { WriteIndented = false };
        File.AppendAllText(path, JsonSerializer.Serialize(checkpoint, compactOptions) + Environment.NewLine);
    }

    public static void ValidateRequest(SakuraTestRequest request)
    {
        if (request.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported request schema {request.SchemaVersion}; expected {CurrentSchemaVersion}.");
        if (string.IsNullOrWhiteSpace(request.RunId)
            || string.IsNullOrWhiteSpace(request.Layer)
            || string.IsNullOrWhiteSpace(request.ScenarioId)
            || !Path.IsPathFullyQualified(request.ArtifactRoot)
            || !IsWithin(request.ArtifactRoot, request.ResultPath)
            || !IsWithin(request.ArtifactRoot, request.CheckpointPath))
            throw new InvalidDataException("Runtime request identity or artifact paths are invalid.");
        if (request.PriorSnapshotPath is not null
            && !IsWithin(request.ArtifactRoot, request.PriorSnapshotPath))
            throw new InvalidDataException("Runtime request snapshot path is outside the artifact root.");

        if (request.Layer == "multiplayer")
            ValidateMultiplayer(request);
        else if (request.Multiplayer is not null)
            throw new InvalidDataException("A multiplayer block is only valid for the multiplayer layer.");
    }

    private static void ValidateMultiplayer(SakuraTestRequest request)
    {
        var multiplayer = request.Multiplayer
            ?? throw new InvalidDataException("A multiplayer request requires a multiplayer block.");
        if (!SakuraMultiplayerRoles.IsKnown(multiplayer.Role))
            throw new InvalidDataException($"Unknown multiplayer role '{multiplayer.Role}'.");
        if (!IPAddress.TryParse(multiplayer.HostAddress, out var address) || !IPAddress.IsLoopback(address))
            throw new InvalidDataException("The multiplayer host address must be a loopback IP address.");
        if (multiplayer.Port == 0 || multiplayer.LocalNetId == 0)
            throw new InvalidDataException("The multiplayer port and local net id must be non-zero.");
        if (multiplayer.PeerCount < SakuraMultiplayerRoles.MinPeerCount
            || multiplayer.PeerCount > SakuraMultiplayerRoles.MaxPeerCount)
            throw new InvalidDataException(
                $"The multiplayer peer count must be between {SakuraMultiplayerRoles.MinPeerCount} "
                + $"and {SakuraMultiplayerRoles.MaxPeerCount}; found {multiplayer.PeerCount}.");
        if (SakuraMultiplayerRoles.SeatIndex(multiplayer.Role) >= multiplayer.PeerCount)
            throw new InvalidDataException(
                $"Role '{multiplayer.Role}' has no seat in a {multiplayer.PeerCount}-peer session.");
        if (multiplayer.LocalNetId > (ulong)multiplayer.PeerCount)
            throw new InvalidDataException(
                $"Local net id {multiplayer.LocalNetId} is outside a {multiplayer.PeerCount}-peer session.");
        if (!IsWithin(request.ArtifactRoot, multiplayer.CoordinationRoot))
            throw new InvalidDataException("The multiplayer coordination root is outside the artifact root.");
    }

    public static bool IsWithin(string root, string path)
    {
        if (!Path.IsPathFullyQualified(root) || !Path.IsPathFullyQualified(path))
            return false;
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}

public sealed record SakuraTestRequest(
    int SchemaVersion,
    string RunId,
    string Layer,
    string ScenarioId,
    string Phase,
    string ExpectedGameVersion,
    string ExpectedRitsuVersion,
    string ExpectedSakuraVersion,
    ulong Seed,
    string Locale,
    int TimeoutSeconds,
    string ArtifactRoot,
    string ResultPath,
    string CheckpointPath,
    string? PriorSnapshotPath = null,
    SakuraMultiplayerRequest? Multiplayer = null);

public static class SakuraMultiplayerRoles
{
    public const string Host = "Host";
    public const string Client = "Client";
    public const string SecondClient = "SecondClient";

    public const int MinPeerCount = 2;
    public const int MaxPeerCount = 3;

    /// <summary>
    /// Seat order is the ENet net-id order: the host is always net id 1, and each
    /// client seat takes the next id. Scenarios rely on this to address peers.
    /// </summary>
    public static IReadOnlyList<string> SeatOrder { get; } = [Host, Client, SecondClient];

    public static bool IsKnown(string role) => SeatOrder.Contains(role, StringComparer.Ordinal);

    public static int SeatIndex(string role)
    {
        var index = SeatOrder.ToList().IndexOf(role);
        return index >= 0
            ? index
            : throw new InvalidDataException($"Unknown multiplayer role '{role}'.");
    }

    public static ulong NetIdFor(string role) => (ulong)(SeatIndex(role) + 1);

    public static IReadOnlyList<string> SeatsFor(int peerCount) =>
        peerCount >= MinPeerCount && peerCount <= MaxPeerCount
            ? SeatOrder.Take(peerCount).ToList()
            : throw new InvalidDataException($"Unsupported multiplayer peer count {peerCount}.");
}

public static class SakuraMultiplayerScenarios
{
    public const string ClowDefensivePowers = "clow-defensive-powers";
    public const string ClowSweetPartyEffect = "clow-sweet-party-effect";
    public const string ClowSilentHost = "clow-silent-host";
    public const string ClowSilentClient = "clow-silent-client";
    public const string ClowShieldHost = "clow-shield-host";
    public const string ClowShieldClient = "clow-shield-client";
    public const string ClowShieldWard = "clow-shield-ward";
    public const string ClowTwinPlayCount = "clow-twin-play-count";
    public const string KindnessExhaustReturn = "kindness-exhaust-return";
    public const string SealedWandCharge = "sealed-wand-charge";
    public const string TurnEndDamageSync = "turn-end-damage-sync";
    public const string ThreePlayerDefensivePowers = "three-player-defensive-powers";
    public const string ThreePlayerRepairJump = "three-player-repair-jump";
    public const string ThreePlayerRepairJumpLoad = "three-player-repair-jump-load";
    public const string ThreePlayerMirrorCopy = "three-player-mirror-copy";

    public static IReadOnlyList<string> All { get; } =
    [
        ClowDefensivePowers,
        ClowSweetPartyEffect,
        ClowSilentHost,
        ClowSilentClient,
        ClowShieldHost,
        ClowShieldClient,
        ClowShieldWard,
        ClowTwinPlayCount,
        KindnessExhaustReturn,
        SealedWandCharge,
        TurnEndDamageSync,
        ThreePlayerDefensivePowers,
        ThreePlayerRepairJump,
        ThreePlayerRepairJumpLoad,
        ThreePlayerMirrorCopy
    ];

    private static readonly HashSet<string> ThreePlayerScenarios = new(StringComparer.Ordinal)
    {
        ThreePlayerDefensivePowers,
        ThreePlayerRepairJump,
        ThreePlayerRepairJumpLoad,
        ThreePlayerMirrorCopy
    };

    /// <summary>
    /// Peer count is a property of the scenario, not a runner flag: the native
    /// power-scaling and side-turn-end paths differ between two and three players,
    /// so each scenario must pin the seat count it asserts against.
    /// </summary>
    public static int PeerCountFor(string scenarioId) =>
        ThreePlayerScenarios.Contains(scenarioId)
            ? 3
            : SakuraMultiplayerRoles.MinPeerCount;

    public static bool IsSaveLoad(string scenarioId) =>
        string.Equals(scenarioId, ThreePlayerRepairJumpLoad, StringComparison.Ordinal);
}

public sealed record SakuraMultiplayerRequest(
    string Role,
    string HostAddress,
    ushort Port,
    ulong LocalNetId,
    string CoordinationRoot,
    int PeerCount = SakuraMultiplayerRoles.MinPeerCount);

public sealed record SakuraTestCheckpoint(
    int SchemaVersion,
    string RunId,
    string ScenarioId,
    string Phase,
    DateTimeOffset OccurredAtUtc,
    string Message);

public sealed record SakuraTestAssertion(
    string Name,
    string Status,
    string? Expected,
    string? Actual,
    string? Detail = null);

public sealed record SakuraTestFailure(
    string Type,
    string Message,
    string? StackTrace = null);

public sealed record SakuraLoadedMod(
    string Id,
    string? Version,
    string State,
    string Source,
    string? AssemblyName,
    string? AssemblyVersion);

public sealed record SakuraRuntimeEnvironment(
    string GameVersion,
    string RitsuVersion,
    string SakuraVersion,
    string RuntimeTestVersion,
    string OperatingSystem,
    string FrameworkDescription,
    IReadOnlyList<SakuraLoadedMod> LoadedMods);

public sealed record SakuraTestResult(
    int SchemaVersion,
    string RunId,
    string Layer,
    string ScenarioId,
    string Phase,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    long DurationMilliseconds,
    SakuraRuntimeEnvironment? Environment,
    IReadOnlyList<SakuraTestAssertion> Assertions,
    IReadOnlyDictionary<string, object?> SemanticSnapshots,
    IReadOnlyList<SakuraTestFailure> Failures,
    IReadOnlyList<string> Artifacts);

public sealed record SakuraMultiplayerPeerResult(
    string Role,
    ulong LocalNetId,
    int? ProcessExitCode,
    bool TimedOut,
    string RuntimeResultPath,
    string CheckpointPath,
    string GameLogPath,
    string? Failure);

public sealed record SakuraMultiplayerRunnerResult(
    int SchemaVersion,
    string RunId,
    string ScenarioId,
    string Status,
    int PeerCount,
    IReadOnlyList<SakuraMultiplayerPeerResult> Peers,
    IReadOnlyList<ProtectedRootSnapshot> ProtectedRootsBefore,
    IReadOnlyList<ProtectedRootSnapshot> ProtectedRootsAfter,
    string? Failure);

public sealed record ProtectedRootSnapshot(
    string Root,
    long FileCount,
    long DirectoryCount,
    long TotalBytes,
    string Sha256);
