using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SakuraMod.SakuraModCode.Telemetry;
using STS2RitsuLib.Telemetry;

public sealed class TelemetryDeliverySuite
{
    private const string Token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly TelemetryApplicant Applicant = new()
    {
        ApplicantId = "SakuraMod", OwnerModId = "SakuraMod", DisplayName = "SakuraMod", Adapter = null!
    };

    private static TelemetryEnvelope Envelope(string name, string value = "abc") => new()
    {
        ApplicantId = "SakuraMod", EventName = name, RequestId = "run_history",
        Category = TelemetryDataCategory.RunHistory,
        TimestampUtc = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero),
        Payload = new JsonObject { ["value"] = value }
    };

    private static InstallTokenHttpTelemetryAdapter Adapter(Transport transport, ITelemetryQuarantineStore? quarantine = null) =>
        new("https://example.test/batch", "legacy", "SakuraMod", new Tokens(), transport, quarantine ?? new MemoryQuarantine(), _ => { });

    [Fact]
    public async Task BatchesBoundActualEscapedUtf8BytesAndPreserveOrder()
    {
        var transport = new Transport();
        var events = Enumerable.Range(0, 4).Select(i => Envelope($"event{i}", new string('\u754c', 50_000))).ToArray();
        Assert.True((await Adapter(transport).SendAsync(Applicant, events, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(2, transport.Bodies.Count);
        Assert.All(transport.Bodies, body => Assert.InRange(Encoding.UTF8.GetByteCount(body), 1, InstallTokenHttpTelemetryAdapter.MaxBatchBytes));
        Assert.Equal(events.Select(e => e.EventName), transport.Bodies.SelectMany(EventNames));
    }

    [Fact]
    public async Task SmallEventsAlsoRespectReceiverCountLimit()
    {
        var transport = new Transport();
        Assert.True((await Adapter(transport).SendAsync(Applicant,
            Enumerable.Range(0, 501).Select(i => Envelope($"event{i}")).ToArray(), TestContext.Current.CancellationToken)).Success);
        Assert.Equal([500, 1], transport.Bodies.Select(body => EventNames(body).Length));
    }

    [Theory]
    [InlineData("payload_too_large", 600_000)]
    [InlineData("event_json_too_large", 1_100_000)]
    public async Task OversizeEventIsPreservedWhileAdjacentEventsUpload(string reason, int length)
    {
        var transport = new Transport();
        var quarantine = new MemoryQuarantine();
        var oversized = Envelope("oversized", new string('x', length));
        var adapter = Adapter(transport, quarantine);
        var result = await adapter.SendAsync(Applicant, [Envelope("before"), oversized, Envelope("after")], TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(["before", "after"], transport.Bodies.SelectMany(EventNames));
        var saved = Assert.Single(quarantine.Records);
        Assert.Equal(reason, saved.Value);
        Assert.Equal(oversized.Payload!["value"]!.GetValue<string>(), JsonNode.Parse(saved.Key)!["events"]![0]!["payload"]!["value"]!.GetValue<string>());
        Assert.Equal(1, adapter.Stats.Quarantined);
        Assert.Equal(2, adapter.Stats.Succeeded);
    }

    [Fact]
    public async Task OversizedPropertiesAreQuarantinedWithoutSending()
    {
        var transport = new Transport();
        var quarantine = new MemoryQuarantine();
        var envelope = Envelope("properties");
        envelope.Properties["oversized"] = new string('x', InstallTokenHttpTelemetryAdapter.MaxPropertiesBytes);
        Assert.True((await Adapter(transport, quarantine).SendAsync(Applicant, [envelope], TestContext.Current.CancellationToken)).Success);
        Assert.Equal("properties_too_large", Assert.Single(quarantine.Records).Value);
        Assert.Empty(transport.Bodies);
    }

    [Fact]
    public async Task Receiver413SplitsAndQuarantinesOnlyRejectedSingleton()
    {
        var transport = new Transport { Respond = body => EventNames(body).Contains("rejected") ? new(413, null, null) : new(202, null, null) };
        var quarantine = new MemoryQuarantine();
        Assert.True((await Adapter(transport, quarantine).SendAsync(Applicant,
            [Envelope("before"), Envelope("rejected"), Envelope("after")], TestContext.Current.CancellationToken)).Success);
        Assert.Equal("receiver_http_413", Assert.Single(quarantine.Records).Value);
        Assert.Equal(["before", "after"], transport.Bodies.Where(body => !EventNames(body).Contains("rejected")).SelectMany(EventNames));
        Assert.Equal(5, transport.Bodies.Count);
    }

    [Fact]
    public async Task LowerReceiverBodyLimitSplitsWithoutQuarantiningValidEvents()
    {
        var transport = new Transport { Respond = body => EventNames(body).Length > 1 ? new(413, null, null) : new(202, null, null) };
        var quarantine = new MemoryQuarantine();
        Assert.True((await Adapter(transport, quarantine).SendAsync(Applicant, [Envelope("a"), Envelope("b")], TestContext.Current.CancellationToken)).Success);
        Assert.Equal(3, transport.Bodies.Count);
        Assert.Empty(quarantine.Records);
    }

    [Fact]
    public async Task FailedQuarantineKeepsOriginalQueueRetryable()
    {
        var transport = new Transport();
        var quarantine = new MemoryQuarantine { FailSave = true };
        var adapter = Adapter(transport, quarantine);
        var events = new[] { Envelope("before"), Envelope("oversized", new string('x', 600_000)), Envelope("after") };
        Assert.False((await adapter.SendAsync(Applicant, events, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(quarantine.Records);
        Assert.Equal(["before"], transport.Bodies.SelectMany(EventNames));
        quarantine.FailSave = false;
        Assert.True((await adapter.SendAsync(Applicant, events, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(["before", "before", "after"], transport.Bodies.SelectMany(EventNames));
        Assert.Single(quarantine.Records);
    }

    [Fact]
    public async Task Quarantined413IsNotResentWhenQueueRetriesAfterRestart()
    {
        var directory = Directory.CreateTempSubdirectory("sakura-quarantine-");
        try
        {
            var transport = new Transport { Respond = body => EventNames(body).Contains("rejected") ? new(413, null, null) : new(403, null, null) };
            var events = new[] { Envelope("rejected"), Envelope("after") };
            Assert.False((await Adapter(transport, new FileTelemetryQuarantineStore(() => directory.FullName))
                .SendAsync(Applicant, events, TestContext.Current.CancellationToken)).Success);
            var recovered = new Transport();
            Assert.True((await Adapter(recovered, new FileTelemetryQuarantineStore(() => directory.FullName))
                .SendAsync(Applicant, events, TestContext.Current.CancellationToken)).Success);
            Assert.Equal(["after"], recovered.Bodies.SelectMany(EventNames));
            var file = Assert.Single(directory.GetFiles("*.json"));
            using var saved = JsonDocument.Parse(File.ReadAllText(file.FullName));
            Assert.Equal("receiver_http_413", saved.RootElement.GetProperty("reason").GetString());
            Assert.Equal("rejected", saved.RootElement.GetProperty("batch").GetProperty("events")[0].GetProperty("eventName").GetString());
            Assert.Empty(directory.GetFiles("*.tmp"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task OtherRejectionsRemainFailuresWithoutQuarantine(int status)
    {
        var transport = new Transport { Respond = _ => new(status, null, null) };
        var quarantine = new MemoryQuarantine();
        Assert.False((await Adapter(transport, quarantine).SendAsync(Applicant, [Envelope("a")], TestContext.Current.CancellationToken)).Success);
        Assert.Single(transport.Bodies);
        Assert.Empty(quarantine.Records);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(0)]
    public async Task TransientFailuresRetrySameBodyWithFreshNonce(int status)
    {
        var transport = new Transport();
        transport.Results.Enqueue(new(status, null, status == 0 ? "arbitrary network failure" : null, TimeSpan.Zero));
        var adapter = Adapter(transport);
        Assert.True((await adapter.SendAsync(Applicant, [Envelope("a")], TestContext.Current.CancellationToken)).Success);
        Assert.Equal(2, transport.Bodies.Count);
        Assert.Equal(transport.Bodies[0], transport.Bodies[1]);
        Assert.NotEqual(transport.Headers[0]["X-RitsuLib-Nonce"], transport.Headers[1]["X-RitsuLib-Nonce"]);
        Assert.All(transport.Headers, headers => Assert.Equal($"Bearer {Token}", headers["Authorization"]));
        Assert.Equal(1, adapter.Stats.Retries);
    }

    [Fact]
    public void QuarantineDeduplicatesChecksCapacityAndDetectsCorruption()
    {
        var directory = Directory.CreateTempSubdirectory("sakura-quarantine-");
        try
        {
            var store = new FileTelemetryQuarantineStore(() => directory.FullName);
            const string body = "{\"events\":[{\"payload\":\"original\"}]}";
            store.Save(body, "payload_too_large");
            store.Save(body, "payload_too_large");
            Assert.True(store.Contains(body));
            var record = Assert.Single(directory.GetFiles("*.json"));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(record.FullName));
            using (var full = File.Create(Path.Combine(directory.FullName, "full.tmp")))
                full.SetLength(FileTelemetryQuarantineStore.MaxStorageBytes);
            Assert.Throws<IOException>(() => store.Save("{\"events\":[{}]}", "receiver_http_413"));
            Assert.True(store.Contains(body));
            File.WriteAllText(record.FullName, "{}");
            Assert.Throws<IOException>(() => store.Save(body, "payload_too_large"));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static string[] EventNames(string body) =>
        JsonNode.Parse(body)!["events"]!.AsArray().Select(item => item!["eventName"]!.GetValue<string>()).ToArray();

    private sealed class Transport : ITelemetryHttpTransport
    {
        internal Queue<TelemetryHttpResult> Results { get; } = new();
        internal Func<string, TelemetryHttpResult> Respond { get; init; } = _ => new(202, null, null);
        internal List<string> Bodies { get; } = [];
        internal List<IReadOnlyDictionary<string, string>> Headers { get; } = [];
        public Task<TelemetryHttpResult> PostAsync(string url, IReadOnlyDictionary<string, string> headers, string body, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.EndsWith("/batch", url);
            Bodies.Add(body);
            Headers.Add(headers);
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Respond(body));
        }
    }

    private sealed class MemoryQuarantine : ITelemetryQuarantineStore
    {
        internal Dictionary<string, string> Records { get; } = new();
        internal bool FailSave { get; set; }
        public bool Contains(string batchJson) => Records.ContainsKey(batchJson);
        public void Save(string batchJson, string reason)
        {
            if (FailSave) throw new IOException("disk unavailable");
            Records.TryAdd(batchJson, reason);
        }
    }

    private sealed class Tokens : IInstallTokenStore
    {
        public string? Load() => Token;
        public void Save(string token) { }
        public void Clear() { }
    }
}
