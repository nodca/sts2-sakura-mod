using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using STS2RitsuLib;
using STS2RitsuLib.Compat;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal static class SakuraTelemetryCoverage
{
    internal const int ContractVersion = 1;
    internal const string SessionStartedEventName = "sakuramod.session.started";
    internal const string CoverageEventName = "balance_run.coverage";
    internal const string StageStarted = "started";
    internal const string StageCheckpoint = "checkpoint";
    internal const string StageTerminalAttempted = "terminal_attempted";

    private static bool _sessionStartedSent;

    internal static void ResetSessionMarkerForTests() =>
        _sessionStartedSent = false;

    internal static JsonObject BuildSessionStartedPayload(string sakuraModVersion) =>
        JsonSerializer.SerializeToNode(new SakuraTelemetrySessionStarted(ContractVersion, sakuraModVersion))!.AsObject();

    internal static JsonObject BuildCoveragePayload(
        string runKey,
        string stage,
        int playerCount,
        int sakuraPlayerCount,
        SakuraTelemetryCoverageAccumulator accumulator) =>
        JsonSerializer.SerializeToNode(new SakuraTelemetryRunCoverage(
            ContractVersion,
            runKey,
            stage,
            "Standard",
            playerCount,
            sakuraPlayerCount,
            accumulator.Expected,
            accumulator.Captured,
            accumulator.Failures,
            accumulator.LastOfferSequence))!.AsObject();

    internal static CoverageFailureKind ClassifyFailure(Exception exception) =>
        exception is JsonException or NotSupportedException
            ? CoverageFailureKind.Serialization
            : CoverageFailureKind.Unknown;

    internal static bool CaptureIfEnabled(
        ITelemetryClient client,
        string eventName,
        JsonNode payload,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (!client.IsEnabled(SakuraTelemetry.RunHistoryRequestId))
            return false;

        if (properties is null)
            client.CapturePayload(eventName, SakuraTelemetry.RunHistoryRequestId, payload);
        else
            client.CapturePayload(eventName, SakuraTelemetry.RunHistoryRequestId, payload, properties);
        return true;
    }

    internal static void TryCaptureSessionStarted()
    {
        if (_sessionStartedSent)
            return;

        SakuraTelemetry.TryExecute(
            () =>
            {
                var client = RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId);
                if (!client.IsEnabled(SakuraTelemetry.RunHistoryRequestId))
                    return;

                var mods = SakuraTelemetryContract.GameplayMods(RitsuModManager.GetKnownMods());
                if (CaptureIfEnabled(
                    client,
                    SessionStartedEventName,
                    BuildSessionStartedPayload(SakuraTelemetryContract.SakuraModVersion(mods))))
                    _sessionStartedSent = true;
            },
            exception => SakuraTelemetry.LogCaptureFailure("session started", exception));
    }

}

internal enum CoverageFailureKind
{
    Serialization,
    Unknown
}

internal sealed class SakuraTelemetryCoverageAccumulator
{
    public SakuraTelemetryCoverageCounters Expected { get; private set; } = new(1, 0, 0, 1);
    public SakuraTelemetryCoverageCounters Captured { get; private set; } = new(0, 0, 0, 0);
    public SakuraTelemetryCaptureFailures Failures { get; private set; } = new(0, 0);
    public int LastOfferSequence { get; private set; }
    public bool HasUnpublishedChanges { get; private set; }

    public void RecordContextCaptured()
    {
        Captured = Captured with { Context = 1 };
        HasUnpublishedChanges = true;
    }

    public void RecordOffer(bool captured)
    {
        Expected = Expected with { RewardOffers = Expected.RewardOffers + 1 };
        if (captured)
            Captured = Captured with { RewardOffers = Captured.RewardOffers + 1 };
        HasUnpublishedChanges = true;
    }

    public void RecordTake(bool captured)
    {
        Expected = Expected with { RewardTakes = Expected.RewardTakes + 1 };
        if (captured)
            Captured = Captured with { RewardTakes = Captured.RewardTakes + 1 };
        HasUnpublishedChanges = true;
    }

    public void RecordCompletedCaptured()
    {
        Captured = Captured with { Completed = 1 };
        HasUnpublishedChanges = true;
    }

    public void SetLastOfferSequence(int sequence)
    {
        if (sequence == LastOfferSequence)
            return;
        LastOfferSequence = sequence;
        HasUnpublishedChanges = true;
    }

    public void RecordFailure(CoverageFailureKind kind)
    {
        Failures = kind switch
        {
            CoverageFailureKind.Serialization => Failures with { Serialization = Failures.Serialization + 1 },
            _ => Failures with { Unknown = Failures.Unknown + 1 }
        };
        HasUnpublishedChanges = true;
    }

    public void MarkPublished() =>
        HasUnpublishedChanges = false;
}

internal sealed record SakuraTelemetrySessionStarted(
    [property: JsonPropertyName("coverage_contract_version")] int CoverageContractVersion,
    [property: JsonPropertyName("sakura_mod_version")] string SakuraModVersion);

internal sealed record SakuraTelemetryCoverageCounters(
    [property: JsonPropertyName("context")] int Context,
    [property: JsonPropertyName("reward_offers")] int RewardOffers,
    [property: JsonPropertyName("reward_takes")] int RewardTakes,
    [property: JsonPropertyName("completed")] int Completed);

internal sealed record SakuraTelemetryCaptureFailures(
    [property: JsonPropertyName("serialization")] int Serialization,
    [property: JsonPropertyName("unknown")] int Unknown);

internal sealed record SakuraTelemetryRunCoverage(
    [property: JsonPropertyName("coverage_contract_version")] int CoverageContractVersion,
    [property: JsonPropertyName("run_key")] string RunKey,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("game_mode")] string GameMode,
    [property: JsonPropertyName("player_count")] int PlayerCount,
    [property: JsonPropertyName("sakura_player_count")] int SakuraPlayerCount,
    [property: JsonPropertyName("expected")] SakuraTelemetryCoverageCounters Expected,
    [property: JsonPropertyName("captured")] SakuraTelemetryCoverageCounters Captured,
    [property: JsonPropertyName("capture_failure_counts")] SakuraTelemetryCaptureFailures CaptureFailureCounts,
    [property: JsonPropertyName("last_offer_sequence")] int LastOfferSequence);
