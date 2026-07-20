using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

public class SakuraErasePower : SakuraPowerModel
{
    protected override string IconFileName => "erase.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || !Owner.IsAlive)
            return;

        var hpLoss = SakuraEraseRules.NormalHpLoss(Owner.MaxHp, Amount);
        if (hpLoss <= 0)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            hpLoss,
            SakuraPowerValueProps.HpLoss,
            dealer: null,
            cardSource: null);
    }
}
