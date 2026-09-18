using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal interface ITelemetryHttpTransport
{
    Task<TelemetryHttpResult> PostAsync(
        string url,
        IReadOnlyDictionary<string, string> headers,
        string jsonBody,
        CancellationToken cancellationToken);
}

internal readonly record struct TelemetryHttpResult(int StatusCode, string? Body, string? TransportError, TimeSpan? RetryAfter = null)
{
    public bool IsSuccess => TransportError is null && StatusCode is >= 200 and < 300;
}

/// <summary>
/// Persists the per-install telemetry token issued by the receiver. The token
/// is an abuse-containment gate, not a secret: it is stored in the game's
/// user data directory and can be re-registered through the bootstrap endpoint.
/// </summary>
internal interface IInstallTokenStore
{
    string? Load();

    void Save(string token);

    void Clear();
}

/// <summary>
/// Sends ritsulib.telemetry.batch.v1 payloads with the server-issued install
/// token, a request timestamp, and a single-use nonce instead of the shared
/// public write credential. The wire contract (JSON fields, casing, consent
/// gating upstream of the adapter) matches the bundled HttpJsonTelemetryAdapter;
/// registration failures preserve their status and never fall back to shared batch credentials.
/// </summary>
internal sealed class InstallTokenHttpTelemetryAdapter(
    string endpoint,
    string bootstrapCredential,
    string applicantId,
    IInstallTokenStore tokenStore,
    ITelemetryHttpTransport transport,
    ITelemetryQuarantineStore quarantine,
    Action<string> reportDiagnostic) : ITelemetryAdapter
{
    internal const int MaxBatchBytes = 900_000;
    internal const int MaxBatchEvents = 500;
    internal const int MaxPayloadBytes = 512 * 1024;
    internal const int MaxPropertiesBytes = 64 * 1024;
    internal const int MaxRawEventBytes = 1024 * 1024;
    internal const int MaxAttempts = 3;
    internal const int MaxRetryAfterSeconds = 120;
    internal const string BatchSchema = "ritsulib.telemetry.batch.v1";
    internal const string InstallSchema = "ritsulib.telemetry.install.v1";
    internal const string AuthorizationHeaderName = "Authorization";
    internal const string TimestampHeaderName = "X-RitsuLib-Timestamp";
    internal const string NonceHeaderName = "X-RitsuLib-Nonce";
    internal const int InstallTokenHexLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _batchEndpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    private readonly string _installEndpoint = BuildInstallEndpoint(endpoint ?? throw new ArgumentNullException(nameof(endpoint)));
    private readonly string _bootstrapCredential = bootstrapCredential ?? throw new ArgumentNullException(nameof(bootstrapCredential));
    private readonly string _applicantId = applicantId ?? throw new ArgumentNullException(nameof(applicantId));
    private readonly IInstallTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    private readonly ITelemetryHttpTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    private readonly ITelemetryQuarantineStore _quarantine = quarantine ?? throw new ArgumentNullException(nameof(quarantine));
    private readonly Action<string> _reportDiagnostic = reportDiagnostic ?? throw new ArgumentNullException(nameof(reportDiagnostic));
    private long _batches, _events, _attempts, _retries, _succeeded, _failed, _quarantined;

    internal TelemetrySendStats Stats => new(
        Interlocked.Read(ref _batches), Interlocked.Read(ref _events), Interlocked.Read(ref _attempts),
        Interlocked.Read(ref _retries), Interlocked.Read(ref _succeeded), Interlocked.Read(ref _failed),
        Interlocked.Read(ref _quarantined));

    internal readonly record struct TelemetrySendStats(
        long Batches, long Events, long Attempts, long Retries, long Succeeded, long Failed, long Quarantined);

    public string AdapterId => "install_token_http";

    public string EndpointDescription => _batchEndpoint;

    public async ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicant);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicant.ApplicantId);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _batches);
        Interlocked.Add(ref _events, events.Count);

        var batch = new List<byte[]>();
        var emptyBytes = Encoding.UTF8.GetByteCount(SerializeBatch(applicant.ApplicantId, batch));
        var batchBytes = emptyBytes;
        foreach (var envelope in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
            var singleBody = SerializeBatch(applicant.ApplicantId, [json]);
            try
            {
                if (_quarantine.Contains(singleBody))
                {
                    Interlocked.Increment(ref _quarantined);
                    continue;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return QuarantineFailure(exception);
            }
            var oversized = OversizeReason(json);
            var addedBytes = json.Length + (batch.Count == 0 ? 0 : 1);
            if (batch.Count > 0 && (oversized is not null || batchBytes + addedBytes > MaxBatchBytes || batch.Count == MaxBatchEvents))
            {
                var sent = await SendBatchAsync(applicant.ApplicantId, batch, cancellationToken).ConfigureAwait(false);
                if (!sent.Success)
                    return sent;
                batch.Clear();
                batchBytes = emptyBytes;
                addedBytes = json.Length;
            }

            if (oversized is not null)
            {
                var saved = Quarantine(singleBody, oversized, cancellationToken);
                if (!saved.Success)
                    return saved;
                continue;
            }
            batch.Add(json);
            batchBytes += addedBytes;
        }

        return batch.Count == 0
            ? TelemetrySendResult.Ok()
            : await SendBatchAsync(applicant.ApplicantId, batch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TelemetrySendResult> SendBatchAsync(
        string applicantId, IReadOnlyList<byte[]> events, CancellationToken cancellationToken)
    {
        var body = SerializeBatch(applicantId, events);
        TelemetryHttpResult result;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _attempts);
            result = await SendAuthenticatedAsync(body, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                Interlocked.Increment(ref _succeeded);
                return TelemetrySendResult.Ok();
            }
            if (attempt >= MaxAttempts || !IsTransient(result))
                break;
            Interlocked.Increment(ref _retries);
            await Task.Delay(RetryDelay(result, attempt), cancellationToken).ConfigureAwait(false);
        }

        if (result.TransportError is null && result.StatusCode == 413)
        {
            if (events.Count == 1)
                return Quarantine(body, "receiver_http_413", cancellationToken);

            // A proxy/receiver may have a lower body limit. Preserve event order
            // and narrow a rejected batch down before isolating any event.
            var halves = events.Chunk((events.Count + 1) / 2);
            foreach (var half in halves)
            {
                var sent = await SendBatchAsync(applicantId, half, cancellationToken).ConfigureAwait(false);
                if (!sent.Success)
                    return sent;
            }
            return TelemetrySendResult.Ok();
        }
        Interlocked.Increment(ref _failed);
        return ToSendResult(result);
    }

    private TelemetrySendResult Quarantine(string body, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _quarantine.Save(body, reason);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return QuarantineFailure(exception);
        }
        Interlocked.Increment(ref _quarantined);
        _reportDiagnostic($"Telemetry event quarantined ({reason}, {Encoding.UTF8.GetByteCount(body)} bytes); excluded from upload, original saved locally.");
        // RitsuLib only acknowledges the whole prefix. Success here means durable
        // local custody, not HTTP acceptance; the separate counter preserves that distinction.
        return TelemetrySendResult.Ok();
    }

    private TelemetrySendResult QuarantineFailure(Exception exception)
    {
        Interlocked.Increment(ref _failed);
        _reportDiagnostic($"Telemetry quarantine failed; original queue retained: {exception.Message}");
        return TelemetrySendResult.Fail($"telemetry quarantine failed: {exception.Message}");
    }

    private static string? OversizeReason(byte[] json)
    {
        if (json.Length > MaxRawEventBytes)
            return "event_json_too_large";
        using var document = JsonDocument.Parse(json);
        foreach (var (name, limit) in new[] { ("payload", MaxPayloadBytes), ("properties", MaxPropertiesBytes) })
        {
            if (document.RootElement.TryGetProperty(name, out var value)
                && Encoding.UTF8.GetByteCount(value.GetRawText()) > limit)
                return $"{name}_too_large";
        }
        return null;
    }

    internal static bool IsTransient(TelemetryHttpResult result) =>
        result.TransportError is not null || result.StatusCode is 429 or 500 or 502 or 503 or 504;

    internal static TimeSpan RetryDelay(TelemetryHttpResult result, int attempt) =>
        result.RetryAfter is { } retryAfter
            ? TimeSpan.FromSeconds(Math.Clamp(retryAfter.TotalSeconds, 0, MaxRetryAfterSeconds))
            : TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1)));

    private async Task<TelemetryHttpResult> SendAuthenticatedAsync(string body, CancellationToken cancellationToken)
    {
        var token = _tokenStore.Load();
        if (token is null)
        {
            var registration = await RegisterAsync(cancellationToken).ConfigureAwait(false);
            if (registration.Token is null) return registration.Result;
            token = registration.Token;
        }
        var result = await SendWithTokenAsync(token, body, cancellationToken).ConfigureAwait(false);
        if (result.StatusCode != 401) return result;
        _tokenStore.Clear();
        var refreshed = await RegisterAsync(cancellationToken).ConfigureAwait(false);
        return refreshed.Token is null ? refreshed.Result
            : await SendWithTokenAsync(refreshed.Token, body, cancellationToken).ConfigureAwait(false);
    }

    internal static string BuildInstallEndpoint(string batchEndpoint)
    {
        if (batchEndpoint.EndsWith("/batch", StringComparison.OrdinalIgnoreCase))
            return batchEndpoint[..^"/batch".Length] + "/install";
        return batchEndpoint;
    }

    private static string SerializeBatch(string applicantId, IReadOnlyList<byte[]> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", BatchSchema);
            writer.WriteString("applicant_id", applicantId);
            writer.WriteStartArray("events");
            foreach (var json in events)
                writer.WriteRawValue(json);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task<TelemetryHttpResult> SendWithTokenAsync(string token, string body, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            [AuthorizationHeaderName] = $"Bearer {token}",
            [TimestampHeaderName] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            [NonceHeaderName] = Guid.NewGuid().ToString("N")
        };
        return await _transport.PostAsync(_batchEndpoint, headers, body, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string? Token, TelemetryHttpResult Result)> RegisterAsync(CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(
            new { schema = InstallSchema, applicant_id = _applicantId },
            JsonOptions);
        var result = await _transport
            .PostAsync(_installEndpoint, BootstrapHeaders(), body, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess) return (null, result);
        var invalid = new TelemetryHttpResult(502, null, "invalid install registration response");
        if (string.IsNullOrEmpty(result.Body)) return (null, invalid);
        try
        {
            if (JsonNode.Parse(result.Body)?["install_token"] is not JsonNode tokenNode)
                return (null, invalid);
            var token = tokenNode.GetValue<string>();
            if (!IsWellFormedInstallToken(token))
                return (null, invalid);
            _tokenStore.Save(token);
            return (token, result);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return (null, invalid);
        }
    }

    private IReadOnlyDictionary<string, string> BootstrapHeaders() =>
        new Dictionary<string, string>
        {
            [AuthorizationHeaderName] = $"Bearer {_bootstrapCredential}"
        };

    internal static bool IsWellFormedInstallToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != InstallTokenHexLength)
            return false;
        foreach (var character in token)
        {
            if (!Uri.IsHexDigit(character))
                return false;
        }
        return true;
    }

    // Only the final RitsuLib-facing result is text; retry decisions use the HTTP result.
    private static TelemetrySendResult ToSendResult(TelemetryHttpResult result) =>
        result.TransportError is not null
            ? TelemetrySendResult.Fail(result.TransportError)
            : TelemetrySendResult.Fail(result.RetryAfter is { } retryAfter
                ? $"telemetry endpoint returned {result.StatusCode} retry-after={(long)Math.Round(retryAfter.TotalSeconds)}s"
                : $"telemetry endpoint returned {result.StatusCode}");
}

/// <summary>Shared HttpClient transport used in production.</summary>
internal sealed class HttpClientTelemetryTransport : ITelemetryHttpTransport
{
    internal static readonly HttpClientTelemetryTransport Shared = new();

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<TelemetryHttpResult> PostAsync(
        string url,
        IReadOnlyDictionary<string, string> headers,
        string jsonBody,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            var body = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // The receiver only sends integer-second Retry-After values; the
            // HTTP-date form leaves Delta null and is ignored.
            var retryAfter = response.Headers.RetryAfter?.Delta;
            return new TelemetryHttpResult((int)response.StatusCode, body, null, retryAfter);
        }
        catch (HttpRequestException exception)
        {
            return new TelemetryHttpResult(0, null, $"network error: {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new TelemetryHttpResult(0, null, $"timeout: {exception.Message}");
        }
    }
}
