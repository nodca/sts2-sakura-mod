using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SakuraMod.SakuraModCode.Telemetry;

/// <summary>Observe native reward history without changing reward or history state.</summary>
internal static class SakuraTelemetryRewardCapture
{
    private sealed record RewardContext(bool DisallowSkipping);
    private static readonly ConditionalWeakTable<Reward, RewardContext> Contexts = new();

    internal sealed record Observation(RunState Run, PlayerMapPointHistoryEntry Entry,
        int Floor, int Act, int HistoryStart, int GainsStart, string Kind,
        SakuraTelemetryChoiceCard? SpecialCard, HashSet<SakuraTelemetryChoiceCard> Candidates);

    internal static void RegisterSet(RewardsSet set) => Guard(() =>
    {
        if (set.Player.RunState is not RunState run || !SakuraTelemetryRunHooks.CanObserveRewards(run)) return;
        foreach (var reward in set.Rewards)
        {
            // Linked/custom rewards have additional choice rules that this observer cannot prove.
            if (reward.ParentRewardSet is not null ||
                (reward.GetType() != typeof(CardReward) && reward.GetType() != typeof(SpecialCardReward))) continue;
            Contexts.Remove(reward);
            Contexts.Add(reward, new RewardContext(set.DisallowSkipping));
        }
    });

    internal static Observation? Begin(Reward reward)
    {
        Observation? result = null;
        Guard(() =>
        {
            if (reward.SuccessfullySelected || reward.ParentRewardSet is not null
                || !Contexts.TryGetValue(reward, out var context) || reward.Player.RunState is not RunState run
                || !SakuraTelemetryRunHooks.CanObserveRewards(run)
                || run.CurrentMapPointHistoryEntry is not { } point) return;
            var entry = point.GetEntry(reward.Player.NetId);
            var kind = "unknown";
            SakuraTelemetryChoiceCard? special = null;
            var candidates = new HashSet<SakuraTelemetryChoiceCard>();
            if (reward is SpecialCardReward)
            {
                special = Reference(reward.ToSerializable().SpecialCard
                    ?? throw new InvalidOperationException("Special reward has no serialized card."));
                kind = context.DisallowSkipping ? "forced" : "voluntary";
            }
            else if (reward is CardReward cards)
            {
                foreach (var card in cards.Cards) candidates.Add(new(card.Id.Entry, card.CurrentUpgradeLevel));
                // A mandatory multi-card choice still expresses preference. A one-card
                // CardReward may have relic alternatives; without proof, leave it unknown.
                if (!context.DisallowSkipping && cards.CanSkip || candidates.Count > 1) kind = "voluntary";
            }
            result = new Observation(run, entry, run.TotalFloor, run.CurrentActIndex + 1,
                entry.CardChoices.Count, entry.CardsGained.Count, kind, special, candidates);
        });
        return result;
    }

    internal static void End(Observation? observation, bool taken)
    {
        if (observation is not { } state) return;
        Guard(() =>
        {
            if (!SakuraTelemetryRunHooks.CanObserveRewards(state.Run)) return;
            var choices = new List<SakuraTelemetryRewardChoice>();
            // If a hook replaced the selected card with a different model, native
            // history no longer proves which original option was selected. Do not
            // count the remaining originals as rejected voluntary alternatives.
            var resolutionKnown = state.Entry.CardChoices.Skip(state.HistoryStart)
                .Where(entry => entry.wasPicked)
                .All(entry => state.Candidates.Any(candidate => candidate.Id == Reference(entry.Card).Id));
            for (var index = state.HistoryStart; index < state.Entry.CardChoices.Count; index++)
            {
                var entry = state.Entry.CardChoices[index];
                var card = Reference(entry.Card);
                var kind = state.SpecialCard == card
                    || (state.SpecialCard is null && resolutionKnown && state.Candidates.Any(candidate => candidate.Id == card.Id))
                        ? state.Kind : "unknown";
                choices.Add(new(state.Floor, state.Act, index, card, entry.wasPicked, kind));
            }
            if (taken && state.SpecialCard is { } special)
            {
                // Bind to the actual gained-history entry, so a duplicate capture
                // cannot turn one acquired card into two statistical selections.
                var gainIndex = state.Entry.CardsGained.FindIndex(state.GainsStart,
                    card => Reference(card) == special);
                if (gainIndex >= 0)
                    choices.Add(new(state.Floor, state.Act, -1, special, true, state.Kind, gainIndex));
            }
            SakuraTelemetryRunHooks.RecordRewardChoices(state.Run, choices);
        });
    }

    internal static async Task<bool> Observe(Task<bool> original, Observation? state)
    {
        var result = await original; // Preserve native exceptions and the gameplay synchronization context.
        End(state, result);
        return result;
    }

    private static SakuraTelemetryChoiceCard Reference(SerializableCard card) =>
        new((card.Id ?? throw new InvalidOperationException("Reward card has no model id.")).Entry, card.CurrentUpgradeLevel);

    private static void Guard(Action action) => SakuraTelemetry.TryExecute(action,
        exception => SakuraTelemetry.LogCaptureFailure("reward evidence", exception));
}

[HarmonyPatch(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.BeginRewardsSet))]
internal static class SakuraTelemetryRewardSetPatch
{
    [HarmonyPrefix]
    private static void Prefix(RewardsSet set) => SakuraTelemetryRewardCapture.RegisterSet(set);
}

[HarmonyPatch]
internal static class SakuraTelemetryRewardSelectPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.DeclaredMethod(typeof(CardReward), "OnSelect"),
        AccessTools.DeclaredMethod(typeof(SpecialCardReward), "OnSelect")
    ];

    [HarmonyPrefix]
    private static void Prefix(Reward __instance, out SakuraTelemetryRewardCapture.Observation? __state) =>
        __state = SakuraTelemetryRewardCapture.Begin(__instance);

    [HarmonyPostfix]
    private static void Postfix(ref Task<bool> __result, SakuraTelemetryRewardCapture.Observation? __state)
    {
        if (__state is not null) __result = SakuraTelemetryRewardCapture.Observe(__result, __state);
    }
}

[HarmonyPatch]
internal static class SakuraTelemetryRewardSkipPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.DeclaredMethod(typeof(CardReward), nameof(CardReward.OnSkipped)),
        AccessTools.DeclaredMethod(typeof(SpecialCardReward), nameof(SpecialCardReward.OnSkipped)),
        AccessTools.DeclaredMethod(typeof(CardReward), nameof(CardReward.Reroll))
    ];

    [HarmonyPrefix]
    private static void Prefix(Reward __instance, out SakuraTelemetryRewardCapture.Observation? __state) =>
        __state = SakuraTelemetryRewardCapture.Begin(__instance);

    [HarmonyPostfix]
    private static void Postfix(SakuraTelemetryRewardCapture.Observation? __state) =>
        SakuraTelemetryRewardCapture.End(__state, false);
}
