using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

// The scenarios one batch process reached. Completed scenarios have a result (PASS or FAIL); the
// rest never started and go to a fresh process.
public sealed record CombatBatchOutcome(
    IReadOnlyDictionary<string, bool> Completed,
    IReadOnlyList<string> NotRun);

// Runs several combat scenarios in one STS2 process (the host returns to the main menu between
// them). The process stops at the first scenario that does not pass; a watchdog kills it when no
// result arrives within the running scenario's timeout plus a grace for boot or the menu return.
public static class CombatBatchSession
{
    private static readonly TimeSpan BootGrace = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReturnGrace = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public static async Task<CombatBatchOutcome> RunAsync(
        string repoRoot,
        string runId,
        string runRoot,
        RuntimeBuildArtifacts build,
        string batchName,
        IReadOnlyList<string> scenarioIds)
    {
        var batchRoot = Path.Combine(runRoot, "batches", batchName);
        Directory.CreateDirectory(batchRoot);
        var workspace = RuntimeWorkspaceBuilder.Create(
            build.Prerequisites,
            batchRoot,
            build.Package.PackageDirectory,
            build.RuntimeTestAssembly,
            Path.Combine(repoRoot, "tests", "SakuraMod.RuntimeTests", "SakuraMod.RuntimeTests.json"));
        var selfCheckDirectory = Path.Combine(batchRoot, "self-check");
        Directory.CreateDirectory(selfCheckDirectory);
        await RuntimeProfile.WriteStrictSettingsAsync(workspace, selfCheckDirectory, runSelfCheck: false);

        var requests = new List<SakuraTestRequest>(scenarioIds.Count);
        var requestPaths = new List<string>(scenarioIds.Count);
        foreach (var scenarioId in scenarioIds)
        {
            var scenarioRoot = Path.Combine(runRoot, "scenarios", scenarioId);
            Directory.CreateDirectory(scenarioRoot);
            var request = CombatCommand.CreateRequest(
                build.Prerequisites,
                runId,
                scenarioId,
                "single",
                scenarioRoot,
                Path.Combine(scenarioRoot, "runtime-result.json"),
                Path.Combine(scenarioRoot, "checkpoints.jsonl"));
            var requestPath = Path.Combine(scenarioRoot, "request.json");
            await SakuraTestProtocol.WriteAtomicAsync(requestPath, request);
            requests.Add(request);
            requestPaths.Add(requestPath);
        }

        var batchPath = Path.Combine(batchRoot, "batch.json");
        await SakuraTestProtocol.WriteAtomicAsync(
            batchPath,
            new SakuraTestBatch(SakuraTestProtocol.CurrentSchemaVersion, runId, requestPaths));
        var environment = new Dictionary<string, string?>(
            RuntimeProfile.CreateEnvironment(workspace, requestPaths[0]))
        {
            [SakuraTestProtocol.BatchEnvironmentVariable] = batchPath
        };

        var before = await FingerprintAsync(build);
        await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(batchRoot, "protected-roots-before.json"), before);

        Console.WriteLine($"[combat] batch {batchName}: {string.Join(", ", scenarioIds)}");
        var gameLogPath = Path.Combine(batchRoot, "game.log");
        using var kill = new CancellationTokenSource();
        var process = ProcessRunner.RunLoggedAsync(
            workspace.Executable,
            ["--headless", "--force-steam=off"],
            workspace.Root,
            gameLogPath,
            environment,
            kill.Token,
            mirrorOutput: false);

        var reached = 0;
        var timedOut = false;
        var deadline = DateTimeOffset.UtcNow + BootGrace + TimeSpan.FromSeconds(requests[0].TimeoutSeconds);
        while (true)
        {
            while (reached < requests.Count && File.Exists(requests[reached].ResultPath))
            {
                reached++;
                if (reached < requests.Count)
                    deadline = DateTimeOffset.UtcNow + ReturnGrace + TimeSpan.FromSeconds(requests[reached].TimeoutSeconds);
            }

            if (process.IsCompleted)
                break;
            if (DateTimeOffset.UtcNow > deadline)
            {
                timedOut = true;
                await kill.CancelAsync();
                break;
            }

            await Task.WhenAny(process, Task.Delay(PollInterval));
        }

        int? exitCode = null;
        try
        {
            exitCode = (await process).ExitCode;
        }
        catch (OperationCanceledException) when (timedOut)
        {
        }

        while (reached < requests.Count && File.Exists(requests[reached].ResultPath))
            reached++;

        var after = await FingerprintAsync(build);
        await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(batchRoot, "protected-roots-after.json"), after);
        DirectoryFingerprinter.RequireEqual(before, after);

        var completed = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var index = 0; index < reached; index++)
        {
            var request = requests[index];
            string status;
            string? failure = null;
            try
            {
                var result = await RuntimeProcessSession.ReadResultAsync(request.ResultPath, request);
                status = result.Status;
                Console.WriteLine(status == "PASS"
                    ? $"[combat] {request.ScenarioId} PASS: {result.Assertions.Count} semantic assertions"
                    : $"[combat] {request.ScenarioId} FAIL: {string.Join("; ", result.Failures.Select(static f => f.Message))}");
            }
            catch (Exception exception)
            {
                status = "FAIL";
                failure = exception.ToString();
                Console.Error.WriteLine($"[combat] {request.ScenarioId} FAIL: {exception.Message}");
            }

            completed[request.ScenarioId] = status == "PASS";
            await WriteRunnerResultAsync(request, status, exitCode, false, gameLogPath, before, after, failure);
        }

        // The process ended without the next result: either it never got that scenario started
        // (boot, or the return to the main menu failed), or the scenario crashed or hung.
        var notRun = new List<string>();
        if (reached < requests.Count)
        {
            var current = requests[reached];
            var started = File.Exists(current.CheckpointPath);
            Exception reason = timedOut
                ? new TimeoutException($"Batch {batchName} produced no result for {current.ScenarioId} before its deadline.")
                : (Exception)new InvalidOperationException(
                    $"Batch {batchName} exited (code {exitCode}) without a result for {current.ScenarioId}.");
            if (started || reached == 0)
            {
                // A scenario that started and never finished fails; a process that never booted fails
                // everything it was given rather than retrying them one boot at a time.
                var failed = started ? requests.Skip(reached).Take(1) : requests.Skip(reached);
                foreach (var request in failed)
                {
                    await CombatCommand.WriteSyntheticFailureAsync(
                        request.ResultPath, runId, request.ScenarioId, "single", DateTimeOffset.UtcNow, reason);
                    await WriteRunnerResultAsync(request, "FAIL", exitCode, timedOut, gameLogPath, before, after, reason.ToString());
                    completed[request.ScenarioId] = false;
                    Console.Error.WriteLine($"[combat] {request.ScenarioId} FAIL: {reason.Message}");
                }
            }

            notRun.AddRange(requests.Skip(reached).Select(static request => request.ScenarioId)
                .Where(id => !completed.ContainsKey(id)));
        }

        return new CombatBatchOutcome(completed, notRun);
    }

    private static async Task<List<ProtectedDirectoryFingerprint>> FingerprintAsync(RuntimeBuildArtifacts build) =>
    [
        await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealModsDirectory),
        await DirectoryFingerprinter.ComputeAsync(build.Prerequisites.RealUserDataDirectory)
    ];

    private static Task WriteRunnerResultAsync(
        SakuraTestRequest request,
        string status,
        int? exitCode,
        bool timedOut,
        string gameLogPath,
        IReadOnlyList<ProtectedDirectoryFingerprint> before,
        IReadOnlyList<ProtectedDirectoryFingerprint> after,
        string? failure) =>
        SakuraTestProtocol.WriteAtomicAsync(
            Path.Combine(request.ArtifactRoot, "runner-result.json"),
            new RuntimeRunnerResult(
                1,
                request.RunId,
                "combat",
                request.ScenarioId,
                status,
                exitCode,
                timedOut,
                request.ResultPath,
                gameLogPath,
                before,
                after,
                failure));
}
