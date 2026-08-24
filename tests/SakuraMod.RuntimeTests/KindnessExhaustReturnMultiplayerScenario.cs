using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class KindnessExhaustReturnMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;
    private const int FixtureEnergy = 10;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var owner = context.ClientPlayer;
        var enemy = combat.Enemies.First(static enemy => enemy.IsAlive);
        var fixtureContext = new ThrowingPlayerChoiceContext();

        assertions.Equal("fixture_player_count", 2, context.PeerCount);
        await PlayerCmd.GainEnergy(FixtureEnergy, owner);
        foreach (var enemyToStun in combat.Enemies.Where(static enemyToStun => enemyToStun.IsAlive))
            await CreatureCmd.Stun(enemyToStun);
        await MoveHandToDrawAsync(owner);
        await ApplyMagicChargeAsync(fixtureContext, owner);

        var kindness = CreateZeroCostCard<Kindness>(combat, owner);
        var record = CreateZeroCostCard<Record>(combat, owner);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            kindness, PileType.Hand, owner, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            record, PileType.Hand, owner, CardPilePosition.Bottom);
        await context.SignalAndWaitAsync("kindness-native-fixture-ready");
        await context.WaitForActionsAsync();

        var kindnessChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(kindness);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => kindness.Pile?.Type == PileType.Exhaust
                && owner.Creature.GetPower<KindnessPower>()?.Amount == 1,
            "Kindness to exhaust and leave pending KindnessPower");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            kindnessChecksumBaseline,
            "client-owned Kindness with Extra Effect",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-native-applied");

        var recordChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(record);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => record.Pile?.Type == PileType.Hand
                && owner.Creature.GetPower<KindnessPower>() is null,
            "native Exhaust Record to return to hand");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            recordChecksumBaseline,
            "native Exhaust Record rescued by KindnessPower",
            nameof(PlayCardAction));
        assertions.Equal(
            "native_record_zero_cost_after_extra_kindness",
            0m,
            record.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal("native_kindness_stays_exhausted", PileType.Exhaust, kindness.Pile?.Type);
        await context.SignalAndWaitAsync("kindness-native-record-verified");

        await MoveHandToDrawAsync(owner);
        await ApplyMagicChargeAsync(fixtureContext, owner);
        var releasedKindness = CreateZeroCostCard<Kindness>(combat, owner);
        var blade = CreateZeroCostCard<Blade>(combat, owner);
        var spellRelease = CreateZeroCostCard<SpellRelease>(combat, owner);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            blade, PileType.Hand, owner, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            spellRelease, PileType.Hand, owner, CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            releasedKindness, PileType.Hand, owner, CardPilePosition.Bottom);
        await context.SignalAndWaitAsync("kindness-released-fixture-ready");
        await context.WaitForActionsAsync();

        var releasedKindnessChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(releasedKindness);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => releasedKindness.Pile?.Type == PileType.Exhaust
                && owner.Creature.GetPower<KindnessPower>()?.Amount == 1,
            "second Kindness to exhaust and leave pending KindnessPower");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            releasedKindnessChecksumBaseline,
            "second client-owned Kindness with Extra Effect",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-released-applied");

        var releaseChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
        {
            var releaseSelector = new TestCardSelector();
            releaseSelector.PrepareToSelect([0]);
            using (CardSelectCmd.UseSelector(releaseSelector))
            {
                await context.PlayOwnedCardAsync(spellRelease);
            }
        }
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => blade.Keywords.Contains(CardKeyword.Exhaust)
                && blade.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "SpellRelease to release Blade from hand");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            releaseChecksumBaseline,
            "client-owned SpellRelease on Blade",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-released-blade-prepared");

        var bladeChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(blade, enemy);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => blade.Pile?.Type == PileType.Hand
                && owner.Creature.GetPower<KindnessPower>() is null,
            "released Exhaust Blade to return to hand");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            bladeChecksumBaseline,
            "released Exhaust Blade rescued by KindnessPower",
            nameof(PlayCardAction));
        assertions.Equal(
            "released_blade_zero_cost_after_extra_kindness",
            0m,
            blade.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal("released_kindness_stays_exhausted", PileType.Exhaust, releasedKindness.Pile?.Type);
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("kindness-released-blade-verified");

        RuntimeTestHost.WriteCheckpoint(
            request,
            "kindness_exhaust_return_verified",
            "KindnessPower returned both a native Exhaust card and a released Exhaust card to hand without multiplayer divergence.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                owner_net_id = owner.NetId,
                fixture_magic_charge = FixtureMagicCharge,
                fixture_energy = FixtureEnergy,
                setup_mutations = new[]
                {
                    "Client-owned Kindness + Record native Exhaust rescue",
                    "Client-owned Kindness + SpellRelease + released Blade Exhaust rescue"
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
                owner_net_id = owner.NetId,
                native_record_pile = record.Pile?.Type.ToString(),
                native_record_cost = record.EnergyCost.GetWithModifiers(CostModifiers.Local),
                released_blade_pile = blade.Pile?.Type.ToString(),
                released_blade_cost = blade.EnergyCost.GetWithModifiers(CostModifiers.Local),
                kindness_power_amount = owner.Creature.GetPower<KindnessPower>()?.Amount,
                checksum_count = context.ChecksumCount
            }
        };
    }

    private static CardModel CreateZeroCostCard<TCard>(CombatState combat, MegaCrit.Sts2.Core.Entities.Players.Player owner)
        where TCard : CardModel
    {
        var card = combat.CreateCard<TCard>(owner);
        card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        return card;
    }

    private static async Task ApplyMagicChargeAsync(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Players.Player owner) =>
        await PowerCmd.Apply<ClassicMagicChargePower>(
            choiceContext,
            owner.Creature,
            FixtureMagicCharge,
            owner.Creature,
            null,
            silent: true);

    private static async Task MoveHandToDrawAsync(MegaCrit.Sts2.Core.Entities.Players.Player owner)
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
