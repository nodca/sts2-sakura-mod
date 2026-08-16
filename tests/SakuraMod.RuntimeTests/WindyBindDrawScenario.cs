using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Cards;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class WindyBindDrawScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWindyCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        assertions.Equal(
            "windy_has_no_opening_bind",
            0,
            player.Creature.GetPower<WindBindPower>()?.Amount ?? 0);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        assertions.Equal(
            "windy_action_applies_bind",
            WindEnemyRules.BindPerPlayer,
            player.Creature.GetPower<WindBindPower>()?.Amount ?? 0);
        assertions.Equal(
            "windy_first_action_has_no_premature_dazed",
            0,
            AllCombatCards(player).OfType<Dazed>().Count());

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        var combatCards = AllCombatCards(player);
        var dazedCards = combatCards.OfType<Dazed>().ToList();

        assertions.Equal("windy_generated_dazed_count", WindEnemyRules.BindPerPlayer, dazedCards.Count);
        assertions.True(
            "windy_generated_dazed_in_combat_scope",
            dazedCards.All(combat.ContainsCard));
        assertions.True(
            "windy_generated_dazed_bound_to_current_combat",
            dazedCards.All(card => ReferenceEquals(card.CombatState, combat)));
        assertions.True(
            "windy_next_player_turn_reached_play",
            playerCombat.Phase == PlayerTurnPhase.Play && playerCombat.TurnNumber > 2);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "windy_bind_draw_verified",
            "Unresolved Wind Bind generated combat-scoped Dazed cards and the next player draw completed.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dazed_count"] = dazedCards.Count,
            ["turn_number"] = playerCombat.TurnNumber,
            ["hand"] = playerCombat.Hand.Cards.Select(static card => card.Id.ToString()).ToArray(),
            ["draw"] = playerCombat.DrawPile.Cards.Select(static card => card.Id.ToString()).ToArray(),
            ["discard"] = playerCombat.DiscardPile.Cards.Select(static card => card.Id.ToString()).ToArray()
        };
    }

    private static List<MegaCrit.Sts2.Core.Models.CardModel> AllCombatCards(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        return playerCombat.Hand.Cards
            .Concat(playerCombat.DrawPile.Cards)
            .Concat(playerCombat.DiscardPile.Cards)
            .Concat(playerCombat.ExhaustPile.Cards)
            .ToList();
    }
}
