using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
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
        using (RunManager.Instance.CardSelectorStack.UseSelector(player, manifestSelector))
        {
            await CombatScenarioContext.PlayCardAsync(choice);
        }

        var manifestedCards = playerCombat.Hand.Cards
            .Where(card => card.IsTemporary() && SakuraTransparentCardCatalog.IsTransparentCard(card) && card != choice)
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
        assertions.True("manifest_hand_entry", playerCombat.Hand.Cards.Contains(manifested));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "manifest_generated",
            $"Manifest generated {manifestedType.Name} into the live hand.");

        // Stabilize keeps its own contract even though no card offers it now.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            choiceContext => manifested.Stabilize(choiceContext)));
        assertions.True("stabilized_same_card_owner", ReferenceEquals(manifested.Owner, player));
        assertions.True("stabilized_same_combat_identity", combat.ContainsCard(manifested));
        assertions.Equal("stabilized_temporary_removed", false, manifested.IsTemporary());
        assertions.True("stabilized_card_stays_in_hand", playerCombat.Hand.Cards.Contains(manifested));

        // True or False: Forgotten cards appearing in hand hit a random enemy,
        // cards recalled from Memory grant Block.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            await PlayerCmd.GainEnergy(10, player);
            foreach (var enemy in combat.HittableEnemies)
                await CreatureCmd.GainBlock(enemy, 100, ValueProp.Unpowered, null, false);
        }));
        var trueOrFalse = await CombatScenarioContext.AddGeneratedCardToHandAsync<TrueOrFalse>(combat, player);
        await CombatScenarioContext.PlayCardAsync(trueOrFalse);
        var power = player.Creature.GetPower<TrueOrFalsePower>();
        assertions.Equal("true_or_false_power_damage", 8, power?.Amount ?? 0);
        assertions.Equal("true_or_false_power_block", 5, power?.DynamicVars[TrueOrFalsePower.BlockVar].IntValue ?? 0);

        int EnemyDurability() => combat.HittableEnemies.Sum(enemy => enemy.CurrentHp + enemy.Block);

        var durabilityBeforeManifest = EnemyDurability();
        var secondChoice = await CombatScenarioContext.AddGeneratedCardToHandAsync<Choice>(combat, player);
        var secondManifestSelector = new TestCardSelector();
        secondManifestSelector.PrepareToSelect([0]);
        secondManifestSelector.PrepareToSelect([0]);
        using (RunManager.Instance.CardSelectorStack.UseSelector(player, secondManifestSelector))
        {
            await CombatScenarioContext.PlayCardAsync(secondChoice);
        }
        assertions.Equal("manifest_into_hand_hits_random_enemy", 8, durabilityBeforeManifest - EnemyDurability());

        var forgottenTarget = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        var durabilityBeforeGrant = EnemyDurability();
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async choiceContext => await SakuraForgotten.GrantTemporary(choiceContext, forgottenTarget)));
        assertions.Equal("forgotten_granted_in_hand_hits_random_enemy", 8, durabilityBeforeGrant - EnemyDurability());

        // Spiral's next-turn copies sit Forgotten on top of the Draw Pile; drawing one counts.
        var durabilityBeforeDraw = EnemyDurability();
        CardModel? drawnSpiral = null;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            drawnSpiral = combat.CreateCard<Spiral>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                drawnSpiral,
                new GeneratedCardOptions
                {
                    Pile = PileType.Draw,
                    Position = CardPilePosition.Top,
                    AddTemporary = true
                },
                choiceContext);
            assertions.Equal("forgotten_in_draw_pile_does_not_hit", durabilityBeforeDraw, EnemyDurability());
            await CardPileCmd.Draw(choiceContext, 1, player, false);
        }));
        assertions.True("forgotten_spiral_drawn", drawnSpiral?.Pile?.Type == PileType.Hand && drawnSpiral.IsTemporary());
        assertions.Equal("drawn_forgotten_card_hits_random_enemy", 8, durabilityBeforeDraw - EnemyDurability());

        var blockBeforeRecall = player.Creature.Block;
        IReadOnlyList<CardModel> recalled = [];
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            await SakuraMemoryPile.MoveTemporaryCardIntoMemory(forgottenTarget);
            recalled = await SakuraMemoryPile.RecallToHand(player, [forgottenTarget], freeThisTurn: false);
        }));
        assertions.Equal("recall_returned_card", 1, recalled.Count);
        assertions.Equal("recall_grants_block_per_card", 5, player.Creature.Block - blockBeforeRecall);
        assertions.Equal("manifest_selector_released", null, RunManager.Instance.CardSelectorStack.GetSelector(player));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "true_or_false_verified",
            "Stabilize removed Temporary; True or False hit a random enemy per Forgotten card in hand and granted Block per recalled card.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                source_card = typeof(Choice).FullName,
                power_card = typeof(TrueOrFalse).FullName,
                selected_manifest_type = manifestedType.FullName
            },
            ["manifested"] = new
            {
                type = manifestedType.FullName,
                id = manifested.Id,
                owner = manifested.Owner?.NetId,
                pile = manifested.Pile?.Type,
                temporary = manifested.IsTemporary()
            },
            ["recalled"] = recalled.Count
        };
    }
}
