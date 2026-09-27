using System.Text.Json.Serialization;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed record SakuraTelemetryRewardChoice(
    [property: JsonPropertyName("floor")] int Floor,
    [property: JsonPropertyName("act")] int Act,
    [property: JsonPropertyName("history_index")] int HistoryIndex,
    [property: JsonPropertyName("card")] SakuraTelemetryChoiceCard Card,
    [property: JsonPropertyName("was_picked")] bool WasPicked,
    [property: JsonPropertyName("selection_kind")] string SelectionKind,
    [property: JsonPropertyName("gained_index")] int GainedIndex = -1);

internal readonly record struct SakuraTelemetryChoiceCard(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("upgrade")] int Upgrade);
