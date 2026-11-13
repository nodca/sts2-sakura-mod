using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Appear Manifests at the start of each turn, and every Manifested card brings
// the Spell of its element; a second copy stacks both halves.
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

        var (firstManifested, firstSpells) = await EndTurnAsync();
        assertions.True("base_manifest_offers_three", selector.OptionCounts.SequenceEqual([3]));
        assertions.Equal("base_manifest_reaches_hand", 1, firstManifested.Length);
        assertions.True(
            "base_spell_matches_manifest_element",
            firstSpells.Select(card => card.GetType())
                .SequenceEqual(SakuraActions.ElementsOf(firstManifested[0]).Select(AppearPower.SpellTypeFor)));

        var upgradedAppear = await CombatScenarioContext.AddGeneratedCardToHandAsync<Appear>(combat, player);
        upgradedAppear.UpgradeInternal();
        assertions.Equal("upgraded_appear_costs_one", 1, upgradedAppear.EnergyCost.GetWithModifiers(CostModifiers.Local));
        await CombatScenarioContext.PlayCardAsync(upgradedAppear);
        assertions.Equal("stacked_appear_power_amount", 2, player.Creature.GetPower<AppearPower>()?.Amount ?? 0);

        selector.OptionCounts.Clear();
        var (stackedManifested, stackedSpells) = await EndTurnAsync();
        assertions.Equal("stacked_manifest_count", 2, stackedManifested.Length);
        assertions.Equal(
            "stacked_spells_per_manifest",
            stackedManifested.Sum(card => SakuraActions.ElementsOf(card).Count) * 2,
            stackedSpells.Length);
        assertions.Equal("selector_released", null, RunManager.Instance.CardSelectorStack.GetSelector(player));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "appear_power_manifest_verified",
            "Appear Manifested at turn start, added the matching element Spell per Manifested card, and stacked.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stacked_spells"] = stackedSpells.Select(card => card.GetType().Name).ToArray()
        };

        async Task<(CardModel[] Manifested, CardModel[] Spells)> EndTurnAsync()
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
