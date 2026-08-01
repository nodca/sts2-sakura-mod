namespace SakuraMod.SakuraModCode.FourthAct.Dark;

public enum DarkPhase
{
    Veiled,
    TransitionPending,
    EternalNight
}

public enum DarkRegularAction
{
    Confinement,
    NonConfinement
}

public static class DarkEnemyRules
{
    public const int BaseHp = 520;
    public const int ToughHp = 545;
    public const int BaseConfinementDamage = 16;
    public const int DeadlyConfinementDamage = 18;
    public const int BaseNonConfinementDamage = 24;
    public const int DeadlyNonConfinementDamage = 27;
    public const int BaseUltimateDamage = 50;
    public const int DeadlyUltimateDamage = 55;
    public const int MicroLightsPerDraw = 5;
    public const decimal VeilDamageMultiplier = 0.2m;
    public const decimal TransitionHpRatio = 0.6m;
    public const int VeilBreakPlayerSides = 2;
    public const int MaximumNight = 5;

    public static int LightThreshold(int combatStartPlayerCount) =>
        3 * Math.Max(1, combatStartPlayerCount);

    public static int AttackDamage(DarkRegularAction action, int night, bool deadly) =>
        (action == DarkRegularAction.Confinement
            ? deadly ? DeadlyConfinementDamage : BaseConfinementDamage
            : deadly ? DeadlyNonConfinementDamage : BaseNonConfinementDamage)
        + 5 * Math.Max(0, night - 1);

    public static int Block(int night) => 12 + 3 * Math.Max(0, night - 1);

    public static (int Weak, int Frail) ConfinementDebuffs(int night) => night switch
    {
        >= 4 => (1, 1),
        >= 2 => (1, 0),
        _ => (0, 0)
    };

    public static int ConsumeAggregateLight(IList<int> lightByPlayer, int amount)
    {
        var remaining = Math.Max(0, amount);
        for (var index = 0; index < lightByPlayer.Count && remaining > 0; index++)
        {
            var consumed = Math.Min(Math.Max(0, lightByPlayer[index]), remaining);
            lightByPlayer[index] -= consumed;
            remaining -= consumed;
        }

        return amount - remaining;
    }

    public static DarkRegularAction Toggle(DarkRegularAction action) =>
        action == DarkRegularAction.Confinement
            ? DarkRegularAction.NonConfinement
            : DarkRegularAction.Confinement;
}
