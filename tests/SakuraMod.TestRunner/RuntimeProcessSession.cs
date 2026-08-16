using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public sealed record RuntimeProcessOutcome(
    ProcessResult Process,
    SakuraTestResult Result);

public static class RuntimeProcessSession
{
    public static async Task<RuntimeProcessOutcome> RunAsync(
        RuntimeWorkspace workspace,
        SakuraTestRequest request,
        string requestPath,
        string gameLogPath,
        CancellationToken cancellationToken = default)
    {
        await SakuraTestProtocol.WriteAtomicAsync(requestPath, request, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        ProcessResult process;
        try
        {
            process = await ProcessRunner.RunLoggedAsync(
                workspace.Executable,
                ["--headless", "--force-steam=off"],
                workspace.Root,
                gameLogPath,
                RuntimeProfile.CreateEnvironment(workspace, requestPath),
                timeout.Token,
                mirrorOutput: false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"STS2 exceeded the {request.TimeoutSeconds}-second {request.Layer}/{request.ScenarioId} timeout.");
        }

        var result = await ReadResultAsync(request.ResultPath, request, cancellationToken);
        return new RuntimeProcessOutcome(process, result);
    }

    public static async Task<SakuraTestResult> ReadResultAsync(
        string resultPath,
        SakuraTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resultPath))
        {
            throw new FileNotFoundException(
                "STS2 exited without an atomic runtime result.",
                resultPath);
        }

        SakuraTestResult result;
        try
        {
            result = await SakuraTestProtocol.ReadAsync<SakuraTestResult>(
                resultPath,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or System.Text.Json.JsonException)
        {
            throw new InvalidDataException(
                "Runtime result is unreadable or malformed.",
                exception);
        }

        if (result.SchemaVersion != SakuraTestProtocol.CurrentSchemaVersion
            || result.RunId != request.RunId
            || result.Layer != request.Layer
            || result.ScenarioId != request.ScenarioId
            || result.Phase != request.Phase
            || result.Status is not ("PASS" or "FAIL"))
        {
            throw new InvalidDataException(
                "Runtime result identity, schema, or status is invalid.");
        }

        return result;
    }
}
