using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Powers;

public sealed class SwingDamageWindowPower : SakuraPowerModel
{
    protected override string IconFileName => "earth_element.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public static int RequestedMultiplier(bool extraEffect) => extraEffect ? 3 : 2;

    public static int HighestMultiplier(int current, int requested) =>
        Math.Max(current, Math.Clamp(requested, 2, 3));

    public override decimal GetScaledAmountForMultiplayer(
        ICombatState combatState,
        Creature? applier,
        decimal amount,
        Creature target,
        CardModel? cardSource) => amount;

    public void KeepHighestMultiplier(int multiplier) =>
        SetAmount(HighestMultiplier(Amount, multiplier));

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        IsEligible(target, props, dealer, cardSource, Owner, Amount)
            ? Amount
            : 1m;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    internal static bool IsEligible(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        Creature owner,
        int multiplier) =>
        multiplier is 2 or 3
        && target?.GetPower<WeakPower>()?.Amount > 0
        && (dealer == owner || owner.Pets.Contains(dealer))
        && props.IsPoweredAttack()
        && cardSource is { Type: CardType.Attack }
        && cardSource.Owner?.Creature == owner;
}
