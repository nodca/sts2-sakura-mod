using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Powers;

// Clow Wood: every Earth trigger and every fully blocked enemy hit poisons a
// random enemy for Amount.
public class ClassicWoodPower : SakuraPowerModel
{
    protected override string IconFileName => "wood_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    // Called by the Earth element state after it grants its Block.
    internal static async Task AfterEarthTriggered(PlayerChoiceContext choiceContext, Creature owner)
    {
        foreach (var power in owner.Powers.OfType<ClassicWoodPower>().ToArray())
            await power.OnEarthTriggered(choiceContext);
    }

    // Each hit is its own trigger, like the native Thorns.
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || !result.WasFullyBlocked
            || dealer is not { IsAlive: true }
            || dealer.Side == Owner.Side
            || !props.IsPoweredAttack())
            return;

        await OnAttackFullyBlocked(choiceContext, dealer);
    }

    protected virtual Task OnEarthTriggered(PlayerChoiceContext choiceContext) =>
        PoisonRandomEnemy(choiceContext);

    protected virtual Task OnAttackFullyBlocked(PlayerChoiceContext choiceContext, Creature attacker) =>
        PoisonRandomEnemy(choiceContext);

    private async Task PoisonRandomEnemy(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0
            || Owner.Player is not { } player
            || Owner.CombatState?.HittableEnemies.ToList() is not { Count: > 0 } enemies)
            return;

        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target is null)
            return;

        Flash();
        await PowerCmd.Apply<PoisonPower>(choiceContext, target, Amount, Owner, null);
    }
}
