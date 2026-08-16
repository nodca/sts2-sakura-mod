using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ThreePlayerMirrorCopyMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var players = context.OrderedPlayers.ToArray();
        var fixtures = new Dictionary<ulong, ActorFixture>();
        var fixtureContext = new ThrowingPlayerChoiceContext();

        assertions.Equal("fixture_player_count", 3, context.PeerCount);
        foreach (var player in players)
        {
            foreach (var card in player.PlayerCombatState!.Hand.Cards.ToArray())
            {
                await CardPileCmd.Add(
                    card,
                    PileType.Draw,
                    CardPilePosition.Bottom,
                    clonedBy: null,
                    skipVisuals: true);
            }
            var mirror = combat.CreateCard<ClowMirror>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                mirror, PileType.Hand, player, CardPilePosition.Random);
            var candidate = combat.CreateCard<ClowSilent>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                candidate, PileType.Hand, player, CardPilePosition.Random);
            while (player.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
            {
                var filler = combat.CreateCard<SpellRelease>(player);
                await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                    filler, PileType.Hand, player, CardPilePosition.Random);
            }
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<ClassicMagicChargePower>(
                fixtureContext,
                player.Creature,
                FixtureMagicCharge,
                player.Creature,
                null,
                silent: true);

            fixtures.Add(
                player.NetId,
                new ActorFixture(
                    mirror,
                    player.Deck.Cards.ToArray(),
                    player.PlayerCombatState.AllCards
                        .Select(static card => NetCombatCard.FromModel(card).CombatCardIndex)
                        .ToHashSet()));
            assertions.Equal($"full_hand_before_mirror_{player.NetId}", CardPile.MaxCardsInHand, player.PlayerCombatState.Hand.Cards.Count);
        }
        await context.SignalAndWaitAsync("fixture-ready");

        foreach (var player in players)
        {
            var fixture = fixtures[player.NetId];
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(fixture.Mirror);
            await context.SignalAndWaitAsync($"mirror-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => fixture.Mirror.Pile?.Type == PileType.Exhaust
                    && NewCards(player, fixture).Count == 2,
                $"two Mirror copies for player {player.NetId}");

            var copies = NewCards(player, fixture);
            assertions.Equal(
                $"mirror_copy_hand_count_{player.NetId}",
                1,
                copies.Count(static card => card.Pile?.Type == PileType.Hand));
            assertions.Equal(
                $"mirror_copy_discard_count_{player.NetId}",
                1,
                copies.Count(static card => card.Pile?.Type == PileType.Discard));
            assertions.Equal(
                $"hand_capped_after_mirror_{player.NetId}",
                CardPile.MaxCardsInHand,
                player.PlayerCombatState!.Hand.Cards.Count);
            assertions.True(
                $"permanent_deck_unchanged_{player.NetId}",
                fixture.DeckCards.SequenceEqual(player.Deck.Cards));
            assertions.True(
                $"copies_remain_combat_scoped_{player.NetId}",
                copies.All(card => player.PlayerCombatState.AllCards.Contains(card)));
            assertions.True(
                $"copies_absent_from_permanent_deck_{player.NetId}",
                copies.All(card => !player.Deck.Cards.Contains(card)));
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"mirror-settled-{player.NetId}");
        }

        await context.SignalAndWaitAsync("comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "mirror_copy_verified",
            "Each activated Mirror created two combat-scoped copies; full-hand overflow went to discard, not the permanent deck.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                peer_count = context.PeerCount,
                hand_size = CardPile.MaxCardsInHand,
                magic_charge = FixtureMagicCharge
            },
            ["comparison"] = new
            {
                versions = new
                {
                    environment.GameVersion,
                    environment.RitsuVersion,
                    environment.SakuraVersion
                },
                players = players.Select(player => new
                {
                    net_id = player.NetId,
                    deck_count = player.Deck.Cards.Count,
                    hand_count = player.PlayerCombatState!.Hand.Cards.Count,
                    copies = NewCards(player, fixtures[player.NetId])
                        .OrderBy(static card => NetCombatCard.FromModel(card).CombatCardIndex)
                        .Select(static card => new
                        {
                            combat_card_id = NetCombatCard.FromModel(card).CombatCardIndex,
                            type = card.GetType().FullName,
                            pile = card.Pile?.Type.ToString()
                        }).ToArray()
                }).ToArray()
            }
        };
    }

    private static List<CardModel> NewCards(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        ActorFixture fixture) =>
        player.PlayerCombatState!.AllCards
            .Where(card => !fixture.InitialCombatCardIds.Contains(NetCombatCard.FromModel(card).CombatCardIndex))
            .ToList();

    private sealed record ActorFixture(
        ClowMirror Mirror,
        IReadOnlyList<CardModel> DeckCards,
        IReadOnlySet<uint> InitialCombatCardIds);
}
