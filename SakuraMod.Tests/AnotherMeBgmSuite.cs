using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Audio;

public sealed class AnotherMeBgmSuite
{
    [Fact]
    public void AnotherMeBgmUsesOneReplacingCombatMusicChannel()
    {
        var options = AnotherMeBgmPlayback.CreatePlaybackOptions();

        RegressionTestHarness.Require(
            options.Volume == 0f
            && AnotherMeBgmPlayback.MusicVolume > 0f
            && AnotherMeBgmPlayback.FadeInSeconds > 0f
            && AnotherMeBgmPlayback.FadeOutSeconds > 0f
            && options.Scope == AudioLifecycleScope.Combat
            && !options.AllowFadeOutOnStop
            && options.Routing is
            {
                ChannelMode: AudioChannelMode.ReplaceExisting,
                AllowFadeOutOnReplace: false
            },
            "Expected Another Me to fade in from silence on one replacing, combat-scoped music channel.");
    }

    [Fact]
    public void AnotherMeRequestsBgmBeforeItsGameplayCommands()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Ancients/AnotherMe.cs"));
        var playbackIndex = source.IndexOf(
            "AnotherMeBgmPlayback.TryPlay(this);",
            StringComparison.Ordinal);
        var magicIndex = source.IndexOf(
            "await SakuraMagicCharge.GainMagic",
            StringComparison.Ordinal);
        var powerIndex = source.IndexOf(
            "await ApplyPower<AnotherMePower>",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            playbackIndex >= 0
            && playbackIndex < magicIndex
            && magicIndex < powerIndex,
            "Expected Another Me BGM to start before the card awaits either gameplay command.");
    }

    [Fact]
    public void AnotherMeBgmIsLocalAndUsesOnlyItsOwnSetting()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/AnotherMeBgmPlayback.cs"));

        var localOwnerGuard = source.IndexOf(
            "!LocalContext.IsMe(card.Owner)",
            StringComparison.Ordinal);
        var cardBgmSettingGuard = source.IndexOf(
            "!SakuraModConfig.IsCardBgmEnabled()",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            localOwnerGuard >= 0
            && localOwnerGuard < cardBgmSettingGuard
            && !source.Contains("IsSakuraVoiceEnabled", StringComparison.Ordinal)
            && !source.Contains("NRunMusicController.Instance?.StopMusic();", StringComparison.Ordinal)
            && source.Contains("proxy.Call(StopMusicMethod);", StringComparison.Ordinal)
            && source.Contains("controller.StopCustomMusic();", StringComparison.Ordinal)
            && source.Contains("AudioVanillaBridge.RefreshTrackAndAmbience();", StringComparison.Ordinal)
            && source.Contains("ResourceLoader.Load<AudioStream>(ResourcePath)?.GetLength()", StringComparison.Ordinal)
            && source.Contains("completionTween.SetIgnoreTimeScale();", StringComparison.Ordinal)
            && source.Contains("completionTween.SetPauseMode(Tween.TweenPauseMode.Process);", StringComparison.Ordinal)
            && source.Contains("Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume))", StringComparison.Ordinal)
            && source.Contains(".SetTrans(Tween.TransitionType.Sine)", StringComparison.Ordinal)
            && source.Contains(".SetEase(Tween.EaseType.Out);", StringComparison.Ordinal)
            && source.Contains(".SetEase(Tween.EaseType.In);", StringComparison.Ordinal),
            "Expected remote plays to be rejected before reading the dedicated card-BGM setting, with voice settings and ambience kept independent.");
    }
}
