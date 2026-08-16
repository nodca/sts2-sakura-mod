using STS2RitsuLib.Content;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;

internal static class RegressionTestData
{
    public static IReadOnlyList<Type> RegisteredCardTypes =>
        SakuraContentRegistration.AllCardTypesForRegistration().ToList();

    public static IReadOnlyList<Type> RegisteredPowerTypes =>
        SakuraContentRegistration.AllPowerTypesForRegistration().ToList();

    public static IReadOnlySet<string> RemovedClearSupportCardTypeNames { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "DreamWand",
            "DreamCompass",
            "Stabilize",
            "KeroAdvice",
            "KeroRecon",
            "KeroSnackBreak",
            "KeroBond",
            "TomoyoCostume",
            "DreamCostume",
            "RollerbladeDash",
            "MagicBarrier",
            "MemoryFracture",
            "VoidBond",
            "TsubasaAnotherMe",
            "EquivalentExchange",
            "CopiedSoul",
            "SleepingWings",
            "MemoryFeather",
            "DimensionalDrift"
        };

    public static IReadOnlySet<string> RemovedAncientCardTypeNames { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SakuraLegacy",
            "ReturnToOrigin"
        };

    public static IReadOnlyList<string> RemovedAncientCardLocalizationPrefixes { get; } =
    [
        "SAKURA_MOD_CARD_SAKURA_LEGACY.",
        "SAKURA_MOD_CARD_RETURN_TO_ORIGIN."
    ];

    public static IReadOnlySet<string> RemovedLegacyRelicTypeNames { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "DreamKey",
            "StorageRibbon",
            "CatalogNewPage",
            "YueMagicCrystal",
            "BaguaCompass",
            "DreamJournal",
            "SakuraIntuition",
            "DreamKeyTrueForm",
            "KaitoPocketWatch",
            "AkihoAliceBook",
            "NamelessBookTruth",
            "SakuraModRelic"
        };

    public static IReadOnlyList<string> RemovedLegacyRelicLocalizationPrefixes { get; } =
    [
        "SAKURAMOD-DREAM_KEY.",
        "SAKURAMOD-STORAGE_RIBBON.",
        "SAKURAMOD-CATALOG_NEW_PAGE.",
        "SAKURAMOD-YUE_MAGIC_CRYSTAL.",
        "SAKURAMOD-BAGUA_COMPASS.",
        "SAKURAMOD-DREAM_JOURNAL.",
        "SAKURAMOD-SAKURA_INTUITION.",
        "SAKURAMOD-DREAM_KEY_TRUE_FORM.",
        "SAKURAMOD-KAITO_POCKET_WATCH.",
        "SAKURAMOD-AKIHO_ALICE_BOOK.",
        "SAKURAMOD-NAMELESS_BOOK_TRUTH."
    ];

    public static IReadOnlyList<string> RemovedLegacyHostLocalizationPrefixes { get; } =
    [
        "SAKURAMOD-SAKURA_MOD.",
        "THE_ARCHITECT.talk.SAKURAMOD-SAKURA_MOD."
    ];

    public static IReadOnlySet<string> RemovedClearElementPowerTypeNames { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "WindElementPower",
            "WaterElementPower",
            "FireElementPower",
            "EarthElementPower"
        };
}
