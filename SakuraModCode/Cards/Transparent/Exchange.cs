using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Exchange() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static LocString PileSelectionPrompt => CardLoc<Exchange>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey, SakuraMemoryPile.PileId];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var cards = await SakuraActions.SelectHandCards(
            this,
            choiceContext,
            card => card != this && SakuraActions.HasExchangeableEnergyCost(card),
            2);
        if (cards.Count == 2)
        {
            var first = cards[0];
            var second = cards[1];
            var firstWasTemporary = first.IsTemporary();
            var secondWasTemporary = second.IsTemporary();
            var costsExchanged = SakuraActions.TryExchangeEnergyCosts(first, second, restOfCombat: false);
            first.ExchangeTemporaryState(second);
            if (costsExchanged)
                CardStateExchangeVfx.Play(first, second, firstWasTemporary, secondWasTemporary);
        }

        if (activation.IsActive)
            await ChooseAndExchangePiles(choiceContext);
    }

    private async Task ChooseAndExchangePiles(PlayerChoiceContext choiceContext)
    {
        var choices = CreatePileChoices();
        var selected = await SakuraActions.SelectUpToFromCards(
            this,
            choiceContext,
            choices,
            count: 2,
            cancelable: false,
            prompt: PileSelectionPrompt,
            minSelect: 2);
        var selectedPiles = selected.OfType<ExchangePileOptionCard>().ToList();
        if (selectedPiles.Count != 2 || selectedPiles[0].Kind == selectedPiles[1].Kind)
            throw new InvalidOperationException("Exchange requires two distinct pile choices.");

        await ExchangePiles(selectedPiles[0].Kind, selectedPiles[1].Kind);
    }

    private IReadOnlyList<ExchangePileOptionCard> CreatePileChoices()
    {
        ExchangePileOptionCard[] choices =
        [
            SakuraActions.CloneWithCurrentUpgrade<ExchangeMemoryChoice>(this),
            SakuraActions.CloneWithCurrentUpgrade<ExchangeExhaustChoice>(this),
            SakuraActions.CloneWithCurrentUpgrade<ExchangeDrawChoice>(this),
            SakuraActions.CloneWithCurrentUpgrade<ExchangeDiscardChoice>(this)
        ];
        foreach (var choice in choices)
            choice.SetDisplayedCount(PileFor(choice.Kind).Cards.Count);

        return choices;
    }

    internal async Task ExchangePiles(ExchangePileKind firstKind, ExchangePileKind secondKind)
    {
        var firstPile = PileFor(firstKind);
        var secondPile = PileFor(secondKind);
        var firstCards = firstPile.Cards.ToList();
        var secondCards = secondPile.Cards.ToList();
        PileExchangeVfx.Play(
            Owner,
            firstPile.Type,
            secondPile.Type,
            firstCards.Count,
            secondCards.Count);

        foreach (var card in firstCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                this,
                card,
                secondPile.Type,
                CardPilePosition.Bottom);
        foreach (var card in secondCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                this,
                card,
                firstPile.Type,
                CardPilePosition.Bottom);
    }

    private CardPile PileFor(ExchangePileKind kind) =>
        kind == ExchangePileKind.Memory
            ? SakuraMemoryPile.Get(Owner)
                ?? throw new InvalidOperationException("Exchange requires an active Memory pile.")
            : CardPile.Get(StandardPileTypeFor(kind), Owner)
                ?? throw new InvalidOperationException($"Exchange requires an active {kind} pile.");

    private static PileType StandardPileTypeFor(ExchangePileKind kind) =>
        kind switch
        {
            ExchangePileKind.Exhaust => PileType.Exhaust,
            ExchangePileKind.Draw => PileType.Draw,
            ExchangePileKind.Discard => PileType.Discard,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Memory uses the registered custom pile.")
        };

    protected override void OnUpgrade() => RemoveKeywordIfPresent(CardKeyword.Exhaust);
}
