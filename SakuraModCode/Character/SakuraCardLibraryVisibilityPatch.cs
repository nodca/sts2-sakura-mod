using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
internal static class SakuraCardLibraryVisibilityPatch
{
    [HarmonyPostfix]
    private static void GetCardVisibilityPostfix(CardModel card, ref ModelVisibility __result)
    {
        if (__result != ModelVisibility.Locked)
            return;
        if (!SakuraCardCatalog.IsEventOnlyCard(card))
            return;
        if (!SaveManager.Instance.Progress.DiscoveredCards.Contains(card.Id))
            return;

        // Keep event-only cards out of GetUnlockedCards while allowing discovered cards to render in the library grid.
        __result = ModelVisibility.Visible;
    }
}
