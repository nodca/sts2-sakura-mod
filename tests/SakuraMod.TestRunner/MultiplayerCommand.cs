using SakuraMod.TestProtocol;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace SakuraMod.TestRunner;

public static class MultiplayerCommand
{
    private const int DefaultTimeoutSeconds = 120;
    private const ulong ScenarioSeed = 0x53414B555241UL;
    private static readonly TimeSpan HostCheckpointTimeout = TimeSpan.FromSeconds(30);

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Usage: scripts/test-mod multiplayer --scenario <id>");
        writer.WriteLine();
        writer.WriteLine("Available scenarios:");
        foreach (var scenarioId in SakuraMultiplayerScenarios.All)
            writer.WriteLine($"  {scenarioId} ({SakuraMultiplayerScenarios.PeerCountFor(scenarioId)} peers)");
    }

    public static async Task<int> RunAsync(string repoRoot, string[] args)
    {
        var scenarioId = ParseScenario(args);
        if (scenarioId is null)
            return 2;

        var peerCount = SakuraMultiplayerScenarios.PeerCountFor(scenarioId);
        var seats = SakuraMultiplayerRoles.SeatsFor(peerCount);
        var startedAt = DateTimeOffset.UtcNow;
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-multiplayer-{scenarioId}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        var coordinationRoot = Path.Combine(runRoot, "coordination");
        var runnerResultPath = Path.Combine(runRoot, "runner-result.json");
        Directory.CreateDirectory(coordinationRoot);
        Console.WriteLine($"[multiplayer] run: {runId}");
        Console.WriteLine($"[multiplayer] peers: {peerCount} ({string.Join(", ", seats)})");
        Console.WriteLine($"[multiplayer] artifacts: {runRoot}");

        var before = new List<ProtectedDirectoryFingerprint>();
        var after = new List<ProtectedDirectoryFingerprint>();
        RuntimeBuildArtifacts? build = null;
        var peers = CreatePeerPaths(runRoot, seats, attempt: 1);
        var outcomes = new Dictionary<string, RuntimeProcessOutcome>(StringComparer.Ordinal);
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
                var workspacePeers = CreatePeerPaths(runRoot, seats, attempt);
                var workspaces = new Dictionary<string, RuntimeWorkspace>(StringComparer.Ordinal);
                foreach (var peer in workspacePeers)
                {
                    workspaces.Add(peer.Role, await CreateWorkspaceAsync(repoRoot, build, peer));
                }
                try
                {
                    if (SakuraMultiplayerScenarios.IsSaveLoad(scenarioId))
                    {
                        var writePeers = CreatePeerPaths(runRoot, seats, attempt, "write");
                        peers = writePeers;
                        var writeSessions = CreateSessions(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            PhaseCoordinationRoot(runRoot, attempt, "write"),
                            writePeers,
                            workspaces,
                            port: AllocateLoopbackPort(),
                            peerCount,
                            phase: "write");
                        ValidatePeerRequests(writeSessions.Select(static session => session.Request).ToList());
                        var writeOutcomes = await RunPeerGroupAsync(writeSessions);
                        RequireMatchingSnapshot(
                            writeOutcomes.Values.Select(static outcome => outcome.Result).ToList(),
                            "comparison");

                        var readPeers = CreatePeerPaths(runRoot, seats, attempt, "read");
                        peers = readPeers;
                        var readSessions = CreateSessions(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            PhaseCoordinationRoot(runRoot, attempt, "read"),
                            readPeers,
                            workspaces,
                            port: AllocateLoopbackPort(),
                            peerCount,
                            phase: "read");
                        ValidatePeerRequests(readSessions.Select(static session => session.Request).ToList());
                        outcomes = await RunPeerGroupAsync(readSessions);
                    }
                    else
                    {
                        peers = workspacePeers;
                        var sessions = CreateSessions(
                            build.Prerequisites,
                            runId,
                            scenarioId,
                            runRoot,
                            coordinationRoot,
                            peers,
                            workspaces,
                            port: AllocateLoopbackPort(),
                            peerCount,
                            phase: null);
                        ValidatePeerRequests(sessions.Select(static session => session.Request).ToList());
                        outcomes = await RunPeerGroupAsync(sessions);
                    }
                    RequireMatchingSnapshot(
                        outcomes.Values.Select(static outcome => outcome.Result).ToList(),
                        "comparison");
                    break;
                }
                catch (PeerProcessFailureException exception)
                {
                    outcomes[exception.Role] = exception.Outcome;
                    if (attempt >= 3 || !IsRetryableStartupFailure(peers))
                        throw;
                    Console.Error.WriteLine(
                        $"[multiplayer] startup attempt {attempt} failed ({exception.GetBaseException().Message}); retrying with a fresh endpoint.");
                    outcomes = new Dictionary<string, RuntimeProcessOutcome>(StringComparer.Ordinal);
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
            peerCount,
            peers.Select(peer => ProjectPeer(peer, outcomes.GetValueOrDefault(peer.Role), failure)).ToArray(),
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

        Console.WriteLine($"[multiplayer] PASS: all {peerCount} peers converged on the {scenarioId} snapshot");
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

    public static void ValidatePeerRequests(SakuraTestRequest host, SakuraTestRequest client) =>
        ValidatePeerRequests([host, client]);

    public static void ValidatePeerRequests(IReadOnlyList<SakuraTestRequest> requests)
    {
        foreach (var request in requests)
            SakuraTestProtocol.ValidateRequest(request);

        var expectedSeats = SakuraMultiplayerRoles.SeatsFor(requests.Count);
        var first = requests[0];
        var firstMultiplayer = first.Multiplayer!;
        var isSaveLoad = SakuraMultiplayerScenarios.IsSaveLoad(first.ScenarioId);
        var scenarioPeerCount = SakuraMultiplayerScenarios.PeerCountFor(first.ScenarioId);
        if (firstMultiplayer.PeerCount != requests.Count || firstMultiplayer.PeerCount != scenarioPeerCount)
        {
            throw new InvalidDataException(
                $"Declared peer count {firstMultiplayer.PeerCount} does not match the {requests.Count} launched peers "
                + $"or scenario peer count {scenarioPeerCount}.");
        }

        for (var index = 0; index < requests.Count; index++)
        {
            var multiplayer = requests[index].Multiplayer!;
            var expectedPhase = isSaveLoad ? first.Phase : expectedSeats[index].ToLowerInvariant();
            if (requests[index].RunId != first.RunId
                || requests[index].ScenarioId != first.ScenarioId
                || requests[index].Phase != expectedPhase
                || multiplayer.Role != expectedSeats[index]
                || multiplayer.PeerCount != firstMultiplayer.PeerCount
                || multiplayer.HostAddress != firstMultiplayer.HostAddress
                || multiplayer.Port != firstMultiplayer.Port
                || multiplayer.CoordinationRoot != firstMultiplayer.CoordinationRoot
                || multiplayer.LocalNetId != SakuraMultiplayerRoles.NetIdFor(expectedSeats[index]))
                throw new InvalidDataException("Multiplayer request identities do not form a valid peer group.");
        }

        var netIds = requests.Select(static request => request.Multiplayer!.LocalNetId).ToList();
        if (netIds.Distinct().Count() != netIds.Count)
            throw new InvalidDataException("Multiplayer peers must use distinct local net ids.");
    }

    public static void RequireMatchingSnapshot(
        SakuraTestResult host,
        SakuraTestResult client,
        string snapshotName) =>
        RequireMatchingSnapshot([host, client], snapshotName);

    public static void RequireMatchingSnapshot(
        IReadOnlyList<SakuraTestResult> results,
        string snapshotName)
    {
        string? reference = null;
        foreach (var result in results)
        {
            if (!result.SemanticSnapshots.TryGetValue(snapshotName, out var snapshot))
                throw new InvalidDataException($"Every peer must emit the '{snapshotName}' semantic snapshot.");
            var json = JsonSerializer.Serialize(snapshot, SakuraTestProtocol.JsonOptions);
            if (reference is null)
            {
                reference = json;
                continue;
            }
            if (!string.Equals(reference, json, StringComparison.Ordinal))
                throw new InvalidDataException($"Peer '{snapshotName}' semantic snapshots diverged.");
        }
    }

    /// <summary>
    /// The host owns the loopback endpoint, so every client seat starts only after the
    /// host's typed `host_listening` checkpoint. Client seats then start together: the
    /// lobby fills in join order and each seat asserts its own net id.
    /// </summary>
    private static async Task<Dictionary<string, RuntimeProcessOutcome>> RunPeerGroupAsync(
        IReadOnlyList<PeerSession> sessions)
    {
        var hostSession = sessions.Single(
            static session => session.Peer.Role == SakuraMultiplayerRoles.Host);
        using var cancellation = new CancellationTokenSource();
        var running = new List<(string Role, Task<RuntimeProcessOutcome> Task)>();
        try
        {
            var hostTask = StartPeerAsync(hostSession, cancellation.Token);
            running.Add((hostSession.Peer.Role, hostTask));
            await WaitForCheckpointAsync(
                hostSession.Peer.CheckpointPath,
                hostSession.Request,
                "host_listening",
                hostTask,
                HostCheckpointTimeout,
                cancellation.Token);

            foreach (var session in sessions.Where(
                         static session => session.Peer.Role != SakuraMultiplayerRoles.Host))
                running.Add((session.Peer.Role, StartPeerAsync(session, cancellation.Token)));

            var outcomes = new Dictionary<string, RuntimeProcessOutcome>(StringComparer.Ordinal);
            var remaining = running.ToList();
            while (remaining.Count > 0)
            {
                var completed = await Task.WhenAny(remaining.Select(static peer => peer.Task));
                var peer = remaining.Single(value => ReferenceEquals(value.Task, completed));
                var outcome = await peer.Task;
                RequirePassingPeer(outcome, peer.Role);
                outcomes[peer.Role] = outcome;
                remaining.Remove(peer);
            }
            return outcomes;
        }
        catch
        {
            cancellation.Cancel();
            foreach (var peer in running)
                await ObserveAsync(peer.Task);
            throw;
        }
    }

    private static Task<RuntimeProcessOutcome> StartPeerAsync(
        PeerSession session,
        CancellationToken cancellationToken) =>
        RuntimeProcessSession.RunAsync(
            session.Workspace,
            session.Request,
            session.Peer.RequestPath,
            session.Peer.GameLogPath,
            cancellationToken);

    private static IReadOnlyList<PeerPaths> CreatePeerPaths(
        string runRoot,
        IReadOnlyList<string> seats,
        int attempt,
        string? phase = null) =>
        seats
            .Select(role => PeerPaths.Create(runRoot, role, SakuraMultiplayerRoles.NetIdFor(role), attempt, phase))
            .ToList();

    private static IReadOnlyList<PeerSession> CreateSessions(
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string runRoot,
        string coordinationRoot,
        IReadOnlyList<PeerPaths> peers,
        IReadOnlyDictionary<string, RuntimeWorkspace> workspaces,
        ushort port,
        int peerCount,
        string? phase) =>
        peers.Select(peer => new PeerSession(
            peer,
            workspaces[peer.Role],
            CreateRequest(
                prerequisites,
                runId,
                scenarioId,
                phase ?? peer.Role.ToLowerInvariant(),
                runRoot,
                coordinationRoot,
                peer,
                port,
                peerCount))).ToList();

    private static string PhaseCoordinationRoot(string runRoot, int attempt, string phase) =>
        Path.Combine(attempt == 1 ? runRoot : Path.Combine(runRoot, $"attempt-{attempt}"), $"coordination-{phase}");

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
        PeerPaths peer)
    {
        Directory.CreateDirectory(peer.Root);
        var workspace = RuntimeWorkspaceBuilder.Create(
            build.Prerequisites,
            peer.Root,
            build.Package.PackageDirectory,
            build.RuntimeTestAssembly,
            Path.Combine(repoRoot, "tests", "SakuraMod.RuntimeTests", "SakuraMod.RuntimeTests.json"));
        var selfCheck = Path.Combine(peer.Root, "self-check");
        Directory.CreateDirectory(selfCheck);
        await RuntimeProfile.WriteStrictSettingsAsync(workspace, selfCheck, runSelfCheck: false);
        return workspace;
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
        int peerCount) => new(
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
            coordinationRoot,
            peerCount));

    private static ushort AllocateLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return checked((ushort)((IPEndPoint)socket.LocalEndPoint!).Port);
    }

    private static void RequirePassingPeer(RuntimeProcessOutcome outcome, string role)
    {
        if (outcome.Process.ExitCode != 0 || outcome.Result.Status != "PASS")
            throw new PeerProcessFailureException(role, outcome);
    }

    private static bool IsRetryableStartupFailure(PeerPaths host, PeerPaths client) =>
        IsRetryableStartupFailure([host, client]);

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
                peer.Phase,
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
            string role,
            ulong localNetId,
            int attempt,
            string? phase = null)
        {
            var attemptRoot = attempt == 1 ? runRoot : Path.Combine(runRoot, $"attempt-{attempt}");
            var root = Path.Combine(attemptRoot, role.ToLowerInvariant());
            var artifactRoot = phase is null ? root : Path.Combine(root, phase);
            return new PeerPaths(
                role,
                localNetId,
                phase ?? role.ToLowerInvariant(),
                root,
                Path.Combine(artifactRoot, "request.json"),
                Path.Combine(artifactRoot, "result.json"),
                Path.Combine(artifactRoot, "checkpoints.jsonl"),
                Path.Combine(artifactRoot, "game.log"));
        }
    }

    private sealed record PeerSession(
        PeerPaths Peer,
        RuntimeWorkspace Workspace,
        SakuraTestRequest Request);

    private sealed class PeerProcessFailureException : Exception
    {
        public PeerProcessFailureException(string role, RuntimeProcessOutcome outcome)
            : base($"{role} failed: game exit={outcome.Process.ExitCode}, result={outcome.Result.Status}.")
        {
            Role = role;
            Outcome = outcome;
        }

        public string Role { get; }
        public RuntimeProcessOutcome Outcome { get; }
    }
}
