using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib;
using STS2RitsuLib.Networking.ManagedActions;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraRunHooks
{
    private static IDisposable? _combatStartingSubscription;
    private static IDisposable? _creatureDiedSubscription;
    private static IDisposable? _combatVictorySubscription;
    private static IDisposable? _combatEndedSubscription;
    private static IDisposable? _runLoadedSubscription;
    private static readonly HashSet<uint> PublishedDeathsThisCombat = [];
    private static readonly Queue<SealedWandChargeActionPayload> DeferredDeathRewards = [];
    private static bool _registered;
    internal static IRunState? ActiveRunState { get; private set; }

    public static void Register()
    {
        if (_registered)
            return;

        RitsuLibManagedNetActions.Register(SakuraSealedWandChargeAction.Descriptor);
        RitsuLibManagedNetActions.Register(SakuraSealedWandChargeAction.DeferredDescriptor);
        CombatManager.Instance.TurnStarted += OnTurnStarted;
        _combatStartingSubscription = RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(
            OnCombatStarting,
            replayCurrentState: false);
        _creatureDiedSubscription = RitsuLibFramework.SubscribeLifecycle<CreatureDiedEvent>(
            OnCreatureDied,
            replayCurrentState: false);
        _combatVictorySubscription = RitsuLibFramework.SubscribeLifecycle<CombatVictoryEvent>(
            OnCombatVictory,
            replayCurrentState: false);
        _combatEndedSubscription = RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(
            OnCombatEnded,
            replayCurrentState: false);
        _runLoadedSubscription = RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
            OnRunLoaded,
            replayCurrentState: false);
        _registered = true;
    }

    private static void OnCombatStarting(CombatStartingEvent evt)
    {
        PublishedDeathsThisCombat.Clear();
        DeferredDeathRewards.Clear();
        ActiveRunState = evt.RunState;
        foreach (var player in SakuraPlayers(evt.RunState))
            ClowCreate.ReduceCostAtCombatStart(player);
    }

    private static void OnCreatureDied(CreatureDiedEvent evt)
    {
        var netType = RunManager.Instance.NetService.Type;
        if (netType == NetGameType.Client)
            return;

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
        if (!PublishedDeathsThisCombat.Add(combatId))
            return;

        var wasKilledBySeal = wands.Any(wand => wand.ConsumeSealKill(evt.Creature));
        var recipients = wands
            .Select(wand => new SealedWandChargeRecipient(
                wand.Owner.NetId,
                wand.CalculateChargeGainForEnemyDeath(
                    evt.Creature,
                    evt.WasRemovalPrevented,
                    wasKilledBySeal)))
            .ToArray();
        var payload = new SealedWandChargeActionPayload(combatId, recipients);

        if (netType == NetGameType.Singleplayer)
        {
            SakuraSealedWandChargeAction.Apply(evt.RunState, payload);
            return;
        }

        if (netType != NetGameType.Host)
        {
            PublishedDeathsThisCombat.Remove(combatId);
            MainFile.Logger.Error(
                $"Could not synchronize Sealed Wand charge for enemy CombatId={combatId}.");
            return;
        }

        if (RunManager.Instance.ActionExecutor.CurrentlyRunningAction is null)
        {
            DeferredDeathRewards.Enqueue(payload);
            return;
        }

        PublishDeathReward(payload, SakuraSealedWandChargeAction.Descriptor);
    }

    private static void OnTurnStarted(CombatState combatState)
    {
        if (combatState.CurrentSide == CombatSide.Player)
        {
            PublishDeferredDeathRewards(SakuraSealedWandChargeAction.DeferredDescriptor);
        }
    }

    private static void OnCombatEnded(CombatEndedEvent _)
    {
        PublishDeferredDeathRewards(SakuraSealedWandChargeAction.Descriptor);
        PublishedDeathsThisCombat.Clear();
        DeferredDeathRewards.Clear();
        ActiveRunState = null;
    }

    private static void OnCombatVictory(CombatVictoryEvent evt)
    {
        foreach (var player in SakuraPlayers(evt.RunState))
        {
            if (SakuraCreateLegacy.TryConsumeReward(player, evt.Room.RoomType))
                SakuraCreateRewards.AddExclusiveOrNormalRelicReward(player);
        }
    }

    private static void PublishDeferredDeathRewards(
        RitsuLibManagedNetActionDescriptor<SealedWandChargeActionPayload> descriptor)
    {
        while (DeferredDeathRewards.TryDequeue(out var payload))
            PublishDeathReward(payload, descriptor);
    }

    private static void PublishDeathReward(
        SealedWandChargeActionPayload payload,
        RitsuLibManagedNetActionDescriptor<SealedWandChargeActionPayload> descriptor)
    {
        if (RitsuLibManagedNetActions.Request(
                RunManager.Instance,
                descriptor,
                payload))
            return;

        PublishedDeathsThisCombat.Remove(payload.CombatId);
        MainFile.Logger.Error(
            $"Could not synchronize Sealed Wand charge for enemy CombatId={payload.CombatId}.");
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
