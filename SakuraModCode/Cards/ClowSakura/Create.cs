using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowCreate() : ClowCard(CreateBaseCost, CardType.Power, CardRarity.Rare, TargetType.None)
{
    private const int CreateBaseCost = 5;
    private const int CreateRewardCount = 1;
    private static readonly SavedAttachedState<ClowCreate, int> CostReductions =
        new("SakuraMod_ClowCreateCostReductions", () => 0);

    public override int MaxUpgradeLevel => 0;
    public override SakuraElementSet Elements => SakuraElementSet.Earth | SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Relics", CreateRewardCount)];

    public override void AfterCreated() => ApplyCostReduction();

    protected override void AfterDeserialized() => ApplyCostReduction();

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!await TryRemoveDeckVersion())
            return;

        await ApplyPower<ClassicCreatePower>(choiceContext, Owner.Creature, DynamicVars["Relics"].IntValue);
        SakuraCreateRewards.AddNormalRelicReward(Owner);
    }

    public static void ReduceCostAtCombatStart(Player owner)
    {
        foreach (var deckCard in owner.Deck.Cards.OfType<ClowCreate>())
            deckCard.ReduceCostOnce();

        foreach (var combatCard in owner.Piles
                     .Where(static pile => pile.Type.IsCombatPile())
                     .SelectMany(static pile => pile.Cards)
                     .OfType<ClowCreate>())
        {
            var deckSource = combatCard.DeckSource();
            if (deckSource is not null)
                combatCard.ApplyCostReduction(CostReductions[deckSource]);
        }
    }

    private async Task<bool> TryRemoveDeckVersion()
    {
        var deckCard = DeckSource();
        if (deckCard?.Pile?.Type != PileType.Deck)
            return false;

        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        return true;
    }

    private void ReduceCostOnce()
    {
        if (CostReductions[this] >= CreateBaseCost)
            return;

        CostReductions[this]++;
        ApplyCostReduction();
    }

    private void ApplyCostReduction() =>
        ApplyCostReduction(CostReductions[this]);

    private void ApplyCostReduction(int reductions) =>
        EnergyCost.SetCustomBaseCost(Math.Max(0, CreateBaseCost - reductions));

    private ClowCreate? DeckSource()
    {
        HashSet<CardModel> seen = [];
        for (CardModel? current = this; current is not null && seen.Add(current); current = current.CloneOf)
        {
            if (current.DeckVersion is ClowCreate deckVersion)
                return deckVersion;
        }

        return Pile?.Type == PileType.Deck ? this : null;
    }
}

public class SakuraCreate() : SakuraFormCard(0, CardType.Power, TargetType.None)
{
    private const int CreateRewardCount = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth | SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Relics", CreateRewardCount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!await TryRemoveCreateFromDeck())
            return;

        await ApplyPower<ClassicCreatePower>(choiceContext, Owner.Creature, DynamicVars["Relics"].IntValue);
        SakuraCreateRewards.AddNormalRelicReward(Owner);
        SakuraCreateRewards.AddExclusiveOrNormalRelicReward(Owner);
    }

    private async Task<bool> TryRemoveCreateFromDeck()
    {
        var deckCard = Owner.Deck.Cards.OfType<SakuraCreate>().FirstOrDefault();
        if (deckCard is null)
            return false;

        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        return true;
    }
}

