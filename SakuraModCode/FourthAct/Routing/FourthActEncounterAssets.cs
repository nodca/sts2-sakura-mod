using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal static class FourthActEncounterAssets
{
    internal static EncounterAssetProfile WindBoss { get; } = FromVanillaBoss("queen_boss");
    internal static EncounterAssetProfile DarkBoss { get; } = FromVanillaPlaceholderBoss("aeonglass_boss");

    private static EncounterAssetProfile FromVanillaBoss(string encounterEntry)
    {
        var source = ContentAssetProfiles.Encounter(encounterEntry);
        return new EncounterAssetProfile(
            BossNodeSpinePath: source.BossNodeSpinePath,
            MapNodeAssetPaths: [source.BossNodeSpinePath!],
            RunHistoryIconPath: source.RunHistoryIconPath,
            RunHistoryIconOutlinePath: source.RunHistoryIconOutlinePath);
    }

    private static EncounterAssetProfile FromVanillaPlaceholderBoss(string encounterEntry)
    {
        var source = ContentAssetProfiles.Encounter(encounterEntry);
        var bossNodePath = $"res://images/map/placeholder/{encounterEntry}_icon";
        return new EncounterAssetProfile(
            BossNodeSpinePath: bossNodePath,
            MapNodeAssetPaths: [$"{bossNodePath}.png", $"{bossNodePath}_outline.png"],
            RunHistoryIconPath: source.RunHistoryIconPath,
            RunHistoryIconOutlinePath: source.RunHistoryIconOutlinePath);
    }
}
