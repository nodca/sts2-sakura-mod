using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class KindnessDreamAppearMultiplayerScenario
{
    private const int FixtureEnergy = 10;
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var host = context.IsHost ? context.LocalPlayer : context.PeerPlayer;
        var client = context.ClientPlayer;
        var fixtureContext = new ThrowingPlayerChoiceContext();

        assertions.Equal("fixture_player_count", 2, context.PeerCount);
        foreach (var player in new[] { host, client })
        {
            await PlayerCmd.GainEnergy(FixtureEnergy, player);
            foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
                await CreatureCmd.Stun(enemy);
            await MoveHandToDrawAsync(player);
            await ApplyMagicChargeAsync(fixtureContext, player);
        }

        var hostKindness = CreateZeroCostCard<Kindness>(combat, host);
        var hostSword = CreateZeroCostCard<ClowSword>(combat, host);
        var hostShield = CreateZeroCostCard<ClowShield>(combat, host);
        var hostDream = CreateZeroCostCard<SakuraDream>(combat, host);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            hostKindness, PileType.Hand, host, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            hostSword, PileType.Hand, host, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            hostShield, PileType.Hand, host, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            hostDream, PileType.Hand, host, CardPilePosition.Bottom);

        var clientAppear = CreateZeroCostCard<Appear>(combat, client);
        clientAppear.UpgradeInternal();
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            clientAppear, PileType.Hand, client, CardPilePosition.Bottom);

        await context.SignalAndWaitAsync("kindness-dream-appear-fixture-ready");
        await context.WaitForActionsAsync();

        var kindnessChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == host.NetId)
            await context.PlayOwnedCardAsync(hostKindness);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => hostKindness.Pile?.Type == PileType.Exhaust
                && host.Creature.GetPower<KindnessPower>()?.Amount == 1,
            "host Kindness to exhaust and leave pending KindnessPower");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            kindnessChecksumBaseline,
            "host Kindness",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-dream-appear-kindness-applied");

        var appearSelector = new TestCardSelector();
        appearSelector.PrepareToSelect([0]);
        appearSelector.PrepareToSelect([0]);
        var appearChecksumBaseline = context.ChecksumCount;
        using (CardSelectCmd.UseSelector(appearSelector))
        {
            if (context.LocalPlayer.NetId == client.NetId)
            {
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                    new PlayCardAction(clientAppear, null));
            }

            await context.SignalAndWaitAsync("kindness-dream-appear-client-enqueued");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => clientAppear.Pile?.Type is PileType.Discard or PileType.Exhaust,
                "client Appear to finish both manifest copies");
        }

        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            appearChecksumBaseline,
            "client Appear",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-dream-appear-appear-settled");

        var dreamChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == host.NetId)
            await context.PlayOwnedCardAsync(hostDream);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => host.Creature.GetPower<ClassicDreamPower>()?.Amount == 1
                && host.PlayerCombatState!.Hand.Cards.OfType<SakuraSword>().Any()
                && host.PlayerCombatState!.Hand.Cards.OfType<SakuraShield>().Any()
                && hostDream.Pile?.Type == PileType.Hand
                && host.Creature.GetPower<KindnessPower>() is null,
            "host SakuraDream to return from Kindness, apply ClassicDreamPower, and convert Clow hand");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            dreamChecksumBaseline,
            "host SakuraDream after client Appear settled",
            nameof(PlayCardAction));
        assertions.Equal("host_kindness_power_after_dream", null, host.Creature.GetPower<KindnessPower>()?.Amount);
        assertions.Equal("host_sakura_dream_returned_to_hand", PileType.Hand, hostDream.Pile?.Type);
        assertions.Equal("client_appear_resolved", PileType.Discard, clientAppear.Pile?.Type);
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("kindness-dream-appear-verified");

        RuntimeTestHost.WriteCheckpoint(
            request,
            "kindness_dream_appear_verified",
            "KindnessPower and SakuraDream stayed checksum-synchronized after client Appear settled.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                host_net_id = host.NetId,
                client_net_id = client.NetId,
                setup_mutations = new[]
                {
                    "Host Kindness + Clow Sword/Shield + SakuraDream",
                    "Upgraded client Appear manifest, then host SakuraDream with active KindnessPower"
                }
            },
            ["peer"] = new
            {
                role = request.Multiplayer!.Role,
                local_net_id = context.LocalPlayer.NetId,
                checksum_observations = context.ChecksumObservations.Select(static observation => new
                {
                    id = observation.Id,
                    context = observation.Context,
                    checksum = observation.Checksum
                }).ToArray()
            },
            ["comparison"] = new
            {
                versions = new { environment.GameVersion, environment.RitsuVersion, environment.SakuraVersion },
                divergence = false,
                host_kindness_power_amount = host.Creature.GetPower<KindnessPower>()?.Amount,
                host_dream_power_amount = host.Creature.GetPower<ClassicDreamPower>()?.Amount,
                host_dream_pile = hostDream.Pile?.Type.ToString(),
                client_appear_pile = clientAppear.Pile?.Type.ToString(),
                checksum_count = context.ChecksumCount
            }
        };
    }

    private static CardModel CreateZeroCostCard<TCard>(CombatState combat, Player owner)
        where TCard : CardModel
    {
        var card = combat.CreateCard<TCard>(owner);
        card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        return card;
    }

    private static async Task ApplyMagicChargeAsync(
        PlayerChoiceContext choiceContext,
        Player owner) =>
        await PowerCmd.Apply<ClassicMagicChargePower>(
            choiceContext,
            owner.Creature,
            FixtureMagicCharge,
            owner.Creature,
            null,
            silent: true);

    private static async Task MoveHandToDrawAsync(Player owner)
    {
        var hand = owner.PlayerCombatState!.Hand.Cards.ToArray();
        foreach (var card in hand)
        {
            await CardPileCmd.Add(
                card,
                PileType.Draw,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: true);
        }
    }
}
