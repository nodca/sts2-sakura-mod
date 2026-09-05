using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public sealed class SakuraFourthActMap : ActMap
{
    private const int MerchantRow = 1;
    private const int EliteRow = 2;
    private const int ElementalBossRow = 3;
    private const int RestSiteRow = 4;
    private const int GridRowCount = RestSiteRow + 1;

    private readonly MapPoint?[,] _grid;
    public SakuraFourthActMap(IEnumerable<FourthActRouteDefinition> routes) :
        this(FourthActRouteResolver.Resolve(routes))
    {
    }

    internal SakuraFourthActMap(FourthActRouteResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        Routes = resolution.CompleteRoutes;
        if (Routes.Count is < 1 or > 4)
            throw new ArgumentException("A fourth-act map requires one to four complete routes.", nameof(resolution));

        var columnCount = Routes.Count * 2 - 1;
        var centerColumn = columnCount / 2;
        _grid = new MapPoint[columnCount, GridRowCount];

        StartingMapPoint = Point(centerColumn, 0, MapPointType.Ancient);
        MerchantMapPoint = Point(centerColumn, MerchantRow, MapPointType.Shop);
        BossMapPoint = Point(centerColumn, GridRowCount, MapPointType.Boss);
        _grid[centerColumn, MerchantRow] = MerchantMapPoint;
        StartingMapPoint.AddChildPoint(MerchantMapPoint);
        startMapPoints.Add(MerchantMapPoint);

        for (var routeIndex = 0; routeIndex < Routes.Count; routeIndex++)
        {
            var column = routeIndex * 2;
            var elite = AddRoutePoint(column, EliteRow, MapPointType.Elite);
            var elementalBoss = AddRoutePoint(column, ElementalBossRow, MapPointType.Boss);
            var restSite = AddRoutePoint(column, RestSiteRow, MapPointType.RestSite);

            MerchantMapPoint.AddChildPoint(elite);
            elite.AddChildPoint(elementalBoss);
            elementalBoss.AddChildPoint(restSite);
            restSite.AddChildPoint(BossMapPoint);
        }
    }

    public IReadOnlyList<FourthActRouteDefinition> Routes { get; }
    public MapPoint MerchantMapPoint { get; }
    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    protected override MapPoint?[,] Grid => _grid;

    public FourthActRouteDefinition? RouteAt(MapCoord coord) =>
        coord.row is >= EliteRow and <= RestSiteRow
            ? RouteForColumn(Routes, coord.col)
            : null;

    internal static FourthActRouteEncounter? EncounterAt(
        IReadOnlyList<FourthActRouteDefinition> routes,
        MapCoord coord,
        uint runSeed)
    {
        var route = RouteForColumn(routes, coord.col);
        return coord.row switch
        {
            EliteRow when route is not null => new Rng(
                    runSeed,
                    $"sakura_fourth_act/{route.Element}/elite")
                .NextItem(route.EliteCandidates),
            ElementalBossRow when route is not null => route.ElementalBoss,
            _ => null
        };
    }

    private MapPoint AddRoutePoint(
        int column,
        int row,
        MapPointType pointType)
    {
        var point = Point(column, row, pointType);
        _grid[column, row] = point;
        return point;
    }

    private static FourthActRouteDefinition? RouteForColumn(
        IReadOnlyList<FourthActRouteDefinition> routes,
        int column)
    {
        if (column < 0 || column % 2 != 0)
            return null;

        var routeIndex = column / 2;
        return routeIndex < routes.Count ? routes[routeIndex] : null;
    }

    private static MapPoint Point(int column, int row, MapPointType pointType) =>
        new(column, row)
        {
            PointType = pointType,
            CanBeModified = false
        };
}
