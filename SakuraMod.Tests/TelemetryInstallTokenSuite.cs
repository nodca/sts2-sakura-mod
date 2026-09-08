using System.Linq;
using SakuraMod.SakuraModCode.Telemetry;
using STS2RitsuLib.Telemetry;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class TelemetryInstallTokenSuite
{
    private static readonly TelemetryApplicant Applicant = new()
    {
        ApplicantId = "SakuraMod",
        OwnerModId = "SakuraMod",
        DisplayName = "SakuraMod",
        Adapter = null!
    };

    private static TelemetryEnvelope SampleEnvelope() => new()
    {
        ApplicantId = "SakuraMod",
        EventName = "run_history.completed",
        RequestId = "run_history",
        Category = TelemetryDataCategory.RunHistory,
        TimestampUtc = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero),
        Payload = JsonNode.Parse("{\"run_key\":\"abc\"}")!
    };

    [Fact]
    public async Task AdapterRegistersThenSendsInstallTokenHeaders()
    {
        var transport = new FakeTransport();
        transport.Respond["install"] = new TelemetryHttpResult(201, "{\"schema\":\"ritsulib.telemetry.install.v1\",\"install_token\":\"" + ValidToken("7a") + "\"}", null);
        transport.Respond["batch"] = new TelemetryHttpResult(202, null, null);
        var store = new MemoryTokenStore();
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        var result = await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(result.Success, "Expected the first send to succeed through registration and token send.");
        RegressionTestHarness.Require(transport.Requests.Count == 2,
            $"Expected one registration and one batch send, got {transport.Requests.Count} requests.");
        var registration = transport.Requests[0];
        RegressionTestHarness.Require(
            registration.Url == "https://example.test/v1/ritsulib/install"
            && registration.Headers[InstallTokenHttpTelemetryAdapter.AuthorizationHeaderName] == "Bearer legacy-cred"
            && registration.JsonBody.Contains("\"applicant_id\":\"SakuraMod\""),
            "Expected registration to hit the install endpoint with the legacy bootstrap credential.");
        var send = transport.Requests[1];
        RegressionTestHarness.Require(
            send.Url == "https://example.test/v1/ritsulib/batch"
            && send.Headers[InstallTokenHttpTelemetryAdapter.AuthorizationHeaderName] == $"Bearer {ValidToken("7a")}",
            "Expected the batch send to authenticate with the issued install token.");
        RegressionTestHarness.Require(
            DateTimeOffset.TryParse(send.Headers[InstallTokenHttpTelemetryAdapter.TimestampHeaderName], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            && send.Headers[InstallTokenHttpTelemetryAdapter.NonceHeaderName].Length >= 16,
            "Expected the batch send to carry a parseable timestamp and a sufficiently long nonce.");
        RegressionTestHarness.Require(store.Saved == ValidToken("7a"),
            "Expected the issued install token to be persisted for later sends.");
    }

    [Fact]
    public async Task AdapterReusesStoredTokenWithoutRegisteringAgain()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(202, null, null);
        var store = new MemoryTokenStore { Saved = ValidToken("11") };
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);
        await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(transport.Requests.Count == 2,
            $"Expected both sends to reuse the stored token, got {transport.Requests.Count} requests.");
        var firstNonce = transport.Requests[0].Headers[InstallTokenHttpTelemetryAdapter.NonceHeaderName];
        var secondNonce = transport.Requests[1].Headers[InstallTokenHttpTelemetryAdapter.NonceHeaderName];
        RegressionTestHarness.Require(firstNonce != secondNonce,
            "Expected each batch send to use a fresh single-use nonce.");
    }

    [Fact]
    public async Task AdapterReregistersAndRetriesWhenTokenRejected()
    {
        var transport = new FakeTransport();
        transport.Respond["install"] = new TelemetryHttpResult(201, "{\"schema\":\"ritsulib.telemetry.install.v1\",\"install_token\":\"" + ValidToken("9c") + "\"}", null);
        transport.Respond["batch"] = new TelemetryHttpResult(202, null, null);
        transport.BatchStatuses.Enqueue(401);
        var store = new MemoryTokenStore { Saved = ValidToken("42") };
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        var result = await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(result.Success, "Expected the rejected token to be replaced and the batch retried.");
        RegressionTestHarness.Require(transport.Requests.Count == 3,
            $"Expected reject, re-registration, and retry, got {transport.Requests.Count} requests.");
        RegressionTestHarness.Require(
            transport.Requests[2].Headers[InstallTokenHttpTelemetryAdapter.AuthorizationHeaderName] == $"Bearer {ValidToken("9c")}",
            "Expected the retry to authenticate with the freshly issued install token.");
        RegressionTestHarness.Require(store.Cleared && store.Saved == ValidToken("9c"),
            "Expected the rejected token to be cleared and the fresh token persisted.");
    }

    [Fact]
    public async Task AdapterFallsBackToLegacyCredentialWhenRegistrationUnavailable()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(202, null, null);
        var store = new MemoryTokenStore();
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        var result = await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(result.Success, "Expected the legacy fallback send to succeed against a pre-rollout receiver.");
        RegressionTestHarness.Require(
            transport.Requests.Count(request => request.Url.EndsWith("/batch", StringComparison.Ordinal)) == 1,
            "Expected exactly one batch send, sent via the legacy credential fallback.");
        RegressionTestHarness.Require(
            transport.Requests.Last().Headers[InstallTokenHttpTelemetryAdapter.AuthorizationHeaderName] == "Bearer legacy-cred",
            "Expected the fallback send to authenticate with the bundled legacy credential.");
        RegressionTestHarness.Require(
            string.IsNullOrEmpty(store.Saved),
            "Expected no install token to be persisted when registration is unavailable.");
    }

    [Fact]
    public async Task AdapterSurfacesRateLimitingAsTransientFailure()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(429, null, null);
        var store = new MemoryTokenStore { Saved = ValidToken("55") };
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        var result = await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(
            !result.Success && result.ErrorMessage!.Contains("429", StringComparison.OrdinalIgnoreCase),
            "Expected a rate-limited send to surface a retryable failure carrying the status code.");
        RegressionTestHarness.Require(transport.Requests.Count == 3,
            $"Expected the rate-limited token path not to fall back to the legacy credential, got {transport.Requests.Count} requests.");
    }

    [Fact]
    public async Task BatchBodyMatchesServerContract()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(202, null, null);
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", new MemoryTokenStore(), transport, new MemoryQuarantine(), _ => { });

        await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);
        var body = transport.Requests.Last().JsonBody;

        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                JsonNode.Parse(body),
                JsonNode.Parse(
                    "{\"schema\":\"ritsulib.telemetry.batch.v1\",\"applicant_id\":\"SakuraMod\",\"events\":[" +
                    "{\"schema\":\"ritsulib.telemetry.v1\",\"applicantId\":\"SakuraMod\",\"eventName\":\"run_history.completed\"," +
                    "\"requestId\":\"run_history\",\"category\":\"RunHistory\",\"timestampUtc\":\"2026-09-05T12:00:00+00:00\"," +
                    "\"properties\":{},\"payload\":{\"run_key\":\"abc\"}}]}")),
            "Expected the install-token adapter to preserve the exact ritsulib batch JSON contract.");
        JsonDocument.Parse(body).Dispose();
    }

    [Fact]
    public void InstallEndpointDerivesFromBatchEndpoint()
    {
        RegressionTestHarness.Require(
            InstallTokenHttpTelemetryAdapter.BuildInstallEndpoint("https://example.test/v1/ritsulib/batch")
            == "https://example.test/v1/ritsulib/install",
            "Expected the install endpoint to sit beside the batch endpoint.");
        RegressionTestHarness.Require(
            InstallTokenHttpTelemetryAdapter.IsWellFormedInstallToken(ValidToken("ab"))
            && !InstallTokenHttpTelemetryAdapter.IsWellFormedInstallToken("not-a-token"),
            "Expected install token validation to require 64 hex characters.");
    }

    [Fact]
    public void TransientClassificationUsesStructuredHttpResult()
    {
        var cases = new (int Status, bool Transient)[]
        {
            (429, true), (500, true), (502, true), (503, true), (504, true),
            (400, false), (401, false), (403, false), (413, false), (0, false)
        };
        foreach (var (status, transient) in cases)
        {
            Assert.Equal(transient, InstallTokenHttpTelemetryAdapter.IsTransient(new(status, "unrelated 500", null)));
        }
    }

    [Fact]
    public void RetryDelayHonorsRetryAfterWithClamp()
    {
        var cases = new (TimeSpan? RetryAfter, int Attempt, TimeSpan Expected)[]
        {
            (null, 1, TimeSpan.FromMilliseconds(250)),
            (null, 2, TimeSpan.FromMilliseconds(500)),
            (null, 3, TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(45), 1, TimeSpan.FromSeconds(45)),
            (TimeSpan.FromSeconds(1), 2, TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(300), 1, TimeSpan.FromSeconds(InstallTokenHttpTelemetryAdapter.MaxRetryAfterSeconds)),
            (TimeSpan.FromSeconds(500), 3, TimeSpan.FromSeconds(InstallTokenHttpTelemetryAdapter.MaxRetryAfterSeconds)),
            (TimeSpan.FromSeconds(-1), 1, TimeSpan.Zero),
        };
        foreach (var (retryAfter, attempt, expected) in cases)
        {
            var failure = new TelemetryHttpResult(429, null, null, retryAfter);
            var delay = InstallTokenHttpTelemetryAdapter.RetryDelay(failure, attempt);
            RegressionTestHarness.Require(delay == expected,
                $"Expected retry delay {expected} for attempt {attempt} with retry-after {retryAfter}, got {delay}.");
        }
    }

    [Fact]
    public void TransportFailureDoesNotDependOnMessageFormat()
    {
        Assert.True(InstallTokenHttpTelemetryAdapter.IsTransient(new(0, null, "arbitrary transport failure")));
    }

    [Fact]
    public async Task CancellationDuringRetryStopsTheSendWithoutAcknowledgement()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(429, null, null, TimeSpan.FromSeconds(60));
        var store = new MemoryTokenStore { Saved = ValidToken("66") };
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        using var cancellation = new CancellationTokenSource();
        var sending = adapter.SendAsync(Applicant, [SampleEnvelope()], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sending);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task LegacyFallbackFailureAlsoCarriesRetryAfter()
    {
        var transport = new FakeTransport();
        transport.Respond["batch"] = new TelemetryHttpResult(429, null, null, TimeSpan.FromSeconds(1));
        var store = new MemoryTokenStore();
        var adapter = new InstallTokenHttpTelemetryAdapter(
            "https://example.test/v1/ritsulib/batch", "legacy-cred", "SakuraMod", store, transport, new MemoryQuarantine(), _ => { });

        var result = await adapter.SendAsync(Applicant, [SampleEnvelope()], TestContext.Current.CancellationToken);

        RegressionTestHarness.Require(!result.Success, "Expected the legacy fallback to surface the receiver's rate-limit verdict.");
        RegressionTestHarness.Require(
            result.ErrorMessage == "telemetry endpoint returned 429 retry-after=1s",
            $"Expected the legacy failure to carry the Retry-After contract, got {result.ErrorMessage}.");
    }

    private static string ValidToken(string seed) =>
        new string(seed[0], 32) + new string(seed[1], 32);

    private sealed class MemoryQuarantine : ITelemetryQuarantineStore
    {
        public bool Contains(string batchJson) => false;
        public void Save(string batchJson, string reason) => throw new IOException("Unexpected quarantine in authentication test.");
    }

    private sealed class FakeTransport : ITelemetryHttpTransport
    {
        public Dictionary<string, TelemetryHttpResult> Respond { get; } = new();

        public Queue<int> BatchStatuses { get; } = new();

        public List<RecordedRequest> Requests { get; } = [];

        public Task<TelemetryHttpResult> PostAsync(
            string url,
            IReadOnlyDictionary<string, string> headers,
            string jsonBody,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(url, new Dictionary<string, string>(headers), jsonBody));
            if (url.EndsWith("/batch", StringComparison.Ordinal))
            {
                if (BatchStatuses.Count > 0)
                    return Task.FromResult(new TelemetryHttpResult(BatchStatuses.Dequeue(), null, null));
                return Task.FromResult(Respond.TryGetValue("batch", out var batchResult)
                    ? batchResult
                    : new TelemetryHttpResult(404, null, null));
            }
            return Task.FromResult(Respond.TryGetValue("install", out var installResult)
                ? installResult
                : new TelemetryHttpResult(404, null, null));
        }
    }

    private sealed record RecordedRequest(string Url, IReadOnlyDictionary<string, string> Headers, string JsonBody);

    private sealed class MemoryTokenStore : IInstallTokenStore
    {
        public string? Saved { get; set; }

        public bool Cleared { get; private set; }

        public string? Load() => Saved;

        public void Save(string token) => Saved = token;

        public void Clear()
        {
            Cleared = true;
            Saved = null;
        }
    }
}
