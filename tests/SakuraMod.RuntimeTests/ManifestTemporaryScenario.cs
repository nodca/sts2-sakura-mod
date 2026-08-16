using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ManifestTemporaryScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        var choice = await CombatScenarioContext.AddGeneratedCardToHandAsync<Choice>(combat, player);
        var manifestSelector = new TestCardSelector();
        manifestSelector.PrepareToSelect([0]);
        manifestSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(manifestSelector))
        {
            await CombatScenarioContext.PlayCardAsync(choice);
        }

        var manifestedCards = playerCombat.Hand.Cards
            .Where(card => card.IsTemporary() && card.IsManifestAtlasOrigin())
            .ToArray();
        assertions.Equal("manifest_generated_count", 1, manifestedCards.Length);
        if (manifestedCards.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one manifested Temporary card, found {manifestedCards.Length}.");
        }

        var manifested = manifestedCards[0];
        var manifestedType = manifested.GetType();
        assertions.True(
            "manifest_transparent_identity",
            SakuraTransparentCardCatalog.IsTransparentCard(manifested));
        assertions.True("manifest_owner_identity", ReferenceEquals(manifested.Owner, player));
        assertions.True("manifest_combat_identity", combat.ContainsCard(manifested));
        assertions.True("manifest_temporary_before_stabilize", manifested.IsTemporary());
        assertions.True("manifest_origin_before_stabilize", manifested.IsManifestAtlasOrigin());
        assertions.True("manifest_hand_entry", playerCombat.Hand.Cards.Contains(manifested));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "manifest_generated",
            $"Manifest generated {manifestedType.Name} into the live hand.");

        var trueOrFalse = await CombatScenarioContext.AddGeneratedCardToHandAsync<TrueOrFalse>(combat, player);
        trueOrFalse.UpgradeInternal();
        var energyBefore = playerCombat.Energy;
        var stabilizeSelector = new TestCardSelector();
        stabilizeSelector.PrepareToSelect([1]);
        stabilizeSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(stabilizeSelector))
        {
            await CombatScenarioContext.PlayCardAsync(trueOrFalse);
        }

        var captureCandidates = SakuraManifestLoop.CaptureCandidateTypes(player);
        assertions.True("stabilized_same_card_owner", ReferenceEquals(manifested.Owner, player));
        assertions.True("stabilized_same_combat_identity", combat.ContainsCard(manifested));
        assertions.Equal("stabilized_temporary_removed", false, manifested.IsTemporary());
        assertions.True("stabilized_manifest_origin_retained", manifested.IsManifestAtlasOrigin());
        assertions.True("stabilized_card_stays_in_hand", playerCombat.Hand.Cards.Contains(manifested));
        assertions.Equal("stabilize_energy_gain", energyBefore + 2, playerCombat.Energy);
        assertions.True(
            "capture_candidate_handoff",
            captureCandidates.Contains(manifestedType));
        assertions.True(
            "true_or_false_result_pile",
            playerCombat.DiscardPile.Cards.Contains(trueOrFalse));
        assertions.Equal("manifest_selector_released", null, CardSelectCmd.Selector);

        var virtualOnly = await CombatScenarioContext.AddGeneratedCardToHandAsync<TrueOrFalse>(combat, player);
        var energyBeforeVirtual = playerCombat.Energy;
        var temporaryCountBeforeVirtual = playerCombat.Hand.Cards.Count(card => card.IsTemporary());
        assertions.True("virtual_only_is_playable", virtualOnly.CanPlay());
        var virtualSelector = new TestCardSelector();
        virtualSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(virtualSelector))
        {
            await CombatScenarioContext.PlayCardAsync(virtualOnly);
        }

        assertions.Equal(
            "virtual_only_grants_temporary",
            temporaryCountBeforeVirtual + 1,
            playerCombat.Hand.Cards.Count(card => card.IsTemporary()));
        assertions.Equal("virtual_only_does_not_gain_energy", energyBeforeVirtual, playerCombat.Energy);
        assertions.True("virtual_only_exhausts", playerCombat.ExhaustPile.Cards.Contains(virtualOnly));
        assertions.Equal("virtual_selector_released", null, CardSelectCmd.Selector);

        var classicTemporary = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        var isolateClassicTemporaryAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                foreach (var card in playerCombat.Hand.Cards.Where(card => card != classicTemporary).ToArray())
                {
                    await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                        null,
                        card,
                        PileType.Exhaust,
                        CardPilePosition.Bottom);
                }
                await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, classicTemporary);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(isolateClassicTemporaryAction);

        var realOnly = await CombatScenarioContext.AddGeneratedCardToHandAsync<TrueOrFalse>(combat, player);
        var energyBeforeClassicStabilize = playerCombat.Energy;
        assertions.True("classic_temporary_real_only_is_playable", realOnly.CanPlay());
        var classicStabilizeSelector = new TestCardSelector();
        classicStabilizeSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(classicStabilizeSelector))
        {
            await CombatScenarioContext.PlayCardAsync(realOnly);
        }

        assertions.Equal("classic_temporary_stabilized", false, classicTemporary.IsTemporary());
        assertions.Equal(
            "classic_temporary_stabilize_energy_gain",
            energyBeforeClassicStabilize + 2,
            playerCombat.Energy);
        assertions.True("classic_real_only_exhausts", playerCombat.ExhaustPile.Cards.Contains(realOnly));
        assertions.Equal("classic_stabilize_selector_released", null, CardSelectCmd.Selector);

        var noTarget = await CombatScenarioContext.AddGeneratedCardToHandAsync<TrueOrFalse>(combat, player);
        var isolateNoTargetAction = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                foreach (var card in playerCombat.Hand.Cards.Where(card => card != noTarget).ToArray())
                {
                    await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                        null,
                        card,
                        PileType.Exhaust,
                        CardPilePosition.Bottom);
                }
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(isolateNoTargetAction);

        var canPlayWithoutTarget = noTarget.CanPlay(out var unplayableReason, out _);
        assertions.Equal("no_target_is_unplayable", false, canPlayWithoutTarget);
        assertions.True(
            "no_target_uses_card_logic_reason",
            unplayableReason.HasFlag(UnplayableReason.BlockedByCardLogic));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "manifest_stabilized",
            "TrueOrFalse stabilized one target, then required another target before granting Temporary.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                source_card = typeof(Choice).FullName,
                stabilizer_card = typeof(TrueOrFalse).FullName,
                selected_manifest_type = manifestedType.FullName,
                setup_mutations = new[]
                {
                    "Generated Choice -> hand",
                    "TestCardSelector index 0 -> ChoiceManifestChoice",
                    "TestCardSelector index 0 -> first fixed-seed Manifest option",
                    "Generated upgraded TrueOrFalse -> hand",
                    "TestCardSelector index 1 -> TrueOrFalseEnergyChoice",
                    $"TestCardSelector card -> {manifestedType.Name}",
                    "Generated base TrueOrFalse -> hand with no Stabilize candidate",
                    "TestCardSelector index 0 -> first non-Temporary hand card",
                    "Generated Temporary ClowSword as the only non-TrueOrFalse hand card",
                    "TrueOrFalse directly selected Real and Stabilized ClowSword",
                    "Moved all other hand cards to Exhaust for no-target playability check"
                }
            },
            ["manifested"] = new
            {
                type = manifestedType.FullName,
                id = manifested.Id,
                owner = manifested.Owner?.NetId,
                pile = manifested.Pile?.Type,
                temporary = manifested.IsTemporary(),
                manifest_origin = manifested.IsManifestAtlasOrigin(),
                capture_candidates = captureCandidates.Select(type => type.FullName).ToArray()
            },
            ["stabilizer"] = new
            {
                result_pile = trueOrFalse.Pile?.Type,
                energy_before = energyBefore,
                energy_after = playerCombat.Energy
            }
        };
    }
}
