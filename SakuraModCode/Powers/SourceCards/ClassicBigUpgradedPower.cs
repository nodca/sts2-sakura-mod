using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Powers;

// Upgraded Clow Big: Strength applies Amount extra times to attacks whose
// actual cost is at least 2.
public sealed class ClassicBigUpgradedPower : SakuraPowerModel
{
    internal const int MinimumCost = 2;

    // Big reads the original cost so Release and other discounts keep the bonus.
    // X cards still use the X they capture when the play starts.
    private CardModel? _resolvingCard;
    private int _resolvingCost;

    protected override string IconFileName => "big_upgraded.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player)
        {
            _resolvingCard = cardPlay.Card;
            _resolvingCost = ClassicSizeCostRules.OriginalPlayedCost(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card == _resolvingCard)
            _resolvingCard = null;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Amount <= 0
            || cardSource is not { Type: CardType.Attack }
            || cardSource.Owner != Owner.Player
            || dealer != Owner
            || !props.IsPoweredAttack())
            return 0;

        var cost = cardSource == _resolvingCard
            ? _resolvingCost
            : ClassicSizeCostRules.OriginalPreviewCost(cardSource);
        return cost >= MinimumCost
            ? (Owner.GetPower<StrengthPower>()?.Amount ?? 0) * Amount
            : 0;
    }
}

internal static class ClassicSizeCostRules
{
    // The energy a play actually costs: X cards count the X they captured,
    // auto-played X cards count 0, and other cards their current modified cost.
    public static int PlayedCost(CardPlay play) =>
        play.Card.EnergyCost.CostsX
            ? play.IsAutoPlay ? 0 : (int)play.Resources.EnergySpent
            : play.Card.EnergyCost.GetWithModifiers(CostModifiers.All);

    // Printed cost including upgrades but no temporary or combat modifiers.
    public static int OriginalPlayedCost(CardPlay play) =>
        play.Card.EnergyCost.CostsX
            ? PlayedCost(play)
            : play.Card.EnergyCost.GetWithModifiers(CostModifiers.None);

    // Before playing, X cards would spend the current Energy.
    public static int OriginalPreviewCost(CardModel card) =>
        card.EnergyCost.CostsX
            ? card.EnergyCost.GetAmountToSpend()
            : card.EnergyCost.GetWithModifiers(CostModifiers.None);
}
