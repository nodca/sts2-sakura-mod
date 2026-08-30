using System.Text.Json;
using System.Text.Json.Serialization;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed class SizeBoundedTelemetryAdapter(ITelemetryAdapter inner) : ITelemetryAdapter
{
    internal const int MaxBatchBytes = 900_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ITelemetryAdapter _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string AdapterId => _inner.AdapterId;

    public string EndpointDescription => _inner.EndpointDescription;

    public async ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var batch in SplitBatches(applicant.ApplicantId, events))
        {
            var result = await _inner.SendAsync(applicant, batch, cancellationToken);
            if (!result.Success)
                return result;
        }

        return TelemetrySendResult.Ok();
    }

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
