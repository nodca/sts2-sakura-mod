using SakuraMod.TestProtocol;
using SakuraMod.TestRunner;

public sealed class RuntimeRunnerSuite
{
    [Fact]
    public async Task ValidRuntimeResultPassesProtocolValidation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new RuntimeResultFixture();
        await fixture.WriteAsync(fixture.CreateResult(), cancellationToken);

        var result = await RuntimeProcessSession.ReadResultAsync(
            fixture.ResultPath,
            fixture.Request,
            cancellationToken);

        Assert.Equal("PASS", result.Status);
        Assert.Equal(fixture.Request.RunId, result.RunId);
    }

    [Fact]
    public async Task MissingRuntimeResultFailsClosed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new RuntimeResultFixture();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => RuntimeProcessSession.ReadResultAsync(
                fixture.ResultPath,
                fixture.Request,
                cancellationToken));
    }

    [Fact]
    public async Task PartialRuntimeResultIsReportedAsMalformed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new RuntimeResultFixture();
        await File.WriteAllTextAsync(
            fixture.ResultPath,
            "{\"schema_version\":2",
            cancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => RuntimeProcessSession.ReadResultAsync(
                fixture.ResultPath,
                fixture.Request,
                cancellationToken));

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1, "run", "runtime", "smoke", "single", "PASS")]
    [InlineData(2, "other-run", "runtime", "smoke", "single", "PASS")]
    [InlineData(2, "run", "combat", "smoke", "single", "PASS")]
    [InlineData(2, "run", "runtime", "other", "single", "PASS")]
    [InlineData(2, "run", "runtime", "smoke", "other", "PASS")]
    [InlineData(2, "run", "runtime", "smoke", "single", "UNKNOWN")]
    public async Task RuntimeResultIdentitySchemaAndStatusMustMatchRequest(
        int schemaVersion,
        string runId,
        string layer,
        string scenarioId,
        string phase,
        string status)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new RuntimeResultFixture();
        await fixture.WriteAsync(fixture.CreateResult(
            schemaVersion,
            runId,
            layer,
            scenarioId,
            phase,
            status), cancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => RuntimeProcessSession.ReadResultAsync(
                fixture.ResultPath,
                fixture.Request,
                cancellationToken));
    }

    [Fact]
    public async Task FingerprintHandlesExclusivelyLockedZeroByteFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new RuntimeResultFixture();
        var emptyPath = Path.Combine(fixture.Root, "locked-empty.log");
        await File.WriteAllBytesAsync(emptyPath, [], cancellationToken);
        await using var exclusiveLock = new FileStream(
            emptyPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var fingerprint = await DirectoryFingerprinter.ComputeAsync(
            fixture.Root,
            cancellationToken);

        Assert.Equal(1, fingerprint.FileCount);
        Assert.Equal(0, fingerprint.TotalBytes);
        Assert.Matches("^[0-9a-f]{64}$", fingerprint.Sha256);
    }

    [Fact]
    public void ValidMultiplayerPeerPairPassesRequestValidation()
    {
        using var fixture = new RuntimeResultFixture();
        var coordinationRoot = Path.Combine(fixture.Root, "coordination");
        var host = fixture.CreateMultiplayerRequest("Host", 1, coordinationRoot);
        var client = fixture.CreateMultiplayerRequest("Client", 2, coordinationRoot);

        MultiplayerCommand.ValidatePeerRequests(host, client);
    }

    [Fact]
    public void ValidThreePeerGroupPassesRequestValidation()
    {
        using var fixture = new RuntimeResultFixture();
        var coordinationRoot = Path.Combine(fixture.Root, "coordination");
        var host = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("Host", 1, coordinationRoot, peerCount: 3));
        var client = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("Client", 2, coordinationRoot, peerCount: 3));
        var secondClient = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("SecondClient", 3, coordinationRoot, peerCount: 3));

        MultiplayerCommand.ValidatePeerRequests([host, client, secondClient]);
    }

    [Fact]
    public void SaveLoadPeerGroupRequiresOneSharedPhase()
    {
        using var fixture = new RuntimeResultFixture();
        var coordinationRoot = Path.Combine(fixture.Root, "coordination");
        var host = fixture.CreateMultiplayerRequest("Host", 1, coordinationRoot, peerCount: 3) with
        {
            ScenarioId = SakuraMultiplayerScenarios.ThreePlayerRepairJumpLoad,
            Phase = "write"
        };
        var client = fixture.CreateMultiplayerRequest("Client", 2, coordinationRoot, peerCount: 3) with
        {
            ScenarioId = SakuraMultiplayerScenarios.ThreePlayerRepairJumpLoad,
            Phase = "write"
        };
        var secondClient = fixture.CreateMultiplayerRequest("SecondClient", 3, coordinationRoot, peerCount: 3) with
        {
            ScenarioId = SakuraMultiplayerScenarios.ThreePlayerRepairJumpLoad,
            Phase = "write"
        };

        MultiplayerCommand.ValidatePeerRequests([host, client, secondClient]);
        Assert.Throws<InvalidDataException>(() => MultiplayerCommand.ValidatePeerRequests(
            [host, client with { Phase = "read" }, secondClient]));
    }

    [Fact]
    public void MissingOrOutOfOrderThreePeerSeatsFailClosed()
    {
        using var fixture = new RuntimeResultFixture();
        var coordinationRoot = Path.Combine(fixture.Root, "coordination");
        var host = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("Host", 1, coordinationRoot, peerCount: 3));
        var client = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("Client", 2, coordinationRoot, peerCount: 3));
        var secondClient = AsThreePlayerScenario(
            fixture.CreateMultiplayerRequest("SecondClient", 3, coordinationRoot, peerCount: 3));

        Assert.Throws<InvalidDataException>(() => MultiplayerCommand.ValidatePeerRequests([host, client]));
        Assert.Throws<InvalidDataException>(
            () => MultiplayerCommand.ValidatePeerRequests([host, secondClient, client]));
    }

    [Theory]
    [InlineData("Unknown", "127.0.0.1", 12345, 1, "coordination")]
    [InlineData("Host", "192.0.2.1", 12345, 1, "coordination")]
    [InlineData("Host", "127.0.0.1", 0, 1, "coordination")]
    [InlineData("Host", "127.0.0.1", 12345, 0, "coordination")]
    [InlineData("Host", "127.0.0.1", 12345, 1, "../escaped")]
    public void MalformedMultiplayerRequestFailsClosed(
        string role,
        string address,
        ushort port,
        ulong netId,
        string coordinationRelativePath)
    {
        using var fixture = new RuntimeResultFixture();
        var request = fixture.CreateMultiplayerRequest(
            role,
            netId,
            Path.GetFullPath(Path.Combine(fixture.Root, coordinationRelativePath)),
            address,
            port);

        Assert.Throws<InvalidDataException>(() => SakuraTestProtocol.ValidateRequest(request));
    }

    [Fact]
    public void MultiplayerBlockOnSingleProcessLayerFailsClosed()
    {
        using var fixture = new RuntimeResultFixture();
        var multiplayer = fixture.CreateMultiplayerRequest(
            "Host", 1, Path.Combine(fixture.Root, "coordination")).Multiplayer;
        var request = fixture.Request with { Multiplayer = multiplayer };

        Assert.Throws<InvalidDataException>(() => SakuraTestProtocol.ValidateRequest(request));
    }

    [Fact]
    public void DuplicatePeerNetIdsFailClosed()
    {
        using var fixture = new RuntimeResultFixture();
        var coordinationRoot = Path.Combine(fixture.Root, "coordination");
        var host = fixture.CreateMultiplayerRequest("Host", 1, coordinationRoot);
        var client = fixture.CreateMultiplayerRequest("Client", 1, coordinationRoot);

        Assert.Throws<InvalidDataException>(() => MultiplayerCommand.ValidatePeerRequests(host, client));
    }

    [Fact]
    public void DivergentOrMissingPeerSnapshotsFailClosed()
    {
        using var fixture = new RuntimeResultFixture();
        var host = fixture.CreateResult() with
        {
            SemanticSnapshots = new Dictionary<string, object?> { ["comparison"] = new { block = 3 } }
        };
        var client = fixture.CreateResult() with
        {
            SemanticSnapshots = new Dictionary<string, object?> { ["comparison"] = new { block = 2 } }
        };

        Assert.Throws<InvalidDataException>(
            () => MultiplayerCommand.RequireMatchingSnapshot(host, client, "comparison"));
        Assert.Throws<InvalidDataException>(
            () => MultiplayerCommand.RequireMatchingSnapshot(
                host,
                client with { SemanticSnapshots = new Dictionary<string, object?>() },
                "comparison"));
    }

    [Fact]
    public void ThreePeerSnapshotsMustAllMatch()
    {
        using var fixture = new RuntimeResultFixture();
        var matching = Enumerable.Range(0, 3)
            .Select(_ => fixture.CreateResult() with
            {
                SemanticSnapshots = new Dictionary<string, object?>
                {
                    ["comparison"] = new { players = new[] { 1, 2, 3 } }
                }
            })
            .ToArray();

        MultiplayerCommand.RequireMatchingSnapshot(matching, "comparison");
        matching[2] = matching[2] with
        {
            SemanticSnapshots = new Dictionary<string, object?>
            {
                ["comparison"] = new { players = new[] { 1, 2 } }
            }
        };
        Assert.Throws<InvalidDataException>(
            () => MultiplayerCommand.RequireMatchingSnapshot(matching, "comparison"));
    }

    [Fact]
    public async Task HostCheckpointWaitRejectsEarlyPeerExit()
    {
        using var fixture = new RuntimeResultFixture();
        var request = fixture.CreateMultiplayerRequest(
            "Host", 1, Path.Combine(fixture.Root, "coordination"));

        await Assert.ThrowsAsync<InvalidDataException>(() => MultiplayerCommand.WaitForCheckpointAsync(
            request.CheckpointPath,
            request,
            "host_listening",
            Task.CompletedTask,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken));
    }

    private static SakuraTestRequest AsThreePlayerScenario(SakuraTestRequest request) =>
        request with { ScenarioId = SakuraMultiplayerScenarios.ThreePlayerDefensivePowers };

    private sealed class RuntimeResultFixture : IDisposable
    {
        public RuntimeResultFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"sakuramod-runtime-result-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            ResultPath = Path.Combine(Root, "result.json");
            Request = new SakuraTestRequest(
                SakuraTestProtocol.CurrentSchemaVersion,
                "run",
                "runtime",
                "smoke",
                "single",
                "0.107.1",
                "0.5.3",
                "1.1.0",
                1,
                "eng",
                5,
                Root,
                ResultPath,
                Path.Combine(Root, "checkpoints.jsonl"));
        }

        public string Root { get; }
        public string ResultPath { get; }
        public SakuraTestRequest Request { get; }

        public SakuraTestResult CreateResult(
            int schemaVersion = SakuraTestProtocol.CurrentSchemaVersion,
            string runId = "run",
            string layer = "runtime",
            string scenarioId = "smoke",
            string phase = "single",
            string status = "PASS") => new(
            schemaVersion,
            runId,
            layer,
            scenarioId,
            phase,
            status,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            null,
            [],
            new Dictionary<string, object?>(),
            [],
            []);

        public Task WriteAsync(SakuraTestResult result, CancellationToken cancellationToken) =>
            SakuraTestProtocol.WriteAtomicAsync(ResultPath, result, cancellationToken);

        public SakuraTestRequest CreateMultiplayerRequest(
            string role,
            ulong netId,
            string coordinationRoot,
            string address = "127.0.0.1",
            ushort port = 12345,
            int peerCount = SakuraMultiplayerRoles.MinPeerCount)
        {
            var peerRoot = Path.Combine(Root, role.ToLowerInvariant());
            return Request with
            {
                Layer = "multiplayer",
                ScenarioId = "clow-defensive-powers",
                Phase = role.ToLowerInvariant(),
                ArtifactRoot = Root,
                ResultPath = Path.Combine(peerRoot, "result.json"),
                CheckpointPath = Path.Combine(peerRoot, "checkpoints.jsonl"),
                Multiplayer = new SakuraMultiplayerRequest(role, address, port, netId, coordinationRoot, peerCount)
            };
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
