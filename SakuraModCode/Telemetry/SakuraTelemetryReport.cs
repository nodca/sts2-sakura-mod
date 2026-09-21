using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SakuraMod.SakuraModCode.Telemetry;

internal readonly record struct SakuraTelemetryCardReference(string CardId, int UpgradeLevel);

/// <summary>Builds definitions from the same final history that the receiver analyses.</summary>
internal static class SakuraTelemetryReport
{
    internal static IReadOnlyList<SakuraTelemetryCardReference> CardReferences(JsonNode? history)
    {
        var references = new HashSet<SakuraTelemetryCardReference>();
        Visit(history);
        return references.OrderBy(static card => card.CardId, StringComparer.Ordinal)
            .ThenBy(static card => card.UpgradeLevel).ToArray();

        void Add(JsonNode? value, bool modelIdOnly = false)
        {
            var id = ModelEntry(modelIdOnly ? value : (value as JsonObject)?["id"]);
            var upgrade = 0;
            if (!modelIdOnly && value is JsonObject card && card["current_upgrade_level"] is JsonValue level
                && !level.TryGetValue(out upgrade)) return;
            if (id is not null && upgrade >= 0)
                references.Add(new SakuraTelemetryCardReference(id, upgrade));
        }

        void Visit(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                foreach (var item in array) Visit(item);
            }
            else if (node is JsonObject obj)
            {
                foreach (var (name, value) in obj)
                {
                    if (name is "deck" or "cards_gained" or "cards_removed" && value is JsonArray cards)
                        foreach (var card in cards) Add(card);
                    else if (name is "card" or "original_card" or "final_card")
                        Add(value);
                    else if (name == "upgraded_cards" && value is JsonArray upgrades)
                        foreach (var id in upgrades) Add(id, modelIdOnly: true);
                    Visit(value);
                }
            }
        }
    }

    internal static JsonNode Build(
        BalanceRunIdentity identity, JsonNode? basePayload,
        Func<SakuraTelemetryCardReference, SakuraTelemetryCardInfo?> resolve)
    {
        var definitions = new Dictionary<SakuraTelemetryCardReference, SakuraTelemetryCardInfo>();
        foreach (var usage in identity.Usage)
        {
            var card = new SakuraTelemetryCardInfo(usage.CardId, usage.UpgradeLevel, usage.Rarity,
                usage.Type, usage.BaseCostBucket, usage.Category, usage.Owner);
            definitions[new(card.CardId, card.UpgradeLevel)] = card;
        }
        foreach (var reference in CardReferences((basePayload as JsonObject)?["run_history"]))
        {
            if (!definitions.ContainsKey(reference) && resolve(reference) is { } card)
                definitions.Add(reference, card);
        }
        var cards = definitions.Values.OrderBy(static card => card.CardId, StringComparer.Ordinal)
            .ThenBy(static card => card.UpgradeLevel).ToArray();
        return JsonSerializer.SerializeToNode(new SakuraTelemetryBalanceRun(
            SakuraTelemetryContract.ReportVersion, identity.RunKey, identity.ContextChecksum, identity.Usage,
            identity.Context, cards))!;
    }

    internal static SakuraTelemetryCardInfo? Resolve(SakuraTelemetryCardReference reference)
    {
        try
        {
            // Native save restoration creates a detached mutable card, including
            // its upgrades. Never upgrade or otherwise mutate a gameplay card.
            var card = CardModel.FromSerializable(new SerializableCard
            {
                Id = new ModelId("CARD", reference.CardId),
                CurrentUpgradeLevel = reference.UpgradeLevel
            });
            return card.Id.Entry == reference.CardId && card.CurrentUpgradeLevel == reference.UpgradeLevel
                && SakuraTelemetryCardClassifier.TryClassify(card, out var info) ? info : null;
        }
        catch (Exception exception)
        {
            SakuraTelemetry.LogCaptureFailure("card definition", exception);
            return null; // The receiver reports missing definitions explicitly.
        }
    }

    private static string? ModelEntry(JsonNode? node)
    {
        if (node is JsonObject obj)
            node = obj["entry"] ?? obj["Entry"] ?? obj["id"] ?? obj["Id"];
        if (node is not JsonValue value || !value.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id))
            return null;
        id = id.Trim();
        return id.StartsWith("CARD.", StringComparison.Ordinal) ? id[5..] : id;
    }
}
