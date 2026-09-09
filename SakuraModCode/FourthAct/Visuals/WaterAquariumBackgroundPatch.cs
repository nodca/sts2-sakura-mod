using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

// Creature _Ready runs before combat setup creates the background.
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class WaterAquariumBackgroundPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance)
    {
        if (TestMode.IsOn || __instance.Background is not { } background
            || background.FindChild(WaterAquariumVisuals.PaintingNodeName, true, false) is not TextureRect painting)
            return;

        GD.Print("[SakuraMod][Aquarium] Background ready; mounting motion controller.");
        WaterAquariumVisuals.Attach(painting, __instance, background);
    }
}
