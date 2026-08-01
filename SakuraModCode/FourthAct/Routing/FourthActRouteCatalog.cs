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
        CompleteRoutesFrom(DraftRoutes);

    public static IReadOnlyList<Type> CompleteEncounterTypes =>
        CompleteRoutes
            .SelectMany(EncounterTypesForCompleteRoute)
            .Distinct()
            .ToArray();

    public static IReadOnlyList<FourthActRouteDefinition> CompleteRoutesFrom(
        IEnumerable<FourthActRouteDefinition> routes) =>
        routes
            .Where(static route => route.IsComplete)
            .OrderBy(static route => route.StableOrder)
            .ToArray();

    private static IEnumerable<Type> EncounterTypesForCompleteRoute(FourthActRouteDefinition route)
    {
        var boss = route.ElementalBoss
            ?? throw new InvalidOperationException("A complete fourth-act route requires an elemental boss.");
        var endpoint = route.Endpoint.EncounterType
            ?? throw new InvalidOperationException("A complete fourth-act route requires an endpoint encounter.");
        return route.EliteCandidates.Select(static elite => elite.EncounterType)
            .Append(boss.EncounterType)
            .Append(endpoint);
    }
}
