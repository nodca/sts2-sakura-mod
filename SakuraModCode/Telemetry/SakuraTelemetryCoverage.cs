using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using STS2RitsuLib;
using STS2RitsuLib.Compat;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal static class SakuraTelemetryCoverage
{
    internal const int ContractVersion = 2;
    internal const string SessionStartedEventName = "sakuramod.session.started";
    internal const string CoverageEventName = "balance_run.coverage";
    internal const string StageStarted = "started";
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
            accumulator.Failures))!.AsObject();

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
    public SakuraTelemetryCoverageCounters Expected { get; } = new(1);
    public SakuraTelemetryCoverageCounters Captured { get; } = new(0);
    public SakuraTelemetryCaptureFailures Failures { get; private set; } = new(0, 0);
    public void RecordFailure(CoverageFailureKind kind)
    {
        Failures = kind switch
        {
            CoverageFailureKind.Serialization => Failures with { Serialization = Failures.Serialization + 1 },
            _ => Failures with { Unknown = Failures.Unknown + 1 }
        };
    }
}

internal sealed record SakuraTelemetrySessionStarted(
    [property: JsonPropertyName("coverage_contract_version")] int CoverageContractVersion,
    [property: JsonPropertyName("sakura_mod_version")] string SakuraModVersion);

internal sealed record SakuraTelemetryCoverageCounters(
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
    [property: JsonPropertyName("capture_failure_counts")] SakuraTelemetryCaptureFailures CaptureFailureCounts);
