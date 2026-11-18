using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Appear adds a random Forgotten Transparent card at the start of each turn
// without a choice screen or Spells; every real Manifest still brings the Spell
// of its element. Each copy adds one card per turn and one Spell per Manifest.
internal static class AppearPowerScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState!;
        var selector = new RecordingCardSelector();

        var appear = await CombatScenarioContext.AddGeneratedCardToHandAsync<Appear>(combat, player);
        await CombatScenarioContext.PlayCardAsync(appear);
        assertions.Equal("appear_power_amount", 1, player.Creature.GetPower<AppearPower>()?.Amount ?? 0);

        var (firstRandom, firstSpells) = await EndTurnAsync();
        assertions.True("turn_start_has_no_choice_screen", selector.OptionCounts.Count == 0);
        assertions.Equal("turn_start_random_card_reaches_hand", 1, firstRandom.Length);
        assertions.Equal("turn_start_random_card_grants_no_spell", 0, firstSpells.Length);

        var handBeforeManifest = playerCombat.Hand.Cards.ToHashSet();
        IReadOnlyList<CardModel> manifested;
        using (RunManager.Instance.CardSelectorStack.UseSelector(player, selector))
        {
            manifested = await SakuraManifestLoop.Manifest(player, new ThrowingPlayerChoiceContext(), 1);
        }
        var manifestSpells = playerCombat.Hand.Cards
            .Where(card => !handBeforeManifest.Contains(card) && card is ElementSpellCard)
            .ToArray();
        assertions.True("manifest_offers_three", selector.OptionCounts.SequenceEqual([3]));
        assertions.Equal("manifest_reaches_hand", 1, manifested.Count);
        assertions.True(
            "manifest_spell_matches_element",
            manifestSpells.Select(card => card.GetType())
                .SequenceEqual(SakuraActions.ElementsOf(manifested[0]).Select(AppearPower.SpellTypeFor)));

        var upgradedAppear = await CombatScenarioContext.AddGeneratedCardToHandAsync<Appear>(combat, player);
        upgradedAppear.UpgradeInternal();
        assertions.Equal("upgraded_appear_costs_one", 1, upgradedAppear.EnergyCost.GetWithModifiers(CostModifiers.Local));
        await CombatScenarioContext.PlayCardAsync(upgradedAppear);
        assertions.Equal("stacked_appear_power_amount", 2, player.Creature.GetPower<AppearPower>()?.Amount ?? 0);

        selector.OptionCounts.Clear();
        var (stackedRandom, stackedTurnSpells) = await EndTurnAsync();
        assertions.True("stacked_turn_start_has_no_choice_screen", selector.OptionCounts.Count == 0);
        assertions.Equal("stacked_turn_start_random_cards", 2, stackedRandom.Length);
        assertions.Equal("stacked_turn_start_grants_no_spell", 0, stackedTurnSpells.Length);

        var handBeforeStackedManifest = playerCombat.Hand.Cards.ToHashSet();
        IReadOnlyList<CardModel> stackedManifested;
        using (RunManager.Instance.CardSelectorStack.UseSelector(player, selector))
        {
            stackedManifested = await SakuraManifestLoop.Manifest(player, new ThrowingPlayerChoiceContext(), 1);
        }
        var stackedSpells = playerCombat.Hand.Cards
            .Where(card => !handBeforeStackedManifest.Contains(card) && card is ElementSpellCard)
            .ToArray();
        assertions.Equal(
            "stacked_spells_per_manifest",
            stackedManifested.Sum(card => SakuraActions.ElementsOf(card).Count) * 2,
            stackedSpells.Length);
        assertions.Equal("selector_released", null, RunManager.Instance.CardSelectorStack.GetSelector(player));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "appear_power_manifest_verified",
            "Appear added random Forgotten Transparent cards at turn start without Spells, and each stack added one Spell per Manifest.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stacked_spells"] = stackedSpells.Select(card => card.GetType().Name).ToArray()
        };

        async Task<(CardModel[] Random, CardModel[] Spells)> EndTurnAsync()
        {
            var handBefore = playerCombat.Hand.Cards.ToHashSet();
            using (RunManager.Instance.CardSelectorStack.UseSelector(player, selector))
            {
                await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            }

            var added = playerCombat.Hand.Cards.Where(card => !handBefore.Contains(card)).ToArray();
            return (
                added.Where(card => card.IsTemporary() && SakuraTransparentCardCatalog.IsTransparentCard(card)).ToArray(),
                added.Where(card => card is ElementSpellCard).ToArray());
        }
    }

    private sealed class RecordingCardSelector : ICardSelector
    {
        public List<int> OptionCounts { get; } = [];

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var list = options.ToList();
            OptionCounts.Add(list.Count);
            return Task.FromResult<IEnumerable<CardModel>>(list.Take(Math.Max(minSelect, 1)).ToList());
        }

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new NotSupportedException("Appear scenario never offers card rewards.");
    }
}
