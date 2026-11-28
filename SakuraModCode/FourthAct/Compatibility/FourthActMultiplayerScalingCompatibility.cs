using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.FourthAct.Compatibility;

internal static class FourthActMultiplayerScalingCompatibility
{
    internal const int FourthActIndex = 3;
    internal const decimal NonBossScale = 1.2m;
    internal const decimal BossScale = 1.3m;

    internal static bool TryResolve(EncounterModel? encounter, int actIndex, out decimal scale)
    {
        if (actIndex != FourthActIndex)
        {
            scale = default;
            return false;
        }

        scale = encounter?.RoomType == RoomType.Boss ? BossScale : NonBossScale;
        return true;
    }

    internal static bool IsSupportedGameAssembly(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion is "0.1.0+16ac0fe3775f571e5f3b1c81e47d511024515730"
                or "0.1.0+d09d48e516cfd24314c1781f662787904f894aa8"
                or "0.1.0+a7b9a9c09622442b9f0cb1a64310a7503b38efa4";
}

[HarmonyPatch(
    typeof(MultiplayerScalingModel),
    nameof(MultiplayerScalingModel.GetMultiplayerScaling),
    [typeof(EncounterModel), typeof(int)])]
internal static class FourthActMultiplayerScalingPatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        var supported = FourthActMultiplayerScalingCompatibility.IsSupportedGameAssembly(
            typeof(MultiplayerScalingModel).Assembly);
        if (!supported)
        {
            MainFile.Logger.Warn(
                "SakuraMod fourth-act multiplayer scaling compatibility is disabled: unsupported STS2 assembly.");
        }

        return supported;
    }

    [HarmonyPrefix]
    private static bool ResolveFourthActScale(
        EncounterModel? encounter,
        int actIndex,
        ref decimal __result)
    {
        if (!FourthActMultiplayerScalingCompatibility.TryResolve(encounter, actIndex, out var scale))
            return true;

        __result = scale;
        return false;
    }
}
