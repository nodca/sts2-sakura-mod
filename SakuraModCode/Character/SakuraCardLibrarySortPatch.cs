using System.Globalization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NCardGrid), nameof(NCardGrid.SetCards))]
internal static class SakuraCardLibrarySortPatch
{
    private const int TransparentGroup = 0;
    private const int PartnerGroup = 1;
    private const int TechniqueGroup = 2;
    private const int TsubasaGroup = 3;
    private const int OtherEventGroup = 4;
    private const int OtherSakuraGroup = 5;
    private const int MissingIndex = 10_000;

    [HarmonyPrefix]
    private static void SetCardsPrefix(
        NCardGrid __instance,
        ref IReadOnlyList<CardModel> cardsToDisplay,
        ref List<SortingOrders> sortingPriority)
    {
        if (__instance is not NCardLibraryGrid)
            return;
        if (cardsToDisplay.Count == 0 || !cardsToDisplay.All(IsSakuraCard))
            return;

        var originalIndexes = cardsToDisplay
            .Select((card, index) => new { card, index })
            .ToDictionary(entry => entry.card, entry => entry.index);
        var nativeSortingPriority = sortingPriority.ToList();
        var useCatalogOrder = IsDefaultLibrarySort(nativeSortingPriority);

        cardsToDisplay = cardsToDisplay
            .OrderBy(card => card, Comparer<CardModel>.Create(
                (left, right) => CompareSakuraCards(left, right, nativeSortingPriority, originalIndexes, useCatalogOrder)))
            .ToList();
        sortingPriority = [SortingOrders.Ascending];
    }

    private static bool IsSakuraCard(CardModel card) =>
        card.Pool is SakuraModCardPool;

    private static int CompareSakuraCards(
        CardModel left,
        CardModel right,
        IReadOnlyList<SortingOrders> sortingPriority,
        IReadOnlyDictionary<CardModel, int> originalIndexes,
        bool useCatalogOrder)
    {
        var result = GetCategory(left).CompareTo(GetCategory(right));
        if (result != 0)
            return result;

        if (useCatalogOrder)
        {
            result = GetCategoryIndex(left).CompareTo(GetCategoryIndex(right));
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

    private static bool IsDefaultLibrarySort(IReadOnlyList<SortingOrders> sortingPriority) =>
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

        var result = GetCategoryIndex(left).CompareTo(GetCategoryIndex(right));
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

    private static int GetCategory(CardModel card)
    {
        if (SakuraCardCatalog.IsTransparentCard(card))
            return TransparentGroup;
        if (SakuraCardCatalog.IsPartnerCard(card))
            return PartnerGroup;
        if (SakuraCardCatalog.IsTechniqueCard(card))
            return TechniqueGroup;
        if (SakuraCardCatalog.IsTsubasaCard(card))
            return TsubasaGroup;
        if (SakuraCardCatalog.IsEventOnlyCard(card))
            return OtherEventGroup;

        return OtherSakuraGroup;
    }

    private static int GetCategoryIndex(CardModel card)
    {
        var type = card.GetType();
        return GetCategory(card) switch
        {
            TransparentGroup => IndexOf(SakuraCardCatalog.TransparentCardTypes, type),
            PartnerGroup => IndexOf(SakuraCardCatalog.PartnerCardTypes, type),
            TechniqueGroup => IndexOf(SakuraCardCatalog.TechniqueCardTypes, type),
            TsubasaGroup => IndexOf(SakuraCardCatalog.TsubasaCardTypes, type),
            _ => MissingIndex
        };
    }

    private static int IndexOf(IReadOnlyList<Type> cardTypes, Type cardType)
    {
        for (var index = 0; index < cardTypes.Count; index++)
        {
            if (cardTypes[index] == cardType)
                return index;
        }

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
