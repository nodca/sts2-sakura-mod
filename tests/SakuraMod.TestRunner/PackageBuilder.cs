namespace SakuraMod.TestRunner;

public sealed record PackageStageResult(
    string PackageDirectory,
    string PublishLogPath,
    PackageVerificationResult Verification);

public static class PackageBuilder
{
    public static async Task<PackageStageResult> StageAndVerifyAsync(
        string repoRoot,
        string runId,
        string runRoot,
        CancellationToken cancellationToken = default,
        bool reusePck = false)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var packageDirectory = Path.Combine(runRoot, "package", "SakuraMod");
        var stageWasAbsent = !Directory.Exists(packageDirectory);
        if (!stageWasAbsent)
            throw new InvalidOperationException("Package stage existed before the current invocation.");

        var godotPath = GodotPckInspector.FindGodotPath(repoRoot);
        var cache = reusePck ? new RuntimePckCache(repoRoot, godotPath) : null;
        var pckPath = Path.Combine(packageDirectory, "SakuraMod.pck");
        var inputKey = cache is null ? null : await cache.GetInputKeyAsync(cancellationToken);
        var reused = inputKey is not null && await cache!.TryStageAsync(inputKey, pckPath, cancellationToken);
        if (reused)
            Console.WriteLine("[package] Reusing verified PCK for unchanged export inputs.");

        var publishLog = Path.Combine(runRoot, "publish.log");
        var publish = await ProcessRunner.RunLoggedAsync(
            "dotnet",
            [
                "publish",
                Path.Combine(repoRoot, "SakuraMod.csproj"),
                "--nologo",
                $"-p:SakuraPackageRoot={packageDirectory}{Path.DirectorySeparatorChar}",
                $"-p:SakuraTestPckAlreadyStaged={reused.ToString().ToLowerInvariant()}"
            ],
            repoRoot,
            publishLog,
            cancellationToken: cancellationToken);
        if (publish.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed with exit code {publish.ExitCode}.");
        }

        var finalInputKey = inputKey is null ? null : await cache!.GetInputKeyAsync(cancellationToken);
        if (reused && finalInputKey != inputKey)
            throw new InvalidOperationException("PCK export inputs changed during publish. Run the test again.");

        var verifier = new PackageVerifier(new GodotPckInspector(
            godotPath,
            Path.Combine(repoRoot, "tests", "PckInspector")));
        var verification = await verifier.VerifyAsync(new PackageVerificationRequest(
            runId,
            packageDirectory,
            stageWasAbsent,
            startedAt,
            "SakuraMod",
            "v0.107.1",
            "STS2-RitsuLib",
            Path.Combine(runRoot, "pck-inventory.json"),
            Path.Combine(runRoot, "pck-inspector.log")), cancellationToken);
        if (!reused && inputKey is not null && finalInputKey == inputKey)
        {
            var verifiedPck = verification.Files.Single(file => file.Name == "SakuraMod.pck");
            await cache!.StoreAsync(inputKey, pckPath, verifiedPck.Sha256, cancellationToken);
        }

        return new PackageStageResult(packageDirectory, publishLog, verification);
    }
}
