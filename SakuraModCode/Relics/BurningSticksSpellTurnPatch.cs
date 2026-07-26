using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Relics;

[HarmonyPatch(typeof(BurningSticks), nameof(BurningSticks.AfterCardExhausted))]
internal static class BurningSticksSpellTurnPatch
{
    internal static bool ShouldSkip(CardModel card) => card is SpellTurn;

    [HarmonyPrefix]
    private static bool SkipSpellTurn(CardModel card, ref Task __result)
    {
        if (!ShouldSkip(card))
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}
