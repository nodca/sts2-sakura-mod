using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal sealed record FourthActRouteDiagnostic(
    FourthActRouteDefinition Route,
    string Code,
    string Message);

internal sealed record FourthActRouteResolution(
    IReadOnlyList<FourthActRouteDefinition> CompleteRoutes,
    IReadOnlyList<FourthActRouteDiagnostic> Diagnostics)
{
    public bool HasCompleteRoutes => CompleteRoutes.Count > 0;

    public IReadOnlyList<Type> CompleteEncounterTypes =>
        CompleteRoutes
            .SelectMany(static route =>
            {
                var boss = route.ElementalBoss
                    ?? throw new InvalidOperationException("A complete fourth-act route requires an elemental boss.");
                var endpoint = route.Endpoint.EncounterType
                    ?? throw new InvalidOperationException("A complete fourth-act route requires an endpoint encounter.");
                return route.EliteCandidates.Select(static elite => elite.EncounterType)
                    .Append(boss.EncounterType)
                    .Append(endpoint);
            })
            .Distinct()
            .ToArray();
}

internal sealed record FourthActRouteResolutionContext(
    Func<Type, RoomType?> EncounterRoomType,
    Func<SourceCardIdentity, SakuraElementSet?> CardElements)
{
    public static FourthActRouteResolutionContext Native { get; } = new(
        NativeEncounterRoomType,
        NativeCardElements);

    private static RoomType? NativeEncounterRoomType(Type encounterType)
    {
        if (!typeof(EncounterModel).IsAssignableFrom(encounterType) || encounterType.IsAbstract)
            return null;

        var encounter = ModelDb.GetByIdOrNull<EncounterModel>(ModelDb.GetId(encounterType))
            ?? Activator.CreateInstance(encounterType) as EncounterModel;
        return encounter is not null
            ? encounter.RoomType
            : null;
    }

    private static SakuraElementSet? NativeCardElements(SourceCardIdentity identity)
    {
        var cardType = SakuraSourceCardRules.ClowTypeFor(identity);
        if (cardType is null)
            return null;

        var card = ModelDb.GetByIdOrNull<SakuraSourceCard>(ModelDb.GetId(cardType))
            ?? Activator.CreateInstance(cardType) as SakuraSourceCard;
        return card?.Elements;
    }
}

internal static class FourthActRouteResolver
{
    public static FourthActRouteResolution Resolve(
        IEnumerable<FourthActRouteDefinition> routes,
        FourthActRouteResolutionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(routes);
        context ??= FourthActRouteResolutionContext.Native;

        var completeRoutes = new List<FourthActRouteDefinition>();
        var diagnostics = new List<FourthActRouteDiagnostic>();
        foreach (var route in routes)
        {
            ArgumentNullException.ThrowIfNull(route);
            var routeDiagnostics = Diagnose(route, context);
            if (routeDiagnostics.Count == 0)
                completeRoutes.Add(route);
            else
                diagnostics.AddRange(routeDiagnostics);
        }

        return new(
            completeRoutes
                .OrderBy(static route => route.StableOrder)
                .ToArray(),
            diagnostics);
    }

    private static IReadOnlyList<FourthActRouteDiagnostic> Diagnose(
        FourthActRouteDefinition route,
        FourthActRouteResolutionContext context)
    {
        var diagnostics = new List<FourthActRouteDiagnostic>();
        if (route.EliteCandidates.Count == 0)
        {
            diagnostics.Add(Diagnostic(route, "elite-pool-empty", "The route has no elite candidates."));
        }
        else
        {
            foreach (var candidate in route.EliteCandidates)
                DiagnoseEncounter(route, candidate, RoomType.Elite, "elite", context, diagnostics);
        }

        if (route.ElementalBoss is not { } boss)
        {
            diagnostics.Add(Diagnostic(route, "elemental-boss-missing", "The route has no elemental boss."));
        }
        else
        {
            DiagnoseEncounter(route, boss, RoomType.Boss, "elemental-boss", context, diagnostics);
        }

        if (route.Endpoint.EncounterType is not { } endpointType)
        {
            diagnostics.Add(Diagnostic(route, "endpoint-missing", "The route has no endpoint encounter."));
        }
        else if (context.EncounterRoomType(endpointType) != RoomType.Boss)
        {
            diagnostics.Add(Diagnostic(route, "endpoint-invalid", $"Endpoint {endpointType.Name} is not a boss encounter."));
        }

        return diagnostics;
    }

    private static void DiagnoseEncounter(
        FourthActRouteDefinition route,
        FourthActRouteEncounter encounter,
        RoomType expectedRoomType,
        string role,
        FourthActRouteResolutionContext context,
        ICollection<FourthActRouteDiagnostic> diagnostics)
    {
        if (context.EncounterRoomType(encounter.EncounterType) != expectedRoomType)
        {
            diagnostics.Add(Diagnostic(
                route,
                $"{role}-invalid",
                $"{role} {encounter.EncounterType.Name} is not a {expectedRoomType} encounter."));
        }

        var elements = context.CardElements(encounter.RewardIdentity);
        if (elements is null || !elements.Value.HasElement(route.Element))
        {
            diagnostics.Add(Diagnostic(
                route,
                $"{role}-element-mismatch",
                $"{role} reward {encounter.RewardIdentity} does not belong to {route.Element}."));
        }
    }

    private static FourthActRouteDiagnostic Diagnostic(
        FourthActRouteDefinition route,
        string code,
        string message) =>
        new(route, code, message);
}
