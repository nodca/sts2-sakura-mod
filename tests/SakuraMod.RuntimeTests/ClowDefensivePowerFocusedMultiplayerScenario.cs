using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowDefensivePowerFocusedMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;
    private const int OrdinaryDamage = 5;

    public static Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions) =>
        request.ScenarioId switch
        {
            SakuraMultiplayerScenarios.ClowSilentHost =>
                ExecuteSilentAsync(request, environment, assertions, ownerNetId: 1),
            SakuraMultiplayerScenarios.ClowSilentClient =>
                ExecuteSilentAsync(request, environment, assertions, ownerNetId: 2),
            SakuraMultiplayerScenarios.ClowShieldHost =>
                ExecuteShieldAsync(request, environment, assertions, ownerNetId: 1),
            SakuraMultiplayerScenarios.ClowShieldClient =>
                ExecuteShieldAsync(request, environment, assertions, ownerNetId: 2),
            SakuraMultiplayerScenarios.ClowShieldWard =>
                ExecuteShieldWardAsync(request, environment, assertions),
            _ => throw new NotSupportedException($"Unsupported focused defensive-power scenario '{request.ScenarioId}'.")
        };

    private static async Task<Dictionary<string, object?>> ExecuteSilentAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions,
        ulong ownerNetId)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var owner = context.Player(ownerNetId);
        var card = combat.CreateCard<ClowSilent>(owner);
        var fixtureContext = new ThrowingPlayerChoiceContext();
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            card, PileType.Hand, owner, CardPilePosition.Random);
        await PowerCmd.Apply<ClassicMagicChargePower>(
            fixtureContext,
            owner.Creature,
            FixtureMagicCharge,
            owner.Creature,
            null,
            silent: true);

        AssertCardFixture(assertions, owner, card, "silent");
        await context.SignalAndWaitAsync("fixture-ready");
        var checksumBaseline = context.ChecksumCount;

        if (context.LocalPlayer.NetId == ownerNetId)
            await context.PlayOwnedCardAsync(card);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => card.Pile?.Type == PileType.Discard
                && owner.Creature.GetPower<BufferPower>()?.Amount == 1,
            $"completed Silent and BufferPower(1) for player {ownerNetId}");
        assertions.Equal("silent_buffer_applied", 1, owner.Creature.GetPower<BufferPower>()?.Amount ?? 0);

        var hpBefore = owner.Creature.CurrentHp;
        var enemy = combat.Enemies.First(static enemy => enemy.IsAlive);
        await CreatureCmd.Damage(
            fixtureContext,
            owner.Creature,
            OrdinaryDamage,
            ValueProp.Unpowered,
            enemy,
            null);
        assertions.Equal("silent_prevents_hp_loss", hpBefore, owner.Creature.CurrentHp);
        assertions.Equal("silent_consumes_one_buffer", 0, owner.Creature.GetPower<BufferPower>()?.Amount ?? 0);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "silent_behavior_observed",
            $"Player {ownerNetId} received Buffer and prevented {OrdinaryDamage} damage before checksum validation.");

        await context.WaitForActionChecksumsAsync(
            checksumBaseline,
            $"player {ownerNetId} Silent",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("comparison-ready");

        return CreateCardResult(environment, context, owner, card, "silent", hpBefore);
    }

    private static async Task<Dictionary<string, object?>> ExecuteShieldAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions,
        ulong ownerNetId)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var owner = context.Player(ownerNetId);
        var card = combat.CreateCard<ClowShield>(owner);
        var fixtureContext = new ThrowingPlayerChoiceContext();
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            card, PileType.Hand, owner, CardPilePosition.Random);
        await PowerCmd.Apply<ClassicMagicChargePower>(
            fixtureContext,
            owner.Creature,
            FixtureMagicCharge,
            owner.Creature,
            null,
            silent: true);

        AssertCardFixture(assertions, owner, card, "shield");
        await context.SignalAndWaitAsync("fixture-ready");
        var blockBefore = owner.Creature.Block;
        var checksumBaseline = context.ChecksumCount;

        if (context.LocalPlayer.NetId == ownerNetId)
            await context.PlayOwnedCardAsync(card);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => card.Pile?.Type == PileType.Discard
                && owner.Creature.GetPower<ClassicShieldWardPower>()?.Amount == 3,
            $"completed Shield and ClassicShieldWardPower(3) for player {ownerNetId}");
        assertions.True("shield_immediate_block", owner.Creature.Block > blockBefore);
        assertions.Equal(
            "shield_ward_applied",
            3,
            owner.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "shield_behavior_observed",
            $"Player {ownerNetId} received immediate Block and ClassicShieldWardPower(3) before checksum validation.");

        await context.WaitForActionChecksumsAsync(
            checksumBaseline,
            $"player {ownerNetId} Shield",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("comparison-ready");

        return CreateCardResult(environment, context, owner, card, "shield", blockBefore);
    }

    private static async Task<Dictionary<string, object?>> ExecuteShieldWardAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var players = context.Run.Players.OrderBy(static player => player.NetId).ToArray();
        var fixtureContext = new ThrowingPlayerChoiceContext();
        foreach (var player in players)
        {
            await PowerCmd.Apply<ClassicShieldWardPower>(
                fixtureContext,
                player.Creature,
                3,
                player.Creature,
                null,
                silent: true);
            assertions.Equal(
                $"fixture_shield_ward_{player.NetId}",
                3,
                player.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0);
        }
        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await context.SignalAndWaitAsync("fixture-ready");

        var blockBefore = players.ToDictionary(static player => player.NetId, static player => player.Creature.Block);
        var checksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == 1)
            await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("host-turn-ended");
        assertions.Equal("ward_waits_for_side_end_host", blockBefore[1], context.Player(1).Creature.Block);

        if (context.LocalPlayer.NetId == 2)
            await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("client-turn-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(player => player.Creature.Block == blockBefore[player.NetId] + 3),
            "both Shield Ward side-end Block gains");
        foreach (var player in players)
        {
            assertions.Equal(
                $"shield_ward_block_delta_{player.NetId}",
                3,
                player.Creature.Block - blockBefore[player.NetId]);
        }
        RuntimeTestHost.WriteCheckpoint(
            request,
            "shield_ward_behavior_observed",
            "Host-owned and Client-owned Shield Ward each granted 3 Block at side end.");

        await context.WaitForActionChecksumsAsync(
            checksumBaseline,
            "Shield Ward side-end trigger",
            "player turn phase one end action");
        await context.SignalAndWaitAsync("comparison-ready");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[]
                {
                    "Mirrored ClassicShieldWardPower(3) for players 1 and 2",
                    "Mirrored enemy stun before side-end observation"
                }
            },
            ["peer"] = CreatePeerSnapshot(context),
            ["comparison"] = new
            {
                versions = CreateVersions(environment),
                players = players.Select(player => new
                {
                    net_id = player.NetId,
                    block_before = blockBefore[player.NetId],
                    block_after = player.Creature.Block,
                    ward = player.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0
                }).ToArray()
            }
        };
    }

    private static void AssertCardFixture(
        RuntimeAssertionCollector assertions,
        Player owner,
        CardModel card,
        string label)
    {
        assertions.Equal("fixture_player_count", 2, owner.RunState.Players.Count);
        assertions.Equal("fixture_owner_net_id", owner.NetId, card.Owner.NetId);
        assertions.Equal(
            "fixture_magic_charge",
            FixtureMagicCharge,
            owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal("fixture_card", label, label);
    }

    private static Dictionary<string, object?> CreateCardResult(
        SakuraRuntimeEnvironment environment,
        MultiplayerScenarioContext context,
        Player owner,
        CardModel card,
        string effect,
        int baseline) =>
        new(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[]
                {
                    $"Mirrored {card.GetType().Name} creation for player {owner.NetId}",
                    $"Mirrored ClassicMagicChargePower({FixtureMagicCharge}) for player {owner.NetId}"
                }
            },
            ["peer"] = CreatePeerSnapshot(context),
            ["comparison"] = new
            {
                versions = CreateVersions(environment),
                effect,
                owner_net_id = owner.NetId,
                baseline,
                hp = owner.Creature.CurrentHp,
                block = owner.Creature.Block,
                buffer = owner.Creature.GetPower<BufferPower>()?.Amount ?? 0,
                ward = owner.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0,
                card_pile = card.Pile?.Type.ToString()
            }
        };

    private static object CreatePeerSnapshot(MultiplayerScenarioContext context) => new
    {
        local_net_id = context.LocalPlayer.NetId,
        checksum_observations = context.ChecksumObservations.Select(static observation => new
        {
            id = observation.Id,
            context = observation.Context,
            checksum = observation.Checksum
        }).ToArray()
    };

    private static object CreateVersions(SakuraRuntimeEnvironment environment) => new
    {
        environment.GameVersion,
        environment.RitsuVersion,
        environment.SakuraVersion
    };
}
