using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class CardPlayHistoryPatch
{
    [HarmonyPrefix]
    private static void BeforeCardPlayedPrefix(CombatState combatState, CardPlay cardPlay)
    {
        SakuraActions.RememberPlayedReleasedCard(cardPlay);
        SakuraActions.RememberPlayedCard(cardPlay);
    }
}
