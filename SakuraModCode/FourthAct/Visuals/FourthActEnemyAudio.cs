using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum FourthActAudioCue
{
    // Wind
    WindAttack,
    HeavyWindAttack,
    IllusionAttack,
    WindTakeoff,
    WindySummon,
    IllusionReweave,
    FlyLanding,
    SleepCast,
    WindWallBlock,

    // Water / Ice
    WaterWave,
    TidalPull,
    WaterEruption,
    WaterStomp,
    FrostCast,
    IceForm,

    // Fire / Light / Blade
    FireCast,
    FlameBreath,
    FireballCast,
    FireImpact,
    LightCast,
    LightHeal,
    LightBeam,
    LibraWeigh,
    BladeSlash,
    BladeDoubleCut,

    // Earth / Wood / Shadow
    EarthStomp,
    RockAttack,
    EarthCharge,
    EarthPlow,
    LandslideSlam,
    WoodLash,
    WoodGrow,
    ShadowClaws,
    ShadowBite,
    ShadowVeil,
    ShadowSurge,

    // Dark
    DarkAttack,
    DarkTransition,
    DarkBind,
    DarkEclipse,

    // Generic
    Buff,
    BluntHit,
    HeavyHit,
    SlashHit,

    // Element deaths
    WindDeath,
    WaterDeath,
    IceDeath,
    FireDeath,
    LightDeath,
    BladeDeath,
    EarthDeath,
    WoodDeath,
    DarkDeath
}

// The only path table for fourth-act enemy audio. A cue resolves to one of three sources,
// told apart by prefix: a vanilla FMOD event ("event:/"), a mod resource ("res://"), or a
// vanilla plain hit sound played through the debug audio manager (a bare file name).
internal static class FourthActEnemyAudio
{
    private const string Enemy = "event:/sfx/enemy/enemy_attacks";
    private const string Local = MainFile.ResPath + "/sfx/fourth_act";

    internal static string PathFor(FourthActAudioCue cue) => cue switch
    {
        FourthActAudioCue.WindAttack => $"{Enemy}/living_fog/living_fog_attack_blow",
        FourthActAudioCue.HeavyWindAttack => $"{Enemy}/soul_fysh/soul_fysh_wave",
        FourthActAudioCue.IllusionAttack => $"{Enemy}/obscura/obscura_attack",
        FourthActAudioCue.WindTakeoff => $"{Enemy}/thieving_hopper/thieving_hopper_take_off",
        FourthActAudioCue.WindySummon => $"{Enemy}/obscura/obscura_summon",
        FourthActAudioCue.IllusionReweave => $"{Enemy}/obscura/obscura_summon",
        FourthActAudioCue.FlyLanding => $"{Local}/fly_landing.ogg",
        FourthActAudioCue.SleepCast => $"{Local}/sleep_cast.ogg",
        FourthActAudioCue.WindWallBlock => $"{Local}/wind_wall_block.ogg",

        FourthActAudioCue.WaterWave => $"{Enemy}/soul_fysh/soul_fysh_wave",
        FourthActAudioCue.TidalPull => $"{Enemy}/soul_fysh/soul_fysh_beckon",
        FourthActAudioCue.WaterEruption => $"{Enemy}/waterfall_giant/waterfall_giant_eruption",
        FourthActAudioCue.WaterStomp => $"{Enemy}/waterfall_giant/waterfall_giant_attack_stomp",
        FourthActAudioCue.FrostCast => "event:/sfx/characters/defect/defect_frost_channel",
        FourthActAudioCue.IceForm => "event:/sfx/characters/defect/defect_glass_channel",

        FourthActAudioCue.FireCast => $"{Enemy}/knowledge_demon/knowledge_demon_flame",
        FourthActAudioCue.FlameBreath => $"{Enemy}/mechaknight/mechaknight_flamethrower",
        FourthActAudioCue.FireballCast => $"{Enemy}/spectral_knight/spectral_knight_soul_flame",
        FourthActAudioCue.FireImpact => "event:/sfx/characters/attack_fire",
        FourthActAudioCue.LightCast => "event:/sfx/characters/regent/regent_guiding_star",
        FourthActAudioCue.LightHeal => $"{Enemy}/obscura/obscura_hologram_heal",
        FourthActAudioCue.LightBeam => "event:/sfx/characters/defect/defect_hyperbeam",
        FourthActAudioCue.LibraWeigh => "event:/sfx/characters/regent/regent_forge",
        FourthActAudioCue.BladeSlash => $"{Enemy}/the_kin_minion/the_kin_minion_quick_slash",
        FourthActAudioCue.BladeDoubleCut => $"{Enemy}/the_kin_minion/the_kin_minion_boomerang_slash",

        FourthActAudioCue.EarthStomp => $"{Enemy}/waterfall_giant/waterfall_giant_attack_kick",
        FourthActAudioCue.RockAttack => $"{Enemy}/workbug_rock/workbug_rock_attack",
        FourthActAudioCue.EarthCharge => $"{Enemy}/cubex_construct/cubex_construct_charge_attack",
        FourthActAudioCue.EarthPlow => $"{Enemy}/ceremonial_beast/ceremonial_beast_plow",
        FourthActAudioCue.LandslideSlam => $"{Enemy}/lagavulin_matriarch/lagavulin_matriarch_slam",
        FourthActAudioCue.WoodLash => $"{Enemy}/vine_shambler/vine_shambler_grasping_vines",
        FourthActAudioCue.WoodGrow => $"{Enemy}/orb_plant/orb_plant_cast",
        FourthActAudioCue.ShadowClaws => $"{Enemy}/test_subject/test_subject_slash",
        FourthActAudioCue.ShadowBite => $"{Enemy}/phantasmal_gardeners/phantasmal_gardeners_attack_bite",
        FourthActAudioCue.ShadowVeil => $"{Enemy}/soul_fysh/soul_fysh_intangible",
        FourthActAudioCue.ShadowSurge => $"{Enemy}/obscura/obscura_buff",

        FourthActAudioCue.DarkAttack => $"{Enemy}/spectral_knight/spectral_knight_soul_slash",
        FourthActAudioCue.DarkTransition => $"{Enemy}/spectral_knight/spectral_knight_hex",
        FourthActAudioCue.DarkBind => "event:/sfx/characters/defect/defect_dark_channel",
        FourthActAudioCue.DarkEclipse => $"{Enemy}/magi_knight/magi_knight_attack_bomb",

        FourthActAudioCue.Buff => "event:/sfx/buff",
        FourthActAudioCue.BluntHit => "blunt_attack.mp3",
        FourthActAudioCue.HeavyHit => "heavy_attack.mp3",
        FourthActAudioCue.SlashHit => "slash_attack.mp3",

        FourthActAudioCue.WindDeath => $"{Enemy}/living_fog/living_fog_die",
        FourthActAudioCue.WaterDeath => $"{Enemy}/cultists/cultists_die_damp",
        FourthActAudioCue.IceDeath => $"{Enemy}/cultists/cultists_die_calcified",
        FourthActAudioCue.FireDeath => "event:/sfx/enemy/enemy_fade",
        FourthActAudioCue.LightDeath => $"{Enemy}/obscura/obscura_hologram_die",
        FourthActAudioCue.BladeDeath => "event:/sfx/enemy/enemy_fade",
        FourthActAudioCue.EarthDeath => $"{Enemy}/workbug_rock/workbug_rock_die",
        FourthActAudioCue.WoodDeath => $"{Enemy}/phantasmal_gardeners/phantasmal_gardeners_die",
        FourthActAudioCue.DarkDeath => $"{Enemy}/obscura/obscura_die",
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
    };

    internal static bool IsEvent(string path) => path.StartsWith("event:/", StringComparison.Ordinal);

    internal static bool IsResource(string path) => path.StartsWith("res://", StringComparison.Ordinal);

    // The (sfx, tmpSfx) pair native AttackCommand.WithHitFx takes, so a hit sound plays on
    // every hit of a multi-hit attack exactly like a vanilla monster's.
    internal static (string? Event, string? PlainSound) HitArguments(FourthActAudioCue cue)
    {
        var path = PathFor(cue);
        if (IsResource(path))
            throw new InvalidOperationException($"Hit cue {cue} must be a vanilla sound, not {path}.");
        return IsEvent(path) ? (path, null) : (null, path);
    }

    internal static void Play(FourthActAudioCue cue)
    {
        if (TestMode.IsOn)
            return;

        try
        {
            var path = PathFor(cue);
            if (IsEvent(path))
            {
                SfxCmd.Play(path);
                return;
            }

            if (!IsResource(path))
            {
                NDebugAudioManager.Instance?.Play(path);
                return;
            }

            var result = GameAudioService.Shared.PlayOneShot(
                new ResourceSoundFileSource(path),
                new AudioPlaybackOptions
                {
                    Volume = 1f,
                    Scope = AudioLifecycleScope.Combat,
                    DebugName = $"FourthAct.{cue}"
                });
            if (!result.Succeeded)
                MainFile.Logger.Warn($"Fourth-act audio cue {cue} failed: {result.Status} {result.Message}");
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act audio cue {cue} failed: {exception.Message}");
        }
    }
}
