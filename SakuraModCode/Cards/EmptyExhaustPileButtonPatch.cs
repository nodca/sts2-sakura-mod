using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCombatCardPile), "RemoveCard")]
internal static class EmptyExhaustPileButtonPatch
{
    private static readonly FieldInfo? PileField = AccessTools.Field(typeof(NCombatCardPile), "_pile");

    [HarmonyPostfix]
    private static void RemoveCardPostfix(NCombatCardPile __instance)
    {
        if (__instance is not NExhaustPileButton)
            return;

        if (PileField?.GetValue(__instance) is not CardPile { Type: PileType.Exhaust, IsEmpty: true })
            return;

        __instance.Disable();
        __instance.Visible = false;
    }
}
