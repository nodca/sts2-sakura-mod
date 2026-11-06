using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Appear Manifests at the start of each turn; an upgraded copy widens every
// Manifest (including stacked ones) from 3 to 5 choices.
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

        var handBefore = playerCombat.Hand.Cards.ToHashSet();
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        }
        assertions.True("base_manifest_offers_three", selector.OptionCounts.SequenceEqual([3]));
        var manifested = playerCombat.Hand.Cards
            .Where(card => !handBefore.Contains(card) && card.IsTemporary())
            .Where(SakuraTransparentCardCatalog.IsTransparentCard)
            .ToArray();
        assertions.True("base_manifest_reaches_hand", manifested.Length >= 1);

        var upgradedAppear = await CombatScenarioContext.AddGeneratedCardToHandAsync<Appear>(combat, player);
        upgradedAppear.UpgradeInternal();
        await CombatScenarioContext.PlayCardAsync(upgradedAppear);
        assertions.Equal("stacked_appear_power_amount", 2, player.Creature.GetPower<AppearPower>()?.Amount ?? 0);

        selector.OptionCounts.Clear();
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        }
        assertions.True("upgraded_manifests_offer_five", selector.OptionCounts.SequenceEqual([5, 5]));
        assertions.Equal("selector_released", null, CardSelectCmd.Selector);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "appear_power_manifest_verified",
            "Appear Manifested at turn start, stacked, and its upgrade widened every Manifest to 5 choices.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["upgraded_option_counts"] = selector.OptionCounts.ToArray()
        };
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
