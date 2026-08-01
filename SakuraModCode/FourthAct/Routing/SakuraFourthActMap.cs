using MegaCrit.Sts2.Core.Map;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public sealed class SakuraFourthActMap : ActMap
{
    private const int MerchantRow = 1;
    private const int EliteRow = 2;
    private const int ElementalBossRow = 3;
    private const int RestSiteRow = 4;
    private const int GridRowCount = RestSiteRow + 1;

    private readonly MapPoint?[,] _grid;
    private readonly Dictionary<MapCoord, FourthActRouteDefinition> _routesByCoord = [];

    public SakuraFourthActMap(IEnumerable<FourthActRouteDefinition> routes)
    {
        Routes = FourthActRouteCatalog.CompleteRoutesFrom(routes);
        if (Routes.Count is < 1 or > 4)
            throw new ArgumentException("A fourth-act map requires one to four complete routes.", nameof(routes));

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
            var elite = AddRoutePoint(column, EliteRow, MapPointType.Elite, Routes[routeIndex]);
            var elementalBoss = AddRoutePoint(column, ElementalBossRow, MapPointType.Boss, Routes[routeIndex]);
            var restSite = AddRoutePoint(column, RestSiteRow, MapPointType.RestSite, Routes[routeIndex]);

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
        _routesByCoord.GetValueOrDefault(coord);

    private MapPoint AddRoutePoint(
        int column,
        int row,
        MapPointType pointType,
        FourthActRouteDefinition route)
    {
        var point = Point(column, row, pointType);
        _grid[column, row] = point;
        _routesByCoord.Add(point.coord, route);
        return point;
    }

    private static MapPoint Point(int column, int row, MapPointType pointType) =>
        new(column, row)
        {
            PointType = pointType,
            CanBeModified = false
        };
}
