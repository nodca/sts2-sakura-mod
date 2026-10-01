using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Earth.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Water.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal static class SakuraFourthActMapIcons
{
    internal static EncounterAssetProfile? Resolve(
        IReadOnlyList<FourthActRouteDefinition> routes,
        MapPointType pointType,
        MapCoord coord,
        MapCoord bossCoord,
        IReadOnlyList<MapCoord> visitedCoords)
    {
        if (pointType != MapPointType.Boss)
            return null;

        var encounterType = coord == bossCoord
            ? SakuraFourthActMap.EndpointEncounterAt(routes, coord, visitedCoords)
            : SakuraFourthActMap.EncounterAt(routes, coord, runSeed: 0)?.EncounterType;

        if (encounterType == typeof(WindyEncounter)) return FourthActEncounterAssets.WindBoss;
        if (encounterType == typeof(WateryEncounter)) return FourthActEncounterAssets.WaterBoss;
        if (encounterType == typeof(FireyEncounter)) return FourthActEncounterAssets.FireBoss;
        if (encounterType == typeof(EarthyEncounter)) return FourthActEncounterAssets.EarthBoss;
        if (encounterType == typeof(LightEncounter)) return FourthActEncounterAssets.LightBoss;
        if (encounterType == typeof(DarkEncounter)) return FourthActEncounterAssets.DarkBoss;
        return coord == bossCoord ? FourthActEncounterAssets.UnchosenEndpoint : null;
    }

    internal static EncounterAssetProfile? Resolve(NMapPoint point, IRunState runState) =>
        runState is RunState { Act: SakuraFourthAct } state
            ? Resolve(FourthActRouteCatalog.Resolve().CompleteRoutes, point.Point.PointType,
                point.Point.coord, state.Map.BossMapPoint.coord, state.VisitedMapCoords)
            : null;

    internal static void SetTextures(TextureRect icon, TextureRect outline, EncounterAssetProfile assets)
    {
        var path = assets.RunHistoryIconPath!;
        if (icon.Texture?.ResourcePath == path)
            return;

        icon.Texture = ResourceLoader.Load<Texture2D>(path);
        outline.Texture = ResourceLoader.Load<Texture2D>(assets.RunHistoryIconOutlinePath!);
    }
}

// STS2 0.107.1 creates temporary normal nodes for all interior coordinates.
// Skip the empty Boss filename until DrawPaths replaces them with boss scenes.
[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
internal static class SakuraFourthActBranchIconPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NNormalMapPoint __instance, IRunState ____runState) =>
        SakuraFourthActMapIcons.Resolve(__instance, ____runState) is null;
}

// SetMap calls DrawPaths after positioning every node and before initializing
// paths, votes, controller neighbors and travelability. Replace the nodes here
// so those native systems all see the same NBossMapPoint instances.
[HarmonyPatch(typeof(NMapScreen), "DrawPaths")]
internal static class SakuraFourthActBranchBossNodePatch
{
    [HarmonyPrefix]
    private static void Prefix(NMapScreen __instance, ref NMapPoint mapPointNode,
        RunState ____runState, Control ____points,
        Dictionary<MapCoord, NMapPoint> ____mapPointDictionary)
    {
        if (____runState.Act is not SakuraFourthAct)
            return;

        foreach (var original in ____mapPointDictionary.Values.OfType<NNormalMapPoint>().ToArray())
        {
            if (SakuraFourthActMapIcons.Resolve(original, ____runState) is null)
                continue;

            var center = original.Position; // Native GetLineEndpoint for a normal node.
            var boss = NBossMapPoint.Create(original.Point, __instance, ____runState);
            boss.Name = original.Name;
            ____points.RemoveChild(original);
            original.QueueFree();
            ____mapPointDictionary[boss.Point.coord] = boss;
            ____points.AddChild(boss);

            // 300 spacing minus ±21 jitter: 258 minimum. A 374-wide boss at
            // 60% with native 1.05 hover leaves at least 22.38 units of clearance.
            boss.Scale = Vector2.One * 0.6f;
            boss.PivotOffset = boss.Size * 0.5f;
            boss.Position = center - boss.PivotOffset;
        }

        mapPointNode = ____mapPointDictionary[mapPointNode.Point.coord];
    }
}

// All boss scenes share native static rendering. Only texture identity differs
// by coordinate; endpoint identity also follows the persisted route visits.
[HarmonyPatch(typeof(NBossMapPoint), "RefreshColorInstantly")]
internal static class SakuraFourthActBossIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(NBossMapPoint __instance, IRunState ____runState,
        TextureRect ____placeholderImage, TextureRect ____placeholderOutline)
    {
        if (SakuraFourthActMapIcons.Resolve(__instance, ____runState) is not { } assets)
            return;

        SakuraFourthActMapIcons.SetTextures(____placeholderImage, ____placeholderOutline, assets);
    }
}
