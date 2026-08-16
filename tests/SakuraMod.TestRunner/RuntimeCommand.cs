using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public sealed record RuntimeRunnerResult(
    int SchemaVersion,
    string RunId,
    string Layer,
    string ScenarioId,
    string Status,
    int? ProcessExitCode,
    bool TimedOut,
    string RuntimeResultPath,
    string GameLogPath,
    IReadOnlyList<ProtectedDirectoryFingerprint> ProtectedRootsBefore,
    IReadOnlyList<ProtectedDirectoryFingerprint> ProtectedRootsAfter,
    string? Failure);

public static class RuntimeCommand
{
    private const int DefaultTimeoutSeconds = 120;

    public static async Task<int> RunAsync(string repoRoot)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-runtime-smoke-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        Directory.CreateDirectory(runRoot);
        var runtimeResultPath = Path.Combine(runRoot, "runtime-result.json");
        var runnerResultPath = Path.Combine(runRoot, "runner-result.json");
        var gameLogPath = Path.Combine(runRoot, "game.log");
        var before = new List<ProtectedDirectoryFingerprint>();
        var after = new List<ProtectedDirectoryFingerprint>();
        ProcessResult? processResult = null;
        RuntimeProcessOutcome? outcome = null;
        var timedOut = false;
        string? failure = null;

        Console.WriteLine($"[runtime] run: {runId}");
        Console.WriteLine($"[runtime] artifacts: {runRoot}");
        try
        {
            var build = await RuntimeBuildPreparation.PrepareAsync(repoRoot, runId, runRoot);
            var prerequisites = build.Prerequisites;
            var workspace = RuntimeWorkspaceBuilder.Create(
                prerequisites,
                runRoot,
                build.Package.PackageDirectory,
                build.RuntimeTestAssembly,
                Path.Combine(repoRoot, "tests", "SakuraMod.RuntimeTests", "SakuraMod.RuntimeTests.json"));
            var selfCheckDirectory = Path.Combine(runRoot, "self-check");
            Directory.CreateDirectory(selfCheckDirectory);
            await RuntimeProfile.WriteStrictSettingsAsync(workspace, selfCheckDirectory);

            var requestPath = Path.Combine(runRoot, "request.json");
            var request = new SakuraTestRequest(
                SakuraTestProtocol.CurrentSchemaVersion,
                runId,
                "runtime",
                "smoke",
                "single",
                prerequisites.GameVersion,
                prerequisites.RitsuVersion,
                prerequisites.SakuraVersion,
                0x53414B555241UL,
                "eng",
                DefaultTimeoutSeconds,
                runRoot,
                runtimeResultPath,
                Path.Combine(runRoot, "checkpoints.jsonl"));
            before.Add(await DirectoryFingerprinter.ComputeAsync(prerequisites.RealModsDirectory));
            before.Add(await DirectoryFingerprinter.ComputeAsync(prerequisites.RealUserDataDirectory));
            await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(runRoot, "protected-roots-before.json"), before);

            Exception? processFailure = null;
            try
            {
                outcome = await RuntimeProcessSession.RunAsync(
                    workspace,
                    request,
                    requestPath,
                    gameLogPath);
                processResult = outcome.Process;
            }
            catch (Exception exception)
            {
                timedOut = exception is TimeoutException;
                processFailure = exception;
            }

            after.Add(await DirectoryFingerprinter.ComputeAsync(prerequisites.RealModsDirectory));
            after.Add(await DirectoryFingerprinter.ComputeAsync(prerequisites.RealUserDataDirectory));
            await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(runRoot, "protected-roots-after.json"), after);
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
                    $"Runtime smoke failed: game exit={outcome.Process.ExitCode}, result={outcome.Result.Status}.");
            }

            var pass = new RuntimeRunnerResult(
                1,
                runId,
                "runtime",
                "smoke",
                "PASS",
                outcome.Process.ExitCode,
                false,
                runtimeResultPath,
                gameLogPath,
                before,
                after,
                null);
            await SakuraTestProtocol.WriteAtomicAsync(runnerResultPath, pass);
            Console.WriteLine($"[runtime] PASS: {outcome.Result.Assertions.Count} semantic assertions");
            Console.WriteLine($"[runtime] result: {runtimeResultPath}");
            return 0;
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            if (!File.Exists(runtimeResultPath))
            {
                await WriteSyntheticFailureAsync(runtimeResultPath, runId, startedAt, exception);
            }

            var fail = new RuntimeRunnerResult(
                1,
                runId,
                "runtime",
                "smoke",
                "FAIL",
                processResult?.ExitCode,
                timedOut,
                runtimeResultPath,
                gameLogPath,
                before,
                after,
                failure);
            await SakuraTestProtocol.WriteAtomicAsync(runnerResultPath, fail);
            Console.Error.WriteLine($"[runtime] FAIL: {exception.Message}");
            Console.Error.WriteLine($"[runtime] artifacts retained: {runRoot}");
            return 1;
        }
    }

    private static Task WriteSyntheticFailureAsync(
        string path,
        string runId,
        DateTimeOffset startedAt,
        Exception exception) => SakuraTestProtocol.WriteAtomicAsync(
        path,
        new SakuraTestResult(
            SakuraTestProtocol.CurrentSchemaVersion,
            runId,
            "runtime",
            "smoke",
            "single",
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
