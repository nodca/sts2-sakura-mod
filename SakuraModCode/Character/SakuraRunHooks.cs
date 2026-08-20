using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraRunHooks
{
    private static IDisposable? _combatStartingSubscription;
    private static IDisposable? _creatureDiedSubscription;
    private static IDisposable? _combatVictorySubscription;
    private static IDisposable? _runLoadedSubscription;
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        _combatStartingSubscription = RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(
            OnCombatStarting,
            replayCurrentState: false);
        _creatureDiedSubscription = RitsuLibFramework.SubscribeLifecycle<CreatureDiedEvent>(
            OnCreatureDied,
            replayCurrentState: false);
        _combatVictorySubscription = RitsuLibFramework.SubscribeLifecycle<CombatVictoryEvent>(
            OnCombatVictory,
            replayCurrentState: false);
        _runLoadedSubscription = RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
            OnRunLoaded,
            replayCurrentState: false);
        _registered = true;
    }

    private static void OnCombatStarting(CombatStartingEvent evt)
    {
        foreach (var player in SakuraPlayers(evt.RunState))
            ClowCreate.ReduceCostAtCombatStart(player);
    }

    private static void OnCreatureDied(CreatureDiedEvent evt)
    {
        if (evt.WasRemovalPrevented
            || evt.Creature.CombatState is null
            || evt.Creature.Side != CombatSide.Enemy
            || evt.Creature.IsSecondaryEnemy
            || evt.Creature.CombatId is not { } combatId)
            return;

        var wands = SakuraPlayers(evt.RunState)
            .Select(static player => player.GetRelic<ClassicSealedWandRelic>())
            .Where(static relic => relic is not null)
            .Cast<ClassicSealedWandRelic>()
            .ToArray();
        if (wands.Length == 0)
            return;
        var wasKilledBySeal = false;
        foreach (var wand in wands)
        {
            if (wand.ConsumeSealKill(evt.Creature))
                wasKilledBySeal = true;
        }

        foreach (var wand in wands)
        {
            var amount = wand.CalculateChargeGainForEnemyDeath(
                evt.Creature,
                evt.WasRemovalPrevented,
                wasKilledBySeal);
            wand.ApplyDeathCharge(combatId, amount);
        }
    }

    private static void OnCombatVictory(CombatVictoryEvent evt)
    {
        foreach (var player in SakuraPlayers(evt.RunState))
        {
            if (SakuraCreateLegacy.TryConsumeReward(player, evt.Room.RoomType))
                SakuraCreateRewards.AddExclusiveOrNormalRelicReward(player);
        }
    }

    private static void OnRunLoaded(RunLoadedEvent evt)
    {
        foreach (var player in SakuraPlayers(evt.RunState))
        {
            foreach (var moonBell in player.Relics.OfType<ClassicMoonBellRelic>())
                moonBell.RestoreSavedPresentation();
        }
    }

    private static IEnumerable<Player> SakuraPlayers(IRunState runState) =>
        runState.Players.Where(SakuraStarterCompatibility.IsKinomotoSakura);
}
