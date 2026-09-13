using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class StaticCombatBackgroundPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance)
    {
        if (TestMode.IsOn || __instance.Background is not { } background)
            return;

        var painting = background.FindChild(StaticCombatBackgroundVisuals.RooftopPaintingNodeName, true, false) as TextureRect
            ?? background.FindChild(StaticCombatBackgroundVisuals.LightPaintingNodeName, true, false) as TextureRect
            ?? background.FindChild(StaticCombatBackgroundVisuals.EarthPenguinParkPaintingNodeName, true, false) as TextureRect;
        if (painting is not null)
        {
            StaticCombatBackgroundVisuals.Attach(painting, __instance, background);
            if (painting.Name == StaticCombatBackgroundVisuals.LightPaintingNodeName)
                LightEternalDayVisuals.Attach(painting, __instance);
        }
    }
}
