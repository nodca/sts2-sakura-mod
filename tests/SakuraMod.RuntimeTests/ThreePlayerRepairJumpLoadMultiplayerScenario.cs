using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ThreePlayerRepairJumpLoadMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;
    private const int FixtureVulnerable = 2;

    public static Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions) =>
        request.Phase switch
        {
            "write" => ExecuteWriteAsync(request, environment, assertions),
            "read" => ExecuteReadAsync(request, environment, assertions),
            _ => throw new InvalidDataException(
                $"Multiplayer save/load phase must be 'write' or 'read', found '{request.Phase}'.")
        };

    private static async Task<Dictionary<string, object?>> ExecuteWriteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request, shouldSave: true);
        var players = context.OrderedPlayers.ToArray();

        assertions.Equal("fixture_player_count", 3, context.PeerCount);
        foreach (var player in players)
        {
            var repair = context.Run.CreateCard<Repair>(player);
            var jump = context.Run.CreateCard<ClowJump>(player);
            await CardPileCmd.Add(
                repair,
                PileType.Deck,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: true);
            await CardPileCmd.Add(
                jump,
                PileType.Deck,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: true);
            assertions.True(
                $"save_fixture_card_ownership_{player.NetId}",
                ReferenceEquals(repair.Owner, player) && ReferenceEquals(jump.Owner, player));
        }
        await context.SignalAndWaitAsync("save-fixture-ready");

        if (context.IsHost)
        {
            await SaveManager.Instance.SaveRun(preFinishedRoom: null, saveProgress: false);
            var read = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(context.LocalPlayer.NetId);
            assertions.True("host_multiplayer_save_readable", read.Success && read.SaveData is not null);
            assertions.Equal("host_multiplayer_save_player_count", 3, read.SaveData?.Players.Count ?? 0);
        }
        await context.SignalAndWaitAsync("save-written");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "multiplayer_save_write_verified",
            "Host saved a three-player run containing Repair and Jump in every permanent deck.");

        return CaptureDeckSnapshot(request, environment, players);
    }

    private static async Task<Dictionary<string, object?>> ExecuteReadAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.LoadAsync(request);
        var players = context.OrderedPlayers.ToArray();

        assertions.Equal("loaded_player_count", 3, context.PeerCount);
        foreach (var player in players)
        {
            assertions.Equal($"restored_repair_count_{player.NetId}", 1, player.Deck.Cards.OfType<Repair>().Count());
            assertions.Equal($"restored_jump_count_{player.NetId}", 1, player.Deck.Cards.OfType<ClowJump>().Count());
            assertions.True(
                $"restored_deck_card_ownership_{player.NetId}",
                player.Deck.Cards.OfType<Repair>().All(card => ReferenceEquals(card.Owner, player))
                && player.Deck.Cards.OfType<ClowJump>().All(card => ReferenceEquals(card.Owner, player)));
        }
        await context.SignalAndWaitAsync("loaded-decks-verified");

        var combat = await context.EnterWeakCrawlerCombatAsync();
        var cards = new Dictionary<ulong, ActorCards>();
        var fixtureContext = new ThrowingPlayerChoiceContext();
        foreach (var player in players)
        {
            var repair = player.PlayerCombatState!.AllCards.OfType<Repair>().Single();
            var jump = player.PlayerCombatState.AllCards.OfType<ClowJump>().Single();
            await MoveToHandAsync(repair);
            await MoveToHandAsync(jump);
            await PowerCmd.Apply<ClassicMagicChargePower>(
                fixtureContext,
                player.Creature,
                FixtureMagicCharge,
                player.Creature,
                null,
                silent: true);
            await PowerCmd.Apply<VulnerablePower>(
                fixtureContext,
                player.Creature,
                FixtureVulnerable,
                player.Creature,
                null,
                silent: true);
            assertions.True(
                $"restored_combat_card_ownership_{player.NetId}",
                ReferenceEquals(repair.Owner, player) && ReferenceEquals(jump.Owner, player));
            assertions.True(
                $"restored_deck_links_{player.NetId}",
                player.Deck.Cards.Contains(repair.DeckVersion)
                && player.Deck.Cards.Contains(jump.DeckVersion));
            cards.Add(player.NetId, new ActorCards(repair, jump));
        }
        await context.SignalAndWaitAsync("loaded-combat-fixture-ready");

        foreach (var player in players)
        {
            var repair = cards[player.NetId].Repair;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(repair);
            await context.SignalAndWaitAsync($"loaded-repair-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => repair.Pile?.Type == PileType.Exhaust
                    && player.Creature.GetPower<RegenPower>()?.Amount == 3
                    && player.Creature.HasPower<RepairRegenerationPower>(),
                $"loaded Repair regeneration powers for player {player.NetId}");
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"loaded-repair-settled-{player.NetId}");
        }

        foreach (var player in players)
        {
            var jump = cards[player.NetId].Jump;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(jump);
            await context.SignalAndWaitAsync($"loaded-jump-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => jump.Pile?.Type == PileType.Exhaust
                    && !player.Creature.HasPower<VulnerablePower>(),
                $"loaded Jump debuff removal for player {player.NetId}");
            assertions.Equal(
                $"loaded_repair_regen_after_jump_{player.NetId}",
                3,
                player.Creature.GetPower<RegenPower>()?.Amount ?? 0);
            assertions.True(
                $"loaded_repair_protection_after_jump_{player.NetId}",
                player.Creature.HasPower<RepairRegenerationPower>());
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"loaded-jump-settled-{player.NetId}");
        }

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await context.SignalAndWaitAsync("loaded-enemies-stunned");
        foreach (var player in players)
        {
            if (context.LocalPlayer.NetId == player.NetId)
                await context.EndLocalTurnAsync();
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => CombatManager.Instance.IsPlayerReadyToEndTurn(player)
                    || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play,
                $"loaded end-turn request for player {player.NetId}");
            await context.SignalAndWaitAsync($"loaded-turn-ended-{player.NetId}");
        }
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "loaded three-player next turn Play phase");

        foreach (var player in players)
        {
            assertions.Equal(
                $"loaded_repair_regen_retained_after_turn_{player.NetId}",
                3,
                player.Creature.GetPower<RegenPower>()?.Amount ?? 0);
            assertions.True(
                $"loaded_repair_protection_retained_after_turn_{player.NetId}",
                player.Creature.HasPower<RepairRegenerationPower>());
        }

        await context.SignalAndWaitAsync("loaded-comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "loaded_repair_jump_regeneration_verified",
            "All three loaded players retained Repair regeneration through activated Jump and the turn boundary.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                peer_count = context.PeerCount,
                load_path = nameof(MultiplayerScenarioContext.LoadAsync),
                magic_charge = FixtureMagicCharge,
                vulnerable = FixtureVulnerable
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
                    regen = player.Creature.GetPower<RegenPower>()?.Amount ?? 0,
                    protection = player.Creature.GetPower<RepairRegenerationPower>()?.Amount ?? 0,
                    vulnerable = player.Creature.GetPower<VulnerablePower>()?.Amount ?? 0,
                    turn = player.PlayerCombatState?.TurnNumber,
                    phase = player.PlayerCombatState?.Phase.ToString()
                }).ToArray()
            }
        };
    }

    private static Task MoveToHandAsync(CardModel card) =>
        card.Pile?.Type == PileType.Hand
            ? Task.CompletedTask
            : CardPileCmd.Add(
                card,
                PileType.Hand,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: true);

    private static Dictionary<string, object?> CaptureDeckSnapshot(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> players) =>
        new(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                peer_count = players.Count,
                phase = request.Phase
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
                    repair_count = player.Deck.Cards.OfType<Repair>().Count(),
                    jump_count = player.Deck.Cards.OfType<ClowJump>().Count()
                }).ToArray()
            }
        };

    private sealed record ActorCards(Repair Repair, ClowJump Jump);
}
