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
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var packageDirectory = Path.Combine(runRoot, "package", "SakuraMod");
        var stageWasAbsent = !Directory.Exists(packageDirectory);
        var publishLog = Path.Combine(runRoot, "publish.log");
        var publish = await ProcessRunner.RunLoggedAsync(
            "dotnet",
            [
                "publish",
                Path.Combine(repoRoot, "SakuraMod.csproj"),
                "--nologo",
                $"-p:SakuraPackageRoot={packageDirectory}{Path.DirectorySeparatorChar}"
            ],
            repoRoot,
            publishLog,
            cancellationToken: cancellationToken);
        if (publish.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed with exit code {publish.ExitCode}.");
        }

        var verifier = new PackageVerifier(new GodotPckInspector(
            GodotPckInspector.FindGodotPath(repoRoot),
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
        return new PackageStageResult(packageDirectory, publishLog, verification);
    }
}
