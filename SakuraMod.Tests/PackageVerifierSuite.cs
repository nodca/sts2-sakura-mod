using SakuraMod.SakuraModCode;
using SakuraMod.TestRunner;

public sealed class PackageVerifierSuite
{
    [Fact]
    public async Task ValidPackageProducesIdentityHashesAndPckInventory()
    {
        using var fixture = new PackageFixture();
        var result = await fixture.VerifyAsync();

        Assert.Equal("PASS", result.Status);
        Assert.Equal("SakuraMod", result.AssemblyName);
        Assert.Equal(4, result.Files.Count);
        Assert.All(result.Files, file => Assert.Matches("^[0-9a-f]{64}$", file.Sha256));
        Assert.Equal(3, result.PckPathCount);
    }

    [Fact]
    public async Task MissingPackageFileFailsVerification()
    {
        using var fixture = new PackageFixture();
        File.Delete(fixture.PckPath);

        await Assert.ThrowsAsync<FileNotFoundException>(fixture.VerifyAsync);
    }

    [Fact]
    public async Task MissingAnotherMeBgmFailsVerification()
    {
        using var fixture = new PackageFixture();
        File.Delete(fixture.AnotherMeBgmPath);

        await Assert.ThrowsAsync<FileNotFoundException>(fixture.VerifyAsync);
    }

    [Fact]
    public async Task EmptyPackageFileFailsVerification()
    {
        using var fixture = new PackageFixture();
        File.WriteAllBytes(fixture.PckPath, []);

        await Assert.ThrowsAsync<FileNotFoundException>(fixture.VerifyAsync);
    }

    [Fact]
    public async Task PreexistingStageFailsFreshnessVerification()
    {
        using var fixture = new PackageFixture(stageWasAbsent: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(fixture.VerifyAsync);
        Assert.Contains("existed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedManifestFailsVerification()
    {
        using var fixture = new PackageFixture();
        File.WriteAllText(fixture.ManifestPath, "{");

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(fixture.VerifyAsync);
    }

    [Fact]
    public async Task AssemblyIdentityMustMatchManifestModId()
    {
        using var fixture = new PackageFixture(expectedModId: "OtherMod", manifestModId: "OtherMod");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(fixture.VerifyAsync);
        Assert.Contains("Assembly name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestOnlyPckPathFailsVerification()
    {
        using var fixture = new PackageFixture(pckPaths: ["res://SakuraMod/tests/fixture.gd"]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(fixture.VerifyAsync);
        Assert.Contains("forbidden", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingKeroCompanionResourceFailsVerification()
    {
        using var fixture = new PackageFixture(pckPaths: ["res://SakuraMod/mod_image.png"]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(fixture.VerifyAsync);
        Assert.Contains("Kero combat companion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedPackageAssemblyFailsVerification()
    {
        using var fixture = new PackageFixture();
        File.WriteAllBytes(Path.Combine(fixture.PackageDirectory, "SakuraMod.RuntimeTests.dll"), [1]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(fixture.VerifyAsync);
        Assert.Contains("unexpected file", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PckInspectorBaselinesItsOwnHiddenGodotMetadata()
    {
        var inspector = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "tests/PckInspector/main.gd"));

        Assert.Contains("var before := _complete_inventory()", inspector, StringComparison.Ordinal);
        Assert.Contains("var after := _complete_inventory()", inspector, StringComparison.Ordinal);
        Assert.Contains("ProjectSettings.globalize_path(VIRTUAL_ROOT)", inspector, StringComparison.Ordinal);
        Assert.Contains("for path in _host_godot_inventory():", inspector, StringComparison.Ordinal);
        Assert.Contains("FileAccess.file_exists(absolute_root + \"/.gdignore\")", inspector, StringComparison.Ordinal);
        Assert.Contains("if not seen.has(path):", inspector, StringComparison.Ordinal);
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string _root;
        private readonly bool _stageWasAbsent;
        private readonly string _expectedModId;
        private readonly IReadOnlyList<string> _pckPaths;

        public PackageFixture(
            bool stageWasAbsent = true,
            string expectedModId = "SakuraMod",
            string manifestModId = "SakuraMod",
            IReadOnlyList<string>? pckPaths = null)
        {
            _root = Path.Combine(Path.GetTempPath(), $"sakuramod-package-test-{Guid.NewGuid():N}");
            _stageWasAbsent = stageWasAbsent;
            _expectedModId = expectedModId;
            _pckPaths = pckPaths ??
            [
                "res://SakuraMod/mod_image.png",
                "res://SakuraMod/images/charui/combat/kero_companion.png.import",
                "res://.godot/imported/kero_companion.png-test.ctex"
            ];
            Directory.CreateDirectory(_root);
            File.Copy(typeof(MainFile).Assembly.Location, Path.Combine(_root, "SakuraMod.dll"));
            File.WriteAllText(ManifestPath, $$"""
                {
                  "id": "{{manifestModId}}",
                  "version": "v1.1.0",
                  "min_game_version": "v0.107.1",
                  "has_pck": true,
                  "has_dll": true,
                  "dependencies": [
                    {
                      "id": "STS2-RitsuLib",
                      "min_version": "v0.4.56"
                    }
                  ]
                }
                """);
            File.WriteAllBytes(PckPath, [1]);
            Directory.CreateDirectory(Path.GetDirectoryName(AnotherMeBgmPath)!);
            File.WriteAllBytes(AnotherMeBgmPath, "OggS"u8.ToArray());
        }

        public string ManifestPath => Path.Combine(_root, "SakuraMod.json");
        public string PckPath => Path.Combine(_root, "SakuraMod.pck");
        public string AnotherMeBgmPath => Path.Combine(_root, "music", "another_me.ogg");
        public string PackageDirectory => _root;

        public Task<PackageVerificationResult> VerifyAsync()
        {
            var verifier = new PackageVerifier(new FakePckInspector(_pckPaths));
            return verifier.VerifyAsync(new PackageVerificationRequest(
                "test-run",
                _root,
                _stageWasAbsent,
                DateTimeOffset.UtcNow,
                _expectedModId,
                "v0.107.1",
                "STS2-RitsuLib",
                Path.Combine(_root, "inventory.json"),
                Path.Combine(_root, "inspector.log")));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class FakePckInspector(IReadOnlyList<string> paths) : IPckInspector
    {
        public Task<PckInspectionResult> InspectAsync(
            string pckPath,
            string outputPath,
            string logPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PckInspectionResult(1, "PASS", pckPath, paths, paths, null));
    }
}
