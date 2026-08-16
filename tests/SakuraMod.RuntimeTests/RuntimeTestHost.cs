using Godot;
using SakuraMod.TestProtocol;
using System.Diagnostics;

namespace SakuraMod.RuntimeTests;

internal static class RuntimeTestHost
{
    public static async void ExecuteRequestedScenario()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        SakuraTestRequest? request = null;
        var assertions = new RuntimeAssertionCollector();
        SakuraRuntimeEnvironment? environment = null;
        Dictionary<string, object?> snapshots = new(StringComparer.Ordinal);
        List<string> artifacts = [];
        try
        {
            request = ReadAndValidateRequest();
            WriteCheckpoint(request, "main_menu_ready", "Runtime test harness reached the main menu.");
            if (request.Layer == "runtime" && request.ScenarioId == "smoke")
            {
                (environment, snapshots, artifacts) = RuntimeSmokeScenario.Execute(request, assertions);
            }
            else if (request.Layer == "combat")
            {
                (environment, snapshots, artifacts) =
                    await CombatScenarioDispatcher.ExecuteAsync(request, assertions);
            }
            else if (request.Layer == "multiplayer")
            {
                (environment, snapshots, artifacts) =
                    await MultiplayerScenarioDispatcher.ExecuteAsync(request, assertions);
            }
            else
            {
                throw new NotSupportedException(
                    $"Runtime scenario '{request.Layer}/{request.ScenarioId}' is not implemented by this host.");
            }
        }
        catch (Exception exception)
        {
            assertions.AddFailure(Unwrap(exception));
        }

        stopwatch.Stop();
        if (request is null)
        {
            RuntimeTestMod.Logger.Error("Runtime test request could not be loaded; exiting with failure.");
            Quit(1);
            return;
        }

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

        Quit(passed ? 0 : 1);
    }

    private static SakuraTestRequest ReadAndValidateRequest()
    {
        var requestPath = System.Environment.GetEnvironmentVariable(SakuraTestProtocol.RequestEnvironmentVariable);
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
