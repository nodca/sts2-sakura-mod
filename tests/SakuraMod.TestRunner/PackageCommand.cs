using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public static class PackageCommand
{
    public static async Task<int> RunAsync(string repoRoot)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var runId = $"{startedAt:yyyyMMddTHHmmssZ}-package-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(repoRoot, "artifacts", "tests", runId);
        Directory.CreateDirectory(runRoot);
        var resultPath = Path.Combine(runRoot, "package-result.json");

        Console.WriteLine($"[package] run: {runId}");
        Console.WriteLine($"[package] artifacts: {runRoot}");
        try
        {
            var stage = await PackageBuilder.StageAndVerifyAsync(repoRoot, runId, runRoot);
            var result = stage.Verification;
            await SakuraTestProtocol.WriteAtomicAsync(resultPath, result);
            Console.WriteLine($"[package] PASS: {result.Files.Count} files, {result.PckPathCount} mounted PCK paths");
            Console.WriteLine($"[package] result: {resultPath}");
            return 0;
        }
        catch (Exception exception)
        {
            await SakuraTestProtocol.WriteAtomicAsync(resultPath, new
            {
                schemaVersion = 1,
                runId,
                status = "FAIL",
                failure = exception.ToString()
            });
            Console.Error.WriteLine($"[package] FAIL: {exception.Message}");
            Console.Error.WriteLine($"[package] result: {resultPath}");
            return 1;
        }
    }

}
