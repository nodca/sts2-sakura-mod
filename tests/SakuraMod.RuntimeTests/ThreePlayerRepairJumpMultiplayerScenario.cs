using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ThreePlayerRepairJumpMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;
    private const int FixtureVulnerable = 2;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var players = context.OrderedPlayers.ToArray();
        var cards = new Dictionary<ulong, ActorCards>();
        var fixtureContext = new ThrowingPlayerChoiceContext();

        assertions.Equal("fixture_player_count", 3, context.PeerCount);
        foreach (var player in players)
        {
            var repair = combat.CreateCard<Repair>(player);
            var jump = combat.CreateCard<ClowJump>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                repair, PileType.Hand, player, CardPilePosition.Random);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                jump, PileType.Hand, player, CardPilePosition.Random);
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
            cards.Add(player.NetId, new ActorCards(repair, jump));
        }
        await context.SignalAndWaitAsync("fixture-ready");

        foreach (var player in players)
        {
            var repair = cards[player.NetId].Repair;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(repair);
            await context.SignalAndWaitAsync($"repair-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => repair.Pile?.Type == PileType.Exhaust
                    && player.Creature.GetPower<RegenPower>()?.Amount == 3
                    && player.Creature.HasPower<RepairRegenerationPower>(),
                $"Repair regeneration powers for player {player.NetId}");
            assertions.Equal(
                $"repair_regen_type_{player.NetId}",
                PowerType.Buff,
                player.Creature.GetPower<RegenPower>()?.TypeForCurrentAmount);
            assertions.Equal(
                $"repair_protection_type_{player.NetId}",
                PowerType.Buff,
                player.Creature.GetPower<RepairRegenerationPower>()?.TypeForCurrentAmount);
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"repair-settled-{player.NetId}");
        }

        foreach (var player in players)
        {
            var jump = cards[player.NetId].Jump;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(jump);
            await context.SignalAndWaitAsync($"jump-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => jump.Pile?.Type == PileType.Exhaust
                    && !player.Creature.HasPower<VulnerablePower>(),
                $"activated Jump debuff removal for player {player.NetId}");
            assertions.Equal(
                $"repair_regen_after_jump_{player.NetId}",
                3,
                player.Creature.GetPower<RegenPower>()?.Amount ?? 0);
            assertions.True(
                $"repair_protection_after_jump_{player.NetId}",
                player.Creature.HasPower<RepairRegenerationPower>());
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"jump-settled-{player.NetId}");
        }

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await context.SignalAndWaitAsync("enemies-stunned");
        foreach (var player in players)
        {
            if (context.LocalPlayer.NetId == player.NetId)
                await context.EndLocalTurnAsync();
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => CombatManager.Instance.IsPlayerReadyToEndTurn(player)
                    || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play,
                $"end-turn request for player {player.NetId}");
            await context.SignalAndWaitAsync($"turn-ended-{player.NetId}");
        }
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "three-player next turn Play phase");

        foreach (var player in players)
        {
            assertions.Equal(
                $"repair_regen_retained_after_turn_{player.NetId}",
                3,
                player.Creature.GetPower<RegenPower>()?.Amount ?? 0);
            assertions.True(
                $"repair_protection_retained_after_turn_{player.NetId}",
                player.Creature.HasPower<RepairRegenerationPower>());
        }

        await context.SignalAndWaitAsync("comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "repair_jump_regeneration_verified",
            "All three players retained Repair regeneration through activated Jump and the turn boundary.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                peer_count = context.PeerCount,
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
                    regen_type = player.Creature.GetPower<RegenPower>()?.TypeForCurrentAmount.ToString(),
                    protection = player.Creature.GetPower<RepairRegenerationPower>()?.Amount ?? 0,
                    vulnerable = player.Creature.GetPower<VulnerablePower>()?.Amount ?? 0,
                    turn = player.PlayerCombatState?.TurnNumber,
                    phase = player.PlayerCombatState?.Phase.ToString()
                }).ToArray()
            }
        };
    }

    private sealed record ActorCards(Repair Repair, ClowJump Jump);
}
