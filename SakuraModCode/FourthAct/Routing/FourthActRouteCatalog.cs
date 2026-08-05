using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Dark;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public static class FourthActRouteCatalog
{
    internal static FourthActRouteDefinition Wind { get; } = new(
        SakuraElement.Wind,
        WindEnemyCatalog.EliteEncounters,
        WindEnemyCatalog.BossEncounter,
        new(FourthActEndpoint.Dark, DarkEnemyCatalog.EndpointEncounterType));

    internal static IReadOnlyList<FourthActRouteDefinition> DraftRoutes { get; } = [Wind];

    public static IReadOnlyList<FourthActRouteDefinition> CompleteRoutes =>
        Resolve().CompleteRoutes;

    public static IReadOnlyList<Type> CompleteEncounterTypes =>
        Resolve().CompleteEncounterTypes;

    internal static FourthActRouteResolution Resolve() =>
        FourthActRouteResolver.Resolve(DraftRoutes);

    public static IReadOnlyList<FourthActRouteDefinition> CompleteRoutesFrom(
        IEnumerable<FourthActRouteDefinition> routes) =>
        FourthActRouteResolver.Resolve(routes).CompleteRoutes;
}
