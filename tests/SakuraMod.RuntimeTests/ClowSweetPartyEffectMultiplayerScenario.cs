using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowSweetPartyEffectMultiplayerScenario
{
    private const int FixtureDamage = 20;
    private const int FixtureEnergy = 10;
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var players = context.OrderedPlayers.ToArray();
        var owner = context.ClientPlayer;
        var enemy = combat.Enemies.First(static enemy => enemy.IsAlive);
        var fixtureContext = new ThrowingPlayerChoiceContext();
        var normalSweet = combat.CreateCard<ClowSweet>(owner);
        var activatedSweet = combat.CreateCard<ClowSweet>(owner);
        var sakuraSweet = combat.CreateCard<SakuraSweet>(owner);

        assertions.Equal("fixture_player_count", 2, context.PeerCount);
        await PlayerCmd.GainEnergy(FixtureEnergy, owner);
        foreach (var player in players)
            await CreatureCmd.Damage(
                fixtureContext,
                player.Creature,
                FixtureDamage,
                ValueProp.Unpowered,
                enemy,
                null);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            normalSweet, PileType.Hand, owner, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            activatedSweet, PileType.Hand, owner, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            sakuraSweet, PileType.Hand, owner, CardPilePosition.Random);
        await context.SignalAndWaitAsync("fixture-ready");
        await context.WaitForActionsAsync();

        var hpBeforeNormalSweet = CurrentHpByPlayer(players);
        var normalChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(normalSweet);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => normalSweet.Pile?.Type == PileType.Exhaust,
            "normal Clow Sweet to exhaust");
        await context.WaitForActionsAsync();
        foreach (var player in players)
        {
            assertions.Equal(
                $"normal_sweet_heal_{player.NetId}",
                hpBeforeNormalSweet[player.NetId] + normalSweet.DynamicVars.Heal.IntValue,
                player.Creature.CurrentHp);
        }
        await context.WaitForActionChecksumsAsync(
            normalChecksumBaseline,
            "normal Clow Sweet party heal",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("normal-sweet-settled");

        await PowerCmd.Apply<ClassicMagicChargePower>(
            fixtureContext,
            owner.Creature,
            FixtureMagicCharge,
            owner.Creature,
            null,
            silent: true);
        var hpBeforeActivatedSweet = CurrentHpByPlayer(players);
        await context.SignalAndWaitAsync("activated-sweet-ready");
        await context.WaitForActionsAsync();
        var activatedChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(activatedSweet);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => activatedSweet.Pile?.Type == PileType.Exhaust
                && players.All(player => player.Creature.GetPower<RegenPower>()?.Amount == activatedSweet.DynamicVars.Heal.IntValue),
            "activated Clow Sweet party regeneration");
        await context.WaitForActionsAsync();
        foreach (var player in players)
        {
            assertions.Equal(
                $"activated_sweet_regen_{player.NetId}",
                activatedSweet.DynamicVars.Heal.IntValue,
                player.Creature.GetPower<RegenPower>()?.Amount ?? 0);
            assertions.Equal(
                $"activated_sweet_no_immediate_heal_{player.NetId}",
                hpBeforeActivatedSweet[player.NetId],
                player.Creature.CurrentHp);
        }
        await context.WaitForActionChecksumsAsync(
            activatedChecksumBaseline,
            "activated Clow Sweet party regeneration",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("activated-sweet-settled");

        foreach (var player in players)
        {
            if (player.Creature.GetPower<RegenPower>() is { } regeneration)
                await PowerCmd.Remove(regeneration);
            await CreatureCmd.Damage(
                fixtureContext,
                player.Creature,
                FixtureDamage,
                ValueProp.Unpowered,
                enemy,
                null);
        }
        await context.SignalAndWaitAsync("sakura-sweet-ready");
        await context.WaitForActionsAsync();
        var sakuraChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(sakuraSweet);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(player => player.Creature.GetPower<ClassicSweetPower>()?.Amount == sakuraSweet.DynamicVars["Magic"].IntValue),
            "Sakura Sweet party powers");
        await context.WaitForActionsAsync();
        foreach (var player in players)
        {
            assertions.Equal(
                $"sakura_sweet_power_{player.NetId}",
                sakuraSweet.DynamicVars["Magic"].IntValue,
                player.Creature.GetPower<ClassicSweetPower>()?.Amount ?? 0);
        }
        await context.WaitForActionChecksumsAsync(
            sakuraChecksumBaseline,
            "Sakura Sweet party power",
            nameof(PlayCardAction));

        foreach (var enemyToStun in combat.Enemies.Where(static enemyToStun => enemyToStun.IsAlive))
            await CreatureCmd.Stun(enemyToStun);
        var hpBeforeSweetTurn = CurrentHpByPlayer(players);
        await context.SignalAndWaitAsync("turn-start-ready");
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
            "next player turn after Sakura Sweet");
        foreach (var player in players)
        {
            var expectedHeal = ClassicSweetPower.HealAmount(
                player.Creature.MaxHp,
                sakuraSweet.DynamicVars["Magic"].IntValue);
            assertions.Equal(
                $"sakura_sweet_turn_heal_{player.NetId}",
                Math.Min(player.Creature.MaxHp, hpBeforeSweetTurn[player.NetId] + expectedHeal),
                player.Creature.CurrentHp);
        }

        await context.SignalAndWaitAsync("comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "sweet_party_effect_verified",
            "A client-owned Clow Sweet and Sakura Sweet applied their effects to both multiplayer players through native card actions.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                owner_net_id = owner.NetId,
                fixture_damage = FixtureDamage,
                fixture_energy = FixtureEnergy,
                fixture_magic_charge = FixtureMagicCharge
            },
            ["comparison"] = new
            {
                versions = new
                {
                    environment.GameVersion,
                    environment.RitsuVersion,
                    environment.SakuraVersion
                },
                owner_net_id = owner.NetId,
                players = players.Select(player => new
                {
                    net_id = player.NetId,
                    hp = player.Creature.CurrentHp,
                    max_hp = player.Creature.MaxHp,
                    regeneration = player.Creature.GetPower<RegenPower>()?.Amount ?? 0,
                    sweet_percent = player.Creature.GetPower<ClassicSweetPower>()?.Amount ?? 0,
                    turn = player.PlayerCombatState?.TurnNumber,
                    phase = player.PlayerCombatState?.Phase.ToString()
                }).ToArray(),
                checksum_count = context.ChecksumCount
            }
        };
    }

    private static Dictionary<ulong, int> CurrentHpByPlayer(IEnumerable<MegaCrit.Sts2.Core.Entities.Players.Player> players) =>
        players.ToDictionary(static player => player.NetId, static player => player.Creature.CurrentHp);
}
