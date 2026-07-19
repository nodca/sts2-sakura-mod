using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraRunHooks
{
    private static IDisposable? _combatStartingSubscription;
    private static IDisposable? _creatureDiedSubscription;
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
        foreach (var player in SakuraPlayers(evt.RunState))
            player.GetRelic<ClassicSealedWandRelic>()?.TryGainChargeForEnemyDeath(
                evt.Creature,
                evt.WasRemovalPrevented);
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
