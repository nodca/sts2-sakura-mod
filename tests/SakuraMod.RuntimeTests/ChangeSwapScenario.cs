using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Clow Change swaps a hand card with a discard card. The cost trade was
// removed in bd2c87a8; only the pile swap remains.
internal static class ChangeSwapScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var piles = player.PlayerCombatState!;

        await ActAsync(async _ =>
        {
            foreach (var card in piles.Hand.Cards.Concat(piles.DiscardPile.Cards).ToArray())
                await CardPileCmd.RemoveFromCombat(card);
            await PlayerCmd.GainEnergy(5, player);
        });

        var cheap = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        var expensive = combat.CreateCard<Struggle>(player);
        await ActAsync(_ => SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            expensive, PileType.Discard, player, CardPilePosition.Bottom));

        var change = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowChange>(combat, player);
        var selector = new TestCardSelector();
        selector.PrepareToSelect([0]);
        selector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.PlayCardAsync(change);
        }

        assertions.True("swapped_in_card_in_hand", piles.Hand.Cards.Contains(expensive));
        assertions.True("swapped_out_card_in_discard", piles.DiscardPile.Cards.Contains(cheap));
        assertions.Equal("swapped_in_card_keeps_own_cost", 2, expensive.EnergyCost.GetWithModifiers(CostModifiers.All));

        RuntimeTestHost.WriteCheckpoint(request, "change_swap_verified", "Clow Change swapped a hand card with a discard card and left both costs unchanged.");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["swapped_in"] = expensive.Id.Entry,
            ["swapped_out"] = cheap.Id.Entry
        };

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));
    }
}
