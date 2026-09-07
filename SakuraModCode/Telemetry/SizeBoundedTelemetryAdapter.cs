using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed class SizeBoundedTelemetryAdapter(ITelemetryAdapter inner) : ITelemetryAdapter
{
    internal const int MaxBatchBytes = 900_000;
    internal const int MaxAttempts = 3;

    // Ceiling for a server-provided Retry-After wait so a hostile or buggy
    // header cannot stall the send queue for minutes; longer values clamp here.
    internal const int MaxRetryAfterSeconds = 120;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ITelemetryAdapter _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private long _batches;
    private long _events;
    private long _attempts;
    private long _retries;
    private long _succeeded;
    private long _failed;

    internal TelemetrySendStats Stats => new(
        Interlocked.Read(ref _batches), Interlocked.Read(ref _events), Interlocked.Read(ref _attempts),
        Interlocked.Read(ref _retries), Interlocked.Read(ref _succeeded), Interlocked.Read(ref _failed));

    public string AdapterId => _inner.AdapterId;

    public string EndpointDescription => _inner.EndpointDescription;

    public async ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _batches);
        Interlocked.Add(ref _events, events.Count);
        foreach (var batch in SplitBatches(applicant.ApplicantId, events))
        {
            for (var attempt = 1; ; attempt++)
            {
                Interlocked.Increment(ref _attempts);
                var result = await _inner.SendAsync(applicant, batch, cancellationToken);
                if (result.Success)
                {
                    Interlocked.Increment(ref _succeeded);
                    break;
                }
                var failure = TelemetryFailureInfo.Parse(result.ErrorMessage);
                if (attempt >= MaxAttempts || !IsTransient(failure))
                {
                    Interlocked.Increment(ref _failed);
                    return result;
                }

                Interlocked.Increment(ref _retries);
                await Task.Delay(RetryDelay(failure, attempt), cancellationToken);
            }
        }

        return TelemetrySendResult.Ok();
    }

    internal readonly record struct TelemetrySendStats(long Batches, long Events, long Attempts, long Retries, long Succeeded, long Failed);

    // Transient means a later attempt may succeed: transport failures (the
    // adapter's "network error:" / "timeout:" messages) and the receiver's
    // overloaded and rate-limit statuses. Everything else, including the
    // token verdicts 400/401/403, is final for this send.
    internal static bool IsTransient(string? error) => IsTransient(TelemetryFailureInfo.Parse(error));

    internal static bool IsTransient(TelemetryFailureInfo failure) =>
        failure.Transport || failure.StatusCode is 429 or 500 or 502 or 503 or 504;

    // Wait for the server's Retry-After when it provided one (clamped), or
    // fall back to the fixed exponential backoff of 250ms * 2^(attempt-1).
    internal static TimeSpan RetryDelay(TelemetryFailureInfo failure, int attempt) =>
        failure.RetryAfter is { } retryAfter
            ? retryAfter > TimeSpan.FromSeconds(MaxRetryAfterSeconds)
                ? TimeSpan.FromSeconds(MaxRetryAfterSeconds)
                : retryAfter
            : TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1)));

    internal static IReadOnlyList<IReadOnlyList<TelemetryEnvelope>> SplitBatches(
        string applicantId,
        IReadOnlyList<TelemetryEnvelope> events,
        int maxBytes = MaxBatchBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);

        var emptyBatchBytes = JsonSerializer.SerializeToUtf8Bytes(
            new { schema = "ritsulib.telemetry.batch.v1", applicant_id = applicantId, events = Array.Empty<TelemetryEnvelope>() },
            JsonOptions).Length;
        var fixedBytes = emptyBatchBytes - 2;
        var batches = new List<IReadOnlyList<TelemetryEnvelope>>();
        var current = new List<TelemetryEnvelope>();
        var currentBytes = fixedBytes + 2;

        foreach (var envelope in events)
        {
            var envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions).Length;
            var additionalBytes = envelopeBytes + (current.Count == 0 ? 0 : 1);
            if (current.Count > 0 && currentBytes + additionalBytes > maxBytes)
            {
                batches.Add(current);
                current = new List<TelemetryEnvelope>();
                currentBytes = fixedBytes + 2;
                additionalBytes = envelopeBytes;
            }

            current.Add(envelope);
            currentBytes += additionalBytes;
        }

        if (current.Count > 0)
            batches.Add(current);

        return batches;
    }
}
