using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Earth.CardState;

// The sole buried state. A buried card stays in the Discard Pile through the next shuffle.
public sealed class BuriedAffliction : ModAfflictionTemplate
{
    public const string OverlayScenePath =
        MainFile.ResPath + "/scenes/cards/overlays/buried_affliction.tscn";

    public override AfflictionAssetProfile AssetProfile => new(OverlayScenePath);
}
