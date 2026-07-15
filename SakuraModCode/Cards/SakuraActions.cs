using MegaCrit.Sts2.Core;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraActions
{
    private static readonly LocString HandPrompt = new("cards", "SAKURAMOD-GENERIC.handPrompt");
    private static readonly LocString CardPrompt = new("cards", "SAKURAMOD-GENERIC.cardPrompt");

    public static int ExtraEffectTriggerCountThisTurn(Player owner) =>
        owner.Creature.GetPower<SakuraExtraEffectCountThisTurnPower>()?.Amount ?? 0;

    internal static async Task RecordExtraEffectTriggeredThisTurn(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null)
            return;

        await PowerCmd.Apply<SakuraExtraEffectCountThisTurnPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
    }

    public static async Task ReduceCostThisTurn(PlayerChoiceContext choiceContext, SakuraModCard source, CardModel card, int amount = 1)
    {
        if (amount <= 0)
            return;

        var power = source.Owner.Creature.GetPower<SakuraCostReductionPower>()
                    ?? await PowerCmd.Apply<SakuraCostReductionPower>(choiceContext, source.Owner.Creature, amount, source.Owner.Creature, source, false);
        power?.AddTarget(card);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        Creature target,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1)
    {
        await AttackCommand(source, target, damage, props, hitCount)
            .WithNoAttackerAnim()
            .Execute(context);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        Creature target,
        CalculatedDamageVar damage,
        int hitCount = 1)
    {
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, damage.Props))
            .WithNoAttackerAnim()
            .Targeting(target)
            .Execute(context);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        IEnumerable<Creature> targets,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
            return;

        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, props))
            .WithNoAttackerAnim()
            .TargetingFiltered(targetList)
            .Execute(context);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        IEnumerable<Creature> targets,
        CalculatedDamageVar damage,
        int hitCount = 1)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
            return;

        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, damage.Props))
            .WithNoAttackerAnim()
            .TargetingFiltered(targetList)
            .Execute(context);
    }

    public static AttackCommand AttackCommand(
        SakuraModCard source,
        Creature target,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1,
        string? vfx = null,
        string? sfx = null,
        string? tmpSfx = null) =>
        DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, props))
            .Targeting(target)
            .WithHitFx(vfx, sfx, tmpSfx);

    internal static ValueProp AttackProps(SakuraModCard source, ValueProp props) =>
        LucidPiercePower.ShouldPierce(source.Owner.Creature, source)
            ? props | ValueProp.Unblockable
            : props;

    public static SakuraElementSet ElementSetOf(CardModel card) =>
        ElementSetFromKeywords(card.Keywords);

    public static SakuraElementSet StaticElementSetOf(CardModel card) =>
        ElementSetFromKeywords(card.CanonicalKeywords);

    public static IReadOnlyList<SakuraElement> ElementsOf(CardModel card) =>
        ElementSetOf(card).AsElements().ToList();

    public static ClassicElement ClassicElementSetOf(CardModel? card)
    {
        if (card is ClassicSakuraCard classicCard)
            return classicCard.Element;

        return card is null
            ? ClassicElement.None
            : ClassicElementSetFrom(ElementSetOf(card));
    }

    public static bool HasClassicElement(CardModel? card, ClassicElement element) =>
        ClassicElementSetOf(card).HasElement(element);

    public static async Task<bool> ApplyMissingClassicElementStates(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!card.IsMutable)
            return false;

        var applied = false;
        foreach (var element in ClassicElementSetOf(card).AsElements())
            applied |= await ApplyClassicElementStateIfMissing(choiceContext, card.Owner, element);
        return applied;
    }

    internal static ClassicElement ClassicElementSetFrom(SakuraElementSet elements)
    {
        var classicElements = ClassicElement.None;
        foreach (var element in elements.AsElements())
            classicElements |= ClassicElementFrom(element);

        return classicElements;
    }

    private static ClassicElement ClassicElementFrom(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => ClassicElement.Windy,
            SakuraElement.Water => ClassicElement.Watery,
            SakuraElement.Fire => ClassicElement.Firey,
            SakuraElement.Earth => ClassicElement.Earthy,
            _ => ClassicElement.None
        };

    private static async Task<bool> ApplyClassicElementStateIfMissing(
        PlayerChoiceContext choiceContext,
        Player owner,
        ClassicElement element)
    {
        switch (element)
        {
            case ClassicElement.Earthy when owner.Creature.GetPower<ClassicEarthyPower>() is null:
                await PowerCmd.Apply<ClassicEarthyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case ClassicElement.Firey when owner.Creature.GetPower<ClassicFireyPower>() is null:
                await PowerCmd.Apply<ClassicFireyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case ClassicElement.Watery when owner.Creature.GetPower<ClassicWateryPower>() is null:
                await PowerCmd.Apply<ClassicWateryPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case ClassicElement.Windy when owner.Creature.GetPower<ClassicWindyPower>() is null:
                await PowerCmd.Apply<ClassicWindyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            default:
                return false;
        }
    }

    public static CardKeyword KeywordFor(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => SakuraKeywords.Wind,
            SakuraElement.Water => SakuraKeywords.Water,
            SakuraElement.Fire => SakuraKeywords.Fire,
            SakuraElement.Earth => SakuraKeywords.Earth,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static SakuraElementSet ElementSetFromKeywords(IEnumerable<CardKeyword> keywords)
    {
        var elements = SakuraElementSet.None;
        foreach (var keyword in keywords)
            elements |= ElementSetFromKeyword(keyword);

        return elements;
    }

    private static SakuraElementSet ElementSetFromKeyword(CardKeyword keyword)
    {
        if (keyword == SakuraKeywords.Wind)
            return SakuraElementSet.Wind;
        if (keyword == SakuraKeywords.Water)
            return SakuraElementSet.Water;
        if (keyword == SakuraKeywords.Fire)
            return SakuraElementSet.Fire;
        if (keyword == SakuraKeywords.Earth)
            return SakuraElementSet.Earth;

        return SakuraElementSet.None;
    }

    public static T CloneWithCurrentUpgrade<T>(CardModel source) where T : CardModel
    {
        var scope = source.CardScope
            ?? throw new InvalidOperationException($"Cannot create {typeof(T).Name} without a card scope.");
        var copy = scope.CreateCard<T>(source.Owner);
        while (copy.CurrentUpgradeLevel < source.CurrentUpgradeLevel && copy.IsUpgradable)
            copy.UpgradeInternal();
        return copy;
    }

    public static async Task<CardModel?> SelectHandCard(
        SakuraModCard source,
        PlayerChoiceContext context,
        Func<CardModel, bool> predicate,
        bool cancelable = true)
    {
        var selected = await CardSelectCmd.FromHand(
            context,
            source.Owner,
            new CardSelectorPrefs(HandPrompt, 1)
            {
                Cancelable = cancelable,
                RequireManualConfirmation = false
            },
            predicate,
            source);

        return selected.FirstOrDefault();
    }

    public static IReadOnlyList<CardModel> StabilizeCandidates(Player owner) =>
        CardPile.GetCards(owner, PileType.Hand, PileType.Discard)
            .Where(CanGenericStabilize)
            .ToList();

    public static IReadOnlyList<CardModel> StabilizeCandidates(SakuraModCard source) =>
        StabilizeCandidates(source.Owner);

    public static async Task<CardModel?> SelectStabilizeCandidate(
        SakuraModCard source,
        PlayerChoiceContext context,
        bool cancelable = true) =>
        await SelectFromCards(source, context, StabilizeCandidates(source), cancelable);

    private static bool CanGenericStabilize(CardModel card) =>
        card.IsTemporary();


    public static async Task<IReadOnlyList<CardModel>> SelectHandCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        Func<CardModel, bool> predicate,
        int count,
        bool cancelable = true,
        bool pretendCardsCanBePlayed = false)
    {
        if (count <= 0)
            return [];

        if (Hand(source).Count(predicate) < count)
            return [];

        var selected = await CardSelectCmd.FromHand(
            context,
            source.Owner,
            new CardSelectorPrefs(HandPrompt, count)
            {
                Cancelable = cancelable,
                RequireManualConfirmation = false,
                PretendCardsCanBePlayed = pretendCardsCanBePlayed
            },
            predicate,
            source);

        return selected.ToList();
    }

    public static IEnumerable<CardModel> Hand(SakuraModCard source) =>
        CardPile.Get(PileType.Hand, source.Owner)!.Cards;

    public static async Task<CardModel?> SelectFromCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        IEnumerable<CardModel> cards,
        bool cancelable = true) =>
        await SelectFromCards(source.Owner, context, cards, cancelable);

    public static async Task<CardModel?> SelectFromCards(
        Player owner,
        PlayerChoiceContext context,
        IEnumerable<CardModel> cards,
        bool cancelable = true)
    {
        var choices = cards.ToList();
        if (choices.Count == 0)
            return null;

        try
        {
            if (choices.All(choice => choice is SakuraOptionCard) && choices.Count <= 3)
                return await CardSelectCmd.FromChooseACardScreen(context, choices, owner, canSkip: cancelable);

            var selected = await CardSelectCmd.FromSimpleGrid(
                context,
                choices,
                owner,
                new CardSelectorPrefs(CardPrompt, 1)
                {
                    Cancelable = cancelable,
                    RequireManualConfirmation = false
                });

            return selected.FirstOrDefault();
        }
        finally
        {
            RemoveDetachedOptionCards(choices);
        }
    }

    private static void RemoveDetachedOptionCards(IEnumerable<CardModel> choices)
    {
        foreach (var choice in choices)
        {
            if (choice is not SakuraOptionCard || choice.Pile is not null)
                continue;

            choice.CardScope?.RemoveCard(choice);
        }
    }

    public static async Task<CardModel?> SelectFromCardPreviews(
        SakuraModCard source,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        bool cancelable = true) =>
        await SelectFromCardPreviews(source.Owner, context, cards, cancelable);

    public static async Task<CardModel?> SelectFromCardPreviews(
        Player owner,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        bool cancelable = true)
    {
        if (cards.Count == 0)
            return null;

        var previews = cards.Select(card => card.CreateClone()).ToList();
        var selected = await SelectFromCards(owner, context, previews, cancelable);
        if (selected is null)
            return null;

        var index = previews.IndexOf(selected);
        return index >= 0 ? cards[index] : null;
    }

    public static async Task<CardModel?> ChooseAndMoveTopDrawPileCard(
        Player owner,
        PlayerChoiceContext context,
        int lookCount)
    {
        if (lookCount <= 0)
            return null;

        var drawPile = CardPile.Get(PileType.Draw, owner);
        if (drawPile is null || drawPile.IsEmpty)
            return null;

        var candidates = drawPile.Cards.Take(lookCount).ToList();
        var selected = await SelectFromCardPreviews(owner, context, candidates, cancelable: false);
        if (selected is null)
            return null;

        drawPile.MoveToTopInternal(selected);
        drawPile.InvokeContentsChanged();
        return selected;
    }

    public static async Task<IReadOnlyList<CardModel>> SelectUpToFromCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        IEnumerable<CardModel> cards,
        int count,
        bool cancelable = true,
        LocString? prompt = null,
        int? minSelect = null)
    {
        var choices = cards.ToList();
        if (prompt is null && minSelect is null)
        {
            List<CardModel> orderedSelection = [];
            while (orderedSelection.Count < count && choices.Count > 0)
            {
                var card = await SelectFromCards(source, context, choices, cancelable);
                if (card is null)
                    break;

                orderedSelection.Add(card);
                choices.Remove(card);
            }

            return orderedSelection;
        }

        var maxSelect = Math.Min(count, choices.Count);
        if (maxSelect <= 0)
            return [];

        var requiredCount = minSelect ?? (cancelable ? 0 : maxSelect);
        var prefs = new CardSelectorPrefs(prompt ?? CardPrompt, Math.Clamp(requiredCount, 0, maxSelect), maxSelect)
        {
            Cancelable = cancelable
        };

        try
        {
            var selected = await CardSelectCmd.FromSimpleGrid(context, choices, source.Owner, prefs);
            return selected.ToList();
        }
        finally
        {
            RemoveDetachedOptionCards(choices);
        }
    }

    public static async Task MoveExistingCardToHand(AbstractModel? source, CardModel card) =>
        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, source, skipVisuals: false);

    public static async Task MoveExistingCardToPileWithoutVisuals(
        AbstractModel? source,
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        var result = await CardPileCmd.Add(card, pileType, position, source, skipVisuals: true);
        if (result.success && result.cardAdded.Pile is { IsCombatPile: true } pile)
            pile.InvokeCardAddFinished();
    }

    public static bool TryExchangeEnergyCosts(CardModel first, CardModel second, bool restOfCombat)
    {
        if (!TryGetExchangeableEnergyCost(first, out var firstCost)
            || !TryGetExchangeableEnergyCost(second, out var secondCost))
            return false;

        if (restOfCombat)
        {
            first.EnergyCost.SetThisCombat(secondCost);
            second.EnergyCost.SetThisCombat(firstCost);
        }
        else
        {
            first.EnergyCost.SetThisTurn(secondCost);
            second.EnergyCost.SetThisTurn(firstCost);
        }

        return true;
    }

    internal static bool HasExchangeableEnergyCost(CardModel card) =>
        card is not ClassicSakuraCard { ShowsEnergyCost: false }
        && !card.EnergyCost.CostsX
        && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0;

    private static bool TryGetExchangeableEnergyCost(CardModel card, out int cost)
    {
        if (!HasExchangeableEnergyCost(card))
        {
            cost = 0;
            return false;
        }

        cost = card.EnergyCost.GetResolved();
        return cost >= 0;
    }

}
