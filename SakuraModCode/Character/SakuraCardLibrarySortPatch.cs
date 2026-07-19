using System.Globalization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Character;

// RitsuLib 0.4.60 owns compendium filter-row placement, but not the card order inside NCardLibraryGrid.
[HarmonyPatch(typeof(NCardGrid), nameof(NCardGrid.SetCards))]
internal static class SakuraCardLibrarySortPatch
{
    private const int OtherSakuraGroup = 10;
    private const int MissingIndex = 10_000;

    [HarmonyPrefix]
    private static void SetCardsPrefix(
        NCardGrid __instance,
        ref IReadOnlyList<CardModel> cardsToDisplay,
        ref List<SortingOrders> sortingPriority)
    {
        if (!ShouldSortSakuraLibraryGrid(__instance, cardsToDisplay))
            return;

        cardsToDisplay = SortCardsForLibrary(cardsToDisplay, sortingPriority);
        sortingPriority = [SortingOrders.Ascending];
    }

    internal static IReadOnlyList<CardModel> SortCardsForLibrary(
        IReadOnlyList<CardModel> cardsToDisplay,
        IReadOnlyList<SortingOrders> sortingPriority)
    {
        var originalIndexes = cardsToDisplay
            .Select((card, index) => new { card, index })
            .ToDictionary(entry => entry.card, entry => entry.index);
        var nativeSortingPriority = sortingPriority.ToList();
        var useCatalogOrder = IsDefaultLibrarySort(nativeSortingPriority);

        return cardsToDisplay
            .OrderBy(card => card, Comparer<CardModel>.Create(
                (left, right) => CompareSakuraCards(left, right, nativeSortingPriority, originalIndexes, useCatalogOrder)))
            .ToList();
    }

    private static bool ShouldSortSakuraLibraryGrid(NCardGrid grid, IReadOnlyList<CardModel> cardsToDisplay) =>
        grid is NCardLibraryGrid
        && cardsToDisplay.Count > 0
        && cardsToDisplay.All(IsSakuraLibraryCard);

    private static bool IsSakuraLibraryCard(CardModel card) =>
        SakuraCardCatalog.IsPoolCard(card)
        || card.Pool is ClassicSakuraCardPool;

    private static int CompareSakuraCards(
        CardModel left,
        CardModel right,
        IReadOnlyList<SortingOrders> sortingPriority,
        IReadOnlyDictionary<CardModel, int> originalIndexes,
        bool useCatalogOrder)
    {
        var result = CategorySortValue(left).CompareTo(CategorySortValue(right));
        if (result != 0)
            return result;

        if (useCatalogOrder)
        {
            result = CategoryIndex(left).CompareTo(CategoryIndex(right));
            if (result != 0)
                return result;
        }
        else
        {
            result = CompareByNativeSort(left, right, sortingPriority, originalIndexes);
            if (result != 0)
                return result;
        }

        return left.Id.CompareTo(right.Id);
    }

    internal static bool IsDefaultLibrarySort(IReadOnlyList<SortingOrders> sortingPriority) =>
        sortingPriority.Count == 4
        && sortingPriority[0] == SortingOrders.RarityAscending
        && sortingPriority[1] == SortingOrders.TypeAscending
        && sortingPriority[2] == SortingOrders.CostAscending
        && sortingPriority[3] == SortingOrders.AlphabetAscending;

    private static int CompareByNativeSort(
        CardModel left,
        CardModel right,
        IReadOnlyList<SortingOrders> sortingPriority,
        IReadOnlyDictionary<CardModel, int> originalIndexes)
    {
        foreach (var sort in sortingPriority)
        {
            var nativeResult = CompareByNativeSort(left, right, sort, originalIndexes);
            if (nativeResult != 0)
                return nativeResult;
        }

        var result = CategoryIndex(left).CompareTo(CategoryIndex(right));
        return result != 0 ? result : left.Id.CompareTo(right.Id);
    }

    private static int CompareByNativeSort(
        CardModel left,
        CardModel right,
        SortingOrders sort,
        IReadOnlyDictionary<CardModel, int> originalIndexes) =>
        sort switch
        {
            SortingOrders.RarityAscending => GetRaritySortValue(left).CompareTo(GetRaritySortValue(right)),
            SortingOrders.RarityDescending => -GetRaritySortValue(left).CompareTo(GetRaritySortValue(right)),
            SortingOrders.CostAscending => left.EnergyCost.GetResolved().CompareTo(right.EnergyCost.GetResolved()),
            SortingOrders.CostDescending => -left.EnergyCost.GetResolved().CompareTo(right.EnergyCost.GetResolved()),
            SortingOrders.TypeAscending => left.Type.CompareTo(right.Type),
            SortingOrders.TypeDescending => -left.Type.CompareTo(right.Type),
            SortingOrders.AlphabetAscending => string.Compare(
                left.Title,
                right.Title,
                LocManager.Instance.CultureInfo,
                CompareOptions.None),
            SortingOrders.AlphabetDescending => -string.Compare(
                left.Title,
                right.Title,
                LocManager.Instance.CultureInfo,
                CompareOptions.None),
            SortingOrders.Ascending => originalIndexes[left].CompareTo(originalIndexes[right]),
            SortingOrders.Descending => -originalIndexes[left].CompareTo(originalIndexes[right]),
            _ => 0
        };

    internal static int CategorySortValue(CardModel card)
    {
        if (SakuraCardCatalog.TryGetMetadata(card, out var metadata))
        {
            return metadata.Era switch
            {
                SourceEraClass.Clow => 0,
                SourceEraClass.Sakura => 1,
                SourceEraClass.Clear => 2,
                null => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(metadata.Era), metadata.Era, null)
            };
        }

        return OtherSakuraGroup;
    }

    internal static int CategoryIndex(CardModel card)
    {
        if (SakuraCardCatalog.TryGetMetadata(card, out var metadata))
            return metadata.CatalogOrder;

        return MissingIndex;
    }

    private static int GetRaritySortValue(CardModel card)
    {
        if (card.Rarity <= CardRarity.Ancient)
            return (int)card.Rarity;

        return card.Rarity switch
        {
            CardRarity.Status => 6,
            CardRarity.Curse => 7,
            CardRarity.Event => 8,
            CardRarity.Quest => 9,
            CardRarity.Token => 10,
            _ => (int)card.Rarity
        };
    }
}
