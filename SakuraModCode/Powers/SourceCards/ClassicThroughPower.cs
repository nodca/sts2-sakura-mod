using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

// Clow Through: whenever one of the owner's cards enters Memory or the Exhaust
// pile, apply Amount Poison to all enemies. Every card movement is
// one trigger, so swapping a pile into Memory triggers once per card.
public class ClassicThroughPower : SakuraPowerModel
{
    protected override string IconFileName => "through.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        var newPileType = card.Pile?.Type ?? PileType.None;
        if (Amount <= 0
            || card.Owner?.Creature != Owner
            || newPileType == oldPileType
            || !Triggers(oldPileType, newPileType))
            return;

        if (Owner.CombatState?.HittableEnemies.ToList() is not { Count: > 0 } enemies)
            return;

        Flash();
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (var enemy in enemies)
            await PowerCmd.Apply<PoisonPower>(choiceContext, enemy, Amount, Owner, null);
    }

    protected virtual bool Triggers(PileType oldPileType, PileType newPileType) =>
        IsThroughPile(newPileType);

    protected static bool IsThroughPile(PileType pileType) =>
        pileType == PileType.Exhaust || pileType == SakuraMemoryPile.PileType;
}
