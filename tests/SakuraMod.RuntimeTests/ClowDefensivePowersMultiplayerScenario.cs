using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowDefensivePowersMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;
    private const int OrdinaryDamage = 5;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var players = context.Run.Players.OrderBy(static player => player.NetId).ToArray();
        var cards = new Dictionary<ulong, ActorCards>();
        var fixtureContext = new ThrowingPlayerChoiceContext();

        foreach (var player in players)
        {
            var silent = combat.CreateCard<ClowSilent>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                silent, PileType.Hand, player, CardPilePosition.Random);
            var shield = combat.CreateCard<ClowShield>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                shield, PileType.Hand, player, CardPilePosition.Random);
            await PowerCmd.Apply<ClassicMagicChargePower>(
                fixtureContext,
                player.Creature,
                FixtureMagicCharge,
                player.Creature,
                null,
                silent: true);
            cards.Add(player.NetId, new ActorCards(silent, shield));
        }

        assertions.Equal("fixture_player_count", context.PeerCount, players.Length);
        assertions.Equal(
            "fixture_player_ids",
            string.Join(',', Enumerable.Range(1, context.PeerCount)),
            string.Join(',', players.Select(static player => player.NetId)));
        foreach (var player in players)
        {
            assertions.Equal(
                $"fixture_magic_charge_{player.NetId}",
                FixtureMagicCharge,
                player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
            assertions.Equal($"fixture_silent_owner_{player.NetId}", player.NetId, cards[player.NetId].Silent.Owner.NetId);
            assertions.Equal($"fixture_shield_owner_{player.NetId}", player.NetId, cards[player.NetId].Shield.Owner.NetId);
        }
        await context.SignalAndWaitAsync("fixture-ready");

        foreach (var player in players)
        {
            var card = cards[player.NetId].Silent;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(card);
            await context.SignalAndWaitAsync($"silent-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => card.Pile?.Type == PileType.Discard
                    && player.Creature.GetPower<BufferPower>()?.Amount == 1,
                $"completed Silent and BufferPower(1) for player {player.NetId}");
            assertions.Equal(
                $"silent_buffer_applied_{player.NetId}",
                1,
                player.Creature.GetPower<BufferPower>()?.Amount ?? 0);
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"silent-settled-{player.NetId}");

            var hpBefore = player.Creature.CurrentHp;
            var enemy = combat.Enemies.First(static enemy => enemy.IsAlive);
            await CreatureCmd.Damage(
                fixtureContext,
                player.Creature,
                OrdinaryDamage,
                ValueProp.Unpowered,
                enemy,
                null);
            await context.SignalAndWaitAsync($"silent-damaged-{player.NetId}");
            assertions.Equal($"silent_prevents_hp_loss_{player.NetId}", hpBefore, player.Creature.CurrentHp);
            assertions.Equal(
                $"silent_consumes_one_buffer_{player.NetId}",
                0,
                player.Creature.GetPower<BufferPower>()?.Amount ?? 0);
        }

        var magicBeforeShield = players.ToDictionary(
            static player => player.NetId,
            static player => player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        var blockBeforeShield = players.ToDictionary(
            static player => player.NetId,
            static player => player.Creature.Block);
        await context.SignalAndWaitAsync("silent-phase-complete");
        foreach (var player in players)
        {
            var card = cards[player.NetId].Shield;
            if (context.LocalPlayer.NetId == player.NetId)
                await context.PlayOwnedCardAsync(card);
            await context.SignalAndWaitAsync($"shield-played-{player.NetId}");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => card.Pile?.Type == PileType.Discard
                    && player.Creature.GetPower<ClassicShieldWardPower>()?.Amount == 3,
                $"completed Shield and ClassicShieldWardPower(3) for player {player.NetId}");
            assertions.True(
                $"shield_extra_charge_active_{player.NetId}",
                magicBeforeShield[player.NetId] >= 10);
            assertions.True(
                $"shield_immediate_block_{player.NetId}",
                player.Creature.Block > blockBeforeShield[player.NetId]);
            assertions.Equal(
                $"shield_ward_applied_{player.NetId}",
                3,
                player.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0);
            await context.WaitForActionsAsync();
            await context.SignalAndWaitAsync($"shield-settled-{player.NetId}");
        }

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await context.SignalAndWaitAsync("enemies-stunned");
        var blockBeforeWard = players.ToDictionary(
            static player => player.NetId,
            static player => player.Creature.Block);

        foreach (var player in players)
        {
            if (context.LocalPlayer.NetId == player.NetId)
                await context.EndLocalTurnAsync();
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => CombatManager.Instance.IsPlayerReadyToEndTurn(player)
                    || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play,
                $"end-turn request for player {player.NetId}");
            await context.SignalAndWaitAsync($"turn-ended-{player.NetId}");
            if (player.NetId != players[^1].NetId)
            {
                assertions.Equal(
                    $"ward_waits_for_side_end_{player.NetId}",
                    blockBeforeWard[player.NetId],
                    player.Creature.Block);
            }
        }
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(player => player.Creature.Block == blockBeforeWard[player.NetId] + 3),
            $"all {context.PeerCount} Shield Ward side-end Block gains");
        foreach (var player in players)
        {
            assertions.Equal(
                $"shield_ward_block_delta_{player.NetId}",
                3,
                player.Creature.Block - blockBeforeWard[player.NetId]);
        }

        await context.SignalAndWaitAsync("comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "defensive_powers_verified",
            $"All {context.PeerCount} peer-owned Silent and Shield cards converged through native multiplayer actions.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                setup_mutations = new[]
                {
                    $"Mirrored ClowSilent and ClowShield creation for {context.PeerCount} players",
                    $"Mirrored ClassicMagicChargePower({FixtureMagicCharge}) for {context.PeerCount} players",
                    "Mirrored enemy stun before side-end observation"
                }
            },
            ["peer"] = new
            {
                role = request.Multiplayer!.Role,
                local_net_id = context.LocalPlayer.NetId,
                request.Multiplayer.HostAddress,
                request.Multiplayer.Port
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
                    character = player.Character.Id.ToString(),
                    hp = player.Creature.CurrentHp,
                    block = player.Creature.Block,
                    turn = player.PlayerCombatState?.TurnNumber,
                    phase = player.PlayerCombatState?.Phase.ToString(),
                    powers = player.Creature.Powers
                        .Where(static power => power is ClassicMagicChargePower or ClassicShieldWardPower or BufferPower)
                        .OrderBy(static power => power.GetType().FullName, StringComparer.Ordinal)
                        .Select(static power => new { type = power.GetType().FullName, power.Amount })
                        .ToArray(),
                    cards = new[]
                    {
                        ProjectCard(cards[player.NetId].Silent),
                        ProjectCard(cards[player.NetId].Shield)
                    }
                }).ToArray(),
                shield_baseline = players.Select(player => new
                {
                    net_id = player.NetId,
                    magic_charge = magicBeforeShield[player.NetId],
                    block = blockBeforeShield[player.NetId]
                }).ToArray(),
                action_state = new
                {
                    current_side = combat.CurrentSide.ToString(),
                    ending_turn_phase_one = CombatManager.Instance.EndingPlayerTurnPhaseOne,
                    ending_turn_phase_two = CombatManager.Instance.EndingPlayerTurnPhaseTwo,
                    current_action = RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.State.ToString()
                }
            }
        };
    }

    private static object ProjectCard(CardModel card) => new
    {
        type = card.GetType().FullName,
        owner_net_id = card.Owner.NetId,
        combat_card_id = NetCombatCard.FromModel(card).CombatCardIndex,
        pile = card.Pile?.Type.ToString()
    };

    private sealed record ActorCards(ClowSilent Silent, ClowShield Shield);
}
