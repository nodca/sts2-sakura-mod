using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal static class FourthActEncounterAssets
{
    internal static EncounterAssetProfile WindBoss { get; } = Boss("windy");
    internal static EncounterAssetProfile WaterBoss { get; } = Boss("watery");
    internal static EncounterAssetProfile FireBoss { get; } = Boss("firey");
    internal static EncounterAssetProfile EarthBoss { get; } = Boss("earthy");
    internal static EncounterAssetProfile LightBoss { get; } = Boss("light");
    internal static EncounterAssetProfile DarkBoss { get; } = Boss("dark");
    internal static EncounterAssetProfile UnchosenEndpoint { get; } = Boss("light_dark");

    private static EncounterAssetProfile Boss(string name)
    {
        var bossNodePath = $"res://SakuraMod/images/map/fourth_act/{name}_icon";
        // An extensionless stem selects the native static icon + paper-outline path.
        return new EncounterAssetProfile(
            BossNodeSpinePath: bossNodePath,
            MapNodeAssetPaths: [$"{bossNodePath}.png", $"{bossNodePath}_outline.png"],
            RunHistoryIconPath: $"{bossNodePath}.png",
            RunHistoryIconOutlinePath: $"{bossNodePath}_outline.png");
    }
}
