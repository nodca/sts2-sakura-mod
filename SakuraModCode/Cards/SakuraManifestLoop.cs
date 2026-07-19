using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraManifestLoop
{
    private const int CommonManifestWeight = 6;
    private const int UncommonManifestWeight = 3;
    private const int RareManifestWeight = 1;
    private const int BaseManifestChoiceCount = 3;

    private static readonly LocString ManifestPrompt = new("cards", "SAKURAMOD-GENERIC.manifestPrompt");
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, Type>> CaptureCandidatesByCombat = new();

    public static Task<IReadOnlyList<CardModel>> Manifest(
        SakuraCardModel source,
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

        return manifested;
    }

    public static async Task<CardModel?> AddTemporaryTransparentCopyToHand(
        SakuraCardModel source,
        PlayerChoiceContext context,
        bool freeThisTurn)
    {
        var owner = source.Owner;
        var choices = TransparentCardChoices(owner, source.GetType())
            .Select(card => SakuraGeneratedCardLifecycle.CreateCombatCardFromTemplate(owner, card))
            .ToList();
        if (choices.Count == 0)
            return null;

        try
        {
            var selected = await SakuraActions.SelectFromCards(source, context, choices, cancelable: false);
            if (selected is null)
                return null;

            return await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                selected,
                new GeneratedCardOptions
                {
                    Pile = PileType.Hand,
                    AddTemporary = true,
                    FreeThisTurn = freeThisTurn
                },
                context);
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
        CaptureCandidateTypes(owner)
            .Select(SakuraTransparentCardCatalog.CardTemplate)
            .ToList();

    public static Task OnTemporaryStabilized(PlayerChoiceContext context, CardModel card)
    {
        RememberCaptureCandidate(card);
        return Task.CompletedTask;
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
        var weightedSources = source switch
        {
            ManifestSource.RareAtlas => WeightedManifestAtlasSources(card => card.Rarity == CardRarity.Rare),
            _ => WeightedManifestAtlasSources(SakuraTransparentCardCatalog.IsDefaultManifestAtlasCard)
        };
        if (excludedType is not null)
            weightedSources.RemoveAll(card => card.GetType() == excludedType);

        List<CardModel> choices = [];
        var rng = owner.RunState.Rng.CombatCardSelection;

        while (choices.Count < BaseManifestChoiceCount && weightedSources.Count > 0)
        {
            var picked = rng.NextItem(weightedSources);
            if (picked is null)
                break;

            choices.Add(picked);
            var pickedType = picked.GetType();
            weightedSources.RemoveAll(card => card.GetType() == pickedType);
        }

        return new ManifestChoiceSet(choices, CaptureEligible: true);
    }

    private static List<CardModel> TransparentCardChoices(Player owner, Type excludedType)
    {
        var candidates = SakuraTransparentCardCatalog.TransparentCardTypes
            .Select(SakuraTransparentCardCatalog.CardTemplate)
            .Where(card => card.GetType() != excludedType)
            .ToList();
        var choices = new List<CardModel>(BaseManifestChoiceCount);
        var rng = owner.RunState.Rng.CombatCardSelection;

        while (choices.Count < BaseManifestChoiceCount && candidates.Count > 0)
        {
            var picked = rng.NextItem(candidates);
            if (picked is null)
                break;

            choices.Add(picked);
            candidates.RemoveAll(card => card.GetType() == picked.GetType());
        }

        return choices;
    }

    private static List<CardModel> WeightedManifestAtlasSources(Func<CardModel, bool>? predicate = null) =>
        SakuraTransparentCardCatalog.TransparentCardTypes
            .Select(SakuraTransparentCardCatalog.CardTemplate)
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
            || !SakuraTransparentCardCatalog.IsTransparentCard(card)
            || !card.IsManifestAtlasOrigin())
            return;

        var cardsByOwner = CaptureCandidatesByCombat.GetValue(card.CombatState, _ => []);
        cardsByOwner.TryAdd(owner, card.GetType());
    }

    private enum ManifestSource
    {
        Default,
        RareAtlas
    }

    private sealed record ManifestChoiceSet(IReadOnlyList<CardModel> Cards, bool CaptureEligible);
}
