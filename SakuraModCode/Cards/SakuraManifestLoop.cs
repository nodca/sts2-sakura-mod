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

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraManifestLoop
{
    // The Transparent pool is top-heavy (few Commons, many Rares), so a gentler
    // curve than the usual 6:3:1 keeps the few Commons from crowding every reveal.
    private const int CommonManifestWeight = 3;
    private const int UncommonManifestWeight = 2;
    private const int RareManifestWeight = 1;
    internal const int BaseManifestChoiceCount = 3;

    private static readonly LocString ManifestPrompt = new("cards", "SAKURAMOD-GENERIC.manifestPrompt");

    public static Task<IReadOnlyList<CardModel>> Manifest(
        SakuraCardModel source,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0,
        bool upgraded = false,
        int choiceCount = BaseManifestChoiceCount) =>
        Manifest(source.Owner, context, amount, stabilize, excludedType, rareAtlasChoices, upgraded, choiceCount);

    public static async Task<IReadOnlyList<CardModel>> Manifest(
        Player owner,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0,
        bool upgraded = false,
        int choiceCount = BaseManifestChoiceCount)
    {
        List<CardModel> manifested = [];

        for (var i = 0; i < amount; i++)
        {
            var source = i < rareAtlasChoices
                ? ManifestSource.RareAtlas
                : ManifestSource.Default;
            var choiceCards = ManifestChoices(owner, excludedType, source, choiceCount);
            var choices = choiceCards
                .Select(choice =>
                {
                    var card = SakuraGeneratedCardLifecycle.CreateManifestChoice(owner, choice);
                    if (upgraded)
                        SakuraGeneratedCardLifecycle.UpgradeToLevel(card, 1);
                    return card;
                })
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
                    addTemporary: !stabilize);
                manifested.Add(copy);
            }
            finally
            {
                SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(choices);
            }
        }

        return manifested;
    }

    private static List<CardModel> ManifestChoices(Player owner, Type? excludedType, ManifestSource source, int choiceCount)
    {
        var weightedSources = source switch
        {
            ManifestSource.RareAtlas => WeightedManifestAtlasSources(card => card.Rarity == CardRarity.Rare),
            _ => WeightedManifestAtlasSources()
        };
        if (excludedType is not null)
            weightedSources.RemoveAll(card => card.GetType() == excludedType);

        List<CardModel> choices = [];
        var rng = owner.RunState.Rng.CombatCardSelection;

        while (choices.Count < choiceCount && weightedSources.Count > 0)
        {
            var picked = rng.NextItem(weightedSources);
            if (picked is null)
                break;

            choices.Add(picked);
            var pickedType = picked.GetType();
            weightedSources.RemoveAll(card => card.GetType() == pickedType);
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

    private enum ManifestSource
    {
        Default,
        RareAtlas
    }
}
