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
    private bool[]? _effectsResolvedThisTurn;
    private bool _triggeredThisTurn;

    protected override string IconFileName => "earth_element.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void QueueEffect(bool extraEffect) =>
        _pendingEffects.Enqueue(extraEffect);

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner.Side != CombatSide.Player)
            return;

        var pendingEffects = _pendingEffects.ToArray();
        _pendingEffects.Clear();
        _effectsResolvedThisTurn = pendingEffects;
        _triggeredThisTurn = SiegeRules.ShouldTrigger(Owner.Block);
        if (_triggeredThisTurn)
        {
            Flash();
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
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy
            || Owner.Side != CombatSide.Player
            || _effectsResolvedThisTurn is not { } pendingEffects)
            return;

        _effectsResolvedThisTurn = null;
        var triggeredThisTurn = _triggeredThisTurn;
        _triggeredThisTurn = false;
        if (triggeredThisTurn)
        {
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
        }
        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _pendingEffects.Clear();
        _effectsResolvedThisTurn = null;
        _triggeredThisTurn = false;
        return Task.CompletedTask;
    }
}
