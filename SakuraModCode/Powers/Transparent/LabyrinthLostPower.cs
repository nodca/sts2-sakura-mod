using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public sealed class LabyrinthLostPower : SakuraPowerModel
{
    private static readonly SavedAttachedState<LabyrinthLostPower, bool> FirstAttackReceived =
        new("SakuraMod_LabyrinthFirstAttackReceived", () => false);

    protected override string IconFileName => "labyrinth_lost.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && target == Owner && amount > 0 && props.IsPoweredAttack() && !FirstAttackReceived[this]
            && !SakuraActions.HasElement(cardSource, SakuraElement.Earth)
            ? 0.5m
            : 1m;

    // This native commit runs before Block and follow-up damage; previews never spend the protection.
    public override Task AfterModifyingDamageAmount(CardModel? cardSource)
    {
        FirstAttackReceived[this] = true;
        Flash();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        FirstAttackReceived[this] = false;
        return Task.CompletedTask;
    }
}
