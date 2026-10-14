using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public static class CombatCommand
{
    private const int DefaultTimeoutSeconds = 120;
    private const ulong ScenarioSeed = 0x53414B555241UL;
    internal static readonly string[] ScenarioIds =
    [
        "starter-run",
        "clow-shield-singleplayer",
        "clow-mist-slippery",
        "clow-fly-airborne",
        "extra-effect-choice",
        "extra-effect-play",
        "exchange-four-pile-selection",
        "manifest-temporary",
        "generated-pile-memory",
        "element-turn-cleanup",
        "dream-turn-restoration",
        "spell-turn-transformation",
        "wand-turn-persistence",
        "labyrinth-field",
        "magic-charge-thresholds",
        "sakura-ancient-cards",
        "siege-end-turn",
        "freeze-turn-coverage",
        "frostbite-threshold",
        "snow-frostbite",
        "save-load-restoration",
        "fourth-act-save-load",
        "fourth-act-terminal-transition",
        "fourth-act-finished-combat-transition",
        "combat-transition-cleanup",
        "dark-selection-combat-reentry",
        "through-piercing",
        "sakura-erase",
        "affliction-visual-layout",
        "queen-bound-cleanup",
        "fourth-act-card-afflictions",
        "fourth-act-audio",
        "cleansing-stats",
        "dark-endpoint",
        "light-eternal-day",
        "windy-bind-draw",
        "repair-jump-regeneration"
    ];
    private static readonly HashSet<string> SaveLoadScenarioIds =
    [
        "light-eternal-day",
        "save-load-restoration",
        "fourth-act-save-load",
        "fourth-act-finished-combat-transition"
    ];

    private const int DefaultJobs = 2;

    private sealed record CombatOptions(IReadOnlyList<string> Scenarios, bool Single, int Jobs, bool Isolated);

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Usage: scripts/test-mod combat [--scenario <id> | --only <id,id,...>] [--jobs <n>] [--isolated]");
        writer.WriteLine();
        writer.WriteLine("Without --scenario, runs every combat scenario: batched in reused game processes,");
        writer.WriteLine($"spread over --jobs parallel processes (default {DefaultJobs}). --isolated starts one process");
        writer.WriteLine("per scenario instead (to bisect state leaking between scenarios).");
        writer.WriteLine("--only runs that subset the same way; --scenario runs one scenario in its own process.");
        writer.WriteLine("Available scenarios:");
        foreach (var scenarioId in ScenarioIds)
            writer.WriteLine($"  {scenarioId}");
    }

    public static async Task<int> RunAsync(string repoRoot, string[] args)
    {
        var options = ParseOptions(args);
        if (options is null)
        {
            return 2;
        }

        var requested = options.Scenarios;
        var startedAt = DateTimeOffset.UtcNow;
        var suiteName = options.Single ? requested[0] : "suite";
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-combat-{suiteName}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        Directory.CreateDirectory(runRoot);
        Console.WriteLine($"[combat] run: {runId}");
        Console.WriteLine($"[combat] artifacts: {runRoot}");

        try
        {
            var build = await RuntimeBuildPreparation.PrepareAsync(repoRoot, runId, runRoot);
            if (options.Single)
            {
                await RunScenarioAsync(repoRoot, runId, runRoot, requested[0], build);
                Console.WriteLine($"[combat] PASS: {requested.Count} scenario(s)");
                return 0;
            }

            var failed = await RunSuiteAsync(repoRoot, runId, runRoot, build, options);
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            if (failed.Count == 0)
            {
                Console.WriteLine($"[combat] PASS: {requested.Count} scenario(s) in {elapsed.TotalSeconds:F0}s");
                return 0;
            }

            Console.Error.WriteLine(
                $"[combat] FAIL: {failed.Count} of {requested.Count} scenario(s) in {elapsed.TotalSeconds:F0}s: " +
                string.Join(", ", failed));
            Console.Error.WriteLine($"[combat] artifacts retained: {runRoot}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[combat] FAIL: {exception.Message}");
            Console.Error.WriteLine($"[combat] artifacts retained: {runRoot}");
            return 1;
        }
    }

    private static CombatOptions? ParseOptions(string[] args)
    {
        string? scenario = null;
        string[]? only = null;
        var jobs = DefaultJobs;
        var isolated = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--scenario" when index + 1 < args.Length:
                    scenario = args[++index];
                    break;
                case "--jobs" when index + 1 < args.Length && int.TryParse(args[index + 1], out var parsed) && parsed > 0:
                    jobs = parsed;
                    index++;
                    break;
                case "--only" when index + 1 < args.Length:
                    only = args[++index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--isolated":
                    isolated = true;
                    break;
                default:
                    PrintHelp(Console.Error);
                    return null;
            }
        }

        if (only is not null)
        {
            var unknown = only.Where(id => !ScenarioIds.Contains(id, StringComparer.Ordinal)).ToList();
            if (scenario is not null || only.Length == 0 || unknown.Count > 0)
            {
                Console.Error.WriteLine(
                    $"--only takes known scenario ids and excludes --scenario. Unknown: {string.Join(", ", unknown)}");
                return null;
            }

            return new CombatOptions(only.Distinct(StringComparer.Ordinal).ToList(), false, jobs, isolated);
        }

        if (scenario is null)
        {
            return new CombatOptions(ScenarioIds, false, jobs, isolated);
        }

        if (!ScenarioIds.Contains(scenario, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(
                $"Unknown combat scenario '{scenario}'. Available: {string.Join(", ", ScenarioIds)}");
            return null;
        }

        return new CombatOptions([scenario], true, 1, true);
    }

    // Deals the scenarios round-robin into --jobs queues that run concurrently. A queue runs its
    // batchable scenarios in reused processes, then its save/load scenarios (which must cross a
    // process boundary between their write and read phases) one process pair each.
    private static async Task<IReadOnlyList<string>> RunSuiteAsync(
        string repoRoot,
        string runId,
        string runRoot,
        RuntimeBuildArtifacts build,
        CombatOptions options)
    {
        var batchable = options.Scenarios.Where(id => !options.Isolated && !SaveLoadScenarioIds.Contains(id)).ToList();
        var separate = options.Scenarios.Except(batchable).ToList();
        var queues = Enumerable.Range(0, Math.Min(options.Jobs, options.Scenarios.Count))
            .Select(static _ => (Batched: new List<string>(), Separate: new List<string>()))
            .ToList();
        for (var index = 0; index < batchable.Count; index++)
            queues[index % queues.Count].Batched.Add(batchable[index]);
        for (var index = 0; index < separate.Count; index++)
            queues[(batchable.Count + index) % queues.Count].Separate.Add(separate[index]);

        var passed = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        await Task.WhenAll(queues.Select((queue, job) => Task.Run(async () =>
        {
            var remaining = queue.Batched;
            for (var attempt = 0; remaining.Count > 0; attempt++)
            {
                var outcome = await CombatBatchSession.RunAsync(
                    repoRoot, runId, runRoot, build, $"job{job}-{attempt}", remaining);
                foreach (var (scenarioId, ok) in outcome.Completed)
                    passed[scenarioId] = ok;
                remaining = outcome.NotRun.ToList();
            }

            foreach (var scenarioId in queue.Separate)
            {
                try
                {
                    await RunScenarioAsync(repoRoot, runId, runRoot, scenarioId, build);
                    passed[scenarioId] = true;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"[combat] {scenarioId} FAIL: {exception.Message}");
                    passed[scenarioId] = false;
                }
            }
        })));

        return options.Scenarios.Where(id => !passed.GetValueOrDefault(id)).ToList();
    }

    private static async Task RunScenarioAsync(
        string repoRoot,
        string runId,
        string runRoot,
        string scenarioId,
        RuntimeBuildArtifacts build)
    {
        var scenarioStartedAt = DateTimeOffset.UtcNow;
        var scenarioRoot = Path.Combine(runRoot, "scenarios", scenarioId);
        Directory.CreateDirectory(scenarioRoot);
        var isSaveLoad = SaveLoadScenarioIds.Contains(scenarioId);
        var resultPath = Path.Combine(
            scenarioRoot,
            isSaveLoad ? "read-result.json" : "runtime-result.json");
        var runnerResultPath = Path.Combine(scenarioRoot, "runner-result.json");
        var gameLogPath = Path.Combine(
            scenarioRoot,
            isSaveLoad ? "read-game.log" : "game.log");
        var before = new List<ProtectedDirectoryFingerprint>();
        var after = new List<ProtectedDirectoryFingerprint>();
        ProcessResult? process = null;
        var timedOut = false;

        Console.WriteLine($"[combat] scenario: {scenarioId}");
        try
        {
            var workspace = RuntimeWorkspaceBuilder.Create(
                build.Prerequisites,
                scenarioRoot,
                build.Package.PackageDirectory,
                build.RuntimeTestAssembly,
                Path.Combine(repoRoot, "tests", "SakuraMod.RuntimeTests", "SakuraMod.RuntimeTests.json"));
            var selfCheckDirectory = Path.Combine(scenarioRoot, "self-check");
            Directory.CreateDirectory(selfCheckDirectory);
            await RuntimeProfile.WriteStrictSettingsAsync(
                workspace,
                selfCheckDirectory,
                runSelfCheck: false);

            before.Add(await DirectoryFingerprinter.ComputeAsync(
                build.Prerequisites.RealModsDirectory));
            before.Add(await DirectoryFingerprinter.ComputeAsync(
                build.Prerequisites.RealUserDataDirectory));
            await SakuraTestProtocol.WriteAtomicAsync(
                Path.Combine(scenarioRoot, "protected-roots-before.json"),
                before);

            RuntimeProcessOutcome? outcome = null;
            Exception? processFailure = null;
            try
            {
                outcome = isSaveLoad
                    ? await RunSaveLoadPhasesAsync(
                        workspace,
                        build.Prerequisites,
                        runId,
                        scenarioId,
                        scenarioRoot)
                    : await RunSinglePhaseAsync(
                        workspace,
                        build.Prerequisites,
                        runId,
                        scenarioId,
                        scenarioRoot,
                        resultPath,
                        gameLogPath);
                process = outcome.Process;
            }
            catch (Exception exception)
            {
                timedOut = exception is TimeoutException;
                processFailure = exception;
            }

            after.Add(await DirectoryFingerprinter.ComputeAsync(
                build.Prerequisites.RealModsDirectory));
            after.Add(await DirectoryFingerprinter.ComputeAsync(
                build.Prerequisites.RealUserDataDirectory));
            await SakuraTestProtocol.WriteAtomicAsync(
                Path.Combine(scenarioRoot, "protected-roots-after.json"),
                after);
            DirectoryFingerprinter.RequireEqual(before, after);

            if (processFailure is not null)
            {
                throw processFailure;
            }

            if (outcome is null)
            {
                throw new InvalidOperationException("STS2 process did not produce an outcome.");
            }

            if (outcome.Process.ExitCode != 0 || outcome.Result.Status != "PASS")
            {
                throw new InvalidOperationException(
                    $"Combat scenario {scenarioId} failed: " +
                    $"game exit={outcome.Process.ExitCode}, result={outcome.Result.Status}.");
            }

            await SakuraTestProtocol.WriteAtomicAsync(
                runnerResultPath,
                new RuntimeRunnerResult(
                    1,
                    runId,
                    "combat",
                    scenarioId,
                    "PASS",
                    outcome.Process.ExitCode,
                    false,
                    resultPath,
                    gameLogPath,
                    before,
                    after,
                    null));
            Console.WriteLine(
                $"[combat] {scenarioId} PASS: {outcome.Result.Assertions.Count} semantic assertions");
        }
        catch (Exception exception)
        {
            if (!File.Exists(resultPath))
            {
                await WriteSyntheticFailureAsync(
                    resultPath,
                    runId,
                    scenarioId,
                    isSaveLoad ? "read" : "single",
                    scenarioStartedAt,
                    exception);
            }

            await SakuraTestProtocol.WriteAtomicAsync(
                runnerResultPath,
                new RuntimeRunnerResult(
                    1,
                    runId,
                    "combat",
                    scenarioId,
                    "FAIL",
                    process?.ExitCode,
                    timedOut,
                    resultPath,
                    gameLogPath,
                    before,
                    after,
                    exception.ToString()));
            throw;
        }
    }

    private static Task<RuntimeProcessOutcome> RunSinglePhaseAsync(
        RuntimeWorkspace workspace,
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string scenarioRoot,
        string resultPath,
        string gameLogPath)
    {
        var requestPath = Path.Combine(scenarioRoot, "request.json");
        var request = CreateRequest(
            prerequisites,
            runId,
            scenarioId,
            "single",
            scenarioRoot,
            resultPath,
            Path.Combine(scenarioRoot, "checkpoints.jsonl"));
        return RuntimeProcessSession.RunAsync(
            workspace,
            request,
            requestPath,
            gameLogPath);
    }

    private static async Task<RuntimeProcessOutcome> RunSaveLoadPhasesAsync(
        RuntimeWorkspace workspace,
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string scenarioRoot)
    {
        var snapshotPath = Path.Combine(scenarioRoot, "save-snapshot.json");
        var writeRequest = CreateRequest(
            prerequisites,
            runId,
            scenarioId,
            "write",
            scenarioRoot,
            Path.Combine(scenarioRoot, "write-result.json"),
            Path.Combine(scenarioRoot, "write-checkpoints.jsonl"),
            snapshotPath);
        var writeOutcome = await RuntimeProcessSession.RunAsync(
            workspace,
            writeRequest,
            Path.Combine(scenarioRoot, "write-request.json"),
            Path.Combine(scenarioRoot, "write-game.log"));
        RequirePassingPhase(writeOutcome, scenarioId, "write");

        var readRequest = CreateRequest(
            prerequisites,
            runId,
            scenarioId,
            "read",
            scenarioRoot,
            Path.Combine(scenarioRoot, "read-result.json"),
            Path.Combine(scenarioRoot, "read-checkpoints.jsonl"),
            snapshotPath);
        var readOutcome = await RuntimeProcessSession.RunAsync(
            workspace,
            readRequest,
            Path.Combine(scenarioRoot, "read-request.json"),
            Path.Combine(scenarioRoot, "read-game.log"));
        RequirePassingPhase(readOutcome, scenarioId, "read");
        return readOutcome;
    }

    internal static SakuraTestRequest CreateRequest(
        RuntimePrerequisites prerequisites,
        string runId,
        string scenarioId,
        string phase,
        string scenarioRoot,
        string resultPath,
        string checkpointPath,
        string? snapshotPath = null) => new(
        SakuraTestProtocol.CurrentSchemaVersion,
        runId,
        "combat",
        scenarioId,
        phase,
        prerequisites.GameVersion,
        prerequisites.RitsuVersion,
        prerequisites.SakuraVersion,
        ScenarioSeed,
        "eng",
        DefaultTimeoutSeconds,
        scenarioRoot,
        resultPath,
        checkpointPath,
        snapshotPath);

    private static void RequirePassingPhase(
        RuntimeProcessOutcome outcome,
        string scenarioId,
        string phase)
    {
        if (outcome.Process.ExitCode != 0 || outcome.Result.Status != "PASS")
        {
            throw new InvalidOperationException(
                $"Combat scenario {scenarioId}/{phase} failed: " +
                $"game exit={outcome.Process.ExitCode}, result={outcome.Result.Status}.");
        }
    }

    internal static Task WriteSyntheticFailureAsync(
        string path,
        string runId,
        string scenarioId,
        string phase,
        DateTimeOffset startedAt,
        Exception exception) => SakuraTestProtocol.WriteAtomicAsync(
        path,
        new SakuraTestResult(
            SakuraTestProtocol.CurrentSchemaVersion,
            runId,
            "combat",
            scenarioId,
            phase,
            "FAIL",
            startedAt,
            DateTimeOffset.UtcNow,
            (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            null,
            [],
            new Dictionary<string, object?>(),
            [new SakuraTestFailure(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.StackTrace)],
            []));
}
