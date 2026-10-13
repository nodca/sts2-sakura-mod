using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Water.CardState;

// Presentation of WaterFrozenPower on each hand card. The Power owns the play gate, so a
// leftover projection can never block a card.
public sealed class FrozenAffliction : ModAfflictionTemplate
{
    public const string OverlayScenePath =
        MainFile.ResPath + "/scenes/cards/overlays/frozen_affliction.tscn";

    public override AfflictionAssetProfile AssetProfile => new(OverlayScenePath);

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => true;
}
