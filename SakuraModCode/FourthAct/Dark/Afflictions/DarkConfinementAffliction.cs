using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Afflictions;

public sealed class DarkConfinementAffliction : ModAfflictionTemplate
{
    public override AfflictionAssetProfile AssetProfile =>
        new(DarkEnemyAssets.ConfinementOverlay);
}
