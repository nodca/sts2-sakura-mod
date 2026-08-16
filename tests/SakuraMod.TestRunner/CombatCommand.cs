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
        "extra-effect-choice",
        "manifest-temporary",
        "generated-pile-memory",
        "element-turn-cleanup",
        "dream-turn-restoration",
        "spell-turn-transformation",
        "labyrinth-attack-immunity",
        "magic-charge-thresholds",
        "sakura-ancient-cards",
        "siege-end-turn",
        "save-load-restoration",
        "fourth-act-save-load",
        "fourth-act-terminal-transition",
        "fourth-act-finished-combat-transition",
        "combat-transition-cleanup",
        "dark-selection-combat-reentry",
        "through-piercing",
        "sakura-erase",
        "affliction-visual-layout",
        "dark-endpoint",
        "windy-bind-draw",
        "repair-jump-regeneration"
    ];
    private static readonly HashSet<string> SaveLoadScenarioIds =
    [
        "save-load-restoration",
        "fourth-act-save-load"
    ];

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Usage: scripts/test-mod combat [--scenario <id>]");
        writer.WriteLine();
        writer.WriteLine("Without --scenario, runs every combat scenario.");
        writer.WriteLine("Available scenarios:");
        foreach (var scenarioId in ScenarioIds)
            writer.WriteLine($"  {scenarioId}");
    }

    public static async Task<int> RunAsync(string repoRoot, string[] args)
    {
        var requested = ParseRequestedScenarios(args);
        if (requested is null)
        {
            return 2;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var suiteName = requested.Count == 1 ? requested[0] : "suite";
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-combat-{suiteName}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        Directory.CreateDirectory(runRoot);
        Console.WriteLine($"[combat] run: {runId}");
        Console.WriteLine($"[combat] artifacts: {runRoot}");

        try
        {
            var build = await RuntimeBuildPreparation.PrepareAsync(repoRoot, runId, runRoot);
            foreach (var scenarioId in requested)
            {
                await RunScenarioAsync(repoRoot, runId, runRoot, scenarioId, build);
            }

            Console.WriteLine($"[combat] PASS: {requested.Count} scenario(s)");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[combat] FAIL: {exception.Message}");
            Console.Error.WriteLine($"[combat] artifacts retained: {runRoot}");
            return 1;
        }
    }

    private static IReadOnlyList<string>? ParseRequestedScenarios(string[] args)
    {
        if (args.Length == 0)
        {
            return ScenarioIds;
        }

        if (args.Length != 2 || args[0] != "--scenario")
        {
            PrintHelp(Console.Error);
            return null;
        }

        if (!ScenarioIds.Contains(args[1], StringComparer.Ordinal))
        {
            Console.Error.WriteLine(
                $"Unknown combat scenario '{args[1]}'. Available: {string.Join(", ", ScenarioIds)}");
            return null;
        }

        return [args[1]];
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

    private static SakuraTestRequest CreateRequest(
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

    private static Task WriteSyntheticFailureAsync(
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
