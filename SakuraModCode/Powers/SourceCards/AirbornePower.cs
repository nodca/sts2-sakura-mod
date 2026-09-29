using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.Powers;

public class AirbornePower : SakuraPowerModel
{
    // Fixed rule constant rather than a card number, so Release cannot raise it.
    public const decimal DamageMultiplier = 0.5m;

    protected override string IconFileName => "airborne_power.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && target == Owner && dealer?.Side != Owner.Side && props.IsPoweredAttack()
            ? DamageMultiplier
            : 1m;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner.Side == side && participants.Contains(Owner) && Amount > 0)
            await PowerCmd.Decrement(this);
    }
}
