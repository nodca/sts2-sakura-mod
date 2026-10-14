using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using SakuraMod.TestProtocol;
using System.Diagnostics;

namespace SakuraMod.RuntimeTests;

internal static class RuntimeTestHost
{
    // A batch keeps one game process for several scenarios; between them the run is left through
    // the native NGame.ReturnToMainMenu (RunManager.CleanUp + a fresh main menu).
    private static readonly TimeSpan MainMenuTimeout = TimeSpan.FromSeconds(30);

    public static async void ExecuteRequestedScenario()
    {
        var batchPath = System.Environment.GetEnvironmentVariable(SakuraTestProtocol.BatchEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(batchPath))
        {
            await ExecuteBatchAsync(batchPath);
            return;
        }

        SakuraTestRequest? request = null;
        Exception? loadFailure = null;
        try
        {
            request = ReadAndValidateRequest(
                System.Environment.GetEnvironmentVariable(SakuraTestProtocol.RequestEnvironmentVariable));
        }
        catch (Exception exception)
        {
            loadFailure = exception;
        }

        if (request is null)
        {
            RuntimeTestMod.Logger.Error($"Runtime test request could not be loaded; exiting with failure. {loadFailure}");
            Quit(1);
            return;
        }

        Quit(await ExecuteAndWriteAsync(request) ? 0 : 1);
    }

    // Runs every request in order. The first scenario that does not pass ends the process after its
    // result is written: its game state is no longer trusted, and the runner continues the rest in a
    // fresh process.
    private static async Task ExecuteBatchAsync(string batchPath)
    {
        SakuraTestBatch batch;
        try
        {
            if (!Path.IsPathFullyQualified(batchPath))
                throw new InvalidDataException(
                    $"{SakuraTestProtocol.BatchEnvironmentVariable} must contain an absolute batch path.");
            batch = SakuraTestProtocol.Read<SakuraTestBatch>(batchPath);
            SakuraTestProtocol.ValidateBatch(batch);
        }
        catch (Exception exception)
        {
            RuntimeTestMod.Logger.Error($"Runtime test batch could not be loaded; exiting with failure. {exception}");
            Quit(1);
            return;
        }

        for (var index = 0; index < batch.RequestPaths.Count; index++)
        {
            SakuraTestRequest request;
            try
            {
                request = ReadAndValidateRequest(batch.RequestPaths[index]);
                if (index > 0)
                    await ReturnToMainMenuAsync();
            }
            catch (Exception exception)
            {
                RuntimeTestMod.Logger.Error($"Runtime test batch could not continue at {batch.RequestPaths[index]}: {exception}");
                Quit(1);
                return;
            }

            if (!await ExecuteAndWriteAsync(request))
            {
                Quit(1);
                return;
            }
        }

        Quit(0);
    }

    // Runs one scenario under its own timeout and writes its atomic result; true when it passed.
    private static async Task<bool> ExecuteAndWriteAsync(SakuraTestRequest request)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var assertions = new RuntimeAssertionCollector();
        SakuraRuntimeEnvironment? environment = null;
        Dictionary<string, object?> snapshots = new(StringComparer.Ordinal);
        List<string> artifacts = [];
        try
        {
            WriteCheckpoint(request, "main_menu_ready", "Runtime test harness reached the main menu.");
            var scenario = DispatchAsync(request, assertions);
            var deadline = Task.Delay(TimeSpan.FromSeconds(request.TimeoutSeconds));
            if (await Task.WhenAny(scenario, deadline) != scenario)
                throw new TimeoutException(
                    $"Scenario {request.Layer}/{request.ScenarioId} exceeded its {request.TimeoutSeconds}-second timeout.");
            (environment, snapshots, artifacts) = await scenario;
        }
        catch (Exception exception)
        {
            assertions.AddFailure(Unwrap(exception));
        }

        stopwatch.Stop();
        var passed = assertions.Passed;
        var result = new SakuraTestResult(
            SakuraTestProtocol.CurrentSchemaVersion,
            request.RunId,
            request.Layer,
            request.ScenarioId,
            request.Phase,
            passed ? "PASS" : "FAIL",
            startedAt,
            DateTimeOffset.UtcNow,
            (long)stopwatch.Elapsed.TotalMilliseconds,
            environment,
            assertions.Assertions,
            snapshots,
            assertions.Failures,
            artifacts);
        try
        {
            SakuraTestProtocol.WriteAtomic(request.ResultPath, result);
            WriteCheckpoint(request, "result_written", $"Runtime result is {result.Status}.");
        }
        catch (Exception exception)
        {
            RuntimeTestMod.Logger.Error($"Failed to write runtime test result: {exception}");
            passed = false;
        }

        return passed;
    }

    private static async Task<(
        SakuraRuntimeEnvironment? Environment,
        Dictionary<string, object?> Snapshots,
        List<string> Artifacts)> DispatchAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        if (request.Layer == "runtime" && request.ScenarioId == "smoke")
            return RuntimeSmokeScenario.Execute(request, assertions);
        if (request.Layer == "combat")
            return await CombatScenarioDispatcher.ExecuteAsync(request, assertions);
        if (request.Layer == "multiplayer")
            return await MultiplayerScenarioDispatcher.ExecuteAsync(request, assertions);
        throw new NotSupportedException(
            $"Runtime scenario '{request.Layer}/{request.ScenarioId}' is not implemented by this host.");
    }

    private static async Task ReturnToMainMenuAsync()
    {
        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable between batched scenarios.");
        await game.ReturnToMainMenu();
        var stopwatch = Stopwatch.StartNew();
        while (game.MainMenu is null || !game.MainMenu.IsNodeReady())
        {
            if (stopwatch.Elapsed >= MainMenuTimeout)
                throw new TimeoutException("The main menu did not return between batched scenarios.");
            await game.AwaitProcessFrame();
        }

        await game.AwaitProcessFrame();
    }

    private static SakuraTestRequest ReadAndValidateRequest(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath) || !Path.IsPathFullyQualified(requestPath))
        {
            throw new InvalidDataException(
                $"{SakuraTestProtocol.RequestEnvironmentVariable} must contain an absolute request path.");
        }

        var request = SakuraTestProtocol.Read<SakuraTestRequest>(requestPath);
        SakuraTestProtocol.ValidateRequest(request);
        return request;
    }

    internal static void WriteCheckpoint(SakuraTestRequest request, string phase, string message) =>
        SakuraTestProtocol.AppendCheckpoint(
            request.CheckpointPath,
            new SakuraTestCheckpoint(
                SakuraTestProtocol.CurrentSchemaVersion,
                request.RunId,
                request.ScenarioId,
                phase,
                DateTimeOffset.UtcNow,
                message));

    private static Exception Unwrap(Exception exception) =>
        exception is System.Reflection.TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException!
            : exception;

    private static void Quit(int exitCode)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.Quit(exitCode);
            return;
        }

        throw new InvalidOperationException("Godot main loop is not a SceneTree.");
    }
}
