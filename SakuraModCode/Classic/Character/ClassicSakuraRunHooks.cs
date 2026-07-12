using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib;

namespace SakuraMod.SakuraModCode.Classic.Character;

internal static class ClassicSakuraRunHooks
{
    private static IDisposable? _combatStartingSubscription;
    private static IDisposable? _creatureDiedSubscription;
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
        _registered = true;
    }

    private static void OnCombatStarting(CombatStartingEvent evt)
    {
        foreach (var player in ClassicSakuraPlayers(evt.RunState))
            ClowCreate.ReduceCostAtCombatStart(player);
    }

    private static void OnCreatureDied(CreatureDiedEvent evt)
    {
        foreach (var player in ClassicSakuraPlayers(evt.RunState))
            player.GetRelic<ClassicSealedWandRelic>()?.TryGainChargeForEnemyDeath(
                evt.Creature,
                evt.WasRemovalPrevented);
    }

    private static IEnumerable<Player> ClassicSakuraPlayers(IRunState runState) =>
        runState.Players.Where(SakuraStarterCompatibility.IsKinomotoSakura);
}
