using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class SiegePendingPower : SakuraPowerModel
{
    private readonly Queue<bool> _pendingEffects = [];

    protected override string IconFileName => "earth_element.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void QueueEffect(bool extraEffect) =>
        _pendingEffects.Enqueue(extraEffect);

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner.Side != CombatSide.Player)
            return;

        var pendingEffects = _pendingEffects.ToArray();
        _pendingEffects.Clear();
        if (SiegeRules.ShouldTrigger(Owner.Block))
        {
            Flash();
            foreach (var _ in pendingEffects)
            {
                var enemies = Owner.CombatState?.HittableEnemies.ToList() ?? [];
                if (enemies.Count > 0)
                {
                    await PowerCmd.Apply<WeakPower>(
                        choiceContext,
                        enemies,
                        SiegeRules.WeakAmount,
                        Owner,
                        null,
                        false);
                }
            }

            if (pendingEffects.Length > 0)
            {
                await PowerCmd.Apply<SiegeGrowthPower>(
                    choiceContext,
                    Owner,
                    SiegeRules.GrowthPerTrigger * pendingEffects.Length,
                    Owner,
                    null,
                    false);
            }

            foreach (var extraEffect in pendingEffects.Where(static extraEffect => extraEffect))
            {
                var damage = SiegeRules.ExtraDamage(Owner.Block);
                foreach (var enemy in Owner.CombatState?.HittableEnemies.ToList() ?? [])
                {
                    await CreatureCmd.Damage(
                        choiceContext,
                        enemy,
                        damage,
                        SakuraPowerValueProps.Damage,
                        Owner,
                        null);
                }
            }
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _pendingEffects.Clear();
        return Task.CompletedTask;
    }
}

