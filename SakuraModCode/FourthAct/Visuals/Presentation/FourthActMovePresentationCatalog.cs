using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Earth.Models;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Water.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using Cue = SakuraMod.SakuraModCode.FourthAct.Visuals.FourthActAudioCue;
using Fx = SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation.FourthActFxId;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

// How one fourth-act move looks and sounds. Attacks hit with ImpactFx (default: the
// element impact) and ImpactCue on every hit; other moves play ImpactFx on the targets the
// move passes after it resolves. The standee never moves: Charge lights its rim while the
// move winds up, and Pose swaps in its action pose when it has one.
//
// CastFx plays on the caster at CastAnchor; TravelFx then flies (or aims) from there at the
// target, and the move resolves once it arrives. ImpactCycle replaces ImpactFx with one
// variant per hit, starting at the variant the move passes (a pan, a fireball's size).
// A travel lands at ImpactAnchor; Shake runs the native screen shake on every hit.
internal sealed record FourthActMovePresentation(
    FourthActElement Element,
    FourthActAudioCue? CastCue = null,
    FourthActAudioCue? ImpactCue = null,
    FourthActFxId? CastFx = null,
    FourthActFxId? ImpactFx = null,
    bool Pose = false,
    bool Charge = true,
    FourthActFxId? TravelFx = null,
    IReadOnlyList<FourthActFxId>? ImpactCycle = null,
    FourthActFxAnchor CastAnchor = FourthActFxAnchor.Center,
    FourthActFxAnchor ImpactAnchor = FourthActFxAnchor.Center,
    ShakeStrength Shake = ShakeStrength.None)
{
    internal FourthActFxId ImpactFor(int variant, int hit) =>
        ImpactCycle is { Count: > 0 } cycle
            ? cycle[(variant + hit) % cycle.Count]
            : ImpactFx ?? FourthActElements.Profile(Element).Impact;
}

internal static class FourthActMovePresentationCatalog
{
    // Presentations played inside a move rather than as one: Shadow's echo resolves one of
    // four sub-actions per player, and Dark's stun recovery has no MoveState of its own.
    internal const string ShadowClaws = "SHADOW_CLAWS";
    internal const string ShadowBite = "SHADOW_BITE";
    internal const string ShadowVeil = "SHADOW_VEIL";
    internal const string ShadowSurge = "SHADOW_SURGE";
    internal const string DarkTransition = "DARK_TRANSITION";
    internal const string BenedictionHeal = "BENEDICTION_HEAL";

    // Long-lived layers a monster keeps while it is alive (FourthActStandeeMaterial owns them).
    private static readonly Dictionary<Type, FourthActFxId> Ambients = new()
    {
        [typeof(FireyMonster)] = FourthActFxId.FireHeatHaze,
        [typeof(LightMonster)] = FourthActFxId.LightRays,
        [typeof(ShadowMonster)] = FourthActFxId.ShadowPool,
        [typeof(WoodMonster)] = FourthActFxId.FallingLeaves,
        [typeof(EarthyMonster)] = FourthActFxId.SovereignDust,
        [typeof(FreezeMonster)] = FourthActFxId.FrostMist,
        [typeof(RainMonster)] = FourthActFxId.RainFall,
        [typeof(WateryMonster)] = FourthActFxId.WaterSurface,
        [typeof(FlyMonster)] = FourthActFxId.SoarCurrents,
        [typeof(WindyMonster)] = FourthActFxId.GaleStreaks,
        [typeof(DarkMonster)] = FourthActFxId.DarkVeil,
        [typeof(ClassicTheNothingMonster)] = FourthActFxId.NothingSphere
    };

    // Effects a monster shows outside its moves (Windy's bind and wall, Sleep's powder, Micro
    // Light and the full-Darkness corona, The Nothing's release),
    // preloaded with the monster like everything else it shows.
    private static readonly Dictionary<Type, FourthActFxId[]> PowerFx = new()
    {
        [typeof(WindyMonster)] = [FourthActFxId.BindCoil, FourthActFxId.BindReturn, FourthActFxId.WindWallShield,
            FourthActFxId.WindWallDeflect],
        [typeof(SleepMonster)] = [FourthActFxId.SleepPowder],
        [typeof(DarkMonster)] = [FourthActFxId.MicroLightMote, FourthActFxId.VeilPierce, FourthActFxId.DarkCorona],
        [typeof(ClassicTheNothingMonster)] = [FourthActFxId.NothingRelease]
    };

    private static readonly Dictionary<Type, FourthActElement> Elements = new()
    {
        [typeof(FlyMonster)] = FourthActElement.Wind,
        [typeof(IllusionMonster)] = FourthActElement.Wind,
        [typeof(IllusionProjectionMonster)] = FourthActElement.Wind,
        [typeof(WindyMonster)] = FourthActElement.Wind,
        [typeof(DashMonster)] = FourthActElement.Wind,
        [typeof(FloatMonster)] = FourthActElement.Wind,
        [typeof(SleepMonster)] = FourthActElement.Wind,
        [typeof(FreezeMonster)] = FourthActElement.Ice,
        [typeof(RainMonster)] = FourthActElement.Water,
        [typeof(WateryMonster)] = FourthActElement.Water,
        [typeof(SwordMonster)] = FourthActElement.Blade,
        [typeof(LibraPanMonster)] = FourthActElement.Light,
        [typeof(FireyMonster)] = FourthActElement.Fire,
        [typeof(LightMonster)] = FourthActElement.Light,
        [typeof(ShadowMonster)] = FourthActElement.Dark,
        [typeof(WoodMonster)] = FourthActElement.Wood,
        [typeof(EarthyMonster)] = FourthActElement.Earth,
        [typeof(DarkMonster)] = FourthActElement.Dark,
        [typeof(ClassicTheNothingMonster)] = FourthActElement.Nothing
    };

    private static readonly Dictionary<Type, Dictionary<string, FourthActMovePresentation>> Moves = Build();

    internal static IReadOnlyCollection<Type> MonsterTypes => Elements.Keys;

    internal static IReadOnlySet<string> SubActionIds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ShadowClaws, ShadowBite, ShadowVeil, ShadowSurge, DarkTransition, BenedictionHeal
    };

    internal static FourthActFxId? AmbientOf(Type monster) =>
        Ambients.TryGetValue(monster, out var ambient) ? ambient : null;

    internal static FourthActElement ElementOf(Type monster) =>
        Elements.TryGetValue(monster, out var element)
            ? element
            : throw new KeyNotFoundException($"{monster.Name} has no fourth-act element.");

    internal static IReadOnlyDictionary<string, FourthActMovePresentation> MovesOf(Type monster) =>
        Moves.TryGetValue(monster, out var moves) ? moves : new Dictionary<string, FourthActMovePresentation>();

    // Missing entries fail open to the element floor so a new move still plays something;
    // the catalog suite keeps that path unreachable for shipped moves.
    internal static FourthActMovePresentation Get(Type monster, string id)
    {
        if (Moves.TryGetValue(monster, out var moves) && moves.TryGetValue(id, out var presentation))
            return presentation;

        MainFile.Logger.Warn($"Fourth-act move {monster.Name}.{id} has no presentation entry.");
        return new FourthActMovePresentation(ElementOf(monster));
    }

    internal static IEnumerable<FourthActFxId> FxOf(Type monster)
    {
        yield return FourthActElements.Profile(ElementOf(monster)).Impact;
        foreach (var presentation in MovesOf(monster).Values)
        {
            if (presentation.CastFx is { } cast)
                yield return cast;
            if (presentation.ImpactFx is { } impact)
                yield return impact;
            if (presentation.TravelFx is { } travel)
                yield return travel;
            foreach (var variant in presentation.ImpactCycle ?? [])
                yield return variant;
        }

        if (AmbientOf(monster) is { } ambient)
            yield return ambient;
        foreach (var fx in PowerFx.GetValueOrDefault(monster) ?? [])
            yield return fx;
    }

    // Preloaded through the native MonsterModel.AssetPaths before the room opens.
    internal static IEnumerable<string> AssetPathsFor(Type monster) =>
        FxOf(monster)
            .Distinct()
            .Select(FourthActFxScenes.PathFor)
            .Append(FourthActElements.StandeeShaderPath(ElementOf(monster)));

    private static Dictionary<Type, Dictionary<string, FourthActMovePresentation>> Build()
    {
        var wind = FourthActElement.Wind;
        var water = FourthActElement.Water;
        var ice = FourthActElement.Ice;
        var fire = FourthActElement.Fire;
        var light = FourthActElement.Light;
        var blade = FourthActElement.Blade;
        var earth = FourthActElement.Earth;
        var wood = FourthActElement.Wood;
        var dark = FourthActElement.Dark;
        var nothing = FourthActElement.Nothing;
        // Windy's first and second light attacks look the same.
        var gusts = new FourthActMovePresentation(wind, Cue.WindAttack, Cue.SlashHit, Fx.GaleGather,
            Pose: true, ImpactCycle: [Fx.WindBladeA, Fx.WindBladeB]);
        var gust = new FourthActMovePresentation(wind, Cue.WindAttack, Cue.BluntHit, Fx.GaleGather, Fx.GustBurst,
            Pose: true, TravelFx: Fx.GustBolt);

        return new Dictionary<Type, Dictionary<string, FourthActMovePresentation>>
        {
            [typeof(FlyMonster)] = Entries(
                ("HIGH_ATTACK", new(wind, Cue.WindAttack, Cue.SlashHit, Fx.WindGather,
                    ImpactCycle: [Fx.WindBladeA, Fx.WindBladeB])),
                ("DIVE", new(wind, Cue.HeavyWindAttack, Cue.HeavyHit, Fx.DiveSpiral, Fx.DiveImpact,
                    ImpactAnchor: FourthActFxAnchor.Feet, Shake: ShakeStrength.Medium)),
                ("TAKEOFF", new(wind, Cue.WindTakeoff, CastFx: Fx.UpdraftLift, Charge: false, CastAnchor: FourthActFxAnchor.Feet))),
            [typeof(IllusionMonster)] = Entries(
                ("BEGUILING_STRIKE", new(wind, Cue.IllusionAttack, Cue.BluntHit, Fx.MirageRipple, Fx.MirageShatter, Pose: true)),
                ("LURED_FALL", new(wind, Cue.IllusionAttack, Cue.HeavyHit, Fx.MirageRipple, Fx.LureVortex, Pose: true,
                    ImpactAnchor: FourthActFxAnchor.Feet, Shake: ShakeStrength.Weak)),
                ("REWEAVE_BEFORE_LURED", new(wind, Cue.IllusionReweave, CastFx: Fx.ReweaveMist, Pose: true)),
                ("REWEAVE_BEFORE_BEGUILING", new(wind, Cue.IllusionReweave, CastFx: Fx.ReweaveMist, Pose: true))),
            [typeof(IllusionProjectionMonster)] = Entries(
                ("PROJECTION", new(wind, Charge: false))),
            [typeof(WindyMonster)] = Entries(
                ("FIRST_GUSTS", gusts),
                ("FIRST_GUST", gust),
                ("SECOND_GUSTS", gusts),
                ("SECOND_GUST", gust),
                ("SUMMON_ATTENDANT", new(wind, Cue.WindySummon, CastFx: Fx.SummonSwirl, Pose: true)),
                ("HEAVY_GALE", new(wind, Cue.HeavyWindAttack, Cue.HeavyHit, Fx.TornadoForm, Fx.TornadoHit, Pose: true,
                    TravelFx: Fx.TornadoTravel, CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet,
                    Shake: ShakeStrength.Strong))),
            [typeof(DashMonster)] = Entries(
                ("DASH", new(wind, Cue.WindAttack, Cue.SlashHit, Fx.DashAfterimage, Fx.DashSlash, Pose: true))),
            // Float passes Windy as the target: the updraft flies there and the guard lands on it.
            [typeof(FloatMonster)] = Entries(
                ("FLOAT", new(wind, Cue.Buff, ImpactFx: Fx.FloatGuard, TravelFx: Fx.FloatUpdraft))),
            [typeof(SleepMonster)] = Entries(
                ("SLEEP", new(wind, Cue.SleepCast, CastFx: Fx.SleepDust, Pose: true))),

            [typeof(FreezeMonster)] = Entries(
                ("HEAVY_STRIKE", new(ice, Cue.FrostCast, Cue.HeavyHit, Fx.IceGather,
                    ImpactCycle: [Fx.IceSpikeA, Fx.IceSpikeB])),
                ("COLD_BLOW", new(ice, Cue.FrostCast, Cue.BluntHit, Fx.FrostGather, Fx.FrostBlast)),
                ("HEAVY_STRIKE_SECOND", new(ice, Cue.FrostCast, Cue.HeavyHit, Fx.IceGather,
                    ImpactCycle: [Fx.IceSpikeA, Fx.IceSpikeB])),
                ("ICE_FORMATION", new(ice, Cue.IceForm, ImpactFx: Fx.IceWall, ImpactAnchor: FourthActFxAnchor.Feet))),
            [typeof(RainMonster)] = Entries(
                ("DOWNPOUR", new(water, Cue.WaterWave, Cue.BluntHit, Fx.RainCloud, Fx.DownpourSplash,
                    CastAnchor: FourthActFxAnchor.Head, ImpactAnchor: FourthActFxAnchor.Feet)),
                ("CLOUD_COVER", new(water, Cue.WaterWave, ImpactFx: Fx.CloudVeil)),
                ("FLOOD", new(water, Cue.WaterEruption, Cue.HeavyHit, Fx.FloodRise, Fx.FloodCrash, TravelFx: Fx.FloodWave,
                    CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet, Shake: ShakeStrength.Medium))),
            // The dragon's reservoir hit flies again through FourthActMoveFx.TravelAsync.
            [typeof(WateryMonster)] = Entries(
                ("TIDAL_DRAW", new(water, Cue.TidalPull, Cue.BluntHit, Fx.TidalVortex, Fx.TidalDrain)),
                ("WATER_DRAGON", new(water, Cue.WaterEruption, Cue.HeavyHit, Fx.DragonRise, Fx.DragonSplash,
                    TravelFx: Fx.WaterDragon, Shake: ShakeStrength.Medium)),
                ("TIDAL_DRAW_SECOND", new(water, Cue.TidalPull, Cue.BluntHit, Fx.TidalVortex, Fx.TidalDrain)),
                ("WATER_DRAGON_SECOND", new(water, Cue.WaterEruption, Cue.HeavyHit, Fx.DragonRise, Fx.DragonSplash,
                    TravelFx: Fx.WaterDragon, Shake: ShakeStrength.Medium)),
                ("FLOOD", new(water, Cue.WaterStomp, Cue.HeavyHit, Fx.FloodRise, Fx.FloodSurge,
                    CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet, Shake: ShakeStrength.Weak))),

            [typeof(SwordMonster)] = Entries(
                ("SLASH", new(blade, Cue.BladeSlash, Cue.SlashHit, Fx.SwordGlint, Fx.SwordSlash)),
                ("DOUBLE_CUT", new(blade, Cue.BladeDoubleCut, Cue.SlashHit, Fx.SwordGlint,
                    ImpactCycle: [Fx.SwordCutDown, Fx.SwordCutUp]))),
            // Variant 0 is the moon (left) pan, 1 the sun (right) pan.
            [typeof(LibraPanMonster)] = Entries(
                ("WEIGH", new(light, Cue.LibraWeigh, Cue.HeavyHit,
                    ImpactCycle: [Fx.LibraMoonWeight, Fx.LibraSunWeight], ImpactAnchor: FourthActFxAnchor.Head))),
            [typeof(FireyMonster)] = Entries(
                ("FLAME_BREATH", new(fire, Cue.FlameBreath, CastFx: Fx.FireCore, TravelFx: Fx.FlameStream)),
                ("ARSON", new(fire, Cue.FireCast, CastFx: Fx.FireCore, ImpactFx: Fx.FireImpact, TravelFx: Fx.Firebrand)),
                ("KINDLE", new(fire, Cue.Buff, CastFx: Fx.FireRing, ImpactFx: Fx.EmberRise, ImpactAnchor: FourthActFxAnchor.Feet)),
                // Variant: 0/1/2 by the target's Burn count.
                ("FIREBALL", new(fire, Cue.FireballCast, Cue.FireImpact, Fx.FireballCharge,
                    TravelFx: Fx.FireballShot, CastAnchor: FourthActFxAnchor.Head,
                    ImpactCycle: [Fx.FireballBlastSmall, Fx.FireballBlastMedium, Fx.FireballBlastLarge]))),
            [typeof(LightMonster)] = Entries(
                ("RADIANCE", new(light, Cue.LightCast, Cue.BluntHit, Fx.LightHalo, Fx.LightBurst, CastAnchor: FourthActFxAnchor.Head)),
                ("BENEDICTION", new(light, Cue.LightCast, Cue.BluntHit, Fx.LightHalo, Fx.LightBurst, CastAnchor: FourthActFxAnchor.Head)),
                (BenedictionHeal, new(light, Cue.LightHeal, ImpactFx: Fx.LightPillar, Charge: false,
                    ImpactAnchor: FourthActFxAnchor.Feet)),
                // Variant: 0/1/2 by the target's hand size.
                ("JUDGMENT", new(light, Cue.LightBeam, Cue.HeavyHit, ImpactAnchor: FourthActFxAnchor.Feet,
                    ImpactCycle: [Fx.JudgmentBeamNarrow, Fx.JudgmentBeamWide, Fx.JudgmentBeamFull]))),

            [typeof(ShadowMonster)] = Entries(
                ("SHADOW_ECHO", new(dark, Charge: false)),
                (ShadowClaws, new(dark, Cue.ShadowClaws, Cue.SlashHit, Fx.ShadowClawGather,
                    ImpactCycle: [Fx.ShadowRakeDown, Fx.ShadowRakeUp])),
                (ShadowBite, new(dark, Cue.ShadowBite, Cue.BluntHit, Fx.ShadowEyes, Fx.ShadowFangs,
                    CastAnchor: FourthActFxAnchor.Head)),
                (ShadowVeil, new(dark, Cue.ShadowVeil, ImpactFx: Fx.ShadowVeilMist, ImpactAnchor: FourthActFxAnchor.Feet)),
                (ShadowSurge, new(dark, Cue.ShadowSurge, ImpactFx: Fx.ShadowSurgeFlame, ImpactAnchor: FourthActFxAnchor.Feet))),
            [typeof(WoodMonster)] = Entries(
                // Variant: 0/1/2 by the target's Rooted (exhausted) count.
                ("TWINING_STRIKE", new(wood, Cue.WoodLash, Cue.BluntHit, Fx.VineSprout,
                    CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet,
                    ImpactCycle: [Fx.VineSnareSmall, Fx.VineSnareMedium, Fx.VineSnareLarge])),
                ("VITAL_SPROUT", new(wood, Cue.WoodGrow, CastFx: Fx.LeafSwirl, ImpactFx: Fx.SproutBloom,
                    ImpactAnchor: FourthActFxAnchor.Feet))),
            [typeof(EarthyMonster)] = Entries(
                // Variant: 0/1/2 by the target's Sediment.
                ("TREMOR", new(earth, Cue.EarthStomp, Cue.HeavyHit, Fx.GroundShock,
                    CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet, Shake: ShakeStrength.Weak,
                    ImpactCycle: [Fx.FissureSmall, Fx.FissureMedium, Fx.FissureLarge])),
                ("ROCKFALL", new(earth, Cue.RockAttack, Cue.BluntHit, Fx.DebrisLift, Shake: ShakeStrength.VeryWeak,
                    ImpactCycle: [Fx.RockDropA, Fx.RockDropB])),
                ("QUAKE_CHARGE", new(earth, Cue.EarthCharge, ImpactFx: Fx.EarthArmor)),
                ("LANDSLIDE", new(earth, Cue.EarthPlow, Cue.LandslideSlam, Fx.DustChurn, Fx.LandslideBurst,
                    TravelFx: Fx.LandslideWave, CastAnchor: FourthActFxAnchor.Feet, ImpactAnchor: FourthActFxAnchor.Feet,
                    Shake: ShakeStrength.Strong))),

            [typeof(DarkMonster)] = Entries(
                ("CONFINEMENT", new(dark, Cue.DarkBind, Cue.BluntHit, Fx.ChainRing, Fx.ShadowChains, Pose: true)),
                ("NON_CONFINEMENT", new(dark, Cue.DarkAttack, Cue.SlashHit, Fx.NightBladeGather, Fx.NightCrescent, Pose: true)),
                ("ULTIMATE", new(dark, Cue.DarkEclipse, Cue.HeavyHit, Fx.EclipseOrb, Fx.EclipseCollapse, Pose: true,
                    CastAnchor: FourthActFxAnchor.Head, Shake: ShakeStrength.Strong)),
                (DarkTransition, new(dark, Cue.DarkTransition, ImpactFx: Fx.VeilReform, Pose: true))),

            // Everything leaves her sphere: its boundary sweeps out, bulges, pulses, closes in or
            // peels off, and what it touches on Sakura is erased flat.
            [typeof(ClassicTheNothingMonster)] = Entries(
                ("START_VOID", new(nothing, Cue.NothingCast, Cue.NothingSeal, Fx.NothingBoundaryExpand, Fx.NothingArcSeal,
                    TravelFx: Fx.NothingSweep)),
                ("THUMP", new(nothing, Cue.NothingHeavyCast, Cue.NothingErase, Fx.NothingSwell, Fx.NothingErase,
                    Shake: ShakeStrength.Medium)),
                ("BATTER", new(nothing, Cue.NothingCast, Cue.BluntHit, Fx.NothingPulse, Shake: ShakeStrength.VeryWeak,
                    ImpactCycle: [Fx.NothingEraseSmallA, Fx.NothingEraseSmallB])),
                ("DESTRUCTION", new(nothing, Cue.NothingHeavyCast, Cue.NothingErase, Fx.NothingSwell, Fx.NothingErase,
                    Shake: ShakeStrength.Medium)),
                ("STRENGTHEN", new(nothing, Cue.NothingIntangible, CastFx: Fx.NothingGather)),
                ("WEAKEN", new(nothing, Cue.NothingDebuff, CastFx: Fx.NothingPeel, ImpactFx: Fx.NothingBind)))
        };
    }

    private static Dictionary<string, FourthActMovePresentation> Entries(
        params (string Id, FourthActMovePresentation Presentation)[] entries) =>
        entries.ToDictionary(static entry => entry.Id, static entry => entry.Presentation, StringComparer.Ordinal);
}
