using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraManifestLoop
{
    private const int CommonManifestWeight = 6;
    private const int UncommonManifestWeight = 3;
    private const int RareManifestWeight = 1;
    private const int BaseManifestChoiceCount = 3;

    private static readonly LocString ManifestPrompt = new("cards", "SAKURAMOD-GENERIC.manifestPrompt");
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, HashSet<Type>>> CatalogedClearCardsByCombat = new();
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, Type>> CaptureCandidatesByCombat = new();

    public static void Register() =>
        SakuraCaptureRewardHandoff.Register();

    public static Task<IReadOnlyList<CardModel>> Manifest(
        SakuraModCard source,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0) =>
        Manifest(source.Owner, context, amount, stabilize, excludedType, rareAtlasChoices);

    public static async Task<IReadOnlyList<CardModel>> Manifest(
        Player owner,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0)
    {
        List<CardModel> manifested = [];
        amount += owner.Creature.GetPower<MagicSurgePower>()?.ConsumeManifestBonus() ?? 0;

        for (var i = 0; i < amount; i++)
        {
            var source = i < rareAtlasChoices
                ? ManifestSource.RareAtlas
                : ManifestSource.Default;
            var choiceSet = ManifestChoices(owner, excludedType, source);
            var choices = choiceSet.Cards
                .Select(choice => SakuraGeneratedCardLifecycle.CreateManifestChoice(owner, choice))
                .ToList();
            if (choices.Count == 0)
                break;

            try
            {
                var chosen = await CardSelectCmd.FromSimpleGrid(
                    context,
                    choices,
                    owner,
                    new CardSelectorPrefs(ManifestPrompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    });
                var copy = chosen.FirstOrDefault();
                if (copy is null)
                    break;

                await SakuraGeneratedCardLifecycle.AddManifestChoiceToCombat(
                    copy,
                    context,
                    addTemporary: !stabilize,
                    captureEligible: choiceSet.CaptureEligible);
                manifested.Add(copy);
            }
            finally
            {
                SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(choices);
            }
        }

        if (manifested.Count > 0)
            await PowerCmd.Apply<SakuraManifestedThisTurnPower>(context, owner.Creature, 1, owner.Creature, null, false);

        return manifested;
    }

    public static async Task RememberCatalogCard(PlayerChoiceContext context, CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null || !SakuraCardCatalog.IsTransparentCard(card))
            return;

        var cardsByOwner = CatalogedClearCardsByCombat.GetValue(card.CombatState, _ => []);
        if (!cardsByOwner.TryGetValue(owner, out var cards))
        {
            cards = [];
            cardsByOwner[owner] = cards;
        }

        if (cards.Add(card.GetType()))
        {
            await SyncCatalogPower(context, owner);
            await TriggerCatalogedClearCard(context, play);
        }
    }

    public static int CatalogCount(Player owner)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var cards))
            return 0;

        return cards.Count;
    }

    public static IReadOnlyList<Type> CatalogedClearCardTypes(Player owner)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var cards))
            return [];

        return cards.ToList();
    }

    public static bool HasCatalogedClearCard(Player owner, CardModel card)
    {
        var combatState = owner.Creature.CombatState;
        return combatState is not null
            && SakuraCardCatalog.IsTransparentCard(card)
            && CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            && cardsByOwner.TryGetValue(owner, out var cards)
            && cards.Contains(card.GetType());
    }

    public static IReadOnlyList<CardModel> CatalogedClearCards(Player owner, Type? excludedType = null)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var cards))
            return [];

        return SakuraCardCatalog.TransparentCardTypes
            .Where(cards.Contains)
            .Where(type => type != excludedType)
            .Select(SakuraCardCatalog.CardTemplate)
            .ToList();
    }

    public static async Task<CardModel?> SelectCatalogedClearCard(
        SakuraModCard source,
        PlayerChoiceContext context,
        bool cancelable = true,
        Type? excludedType = null)
    {
        var choices = CatalogedClearCards(source.Owner, excludedType)
            .Select(card => SakuraGeneratedCardLifecycle.CreateGeneratedChoice(source, card, upgraded: false))
            .ToList();
        if (choices.Count == 0)
            return null;

        try
        {
            var selected = await SakuraActions.SelectFromCards(source, context, choices, cancelable);
            return selected?.CreateClone();
        }
        finally
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(choices);
        }
    }

    public static IReadOnlyList<Type> CaptureCandidateTypes(Player owner) =>
        owner.Creature.CombatState is { } combatState
            ? CaptureCandidateTypes(combatState, owner)
            : [];

    public static IReadOnlyList<CardModel> CaptureCandidateTemplates(Player owner) =>
        SakuraCaptureRewardHandoff.CaptureCandidateTypes(owner)
            .Select(SakuraCardCatalog.CardTemplate)
            .ToList();

    public static async Task<bool> GrantTemporary(PlayerChoiceContext context, CardModel card)
        => await SakuraGeneratedCardLifecycle.GrantTemporary(context, card);

    public static async Task OnTemporaryStabilized(PlayerChoiceContext context, CardModel card)
    {
        RememberCaptureCandidate(card);

        if (card.Owner?.Creature.GetPower<GrowingMagicPower>() is { } growingMagic)
            await growingMagic.AfterTemporaryStabilized(context);
        if (card.Owner?.GetRelic<StorageRibbon>() is { } storageRibbon)
            await storageRibbon.AfterTemporaryStabilized(context, card);
    }

    public static async Task<CardModel?> AddTemporaryCopyToHand(
        SakuraModCard source,
        PlayerChoiceContext context,
        CardModel card,
        bool release,
        bool freeThisTurn,
        bool preserveRelease = false) =>
        await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(
            card,
            release,
            freeThisTurn,
            preserveRelease,
            context);

    public static async Task<CardModel?> AddRememberedCopyToHand(SakuraModCard source, CardModel card, bool freeThisTurn) =>
        await SakuraGeneratedCardLifecycle.AddRememberedCopyToHand(card, freeThisTurn);

    public static async Task<CardModel?> AddTemporaryRememberedCopyToHand(
        SakuraModCard source,
        PlayerChoiceContext context,
        CardModel card,
        bool freeThisTurn) =>
        await SakuraGeneratedCardLifecycle.AddTemporaryRememberedCopyToHand(
            card,
            freeThisTurn,
            context);

    public static Task<CardModel> AddGeneratedCardToHand(
        CardModel card,
        PlayerChoiceContext? context = null,
        CardPilePosition position = CardPilePosition.Random) =>
        SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(card, context, position);

    public static Task<CardModel> AddTemporaryReleasedCardToCombat(
        CardModel card,
        PlayerChoiceContext context,
        PileType pile = PileType.Hand,
        CardPilePosition position = CardPilePosition.Random) =>
        SakuraGeneratedCardLifecycle.AddTemporaryReleasedCardToCombat(card, context, pile, position);

    public static Task<CardModel> AddRestoredReleasedCardToHand(
        CardModel card,
        PlayerChoiceContext context,
        bool freeThisTurn) =>
        SakuraGeneratedCardLifecycle.AddRestoredReleasedCardToHand(card, context, freeThisTurn);

    public static async Task<CardModel?> AddGeneratedCopyToHand(
        SakuraModCard source,
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null) =>
        await SakuraGeneratedCardLifecycle.AddGeneratedCopyToHand(card, options, context);

    public static async Task<CardModel?> AddGeneratedCopy(
        SakuraModCard source,
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null)
        => await SakuraGeneratedCardLifecycle.AddGeneratedCopy(card, options, context);

    public static async Task<CardModel> AddGeneratedCardToCombat(
        CardModel card,
        GeneratedCardOptions options = default,
        PlayerChoiceContext? context = null)
        => await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(card, options, context);

    public static async Task<CardModel?> AddRandomUncatalogedTemporaryClearCardToHand(
        SakuraModCard source,
        PlayerChoiceContext context)
    {
        var candidates = SakuraCardCatalog.DefaultManifestAtlasTypes
            .Where(type => !HasCatalogedClearCard(source.Owner, SakuraCardCatalog.CardTemplate(type)))
            .Select(SakuraCardCatalog.CardTemplate)
            .ToList();

        if (candidates.Count == 0)
            candidates = SakuraCardCatalog.DefaultManifestAtlasTypes
                .Select(SakuraCardCatalog.CardTemplate)
                .ToList();

        var template = source.Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (template is null)
            return null;

        var card = SakuraGeneratedCardLifecycle.CreateCombatCardFromTemplate(source.Owner, template);
        await SakuraGeneratedCardLifecycle.AddManifestAtlasTemporaryCardToCombat(card, context);
        return card;
    }

    public static async Task<CardModel?> DiscoverGenerated(
        SakuraModCard source,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        bool freeThisTurn = false,
        bool upgraded = false)
    {
        var choices = cards
            .Select(card => SakuraGeneratedCardLifecycle.CreateGeneratedChoice(source, card, upgraded))
            .ToList();
        try
        {
            var chosen = await SakuraActions.SelectFromCards(source, context, choices, cancelable: false);
            if (chosen is null)
                return null;

            await SakuraGeneratedCardLifecycle.AddDiscoveredChoiceToCombat(chosen, context, freeThisTurn);
            return chosen;
        }
        finally
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(choices);
        }
    }

    internal static IReadOnlyList<Type> CaptureCandidateTypes(ICombatState combatState, Player owner)
    {
        if (!CaptureCandidatesByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var type))
            return [];

        return [type];
    }

    private static ManifestChoiceSet ManifestChoices(Player owner, Type? excludedType, ManifestSource source)
    {
        var hasNamelessBookTruth = owner.GetRelic<NamelessBookTruth>() is not null;
        var captureEligible = source == ManifestSource.RareAtlas || !hasNamelessBookTruth;
        var weightedSources = source switch
        {
            ManifestSource.RareAtlas => WeightedManifestAtlasSources(card => card.Rarity == CardRarity.Rare),
            _ when hasNamelessBookTruth => CardPile.GetCards(owner, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust)
                .Where(SakuraCardCatalog.IsTransparentCard)
                .ToList(),
            _ => WeightedManifestAtlasSources(SakuraCardCatalog.IsDefaultManifestAtlasCard)
        };
        if (excludedType is not null)
            weightedSources.RemoveAll(card => card.GetType() == excludedType);

        List<CardModel> choices = [];
        var rng = owner.RunState.Rng.CombatCardSelection;

        while (choices.Count < ManifestChoiceCount(owner) && weightedSources.Count > 0)
        {
            var picked = rng.NextItem(weightedSources);
            if (picked is null)
                break;

            choices.Add(picked);
            var pickedType = picked.GetType();
            weightedSources.RemoveAll(card => card.GetType() == pickedType);
        }

        return new ManifestChoiceSet(choices, captureEligible);
    }

    private static int ManifestChoiceCount(Player owner) =>
        BaseManifestChoiceCount + (owner.GetRelic<SakuraIntuition>()?.AdditionalManifestChoices ?? 0);

    private static List<CardModel> WeightedManifestAtlasSources(Func<CardModel, bool>? predicate = null) =>
        SakuraCardCatalog.TransparentCardTypes
            .Select(SakuraCardCatalog.CardTemplate)
            .Where(card => predicate?.Invoke(card) != false)
            .SelectMany(card => Enumerable.Repeat(card, ManifestWeight(card.Rarity)))
            .ToList();

    private static int ManifestWeight(CardRarity rarity) =>
        rarity switch
        {
            CardRarity.Basic or CardRarity.Common => CommonManifestWeight,
            CardRarity.Uncommon => UncommonManifestWeight,
            CardRarity.Rare => RareManifestWeight,
            _ => RareManifestWeight
        };

    private static void RememberCaptureCandidate(CardModel card)
    {
        if (card.Owner is not { } owner
            || card.CombatState is null
            || !SakuraCardCatalog.IsTransparentCard(card)
            || !card.IsManifestAtlasOrigin())
            return;

        var cardsByOwner = CaptureCandidatesByCombat.GetValue(card.CombatState, _ => []);
        cardsByOwner.TryAdd(owner, card.GetType());
    }

    private static async Task TriggerCatalogedClearCard(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card?.Owner?.GetRelic<CatalogNewPage>() is { } catalogNewPage)
            await catalogNewPage.AfterCatalogedClearCard(context, play);
    }

    private static async Task SyncCatalogPower(PlayerChoiceContext context, Player owner)
    {
        var count = CatalogCount(owner);
        if (count <= 0)
            return;

        var power = owner.Creature.GetPower<SakuraCatalogPower>();
        if (power is null)
        {
            power = await PowerCmd.Apply<SakuraCatalogPower>(
                context,
                owner.Creature,
                SakuraCatalogPower.PresenceAmount,
                owner.Creature,
                null,
                false);
        }

        power?.SetCatalogCount(count);
    }

    private enum ManifestSource
    {
        Default,
        RareAtlas
    }

    private sealed record ManifestChoiceSet(IReadOnlyList<CardModel> Cards, bool CaptureEligible);
}
