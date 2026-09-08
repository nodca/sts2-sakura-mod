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
    private const int FixtureMagicCharge = 0;
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
        await context.SignalAndWaitAsync("kindnessChecksumBaseline-ready");
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(kindness);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => kindness.Pile?.Type is null or PileType.None
                && owner.Creature.GetPower<KindnessPower>()?.Amount == 1,
            "Kindness to leave combat and apply KindnessPower");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            kindnessChecksumBaseline,
            "client-owned Kindness without Extra Effect",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-native-applied");

        var recordChecksumBaseline = context.ChecksumCount;
        await context.SignalAndWaitAsync("recordChecksumBaseline-ready");
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(record);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => record.Pile?.Type == PileType.Exhaust
                && owner.Creature.GetPower<KindnessPower>() is not null,
            "native Exhaust Record to remain exhausted");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            recordChecksumBaseline,
            "native Exhaust preserved with KindnessPower",
            nameof(PlayCardAction));
        assertions.Equal(
            "native_record_cost_restored_after_play",
            record.EnergyCost.Canonical,
            record.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.True("native_record_keeps_exhaust", record.Keywords.Contains(CardKeyword.Exhaust));
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
        await context.SignalAndWaitAsync("releasedKindnessChecksumBaseline-ready");
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(releasedKindness);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => releasedKindness.Pile?.Type is null or PileType.None
                && owner.Creature.GetPower<KindnessPower>() is not null,
            "second Kindness to leave combat and apply KindnessPower");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            releasedKindnessChecksumBaseline,
            "second client-owned Kindness without Extra Effect",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-released-applied");

        var releaseChecksumBaseline = context.ChecksumCount;
        await context.SignalAndWaitAsync("releaseChecksumBaseline-ready");
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
            () => SakuraReleaseState.IsReleased(blade)
                && !blade.Keywords.Contains(CardKeyword.Exhaust)
                && !blade.Keywords.Contains(CardKeyword.Ethereal)
                && blade.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "SpellRelease to release Blade from hand");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            releaseChecksumBaseline,
            "client-owned SpellRelease on Blade",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("kindness-released-blade-prepared");

        var bladeChecksumBaseline = context.ChecksumCount;
        await context.SignalAndWaitAsync("bladeChecksumBaseline-ready");
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(blade, enemy);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => blade.Pile?.Type == PileType.Discard
                && owner.Creature.GetPower<KindnessPower>() is not null,
            "released Blade to enter discard");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            bladeChecksumBaseline,
            "released Blade preserved by KindnessPower",
            nameof(PlayCardAction));
        assertions.Equal(
            "released_blade_zero_cost_after_play",
            0m,
            blade.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.True("release_bonus_survives_play", SakuraReleaseState.IsReleased(blade));
        assertions.True("released_card_cannot_release_again", !SpellRelease.CanRelease(blade));
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("kindness-released-blade-verified");

        var nativeExhaust = combat.CreateCard<Record>(owner);
        SakuraReleaseState.Apply(nativeExhaust, 0.5f);
        assertions.True("released_native_exhaust_preserved", nativeExhaust.Keywords.Contains(CardKeyword.Exhaust));
        assertions.True("released_native_no_added_ethereal", !nativeExhaust.Keywords.Contains(CardKeyword.Ethereal));

        var otherOwnerCard = combat.CreateCard<ClowSword>(context.Run.Players.Single(player => player != owner));
        SakuraReleaseState.Apply(otherOwnerCard, 0.5f);
        assertions.True("kindness_does_not_protect_other_owner", otherOwnerCard.Keywords.Contains(CardKeyword.Exhaust));

        var copy = blade.CreateClone();
        assertions.True("released_copy_is_not_eligible", !SpellRelease.CanRelease(copy));
        foreach (var (name, variable) in blade.DynamicVars)
            assertions.Equal($"released_copy_preserves_{name}", variable.IntValue, copy.DynamicVars[name].IntValue);

        var manifest = CreateZeroCostCard<SakuraMod.SakuraModCode.Cards.Action>(combat, owner);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(manifest, PileType.Hand, owner, CardPilePosition.Bottom);
        var handBeforeManifest = owner.PlayerCombatState!.Hand.Cards.ToHashSet();
        var manifestChecksumBaseline = context.ChecksumCount;
        var selector = new TestCardSelector();
        selector.PrepareToSelect([0]);
        using var manifestSelector = CardSelectCmd.UseSelector(selector);
        await context.SignalAndWaitAsync("kindness-manifest-ready");
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(manifest);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => manifest.Pile?.Type == PileType.Discard
                && owner.PlayerCombatState.Hand.Cards.Any(card => !handBeforeManifest.Contains(card)),
            "Kindness Manifest to create a persistent card");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(manifestChecksumBaseline, "Manifest with Kindness", nameof(PlayCardAction));
        var manifested = owner.PlayerCombatState.Hand.Cards.Where(card => !handBeforeManifest.Contains(card)).ToList();
        assertions.Equal("kindness_manifest_count", 1, manifested.Count);
        assertions.True("kindness_manifest_without_forgotten", manifested.All(card => !card.IsTemporary()));
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("kindness-manifest-verified");

        foreach (var hasKindness in new[] { true, false })
        {
            if (!hasKindness)
                await PowerCmd.Remove(owner.Creature.GetPower<KindnessPower>()!);

            foreach (var upgraded in new[] { false, true })
            {
                var stage = $"appear-kindness-{hasKindness}-upgraded-{upgraded}";
                await MoveHandToDrawAsync(owner);
                if (owner.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                await PowerCmd.Apply<ClassicMagicChargePower>(fixtureContext, owner.Creature, 20, owner.Creature, null, silent: true);
                var appear = combat.CreateCard<Appear>(owner);
                if (upgraded)
                    appear.UpgradeInternal();
                await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(appear, PileType.Hand, owner, CardPilePosition.Bottom);
                var existingCards = owner.PlayerCombatState.AllCards.ToHashSet();
                selector.PrepareToSelect([0]);
                var baseline = context.ChecksumCount;
                await context.SignalAndWaitAsync($"{stage}-ready");
                if (context.LocalPlayer.NetId == owner.NetId)
                    await context.PlayOwnedCardAsync(appear);
                await MultiplayerScenarioContext.WaitForStateAsync(
                    () => appear.Pile?.Type == PileType.Discard
                        && owner.PlayerCombatState.Hand.Cards.Any(card => !existingCards.Contains(card)),
                    stage);
                await context.WaitForActionsAsync();
                await context.WaitForActionChecksumsAsync(baseline, stage, nameof(PlayCardAction));
                var generated = owner.PlayerCombatState.Hand.Cards.Single(card => !existingCards.Contains(card));
                assertions.Equal($"{stage}-forgotten", !hasKindness, generated.IsTemporary());
                assertions.Equal($"{stage}-upgrade", upgraded ? 1 : 0, generated.CurrentUpgradeLevel);
                assertions.True($"{stage}-native-hand-animation", !SakuraGeneratedCardLifecycle.IsGeneratedTransparentHandVisualCard(generated));
                generated.EnergyCost.EndOfTurnCleanup();
                assertions.Equal($"{stage}-extra-combat-cost", 0m, generated.EnergyCost.GetWithModifiers(CostModifiers.Local));
                await context.SignalAndWaitAsync($"{stage}-verified");
            }

            if (hasKindness)
            {
                var temporary = combat.CreateCard<Gale>(owner);
                await SakuraGeneratedCardLifecycle.AddTemporaryGeneratedCardToHand(temporary, false, fixtureContext);
                assertions.True("kindness_preserves_non_manifest_forgotten", temporary.IsTemporary());
            }
        }

        RuntimeTestHost.WriteCheckpoint(
            request,
            "kindness_exhaust_return_verified",
            "KindnessPower preserved native Exhaust and prevented Release-added keywords without multiplayer divergence.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                owner_net_id = owner.NetId,
                fixture_magic_charge = FixtureMagicCharge,
                fixture_energy = FixtureEnergy,
                setup_mutations = new[]
                {
                    "Client-owned Kindness + Record native Exhaust preservation",
                    "Client-owned Kindness + SpellRelease + released Blade preservation"
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
