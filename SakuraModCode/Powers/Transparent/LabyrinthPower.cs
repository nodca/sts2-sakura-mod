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

public class LabyrinthPower : SakuraPowerModel
{
    private readonly HashSet<Creature> _enemies = [];
    private readonly List<CardPlay> _activeCardPlays = [];
    private int _playerTurnEndsUntilRelease = 1;
    private Creature? _pendingReleaseEnemy;

    protected override string IconFileName => "sleep_power.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    internal static bool AllowsCardInteraction(CardModel? card, bool isTrapped, bool isAlive) =>
        !isTrapped || !isAlive || card?.Type != CardType.Attack;

    public async Task Enter(IEnumerable<Creature> enemies)
    {
        _playerTurnEndsUntilRelease = 1;
        _pendingReleaseEnemy = null;
        foreach (var enemy in enemies.Where(enemy => enemy.IsMonster && enemy.IsAlive))
        {
            _enemies.Add(enemy);
            SakuraLabyrinthMove.Apply(enemy);
        }

        await RemoveIfEmpty();
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        _activeCardPlays.Add(play);
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _activeCardPlays.Remove(play);
        return Task.CompletedTask;
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        AllowsCardInteraction(
            _activeCardPlays.LastOrDefault()?.Card,
            _enemies.Contains(creature),
            creature.IsAlive);

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        AllowsCardInteraction(
            cardSource,
            target is not null && _enemies.Contains(target),
            target?.IsAlive == true)
            ? 1m
            : 0m;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Amount <= 0)
            return;

        CleanupDeadEnemies();
        foreach (var enemy in _enemies.ToList())
            SakuraLabyrinthMove.EnsureSuppressed(enemy);

        await RemoveIfEmpty();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0 || player != Owner.Player)
            return;

        CleanupDeadEnemies();
        if (_playerTurnEndsUntilRelease == 0 && !IsTrapped(_pendingReleaseEnemy))
            _pendingReleaseEnemy = PickRandomEnemy();

        foreach (var enemy in _enemies.ToList())
            SakuraLabyrinthMove.EnsureSuppressed(enemy, revealCoveredIntent: enemy == _pendingReleaseEnemy);

        await RemoveIfEmpty();
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        CleanupDeadEnemies();
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            await RemoveIfEmpty();
            return;
        }

        if (_playerTurnEndsUntilRelease > 0)
        {
            _playerTurnEndsUntilRelease--;
            await RemoveIfEmpty();
            return;
        }

        var enemy = IsTrapped(_pendingReleaseEnemy) ? _pendingReleaseEnemy : PickRandomEnemy();
        if (enemy is not null)
        {
            _enemies.Remove(enemy);
            SakuraLabyrinthMove.Restore(enemy);
        }
        _pendingReleaseEnemy = null;

        await RemoveIfEmpty();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && _enemies.Remove(creature))
        {
            if (_pendingReleaseEnemy == creature)
                _pendingReleaseEnemy = null;
            SakuraLabyrinthMove.Clear(creature);
        }

        await RemoveIfEmpty();
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var enemy in _enemies.Where(enemy => enemy.CombatState?.ContainsCreature(enemy) == true))
            SakuraLabyrinthMove.Restore(enemy);
        _enemies.Clear();
        _activeCardPlays.Clear();
        _pendingReleaseEnemy = null;
        return Task.CompletedTask;
    }

    private Creature? PickRandomEnemy()
    {
        var combatState = Owner.CombatState;
        var candidates = _enemies
            .Where(enemy => enemy.IsAlive && combatState?.ContainsCreature(enemy) == true)
            .OrderBy(enemy => enemy.CombatId ?? uint.MaxValue)
            .ThenBy(enemy => enemy.SlotName, StringComparer.Ordinal)
            .ToList();
        return candidates.Count == 0
            ? null
            : Owner.Player?.RunState.Rng.CombatTargets.NextItem(candidates);
    }

    private void CleanupDeadEnemies()
    {
        var combatState = Owner.CombatState;
        foreach (var enemy in _enemies
                     .Where(enemy => !enemy.IsAlive || combatState?.ContainsCreature(enemy) != true)
                     .ToList())
        {
            _enemies.Remove(enemy);
            if (_pendingReleaseEnemy == enemy)
                _pendingReleaseEnemy = null;
            SakuraLabyrinthMove.Clear(enemy);
        }
    }

    private bool IsTrapped(Creature? enemy) =>
        enemy is not null
        && enemy.IsAlive
        && _enemies.Contains(enemy)
        && Owner.CombatState?.ContainsCreature(enemy) == true;

    private async Task RemoveIfEmpty()
    {
        if (_enemies.Count == 0 && Owner.GetPower<LabyrinthPower>() == this)
            await PowerCmd.Remove(this);
    }
}

