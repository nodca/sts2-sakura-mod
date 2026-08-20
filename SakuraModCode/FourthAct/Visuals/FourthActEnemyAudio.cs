using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum FourthActAudioCue
{
    WindAttack,
    HeavyWindAttack,
    IllusionAttack,
    DarkAttack,
    WindTakeoff,
    WindySummon,
    IllusionReweave,
    DarkTransition,
    FlyLanding,
    SleepCast,
    WindWallBlock,
    DarkVeilBreak
}

internal static class FourthActEnemyAudio
{
    private const string WindAttackSfx =
        "event:/sfx/enemy/enemy_attacks/living_fog/living_fog_attack_blow";
    private const string HeavyWindAttackSfx =
        "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_wave";
    private const string IllusionAttackSfx =
        "event:/sfx/enemy/enemy_attacks/obscura/obscura_attack";
    private const string DarkAttackSfx =
        "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash";
    private const string WindTakeoffSfx =
        "event:/sfx/enemy/enemy_attacks/thieving_hopper/thieving_hopper_take_off";
    private const string SummonSfx =
        "event:/sfx/enemy/enemy_attacks/obscura/obscura_summon";
    private const string DarkTransitionSfx =
        "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_hex";
    private const string FlyLandingSfx = $"{MainFile.ResPath}/sfx/fourth_act/fly_landing.ogg";
    private const string SleepCastSfx = $"{MainFile.ResPath}/sfx/fourth_act/sleep_cast.ogg";
    private const string WindWallBlockSfx = $"{MainFile.ResPath}/sfx/fourth_act/wind_wall_block.ogg";
    private const string DarkVeilBreakSfx = $"{MainFile.ResPath}/sfx/fourth_act/dark_veil_break.ogg";

    internal static string PathFor(FourthActAudioCue cue) => cue switch
    {
        FourthActAudioCue.WindAttack => WindAttackSfx,
        FourthActAudioCue.HeavyWindAttack => HeavyWindAttackSfx,
        FourthActAudioCue.IllusionAttack => IllusionAttackSfx,
        FourthActAudioCue.DarkAttack => DarkAttackSfx,
        FourthActAudioCue.WindTakeoff => WindTakeoffSfx,
        FourthActAudioCue.WindySummon => SummonSfx,
        FourthActAudioCue.IllusionReweave => SummonSfx,
        FourthActAudioCue.DarkTransition => DarkTransitionSfx,
        FourthActAudioCue.FlyLanding => FlyLandingSfx,
        FourthActAudioCue.SleepCast => SleepCastSfx,
        FourthActAudioCue.WindWallBlock => WindWallBlockSfx,
        FourthActAudioCue.DarkVeilBreak => DarkVeilBreakSfx,
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
    };

    internal static void Play(FourthActAudioCue cue)
    {
        if (TestMode.IsOn)
            return;

        try
        {
            var path = PathFor(cue);
            if (IsResourceCue(cue))
            {
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
                return;
            }

            SfxCmd.Play(path);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act audio cue {cue} failed: {exception.Message}");
        }
    }

    private static bool IsResourceCue(FourthActAudioCue cue) => cue is
        FourthActAudioCue.FlyLanding or
        FourthActAudioCue.SleepCast or
        FourthActAudioCue.WindWallBlock or
        FourthActAudioCue.DarkVeilBreak;
}
