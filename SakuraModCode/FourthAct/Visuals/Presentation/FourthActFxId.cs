using System.Text;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

// One scene per id under scenes/vfx/fourth_act/<group>/. Element impacts are the floor every
// attack falls back to; route tasks add their own ids (casts, signature moves) beside them.
internal enum FourthActFxId
{
    WindImpact,
    WaterImpact,
    IceImpact,
    FireImpact,
    LightImpact,
    BladeImpact,
    EarthImpact,
    WoodImpact,
    DarkImpact,

    // Fire / Light route.
    SwordGlint,
    SwordSlash,
    SwordCutDown,
    SwordCutUp,
    LibraMoonWeight,
    LibraSunWeight,
    FireCore,
    FlameStream,
    Firebrand,
    FireRing,
    EmberRise,
    FireballCharge,
    FireballShot,
    FireballBlastSmall,
    FireballBlastMedium,
    FireballBlastLarge,
    FireHeatHaze,
    LightHalo,
    LightBurst,
    LightPillar,
    JudgmentBeamNarrow,
    JudgmentBeamWide,
    JudgmentBeamFull,
    LightRays,

    // Earth route.
    ShadowClawGather,
    ShadowRakeDown,
    ShadowRakeUp,
    ShadowEyes,
    ShadowFangs,
    ShadowVeilMist,
    ShadowSurgeFlame,
    ShadowPool,
    VineSprout,
    VineSnareSmall,
    VineSnareMedium,
    VineSnareLarge,
    LeafSwirl,
    SproutBloom,
    FallingLeaves,
    GroundShock,
    FissureSmall,
    FissureMedium,
    FissureLarge,
    DebrisLift,
    RockDropA,
    RockDropB,
    EarthArmor,
    DustChurn,
    LandslideWave,
    LandslideBurst,
    SovereignDust,

    // Water route.
    IceGather,
    IceSpikeA,
    IceSpikeB,
    FrostGather,
    FrostBlast,
    IceWall,
    FrostMist,
    RainCloud,
    DownpourSplash,
    CloudVeil,
    FloodRise,
    FloodWave,
    FloodCrash,
    RainFall,
    TidalVortex,
    TidalDrain,
    DragonRise,
    WaterDragon,
    DragonSplash,
    FloodSurge,
    WaterSurface,

    // Wind route.
    WindBladeA,
    WindBladeB,
    WindGather,
    DiveSpiral,
    DiveImpact,
    UpdraftLift,
    SoarCurrents,
    MirageRipple,
    MirageShatter,
    LureVortex,
    ReweaveMist,
    GaleGather,
    GustBolt,
    GustBurst,
    TornadoForm,
    TornadoTravel,
    TornadoHit,
    SummonSwirl,
    BindCoil,
    BindReturn,
    WindWallShield,
    WindWallDeflect,
    GaleStreaks,
    DashAfterimage,
    DashSlash,
    FloatUpdraft,
    FloatGuard,
    SleepDust,
    SleepPowder,

    // Dark route.
    ChainRing,
    ShadowChains,
    NightBladeGather,
    NightCrescent,
    EclipseOrb,
    EclipseCollapse,
    VeilReform,
    MicroLightMote,
    VeilPierce,
    DarkVeil,
    DarkCorona
}

// Where on a creature an effect is placed.
internal enum FourthActFxAnchor
{
    Center,
    Head,
    Feet
}

internal static class FourthActFxScenes
{
    private const string Root = MainFile.ResPath + "/scenes/vfx/fourth_act";

    internal static string PathFor(FourthActFxId id) => $"{Root}/{GroupOf(id)}/{SnakeCase(id)}.tscn";

    private static string GroupOf(FourthActFxId id) => id switch
    {
        <= FourthActFxId.DarkImpact => "common",
        <= FourthActFxId.LightRays => "fire_light",
        <= FourthActFxId.SovereignDust => "earth",
        <= FourthActFxId.WaterSurface => "water",
        <= FourthActFxId.SleepPowder => "wind",
        <= FourthActFxId.DarkCorona => "dark",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    private static string SnakeCase(FourthActFxId id)
    {
        var name = id.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            if (char.IsUpper(name[index]) && index > 0)
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(name[index]));
        }

        return builder.ToString();
    }
}
