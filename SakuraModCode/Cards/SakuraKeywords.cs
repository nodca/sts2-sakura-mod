using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraKeywords
{
    [CustomEnum("WIND")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Wind;

    [CustomEnum("WATER")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Water;

    [CustomEnum("FIRE")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Fire;

    [CustomEnum("EARTH")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Earth;

    [CustomEnum("STABILIZE")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Stabilize;

    [CustomEnum("CATALOG")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Catalog;

    [CustomEnum("MANIFEST")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Manifest;

    [CustomEnum("SAKURA_CARD")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword SakuraCard;

    [CustomEnum("ECHO")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Echo;

    [CustomEnum("INVISIBLE")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Invisible;

    [CustomEnum("REMOVABLE")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Removable;

    [CustomEnum("COST_DECREASING")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword CostDecreasing;

    [CustomEnum("ENTITY_LIMITED")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword EntityLimited;

    [CustomEnum("BURN")]
    [KeywordProperties(AutoKeywordPosition.None)]
    public static CardKeyword Burn;

    [CustomEnum("LONER")]
    [KeywordProperties(AutoKeywordPosition.After)]
    public static CardKeyword Loner;
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
