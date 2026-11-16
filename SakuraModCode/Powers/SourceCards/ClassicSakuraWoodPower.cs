using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.Powers;

// Sakura Wood: Earth triggers and fully blocked hits both poison all enemies
// for Amount. Fully blocked hits count per hit, like Clow Wood.
public class ClassicSakuraWoodPower : ClassicWoodPower
{
    protected override string IconFileName => "wood_power_sakuracard.png";
    public override PowerStackType StackType => PowerStackType.Single;

    protected override Task OnEarthTriggered(PlayerChoiceContext choiceContext) =>
        PoisonAllEnemies(choiceContext);

    protected override Task OnAttackFullyBlocked(PlayerChoiceContext choiceContext, Creature attacker) =>
        PoisonAllEnemies(choiceContext);

    private async Task PoisonAllEnemies(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0 || Owner.CombatState?.HittableEnemies.ToList() is not { Count: > 0 } enemies)
            return;

        Flash();
        foreach (var enemy in enemies)
            await PowerCmd.Apply<PoisonPower>(choiceContext, enemy, Amount, Owner, null);
    }
}
