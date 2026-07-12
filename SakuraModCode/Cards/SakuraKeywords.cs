using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraKeywords
{
    public static readonly CardKeyword Wind = Keyword("WIND");
    public static readonly CardKeyword Water = Keyword("WATER");
    public static readonly CardKeyword Fire = Keyword("FIRE");
    public static readonly CardKeyword Earth = Keyword("EARTH");
    public static readonly CardKeyword Stabilize = Keyword("STABILIZE");
    public static readonly CardKeyword Manifest = Keyword("MANIFEST");
    public static readonly CardKeyword SakuraCard = Keyword("SAKURA_CARD");
    public static readonly CardKeyword Echo = Keyword("ECHO");
    public static readonly CardKeyword Invisible = Keyword("INVISIBLE");
    public static readonly CardKeyword Removable = Keyword("REMOVABLE");
    public static readonly CardKeyword CostDecreasing = Keyword("COST_DECREASING");
    public static readonly CardKeyword EntityLimited = Keyword("ENTITY_LIMITED");
    public static readonly CardKeyword Purge = Keyword("PURGE");
    public static readonly CardKeyword Loner = Keyword("LONER");
    public static readonly CardKeyword Frostbite = Keyword("FROSTBITE");

    private static readonly IReadOnlyDictionary<string, ModKeywordCardDescriptionPlacement> DescriptionPlacements =
        new Dictionary<string, ModKeywordCardDescriptionPlacement>
        {
            ["LONER"] = ModKeywordCardDescriptionPlacement.AfterCardDescription
        };

    public static void Register()
    {
        var registry = ModKeywordRegistry.For(MainFile.ModId);
        foreach (var stem in Stems)
        {
            registry.RegisterOwned(
                stem,
                "card_keywords",
                LocKey(stem, "title"),
                "card_keywords",
                LocKey(stem, "description"),
                iconPath: null,
                DescriptionPlacements.GetValueOrDefault(stem, ModKeywordCardDescriptionPlacement.None),
                includeInCardHoverTip: true);
        }
    }

    private static readonly string[] Stems =
    [
        "WIND",
        "WATER",
        "FIRE",
        "EARTH",
        "STABILIZE",
        "MANIFEST",
        "SAKURA_CARD",
        "ECHO",
        "INVISIBLE",
        "REMOVABLE",
        "COST_DECREASING",
        "ENTITY_LIMITED",
        "PURGE",
        "LONER",
        "FROSTBITE"
    ];

    private static CardKeyword Keyword(string stem) =>
        ModKeywordRegistry.GetCardKeyword(ModContentRegistry.GetQualifiedKeywordId(MainFile.ModId, stem));

    private static string LocKey(string stem, string suffix) =>
        $"{MainFile.ModId.ToUpperInvariant()}-{stem}.{suffix}";
}

public enum SakuraElement
{
    Wind,
    Water,
    Fire,
    Earth
}

[Flags]
public enum SakuraElementSet
{
    None = 0,
    Wind = 1 << SakuraElement.Wind,
    Water = 1 << SakuraElement.Water,
    Fire = 1 << SakuraElement.Fire,
    Earth = 1 << SakuraElement.Earth,
    All = Wind | Water | Fire | Earth
}

public static class SakuraElementSets
{
    public static readonly IReadOnlyList<SakuraElement> AllElements =
    [
        SakuraElement.Wind,
        SakuraElement.Water,
        SakuraElement.Fire,
        SakuraElement.Earth
    ];

    public static SakuraElementSet ToSet(this SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => SakuraElementSet.Wind,
            SakuraElement.Water => SakuraElementSet.Water,
            SakuraElement.Fire => SakuraElementSet.Fire,
            SakuraElement.Earth => SakuraElementSet.Earth,
            _ => SakuraElementSet.None
        };

    public static IEnumerable<SakuraElement> AsElements(this SakuraElementSet set) =>
        AllElements.Where(element => set.HasElement(element));

    public static bool HasElement(this SakuraElementSet set, SakuraElement element) =>
        (set & element.ToSet()) != SakuraElementSet.None;
}
