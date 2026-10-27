using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Remind moves the real Memory records back: identity, per-card Release state and
// in-combat upgrades survive, and a full hand sends the overflow to the Discard Pile.
internal static class RemindRecallsOriginalsScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState!;
        var memory = SakuraMemoryPile.Get(player)!;

        var released = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var upgraded = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player);
        await ActAsync(async _ =>
        {
            SakuraReleaseState.Apply(released, 0.5f);
            upgraded.UpgradeInternal();
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(null, released, memory.Type, CardPilePosition.Bottom);
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(null, upgraded, memory.Type, CardPilePosition.Bottom);
        });
        assertions.Equal("memory_seeded", 2, SakuraMemoryPile.Count(player));

        var recalledCount = 0;
        await ActAsync(async choiceContext =>
            recalledCount = await Remind.RecallIntoHand(player, choiceContext, 2, upgradeCopies: false));
        assertions.Equal("remind_recalls_two", 2, recalledCount);
        assertions.Equal("memory_emptied", 0, SakuraMemoryPile.Count(player));
        assertions.True(
            "recalled_cards_are_original_records",
            playerCombat.Hand.Cards.Contains(released) && playerCombat.Hand.Cards.Contains(upgraded));
        assertions.True(
            "recalled_originals_stay_in_combat",
            !released.HasBeenRemovedFromState && !upgraded.HasBeenRemovedFromState
            && combat.ContainsCard(released) && combat.ContainsCard(upgraded));
        assertions.True("release_state_kept", SakuraReleaseState.IsReleased(released) && !SpellRelease.CanRelease(released));
        assertions.True("in_combat_upgrade_kept", upgraded.IsUpgraded);
        assertions.True(
            "recalled_cards_are_ordinary",
            !released.IsTemporary() && !upgraded.IsTemporary());
        assertions.True(
            "recalled_cards_free_this_turn",
            released.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
            && upgraded.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0);

        while (playerCombat.Hand.Cards.Count < CardPile.MaxCardsInHand)
            await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        var overflow = playerCombat.Hand.Cards.OfType<ClowSword>().Last();
        await ActAsync(_ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, overflow, memory.Type, CardPilePosition.Bottom));
        await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        assertions.Equal("hand_full_before_overflow_recall", CardPile.MaxCardsInHand, playerCombat.Hand.Cards.Count);
        await ActAsync(async choiceContext =>
            await Remind.RecallIntoHand(player, choiceContext, 1, upgradeCopies: false));
        assertions.True("full_hand_recall_goes_to_discard", playerCombat.DiscardPile.Cards.Contains(overflow));
        assertions.Equal("full_hand_recall_leaves_memory", 0, SakuraMemoryPile.Count(player));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "remind_recalls_originals_verified",
            "Remind moved the original Memory records into the hand, kept Release and upgrades, and overflowed to the Discard Pile.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["recalled"] = recalledCount,
            ["overflow_pile"] = overflow.Pile?.Type.ToString()
        };

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));
    }
}
