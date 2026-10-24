using MegaCrit.Sts2.Core.Entities.Cards;

namespace SakuraMod.SakuraModCode.Powers;

// Sakura Through: also triggers when a card leaves Memory or the Exhaust pile,
// so a Forgotten card pays out once on entering Memory and again on Remind.
// A move between the two piles is still one trigger.
public sealed class ClassicThroughSakuraPower : ClassicThroughPower
{
    protected override string IconFileName => "through_sakura.svg";

    protected override bool Triggers(PileType oldPileType, PileType newPileType) =>
        IsThroughPile(oldPileType) || IsThroughPile(newPileType);
}
