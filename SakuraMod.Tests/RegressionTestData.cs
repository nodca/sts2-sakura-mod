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
}
