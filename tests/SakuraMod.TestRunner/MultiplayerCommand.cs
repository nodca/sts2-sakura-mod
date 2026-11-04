using SakuraMod.TestProtocol;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace SakuraMod.TestRunner;

public static class MultiplayerCommand
{
    private const int DefaultTimeoutSeconds = 120;
    private const ulong ScenarioSeed = 0x53414B555241UL;
    private const ulong HostNetId = 1;
    private static readonly TimeSpan HostCheckpointTimeout = TimeSpan.FromSeconds(30);

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Usage: scripts/test-mod multiplayer --scenario <id>");
        writer.WriteLine();
        writer.WriteLine("Available scenarios:");
        foreach (var scenarioId in SakuraMultiplayerScenarios.All)
            writer.WriteLine($"  {scenarioId} ({SakuraMultiplayerScenarios.ExpectedPlayerCount(scenarioId)} peers)");
    }

    public static async Task<int> RunAsync(string repoRoot, string[] args)
    {
        var scenarioId = ParseScenario(args);
        if (scenarioId is null)
            return 2;

        var startedAt = DateTimeOffset.UtcNow;
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-multiplayer-{scenarioId}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        var coordinationRoot = Path.Combine(runRoot, "coordination");
        var runnerResultPath = Path.Combine(runRoot, "runner-result.json");
        var expectedPlayerCount = SakuraMultiplayerScenarios.ExpectedPlayerCount(scenarioId);
        Directory.CreateDirectory(coordinationRoot);
        Console.WriteLine($"[multiplayer] run: {runId}");
        Console.WriteLine($"[multiplayer] artifacts: {runRoot}");
        Console.WriteLine($"[multiplayer] peers: {expectedPlayerCount}");

        var before = new List<ProtectedDirectoryFingerprint>();
        var after = new List<ProtectedDirectoryFingerprint>();
        RuntimeBuildArtifacts? build = null;
        var peers = CreatePeerPaths(runRoot, expectedPlayerCount, attempt: 1);
        var outcomes = new Dictionary<ulong, RuntimeProcessOutcome>();
        Exception? failure = null;

        try
        {
            build = await RuntimeBuildPreparation.PrepareAsync(repoRoot, runId, runRoot);
            before.Add(await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealModsDirectory));
            before.Add(await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealUserDataDirectory));
            await SakuraTestProtocol.WriteAtomicAsync(
                Path.Combine(runRoot, "protected-roots-before.json"), before);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                peers = CreatePeerPaths(runRoot, expectedPlayerCount, attempt);
                var attemptCoordinationRoot = attempt == 1
                    ? coordinationRoot
                    : Path.Combine(coordinationRoot, $"attempt-{attempt}");
                Directory.CreateDirectory(attemptCoordinationRoot);
                var workspaces = new Dictionary<ulong, RuntimeWorkspace>();
                foreach (var peer in peers)
                    workspaces.Add(peer.LocalNetId, await CreateWorkspaceAsync(repoRoot, build, peer, scenarioId));
                try
                {
                    if (SakuraMultiplayerScenarios.IsSaveLoad(scenarioId))
                    {
                        var writePeers = CreatePeerPaths(runRoot, expectedPlayerCount, attempt, phase: "write");
                        var writePreparedPeers = CreatePreparedPeers(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            Path.Combine(attemptCoordinationRoot, "write"),
                            writePeers,
                            workspaces,
                            AllocateLoopbackPort(),
                            expectedPlayerCount,
                            phase: "write");
                        Directory.CreateDirectory(Path.Combine(attemptCoordinationRoot, "write"));
                        ValidatePeerRequests(writePreparedPeers.Select(static peer => peer.Request).ToArray());
                        var writeOutcomes = await RunPeerGroupAsync(writePreparedPeers);
                        RequireMatchingSnapshot(
                            writePeers.Select(peer => writeOutcomes[peer.LocalNetId].Result).ToArray(),
                            "comparison");

                        peers = CreatePeerPaths(runRoot, expectedPlayerCount, attempt, phase: "read");
                        var readCoordinationRoot = Path.Combine(attemptCoordinationRoot, "read");
                        Directory.CreateDirectory(readCoordinationRoot);
                        var readPreparedPeers = CreatePreparedPeers(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            readCoordinationRoot,
                            peers,
                            workspaces,
                            AllocateLoopbackPort(),
                            expectedPlayerCount,
                            phase: "read");
                        ValidatePeerRequests(readPreparedPeers.Select(static peer => peer.Request).ToArray());
                        outcomes = await RunPeerGroupAsync(readPreparedPeers);
                    }
                    else
                    {
                        var preparedPeers = CreatePreparedPeers(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            attemptCoordinationRoot,
                            peers,
                            workspaces,
                            AllocateLoopbackPort(),
                            expectedPlayerCount,
                            phase: null);
                        ValidatePeerRequests(preparedPeers.Select(static peer => peer.Request).ToArray());
                        outcomes = await RunPeerGroupAsync(preparedPeers);
                    }
                    RequireMatchingSnapshot(
                        peers.Select(peer => outcomes[peer.LocalNetId].Result).ToArray(),
                        "comparison");
                    break;
                }
                catch (PeerProcessFailureException exception)
                {
                    outcomes[exception.LocalNetId] = exception.Outcome;
                    if (attempt >= 3 || !IsRetryableStartupFailure(peers))
                        throw;
                    Console.Error.WriteLine(
                        $"[multiplayer] startup attempt {attempt} failed ({exception.GetBaseException().Message}); retrying with a fresh endpoint.");
                    outcomes.Clear();
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (build is not null)
            {
                after.Add(await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealModsDirectory));
                after.Add(await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealUserDataDirectory));
                await SakuraTestProtocol.WriteAtomicAsync(
                    Path.Combine(runRoot, "protected-roots-after.json"), after);
                try
                {
                    DirectoryFingerprinter.RequireEqual(before, after);
                }
                catch (Exception isolationFailure)
                {
                    failure = failure is null
                        ? isolationFailure
                        : new AggregateException(failure, isolationFailure);
                }
            }
        }

        if (failure is not null)
        {
            foreach (var peer in peers)
                await WriteSyntheticPeerFailureIfMissingAsync(peer, runId, scenarioId, startedAt, failure);
        }

        var aggregate = new SakuraMultiplayerRunnerResult(
            SakuraTestProtocol.CurrentSchemaVersion,
            runId,
            scenarioId,
            failure is null ? "PASS" : "FAIL",
            expectedPlayerCount,
            peers.Select(peer => ProjectPeer(
                peer,
                outcomes.GetValueOrDefault(peer.LocalNetId),
                failure)).ToArray(),
            before.Select(ProjectFingerprint).ToArray(),
            after.Select(ProjectFingerprint).ToArray(),
            failure?.ToString());
        await SakuraTestProtocol.WriteAtomicAsync(runnerResultPath, aggregate);

        if (failure is not null)
        {
            Console.Error.WriteLine($"[multiplayer] FAIL: {failure.GetBaseException().Message}");
            Console.Error.WriteLine($"[multiplayer] artifacts retained: {runRoot}");
            return 1;
        }

        Console.WriteLine($"[multiplayer] PASS: all {expectedPlayerCount} peers converged on the {scenarioId} snapshot");
        return 0;
    }

    internal static string? ParseScenario(string[] args)
    {
        if (args.Length != 2 || args[0] != "--scenario")
        {
            PrintHelp(Console.Error);
            return null;
        }
        if (!SakuraMultiplayerScenarios.All.Contains(args[1], StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"Unknown multiplayer scenario '{args[1]}'.");
            PrintHelp(Console.Error);
            return null;
        }
        return args[1];
    }

    public static void ValidatePeerRequests(IReadOnlyList<SakuraTestRequest> requests)
    {
        if (requests.Count < 2)
            throw new InvalidDataException("A multiplayer peer group requires at least two requests.");
        foreach (var request in requests)
            SakuraTestProtocol.ValidateRequest(request);

        var first = requests[0];
        var expectedPlayerCount = first.Multiplayer!.ExpectedPlayerCount;
        var expectedNetIds = Enumerable.Range(1, expectedPlayerCount)
            .Select(static value => (ulong)value)
            .ToArray();
        var actualNetIds = requests.Select(static request => request.Multiplayer!.LocalNetId)
            .Order()
            .ToArray();
        if (requests.Count != expectedPlayerCount
            || requests.Count(static request => request.Multiplayer!.Role == SakuraMultiplayerRoles.Host) != 1
            || requests.Count(static request => request.Multiplayer!.Role == SakuraMultiplayerRoles.Client)
                != expectedPlayerCount - 1
            || first.Multiplayer.Role != SakuraMultiplayerRoles.Host
            || first.Multiplayer.LocalNetId != HostNetId
            || !actualNetIds.SequenceEqual(expectedNetIds)
            || requests.Any(request => request.RunId != first.RunId
                || request.ScenarioId != first.ScenarioId
                || request.Multiplayer!.ExpectedPlayerCount != expectedPlayerCount
                || request.Multiplayer.HostAddress != first.Multiplayer.HostAddress
                || request.Multiplayer.Port != first.Multiplayer.Port
                || request.Multiplayer.CoordinationRoot != first.Multiplayer.CoordinationRoot)
            || (SakuraMultiplayerScenarios.IsSaveLoad(first.ScenarioId)
                ? requests.Any(request => request.Phase != first.Phase)
                    || first.Phase is not ("write" or "read")
                : requests.Select(static request => request.Phase).Distinct(StringComparer.Ordinal).Count() != requests.Count))
        {
            throw new InvalidDataException(
                "Multiplayer requests do not form one complete, uniquely identified peer group.");
        }
    }

    public static void RequireMatchingSnapshot(
        IReadOnlyList<SakuraTestResult> results,
        string snapshotName)
    {
        if (results.Count < 2)
            throw new InvalidDataException("Snapshot comparison requires at least two peer results.");
        if (!results[0].SemanticSnapshots.TryGetValue(snapshotName, out var baselineSnapshot))
            throw new InvalidDataException($"Every peer must emit the '{snapshotName}' semantic snapshot.");
        var baselineJson = JsonSerializer.Serialize(baselineSnapshot, SakuraTestProtocol.JsonOptions);
        foreach (var result in results.Skip(1))
        {
            if (!result.SemanticSnapshots.TryGetValue(snapshotName, out var snapshot))
                throw new InvalidDataException($"Every peer must emit the '{snapshotName}' semantic snapshot.");
            var json = JsonSerializer.Serialize(snapshot, SakuraTestProtocol.JsonOptions);
            if (!string.Equals(baselineJson, json, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Peer '{result.Phase}' '{snapshotName}' semantic snapshot diverged from the host.");
            }
        }
    }

    private static async Task<Dictionary<ulong, RuntimeProcessOutcome>> RunPeerGroupAsync(
        IReadOnlyList<PreparedPeer> peers)
    {
        var host = peers.Single(static peer => peer.Paths.Role == SakuraMultiplayerRoles.Host);
        using var cancellation = new CancellationTokenSource();
        var hostTask = RuntimeProcessSession.RunAsync(
            host.Workspace,
            host.Request,
            host.Paths.RequestPath,
            host.Paths.GameLogPath,
            cancellation.Token);
        var tasks = new Dictionary<ulong, Task<RuntimeProcessOutcome>>
        {
            [host.Paths.LocalNetId] = hostTask
        };
        try
        {
            await WaitForCheckpointAsync(
                host.Paths.CheckpointPath,
                host.Request,
                "host_listening",
                hostTask,
                HostCheckpointTimeout,
                cancellation.Token);
            foreach (var client in peers.Where(static peer => peer.Paths.Role == SakuraMultiplayerRoles.Client))
            {
                tasks[client.Paths.LocalNetId] = RuntimeProcessSession.RunAsync(
                    client.Workspace,
                    client.Request,
                    client.Paths.RequestPath,
                    client.Paths.GameLogPath,
                    cancellation.Token);
            }

            var remaining = peers.ToList();
            var outcomes = new Dictionary<ulong, RuntimeProcessOutcome>();
            while (remaining.Count > 0)
            {
                var completed = await Task.WhenAny(remaining.Select(peer => tasks[peer.Paths.LocalNetId]));
                var peer = remaining.Single(value => ReferenceEquals(tasks[value.Paths.LocalNetId], completed));
                var outcome = await completed;
                RequirePassingPeer(outcome, peer.Paths);
                outcomes[peer.Paths.LocalNetId] = outcome;
                remaining.Remove(peer);
            }
            return outcomes;
        }
        catch
        {
            cancellation.Cancel();
            foreach (var task in tasks.Values)
                await ObserveAsync(task);
            throw;
        }
    }

    public static async Task WaitForCheckpointAsync(
        string checkpointPath,
        SakuraTestRequest request,
        string phase,
        Task peerTask,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            if (TryFindCheckpoint(checkpointPath, request, phase))
                return;
            if (peerTask.IsCompleted)
            {
                await peerTask;
                throw new InvalidDataException($"Peer exited before checkpoint '{phase}'.");
            }
            try
            {
                await Task.Delay(50, deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for peer checkpoint '{phase}'.");
            }
        }
    }

    private static bool TryFindCheckpoint(string path, SakuraTestRequest request, string phase)
    {
        if (!File.Exists(path))
            return false;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            SakuraTestCheckpoint? checkpoint;
            try
            {
                checkpoint = JsonSerializer.Deserialize<SakuraTestCheckpoint>(line, SakuraTestProtocol.JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }
            if (checkpoint is not null
                && checkpoint.SchemaVersion == SakuraTestProtocol.CurrentSchemaVersion
                && checkpoint.RunId == request.RunId
                && checkpoint.ScenarioId == request.ScenarioId
                && checkpoint.Phase == phase)
                return true;
        }
        return false;
    }

    private static async Task<RuntimeWorkspace> CreateWorkspaceAsync(
        string repoRoot,
        RuntimeBuildArtifacts build,
        PeerPaths peer,
        string scenarioId)
    {
        Directory.CreateDirectory(peer.Root);
        var workspace = RuntimeWorkspaceBuilder.Create(
            build.Prerequisites,
            peer.Root,
            build.Package.PackageDirectory,
            build.RuntimeTestAssembly,
            Path.Combine(repoRoot, "tests", "SakuraMod.RuntimeTests", "SakuraMod.RuntimeTests.json"),
            ExtraModsFor(scenarioId));
        var selfCheck = Path.Combine(peer.Root, "self-check");
        Directory.CreateDirectory(selfCheck);
        await RuntimeProfile.WriteStrictSettingsAsync(
            workspace,
            selfCheck,
            runSelfCheck: false,
            enableKitLibTestModules: SakuraMultiplayerScenarios.RequiresKitLib(scenarioId));
        return workspace;
    }

    private static IReadOnlyDictionary<string, string>? ExtraModsFor(string scenarioId)
    {
        var extraMods = new Dictionary<string, string>(StringComparer.Ordinal);
        if (SakuraMultiplayerScenarios.RequiresKitLib(scenarioId))
        {
            var configured = Environment.GetEnvironmentVariable("SAKURAMOD_KITLIB_ROOT");
            var root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share", "Steam", "steamapps", "workshop", "content", "2868840", "3747619669")
                : Path.GetFullPath(configured);
            if (!File.Exists(Path.Combine(root, "mod_manifest.json"))
                || !File.Exists(Path.Combine(root, "KitLib.dll")))
            {
                throw new DirectoryNotFoundException(
                    $"KitLib Workshop package is incomplete or unavailable: {root}");
            }

            extraMods["KitLib"] = root;
        }

        if (SakuraMultiplayerScenarios.RequiresRandomForeseer(scenarioId))
        {
            var configured = Environment.GetEnvironmentVariable("SAKURAMOD_RANDOM_FORESEER_ROOT");
            var root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share", "Steam", "steamapps", "workshop", "content", "2868840", "3747531952")
                : Path.GetFullPath(configured);
            if (!File.Exists(Path.Combine(root, "RandomForeseer.json"))
                || !File.Exists(Path.Combine(root, "RandomForeseer.dll")))
            {
                throw new DirectoryNotFoundException(
                    $"RandomForeseer Workshop package is incomplete or unavailable: {root}");
            }

            extraMods["RandomForeseer"] = root;
        }

        return extraMods.Count == 0 ? null : extraMods;
    }

    private static SakuraTestRequest CreateRequest(
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string phase,
        string runRoot,
        string coordinationRoot,
        PeerPaths peer,
        ushort port,
        int expectedPlayerCount) => new(
        SakuraTestProtocol.CurrentSchemaVersion,
        runId,
        "multiplayer",
        scenarioId,
        phase,
        prerequisites.GameVersion,
        prerequisites.RitsuVersion,
        prerequisites.SakuraVersion,
        ScenarioSeed,
        "eng",
        DefaultTimeoutSeconds,
        runRoot,
        peer.ResultPath,
        peer.CheckpointPath,
        Multiplayer: new SakuraMultiplayerRequest(
            peer.Role,
            IPAddress.Loopback.ToString(),
            port,
            peer.LocalNetId,
            expectedPlayerCount,
            coordinationRoot));

    private static IReadOnlyList<PreparedPeer> CreatePreparedPeers(
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string runRoot,
        string coordinationRoot,
        IReadOnlyList<PeerPaths> peers,
        IReadOnlyDictionary<ulong, RuntimeWorkspace> workspaces,
        ushort port,
        int expectedPlayerCount,
        string? phase) => peers.Select(peer => new PreparedPeer(
            peer,
            workspaces[peer.LocalNetId],
            CreateRequest(
                prerequisites,
                runId,
                scenarioId,
                phase ?? peer.Name,
                runRoot,
                coordinationRoot,
                peer,
                port,
                expectedPlayerCount))).ToArray();

    private static ushort AllocateLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return checked((ushort)((IPEndPoint)socket.LocalEndPoint!).Port);
    }

    private static void RequirePassingPeer(RuntimeProcessOutcome outcome, PeerPaths peer)
    {
        if (outcome.Process.ExitCode != 0 || outcome.Result.Status != "PASS")
            throw new PeerProcessFailureException(peer.Role, peer.LocalNetId, outcome);
    }

    private static bool IsRetryableStartupFailure(IReadOnlyList<PeerPaths> peers)
    {
        foreach (var peer in peers)
        {
            if (!File.Exists(peer.ResultPath))
                continue;
            try
            {
                var result = SakuraTestProtocol.Read<SakuraTestResult>(peer.ResultPath);
                if (result.Failures.Any(static failure =>
                        failure.Type.Contains("ClientConnectionFailedException", StringComparison.Ordinal)
                        || failure.Message.Contains("bind loopback ENet host", StringComparison.OrdinalIgnoreCase)
                        || failure.Message.Contains("before checkpoint 'host_listening'", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                return false;
            }
        }
        return false;
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch { }
    }

    private static Task WriteSyntheticPeerFailureIfMissingAsync(
        PeerPaths peer,
        string runId,
        string scenarioId,
        DateTimeOffset startedAt,
        Exception exception)
    {
        if (File.Exists(peer.ResultPath))
            return Task.CompletedTask;
        return SakuraTestProtocol.WriteAtomicAsync(
            peer.ResultPath,
            new SakuraTestResult(
                SakuraTestProtocol.CurrentSchemaVersion,
                runId,
                "multiplayer",
                scenarioId,
                peer.Name,
                "FAIL",
                startedAt,
                DateTimeOffset.UtcNow,
                (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                null,
                [],
                new Dictionary<string, object?>(),
                [new SakuraTestFailure(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.StackTrace)],
                []));
    }

    private static SakuraMultiplayerPeerResult ProjectPeer(
        PeerPaths peer,
        RuntimeProcessOutcome? outcome,
        Exception? failure) => new(
        peer.Role,
        peer.LocalNetId,
        outcome?.Process.ExitCode,
        failure is TimeoutException,
        peer.ResultPath,
        peer.CheckpointPath,
        peer.GameLogPath,
        outcome is null ? failure?.Message : null);

    private static ProtectedRootSnapshot ProjectFingerprint(ProtectedDirectoryFingerprint value) => new(
        value.Path,
        value.FileCount,
        value.DirectoryCount,
        value.TotalBytes,
        value.Sha256);

    private sealed record PeerPaths(
        string Name,
        string Role,
        ulong LocalNetId,
        string Phase,
        string Root,
        string RequestPath,
        string ResultPath,
        string CheckpointPath,
        string GameLogPath)
    {
        public static PeerPaths Create(
            string runRoot,
            ulong localNetId,
            int attempt,
            string? phase = null)
        {
            var role = localNetId == HostNetId
                ? SakuraMultiplayerRoles.Host
                : SakuraMultiplayerRoles.Client;
            var name = role == SakuraMultiplayerRoles.Host ? "host" : $"client-{localNetId}";
            var attemptRoot = attempt == 1 ? runRoot : Path.Combine(runRoot, $"attempt-{attempt}");
            var root = Path.Combine(attemptRoot, name);
            var artifactRoot = phase is null ? root : Path.Combine(root, phase);
            return new PeerPaths(
                name,
                role,
                localNetId,
                phase ?? name,
                root,
                Path.Combine(artifactRoot, "request.json"),
                Path.Combine(artifactRoot, "result.json"),
                Path.Combine(artifactRoot, "checkpoints.jsonl"),
                Path.Combine(artifactRoot, "game.log"));
        }
    }

    private sealed class PeerProcessFailureException : Exception
    {
        public PeerProcessFailureException(string role, ulong localNetId, RuntimeProcessOutcome outcome)
            : base($"{role} {localNetId} failed: game exit={outcome.Process.ExitCode}, result={outcome.Result.Status}.")
        {
            Role = role;
            LocalNetId = localNetId;
            Outcome = outcome;
        }

        public string Role { get; }
        public ulong LocalNetId { get; }
        public RuntimeProcessOutcome Outcome { get; }
    }

    private sealed record PreparedPeer(
        PeerPaths Paths,
        RuntimeWorkspace Workspace,
        SakuraTestRequest Request);

    private static IReadOnlyList<PeerPaths> CreatePeerPaths(
        string runRoot,
        int expectedPlayerCount,
        int attempt,
        string? phase = null) =>
        Enumerable.Range(1, expectedPlayerCount)
            .Select(value => PeerPaths.Create(runRoot, (ulong)value, attempt, phase))
            .ToArray();
}
