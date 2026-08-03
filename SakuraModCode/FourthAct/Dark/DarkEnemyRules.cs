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
    public const int MicroLightsPerDraw = 3;
    public const int MaxHandSizeReduction = 2;
    public const int InitialVeilLayers = 3;
    public const int MicroLightThreshold = 3;
    public const decimal InitialVeilDamageReduction = 0.75m;
    public const decimal VeilDamageReductionPerLayer = InitialVeilDamageReduction / InitialVeilLayers;
    public const decimal TransitionHpRatio = 0.6m;
    public const int VeilBreakPlayerSides = 2;
    public const int MaximumNight = 5;

    public static int VisibleNightRegions(int nightAmount) =>
        Math.Clamp(nightAmount, 0, MaximumNight);

    public static decimal VeilDamageMultiplier(int veilLayers) =>
        1m - VeilDamageReductionPerLayer * Math.Clamp(veilLayers, 0, InitialVeilLayers);

    public static int ModifyMaxHandSize(int currentMaxHandSize) =>
        Math.Max(0, currentMaxHandSize - MaxHandSizeReduction);

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

    public static int ConsumeMicroLight(int currentMicroLight, int amount) =>
        Math.Max(0, currentMicroLight - Math.Max(0, amount));

    public static int MicroLightsFromAction(DarkRegularAction action) => action switch
    {
        DarkRegularAction.Confinement or DarkRegularAction.NonConfinement => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static bool AdvertisesStatusIntent(DarkRegularAction action) => action switch
    {
        DarkRegularAction.Confinement or DarkRegularAction.NonConfinement => false,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static DarkRegularAction Toggle(DarkRegularAction action) =>
        action == DarkRegularAction.Confinement
            ? DarkRegularAction.NonConfinement
            : DarkRegularAction.Confinement;
}
