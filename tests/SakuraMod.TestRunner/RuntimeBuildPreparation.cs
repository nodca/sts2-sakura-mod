using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public sealed record RuntimeBuildArtifacts(
    RuntimePrerequisites Prerequisites,
    PackageStageResult Package,
    string RuntimeTestAssembly);

public static class RuntimeBuildPreparation
{
    public static async Task<RuntimeBuildArtifacts> PrepareAsync(
        string repoRoot,
        string runId,
        string runRoot,
        CancellationToken cancellationToken = default)
    {
        var prerequisites = RuntimePreflight.Inspect(repoRoot);
        await SakuraTestProtocol.WriteAtomicAsync(
            Path.Combine(runRoot, "preflight.json"),
            new { schema_version = 1, status = "PASS", prerequisites },
            cancellationToken);

        var package = await PackageBuilder.StageAndVerifyAsync(
            repoRoot,
            runId,
            runRoot,
            cancellationToken);
        await SakuraTestProtocol.WriteAtomicAsync(
            Path.Combine(runRoot, "package-result.json"),
            package.Verification,
            cancellationToken);

        var runtimeAssembly = await BuildRuntimeTestModAsync(
            repoRoot,
            runRoot,
            cancellationToken);
        return new RuntimeBuildArtifacts(prerequisites, package, runtimeAssembly);
    }

    private static async Task<string> BuildRuntimeTestModAsync(
        string repoRoot,
        string runRoot,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(
            repoRoot,
            "tests",
            "SakuraMod.RuntimeTests",
            "SakuraMod.RuntimeTests.csproj");
        var build = await ProcessRunner.RunLoggedAsync(
            "dotnet",
            ["build", projectPath, "--nologo", "-p:SkipSakuraPackageCopy=true"],
            repoRoot,
            Path.Combine(runRoot, "runtime-test-build.log"),
            cancellationToken: cancellationToken);
        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Runtime test mod build failed with exit code {build.ExitCode}.");
        }

        var assembly = Path.Combine(
            repoRoot,
            "tests",
            "SakuraMod.RuntimeTests",
            "bin",
            "Debug",
            "net9.0",
            "SakuraMod.RuntimeTests.dll");
        var file = new FileInfo(assembly);
        if (!file.Exists || file.Length == 0)
        {
            throw new FileNotFoundException(
                "Runtime test mod assembly is missing after a successful build.",
                assembly);
        }

        return file.FullName;
    }
}
