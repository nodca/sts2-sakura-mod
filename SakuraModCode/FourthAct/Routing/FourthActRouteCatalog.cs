using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Water;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Earth;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public static class FourthActRouteCatalog
{
    internal static FourthActRouteDefinition Wind { get; } = new(
        SakuraElement.Wind,
        WindEnemyCatalog.EliteEncounters,
        WindEnemyCatalog.BossEncounter,
        new(FourthActEndpoint.Dark, DarkEnemyCatalog.EndpointEncounterType));

    internal static FourthActRouteDefinition Water { get; } = new(
        SakuraElement.Water,
        WaterEnemyCatalog.EliteEncounters,
        WaterEnemyCatalog.BossEncounter,
        new(FourthActEndpoint.Dark, DarkEnemyCatalog.EndpointEncounterType));

    internal static FourthActRouteDefinition Fire { get; } = new(
        SakuraElement.Fire,
        FireEnemyCatalog.EliteEncounters,
        FireEnemyCatalog.BossEncounter,
        new(FourthActEndpoint.Light, FireEnemyCatalog.EndpointEncounterType));

    internal static FourthActRouteDefinition Earth { get; } = new(
        SakuraElement.Earth,
        EarthEnemyCatalog.EliteEncounters,
        EarthEnemyCatalog.BossEncounter,
        new(FourthActEndpoint.Light, EarthEnemyCatalog.EndpointEncounterType));

    internal static IReadOnlyList<FourthActRouteDefinition> DraftRoutes { get; } = [Wind, Water, Fire, Earth];

    public static IReadOnlyList<FourthActRouteDefinition> CompleteRoutes =>
        Resolve().CompleteRoutes;

    public static IReadOnlyList<Type> CompleteEncounterTypes =>
        Resolve().CompleteEncounterTypes;

    internal static FourthActRouteResolution Resolve() =>
        FourthActRouteResolver.Resolve(DraftRoutes);

    internal static FourthActRouteEncounter? RewardEncounterFor(Type encounterType) =>
        Resolve().CompleteRoutes
            .SelectMany(static route => route.EliteCandidates
                .Append(route.ElementalBoss
                    ?? throw new InvalidOperationException("A complete fourth-act route requires an elemental boss.")))
            .FirstOrDefault(encounter => encounter.EncounterType == encounterType);

    public static IReadOnlyList<FourthActRouteDefinition> CompleteRoutesFrom(
        IEnumerable<FourthActRouteDefinition> routes) =>
        FourthActRouteResolver.Resolve(routes).CompleteRoutes;
}
