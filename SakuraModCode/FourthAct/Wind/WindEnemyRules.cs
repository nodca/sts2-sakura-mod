namespace SakuraMod.SakuraModCode.FourthAct.Wind;

public enum WindyAction
{
    FirstGusts,
    FirstGust,
    SecondGusts,
    SecondGust,
    SummonAttendant,
    HeavyGale
}

public static class WindEnemyRules
{
    public const int BindPerPlayer = 5;

    public static int WindBindForAction(WindyAction action) => action switch
    {
        WindyAction.FirstGusts
            or WindyAction.FirstGust
            or WindyAction.SecondGusts
            or WindyAction.SecondGust
            or WindyAction.SummonAttendant
            or WindyAction.HeavyGale => BindPerPlayer,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static int WindBindForAttendantAction() => 0;

    public static int WallFromUnresolvedBind(int unresolvedBind) =>
        Math.Max(0, unresolvedBind + 1) / 2;

    public static int WallCap(int participatingPlayerCount) =>
        2 * Math.Max(0, participatingPlayerCount);

    public static int AggregateWall(int existingWall, IEnumerable<int> unresolvedByPlayer, int participatingPlayerCount) =>
        Math.Max(
            Math.Max(0, existingWall),
            Math.Min(
                WallCap(participatingPlayerCount),
                Math.Max(0, existingWall) + unresolvedByPlayer.Sum(WallFromUnresolvedBind)));

    public static int FailedBindAttackBonus(IEnumerable<int> unresolvedByPlayer) =>
        unresolvedByPlayer.Sum(static unresolved => Math.Max(0, unresolved));
}
