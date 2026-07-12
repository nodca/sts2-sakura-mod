using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed record SakuraTelemetryUsageRow(
    [property: JsonPropertyName("id")] string CardId,
    [property: JsonPropertyName("upgrade")] int UpgradeLevel,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("base_cost_bucket")] string BaseCostBucket,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("provenance")] string Provenance,
    [property: JsonPropertyName("generated_count")] int GeneratedCount,
    [property: JsonPropertyName("draw_count")] int DrawCount,
    [property: JsonPropertyName("combats_seen")] int CombatsSeen);

internal sealed class SakuraTelemetryUsageAccumulator
{
    private readonly Dictionary<UsageKey, MutableUsage> _usage = new();
    private readonly HashSet<object> _generatedInstances = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<UsageKey> _seenThisCombat = [];

    internal void Restore(IEnumerable<SakuraTelemetryUsageRow> rows)
    {
        foreach (var row in rows)
        {
            var key = new UsageKey(row.CardId, row.UpgradeLevel, row.Provenance);
            _usage[key] = new MutableUsage(key, row);
        }
    }

    internal void BeginCombat(IEnumerable<(object Instance, SakuraTelemetryCardInfo Card)> deck)
    {
        _generatedInstances.Clear();
        _seenThisCombat.Clear();
        foreach (var (_, card) in deck)
            RecordSeen(card, "deck_owned");
    }

    internal void RecordGenerated(object instance, SakuraTelemetryCardInfo card)
    {
        _generatedInstances.Add(instance);
        var usage = Get(card, "generated");
        usage.GeneratedCount++;
        RecordSeen(usage);
    }

    internal void RecordDraw(object instance, SakuraTelemetryCardInfo card)
    {
        var usage = Get(card, Provenance(instance));
        usage.DrawCount++;
        RecordSeen(usage);
    }

    internal void EndCombat() =>
        _generatedInstances.Clear();

    internal IReadOnlyList<SakuraTelemetryUsageRow> Snapshot() =>
        _usage.Values
            .OrderBy(static usage => usage.Key.CardId, StringComparer.Ordinal)
            .ThenBy(static usage => usage.Key.UpgradeLevel)
            .ThenBy(static usage => usage.Key.Provenance, StringComparer.Ordinal)
            .Select(static usage => usage.ToRow())
            .ToArray();

    private string Provenance(object instance) =>
        _generatedInstances.Contains(instance) ? "generated" : "deck_owned";

    private void RecordSeen(SakuraTelemetryCardInfo card, string provenance) =>
        RecordSeen(Get(card, provenance));

    private void RecordSeen(MutableUsage usage)
    {
        if (_seenThisCombat.Add(usage.Key))
            usage.CombatsSeen++;
    }

    private MutableUsage Get(SakuraTelemetryCardInfo card, string provenance)
    {
        var key = new UsageKey(card.CardId, card.UpgradeLevel, provenance);
        if (_usage.TryGetValue(key, out var usage))
            return usage;

        usage = new MutableUsage(key, card);
        _usage.Add(key, usage);
        return usage;
    }

    private readonly record struct UsageKey(string CardId, int UpgradeLevel, string Provenance);

    private sealed class MutableUsage(UsageKey key, SakuraTelemetryCardInfo card)
    {
        public UsageKey Key { get; } = key;
        public int GeneratedCount { get; set; }
        public int DrawCount { get; set; }
        public int CombatsSeen { get; set; }

        public MutableUsage(UsageKey key, SakuraTelemetryUsageRow row)
            : this(
                key,
                new SakuraTelemetryCardInfo(
                    row.CardId,
                    row.UpgradeLevel,
                    row.Rarity,
                    row.Type,
                    row.BaseCostBucket,
                    row.Category,
                    row.Owner))
        {
            GeneratedCount = row.GeneratedCount;
            DrawCount = row.DrawCount;
            CombatsSeen = row.CombatsSeen;
        }

        public SakuraTelemetryUsageRow ToRow() =>
            new(
                card.CardId,
                card.UpgradeLevel,
                card.Rarity,
                card.Type,
                card.BaseCostBucket,
                card.Category,
                card.Owner,
                Key.Provenance,
                GeneratedCount,
                DrawCount,
                CombatsSeen);
    }
}
