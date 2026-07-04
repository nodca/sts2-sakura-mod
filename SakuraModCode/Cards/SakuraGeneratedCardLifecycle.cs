using Godot;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public readonly record struct GeneratedCardOptions
{
    public PileType? Pile { get; init; }
    public CardPilePosition? Position { get; init; }
    public bool RemoveRelease { get; init; }
    public bool RemoveTemporary { get; init; }
    public bool RemoveManifestAtlasOrigin { get; init; }
    public bool AddTemporary { get; init; }
    public bool AddRelease { get; init; }
    public bool AddManifestAtlasOrigin { get; init; }
    public bool FreeThisTurn { get; init; }
}

internal static class SakuraGeneratedCardLifecycle
{
    private static readonly ConditionalWeakTable<CardModel, GeneratedTransparentHandVisualMarker> GeneratedTransparentHandVisualCards = new();

    public static async Task<bool> GrantTemporary(PlayerChoiceContext context, CardModel card)
    {
        if (card.IsTemporary())
            return false;

        card.MakeTemporary();
        await TriggerTemporaryGranted(context, card);
        return true;
    }

    public static async Task<CardModel?> AddTemporaryCopyToHand(
        CardModel card,
        bool release,
        bool freeThisTurn,
        bool preserveRelease,
        PlayerChoiceContext context) =>
        await AddGeneratedCopyToHand(
            card,
            TemporaryCopyOptions(release, freeThisTurn, preserveRelease),
            context);

    public static async Task<CardModel?> AddRememberedCopyToHand(CardModel card, bool freeThisTurn) =>
        await AddGeneratedCopyToHand(
            card,
            RememberedCopyOptions(freeThisTurn, addTemporary: false));

    public static async Task<CardModel?> AddTemporaryRememberedCopyToHand(
        CardModel card,
        bool freeThisTurn,
        PlayerChoiceContext context) =>
        await AddGeneratedCopyToHand(
            card,
            RememberedCopyOptions(freeThisTurn, addTemporary: true),
            context);

    public static Task<CardModel> AddGeneratedCardToHand(
        CardModel card,
        PlayerChoiceContext? context = null,
        CardPilePosition position = CardPilePosition.Random) =>
        AddGeneratedCardToCombat(card, GeneratedCardToHandOptions(position), context);

    public static Task<CardModel> AddTemporaryReleasedCardToCombat(
        CardModel card,
        PlayerChoiceContext context,
        PileType pile = PileType.Hand,
        CardPilePosition position = CardPilePosition.Random) =>
        AddGeneratedCardToCombat(card, TemporaryReleasedCardOptions(pile, position), context);

    public static Task<CardModel> AddRestoredReleasedCardToHand(
        CardModel card,
        PlayerChoiceContext context,
        bool freeThisTurn) =>
        AddGeneratedCardToCombat(card, RestoredReleasedCardToHandOptions(freeThisTurn), context);

    public static async Task<CardModel> AddGeneratedCardToCombat(
        CardModel card,
        PileType pile,
        Player? creator,
        CardPilePosition position = CardPilePosition.Random)
    {
        await AddGeneratedCardToCombatWithResult(card, pile, creator, position);
        return card;
    }

    public static async Task<CardPileAddResult> AddGeneratedCardToCombatWithResult(
        CardModel card,
        PileType pile,
        Player? creator,
        CardPilePosition position = CardPilePosition.Random)
    {
        var result = await CardPileCmd.AddGeneratedCardToCombat(card, pile, creator, position);
        NotifyGeneratedPileAddFinished(card, pile);
        return result;
    }

    public static async Task<IReadOnlyList<CardModel>> AddGeneratedCardsToCombat(
        IEnumerable<CardModel> cards,
        PileType pile,
        Player? creator,
        CardPilePosition position = CardPilePosition.Random)
    {
        var list = cards.ToList();
        await AddGeneratedCardsToCombatWithResults(list, pile, creator, position);
        return list;
    }

    public static async Task<IReadOnlyList<CardPileAddResult>> AddGeneratedCardsToCombatWithResults(
        IEnumerable<CardModel> cards,
        PileType pile,
        Player? creator,
        CardPilePosition position = CardPilePosition.Random)
    {
        var list = cards.ToList();
        var results = await CardPileCmd.AddGeneratedCardsToCombat(list, pile, creator, position);
        foreach (var card in list)
            NotifyGeneratedPileAddFinished(card, pile);
        return results;
    }

    public static async Task<CardModel?> AddGeneratedCopyToHand(
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null) =>
        await AddGeneratedCopy(
            card,
            options with
            {
                Pile = PileType.Hand
            },
            context);

    public static async Task<CardModel?> AddGeneratedCopy(
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null)
    {
        var copy = card.CreateClone();
        copy.RemoveManifestAtlasOrigin();
        // Copy-generated Clear Cards keep the native generated-card hand animation intact.
        await AddGeneratedCardToCombat(
            copy,
            options,
            context,
            refreshGeneratedTransparentHandVisual: false);
        return copy;
    }

    public static async Task<CardModel> AddGeneratedCardToCombat(
        CardModel card,
        GeneratedCardOptions options = default,
        PlayerChoiceContext? context = null,
        bool refreshGeneratedTransparentHandVisual = true)
    {
        await AddGeneratedCardToCombatWithResult(card, options, context, refreshGeneratedTransparentHandVisual);
        return card;
    }

    public static async Task<CardPileAddResult> AddGeneratedCardToCombatWithResult(
        CardModel card,
        GeneratedCardOptions options = default,
        PlayerChoiceContext? context = null,
        bool refreshGeneratedTransparentHandVisual = true)
    {
        if (options.RemoveRelease)
            card.RemoveRelease();
        if (options.RemoveTemporary)
            card.RemoveTemporaryForExchange();
        if (options.RemoveManifestAtlasOrigin)
            card.RemoveManifestAtlasOrigin();

        var temporaryGranted = false;
        if (options.AddTemporary)
        {
            var hadTemporary = card.IsTemporary();
            card.MakeTemporary();
            temporaryGranted = !hadTemporary;
        }

        if (options.FreeThisTurn)
            card.SetToFreeThisTurn();
        if (options.AddManifestAtlasOrigin)
            card.MarkManifestAtlasOrigin();

        var destinationPile = options.Pile ?? PileType.Hand;
        TrackGeneratedTransparentHandVisualCard(card, destinationPile, refreshGeneratedTransparentHandVisual);
        var result = await AddGeneratedCardToCombatWithResult(
            card,
            destinationPile,
            card.Owner,
            options.Position ?? CardPilePosition.Random);

        if (temporaryGranted && context is not null)
            await TriggerTemporaryGranted(context, card);

        if (options.AddRelease)
            await SakuraActions.ReleaseAndRecord(context ?? new ThrowingPlayerChoiceContext(), card);

        if (refreshGeneratedTransparentHandVisual)
            RefreshGeneratedTransparentHandVisual(card);
        return result;
    }

    private static void NotifyGeneratedPileAddFinished(CardModel card, PileType requestedPile)
    {
        if (requestedPile == PileType.Hand
            || card.Pile?.Type != requestedPile
            || card.Pile is not { IsCombatPile: true } pile)
            return;

        pile.InvokeCardAddFinished();
    }

    private static void RefreshGeneratedTransparentHandVisual(CardModel card)
    {
        if (!SakuraCardCatalog.IsTransparentCard(card) || card.Pile?.Type != PileType.Hand)
            return;

        var node = NCard.FindOnTable(card, PileType.Hand);
        node?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        RefreshGeneratedTransparentHandLayout(card, node);
    }

    private static void RefreshGeneratedTransparentHandLayout(CardModel card, NCard? node)
    {
        if (node is null || NCombatRoom.Instance?.Ui?.Hand is not { } hand)
            return;

        var holder = hand.GetCardHolder(card) as NHandCardHolder;
        if (holder is null || !ReferenceEquals(holder.CardNode, node))
            return;

        hand.ForceRefreshCardIndices();
        SettleGeneratedTransparentHandHolder(hand, holder, node);
    }

    internal static bool IsGeneratedTransparentHandVisualCard(CardModel? card) =>
        card is not null
        && SakuraCardCatalog.IsTransparentCard(card)
        && GeneratedTransparentHandVisualCards.TryGetValue(card, out _);

    internal static void BeforeGeneratedTransparentHandHolderSetCard(NCard card)
    {
        if (!IsGeneratedTransparentHandVisualCard(card.Model) || card.Scale == Vector2.One)
            return;

        card.Scale = Vector2.One;
    }

    private static void SettleGeneratedTransparentHandHolder(NPlayerHand hand, NHandCardHolder holder, NCard card)
    {
        var activeHolders = hand.ActiveHolders;
        var active = false;
        for (var i = 0; i < activeHolders.Count; i++)
        {
            if (!ReferenceEquals(activeHolders[i], holder))
                continue;

            active = true;
            break;
        }

        if (!active)
            return;

        holder.Position = holder.TargetPosition;
        holder.SetAngleInstantly(holder.TargetAngle);
        holder.SetScaleInstantly(HandPosHelper.GetScale(activeHolders.Count));

        if (card.Position != Vector2.Zero)
            card.Position = Vector2.Zero;
        if (card.Scale != Vector2.One)
            card.Scale = Vector2.One;
    }

    private static void TrackGeneratedTransparentHandVisualCard(
        CardModel card,
        PileType destinationPile,
        bool refreshGeneratedTransparentHandVisual)
    {
        if (!refreshGeneratedTransparentHandVisual
            || destinationPile != PileType.Hand
            || !SakuraCardCatalog.IsTransparentCard(card))
            return;

        GeneratedTransparentHandVisualCards.GetOrCreateValue(card);
    }

    private static string FormatCard(CardModel card) =>
        $"{card.GetType().Name}/{card.Id.Entry}";

    private static string FormatPile(PileType? pile) =>
        pile?.ToString() ?? "null";

    private sealed class GeneratedTransparentHandVisualMarker;

    public static CardModel CreateManifestChoice(Player owner, CardModel source)
    {
        var copy = source.IsCanonical
            ? CreateCombatCardFromTemplate(owner, source)
            : CloneToUpgradeLevel(source, source.CurrentUpgradeLevel);
        UpgradeToLevel(copy, source.CurrentUpgradeLevel);
        return copy;
    }

    public static Task<CardModel> AddManifestChoiceToCombat(
        CardModel card,
        PlayerChoiceContext context,
        bool addTemporary,
        bool captureEligible) =>
        AddGeneratedCardToCombat(
            card,
            ManifestChoiceOptions(addTemporary, captureEligible),
            context,
            refreshGeneratedTransparentHandVisual: false);

    public static Task<CardModel> AddManifestAtlasTemporaryCardToCombat(
        CardModel card,
        PlayerChoiceContext context) =>
        AddGeneratedCardToCombat(
            card,
            ManifestAtlasTemporaryCardOptions(),
            context,
            refreshGeneratedTransparentHandVisual: false);

    public static Task<CardModel> AddDiscoveredChoiceToCombat(
        CardModel card,
        PlayerChoiceContext context,
        bool freeThisTurn) =>
        AddGeneratedCardToCombat(card, DiscoveredChoiceOptions(freeThisTurn), context);

    public static CardModel CreateCombatCardFromTemplate(Player owner, CardModel source)
    {
        var scope = owner.Creature.CombatState
            ?? throw new InvalidOperationException($"Cannot create manifest choice {source.Id.Entry} outside combat.");
        return scope.CreateCard(source, owner);
    }

    public static CardModel CreateGeneratedChoice(SakuraModCard source, CardModel card, bool upgraded)
    {
        var targetUpgradeLevel = upgraded
            ? Math.Max(1, card.CurrentUpgradeLevel)
            : card.CurrentUpgradeLevel;
        var choice = card.IsCanonical
            ? CreateCardFromTemplate(source, card)
            : CloneToUpgradeLevel(card, targetUpgradeLevel);
        UpgradeToLevel(choice, targetUpgradeLevel);
        return choice;
    }

    public static CardModel CloneToUpgradeLevel(CardModel source, int upgradeLevel)
    {
        var copy = source.CreateClone();
        UpgradeToLevel(copy, upgradeLevel);
        return copy;
    }

    public static void UpgradeToLevel(CardModel card, int upgradeLevel)
    {
        while (card.CurrentUpgradeLevel < upgradeLevel && card.IsUpgradable)
            card.UpgradeInternal();
    }

    public static void RemoveDetachedGeneratedChoices(IEnumerable<CardModel> choices)
    {
        foreach (var choice in choices)
        {
            if (choice.Pile is not null)
                continue;

            choice.CardScope?.RemoveCard(choice);
        }
    }

    internal static GeneratedCardOptions TemporaryCopyOptions(bool release, bool freeThisTurn, bool preserveRelease) =>
        new()
        {
            RemoveRelease = !preserveRelease,
            AddTemporary = true,
            AddRelease = release,
            FreeThisTurn = freeThisTurn
        };

    internal static GeneratedCardOptions RememberedCopyOptions(bool freeThisTurn, bool addTemporary) =>
        new()
        {
            RemoveTemporary = true,
            AddTemporary = addTemporary,
            FreeThisTurn = freeThisTurn
        };

    internal static GeneratedCardOptions RestoredReleasedCardToHandOptions(bool freeThisTurn) =>
        new()
        {
            Pile = PileType.Hand,
            RemoveTemporary = true,
            AddRelease = true,
            FreeThisTurn = freeThisTurn
        };

    private static GeneratedCardOptions ManifestChoiceOptions(bool addTemporary, bool captureEligible) =>
        new()
        {
            RemoveRelease = true,
            AddTemporary = addTemporary,
            AddManifestAtlasOrigin = captureEligible
        };

    private static GeneratedCardOptions GeneratedCardToHandOptions(CardPilePosition position) =>
        new()
        {
            Pile = PileType.Hand,
            Position = position
        };

    private static GeneratedCardOptions TemporaryReleasedCardOptions(PileType pile, CardPilePosition position) =>
        new()
        {
            Pile = pile,
            Position = position,
            AddTemporary = true,
            AddRelease = true
        };

    private static GeneratedCardOptions ManifestAtlasTemporaryCardOptions() =>
        new()
        {
            AddTemporary = true,
            AddManifestAtlasOrigin = true
        };

    private static GeneratedCardOptions DiscoveredChoiceOptions(bool freeThisTurn) =>
        new()
        {
            FreeThisTurn = freeThisTurn
        };

    private static CardModel CreateCardFromTemplate(SakuraModCard source, CardModel card)
    {
        var scope = source.CardScope
            ?? throw new InvalidOperationException($"Cannot create generated {card.Id.Entry} without a card scope.");
        return scope.CreateCard(card, source.Owner);
    }

    private static async Task TriggerTemporaryGranted(PlayerChoiceContext context, CardModel card)
    {
        if (card.Owner?.Creature.GetPower<FalseDailyLifePower>() is { } falseDailyLife)
            await falseDailyLife.AfterTemporaryGranted(context);
    }
}
