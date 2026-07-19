using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Compat;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed class BalanceRunIdentity
{
    [JsonPropertyName("run_key")]
    public string RunKey { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public SakuraTelemetryRunContext? Context { get; set; }

    [JsonPropertyName("context_checksum")]
    public string ContextChecksum { get; set; } = string.Empty;

    [JsonPropertyName("usage")]
    public IReadOnlyList<SakuraTelemetryUsageRow> Usage { get; set; } = [];

    [JsonPropertyName("last_offer_sequence")]
    public int LastOfferSequence { get; set; }

    public static BalanceRunIdentity Create() =>
        new() { RunKey = Guid.NewGuid().ToString("D") };

    public bool IsValid() =>
        Guid.TryParseExact(RunKey, "D", out _);
}

internal sealed record SakuraTelemetryGameplayMod(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version);

internal sealed record SakuraTelemetryRunContext(
    [property: JsonPropertyName("balance_contract_version")] int BalanceContractVersion,
    [property: JsonPropertyName("run_key")] string RunKey,
    [property: JsonPropertyName("sakura_mod_version")] string SakuraModVersion,
    [property: JsonPropertyName("ascension")] int Ascension,
    [property: JsonPropertyName("player_count")] int PlayerCount,
    [property: JsonPropertyName("game_mode")] string GameMode,
    [property: JsonPropertyName("gameplay_mods")] IReadOnlyList<SakuraTelemetryGameplayMod> GameplayMods);

internal sealed record SakuraTelemetryBalanceRun(
    [property: JsonPropertyName("balance_contract_version")] int BalanceContractVersion,
    [property: JsonPropertyName("run_key")] string RunKey,
    [property: JsonPropertyName("context_checksum")] string ContextChecksum,
    [property: JsonPropertyName("usage")] IReadOnlyList<SakuraTelemetryUsageRow> Usage);

internal readonly record struct SakuraTelemetryCardInfo(
    [property: JsonPropertyName("id")] string CardId,
    [property: JsonPropertyName("upgrade")] int UpgradeLevel,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("base_cost_bucket")] string BaseCostBucket,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("owner")] string Owner);

internal readonly record struct SakuraTelemetryCardChoice(
    [property: JsonPropertyName("id")] string CardId,
    [property: JsonPropertyName("upgrade")] int UpgradeLevel,
    bool WasPicked);

internal static class SakuraTelemetryContract
{
    internal const int Version = 2;

    internal static IReadOnlyList<SakuraTelemetryGameplayMod> GameplayMods(
        IEnumerable<RitsuModInfo> knownMods) =>
        knownMods
            .Where(static mod => mod.IsLoaded && mod.AffectsGameplay)
            .GroupBy(static mod => mod.Id, StringComparer.Ordinal)
            .Select(static group => group
                .OrderBy(static mod => mod.Version ?? "unknown", StringComparer.Ordinal)
                .First())
            .Select(static mod => new SakuraTelemetryGameplayMod(
                mod.Id,
                string.IsNullOrWhiteSpace(mod.Version) ? "unknown" : mod.Version.Trim()))
            .OrderBy(static mod => mod.Id, StringComparer.Ordinal)
            .ThenBy(static mod => mod.Version, StringComparer.Ordinal)
            .ToArray();

    internal static string SakuraModVersion(IReadOnlyList<SakuraTelemetryGameplayMod> gameplayMods) =>
        gameplayMods.FirstOrDefault(static mod => mod.Id == MainFile.ModId)?.Version ?? "unknown";

    internal static string ContextChecksum(SakuraTelemetryRunContext context)
    {
        var canonical = context with
        {
            GameplayMods = context.GameplayMods
                .OrderBy(static mod => mod.Id, StringComparer.Ordinal)
                .ThenBy(static mod => mod.Version, StringComparer.Ordinal)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(canonical);
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))}";
    }
}

internal static class SakuraTelemetryCardClassifier
{
    internal static bool TryClassify(CardModel card, out SakuraTelemetryCardInfo info)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!TryClassifyOwner(card.GetType(), out var category, out var owner))
        {
            info = default;
            return false;
        }

        info = new SakuraTelemetryCardInfo(
            card.Id.Entry,
            card.CurrentUpgradeLevel,
            card.Rarity.ToString(),
            card.Type.ToString(),
            BaseCostBucket(card),
            category,
            owner);
        return true;
    }

    internal static bool TryClassifyOwner(Type cardType, out string category, out string owner)
    {
        if (SakuraCardCatalog.TryGetMetadata(cardType, out var metadata) && metadata.Era.HasValue)
        {
            category = metadata.Era.Value switch
            {
                SourceEraClass.Clear => "transparent",
                SourceEraClass.Clow => "clow",
                SourceEraClass.Sakura => "sakura",
                _ => throw new ArgumentOutOfRangeException(nameof(cardType))
            };
            owner = MainFile.ModId;
            return true;
        }

        if (typeof(SpellCard).IsAssignableFrom(cardType))
        {
            category = "spell";
            owner = MainFile.ModId;
            return true;
        }

        if (cardType.Assembly == typeof(CardModel).Assembly)
        {
            category = "vanilla";
            owner = "vanilla";
            return true;
        }

        category = string.Empty;
        owner = string.Empty;
        return false;
    }

    private static string BaseCostBucket(CardModel card)
    {
        if (card.EnergyCost.CostsX)
            return "X";

        return card.EnergyCost.Canonical switch
        {
            < 0 => "unplayable",
            >= 4 => "4+",
            var cost => cost.ToString()
        };
    }
}
