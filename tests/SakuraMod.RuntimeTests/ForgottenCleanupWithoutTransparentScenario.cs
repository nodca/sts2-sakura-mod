using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

/// <summary>
/// Forgotten end-of-turn cleanup must keep working on every turn of a combat whose
/// combat piles hold no Transparent Card, such as the Sakura starter deck.
/// </summary>
internal static class ForgottenCleanupWithoutTransparentScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var turns = new List<Dictionary<string, object?>>();

        assertions.Equal(
            "no_transparent_card_in_combat_piles",
            0,
            playerCombat.AllCards.Count(SakuraTransparentCardCatalog.IsTransparentCard));

        for (var turn = 1; turn <= 2; turn++)
        {
            var forgotten = await GrantForgottenToHandCardAsync(player, turn);
            assertions.True($"turn_{turn}_card_forgotten_before_end", forgotten.IsTemporary());

            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

            var memory = SakuraMemoryPile.Get(player)
                ?? throw new InvalidOperationException("Memory pile is unavailable after Forgotten cleanup.");
            turns.Add(new Dictionary<string, object?>
            {
                ["turn"] = turn,
                ["card"] = forgotten.GetType().Name,
                ["pile_after_end"] = forgotten.Pile?.Type.ToString(),
                ["in_memory"] = ReferenceEquals(forgotten.Pile, memory),
                ["still_forgotten"] = forgotten.IsTemporary(),
                ["memory_count"] = memory.Cards.Count,
                ["transparent_in_combat_piles"] =
                    playerCombat.AllCards.Count(SakuraTransparentCardCatalog.IsTransparentCard)
            });
            assertions.True(
                $"turn_{turn}_forgotten_moved_into_memory",
                ReferenceEquals(forgotten.Pile, memory),
                $"{forgotten.GetType().Name} ended turn {turn} in {forgotten.Pile?.Type.ToString() ?? "no pile"}.");
            assertions.Equal($"turn_{turn}_memory_count", turn, memory.Cards.Count);
        }

        RuntimeTestHost.WriteCheckpoint(
            request,
            "forgotten_cleanup_without_transparent",
            "Forgotten cleanup was observed across two turns without a Transparent Card in combat piles.");
        return new Dictionary<string, object?> { ["turns"] = turns };
    }

    private static async Task<CardModel> GrantForgottenToHandCardAsync(Player player, int turn)
    {
        var card = CardPile.GetCards(player, PileType.Hand)
            .FirstOrDefault(static card =>
                !card.IsTemporary()
                && card is not ISakuraForgottenImmune
                && !SakuraTransparentCardCatalog.IsTransparentCard(card))
            ?? throw new InvalidOperationException($"Turn {turn} hand has no card that can become Forgotten.");

        var granted = false;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async choiceContext => granted = await SakuraForgotten.GrantTemporary(choiceContext, card)));
        if (!granted)
            throw new InvalidOperationException($"Turn {turn} could not make {card.GetType().Name} Forgotten.");

        return card;
    }
}
