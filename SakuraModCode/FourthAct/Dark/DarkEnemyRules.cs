namespace SakuraMod.SakuraModCode.FourthAct.Dark;

public enum DarkRegularAction { Confinement, NonConfinement }

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
    public const int MicroLightsPerDraw = 2;
    public const int MaxHandSizeReduction = 2;
    public const int DarknessMaximum = 5;
    public const int DarknessReset = 3;
    public const decimal DarknessDamageReductionPerLayer = 0.2m;
    public const int DarknessAttackBonusPerLayer = 5;
    public const decimal TransitionHpRatio = 0.6m;
    public const int NightBlock = 12;

    public static int ClampDarkness(int value) => Math.Clamp(value, 1, DarknessMaximum);
    public static int ChangeDarkness(int current, int delta) => ClampDarkness(current + delta);
    public static decimal DarknessDamageMultiplier(int darkness) =>
        1m - DarknessDamageReductionPerLayer * ClampDarkness(darkness);
    public static int ModifyMaxHandSize(int currentMaxHandSize) => Math.Max(0, currentMaxHandSize - MaxHandSizeReduction);
    public static int AttackDamage(DarkRegularAction action, int darkness, bool deadly) =>
        (action == DarkRegularAction.Confinement
            ? deadly ? DeadlyConfinementDamage : BaseConfinementDamage
            : deadly ? DeadlyNonConfinementDamage : BaseNonConfinementDamage)
        + DarknessAttackBonusPerLayer * Math.Max(0, ClampDarkness(darkness) - 1);
    public static int UltimateDamage(bool deadly) => deadly ? DeadlyUltimateDamage : BaseUltimateDamage;
    public static bool ShouldUseUltimate(int darkness) => ClampDarkness(darkness) >= DarknessMaximum;
    public static DarkRegularAction Toggle(DarkRegularAction action) =>
        action == DarkRegularAction.Confinement ? DarkRegularAction.NonConfinement : DarkRegularAction.Confinement;
}
