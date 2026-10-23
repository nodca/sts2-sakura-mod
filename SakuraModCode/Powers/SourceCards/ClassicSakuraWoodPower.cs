using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.Powers;

// Sakura Wood: Earth triggers poison all enemies for Amount, and each fully
// blocked hit triggers the attacker's Poison once.
public class ClassicSakuraWoodPower : ClassicWoodPower
{
    protected override string IconFileName => "wood_power_sakuracard.png";
    public override PowerStackType StackType => PowerStackType.Single;

    protected override async Task OnEarthTriggered(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0 || Owner.CombatState?.HittableEnemies.ToList() is not { Count: > 0 } enemies)
            return;

        Flash();
        foreach (var enemy in enemies)
            await PowerCmd.Apply<PoisonPower>(choiceContext, enemy, Amount, Owner, null);
    }

    // One native Poison tick: damage equal to its Amount, then lose 1, so the
    // trigger only pulls later Poison damage forward.
    protected override async Task OnAttackFullyBlocked(PlayerChoiceContext choiceContext, Creature attacker)
    {
        if (attacker.GetPower<PoisonPower>() is not { Amount: > 0 } poison)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, attacker, poison.Amount, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        if (attacker.IsAlive)
            await PowerCmd.Decrement(poison);
    }
}
