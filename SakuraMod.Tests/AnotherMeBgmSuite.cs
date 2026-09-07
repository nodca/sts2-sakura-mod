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
            "SakuraModCode/Cards/CardBgmPlayback.cs"));

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
            && source.Contains("ResourceLoader.Load<AudioStream>(_config.ResourcePath)?.GetLength()", StringComparison.Ordinal)
            && source.Contains("completionTween.SetIgnoreTimeScale();", StringComparison.Ordinal)
            && source.Contains("completionTween.SetPauseMode(Tween.TweenPauseMode.Process);", StringComparison.Ordinal)
            && source.Contains("Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume))", StringComparison.Ordinal)
            && source.Contains(".SetTrans(Tween.TransitionType.Sine)", StringComparison.Ordinal)
            && source.Contains(".SetEase(Tween.EaseType.Out);", StringComparison.Ordinal)
            && source.Contains(".SetEase(Tween.EaseType.In);", StringComparison.Ordinal),
            "Expected remote plays to be rejected before reading the dedicated card-BGM setting, with voice settings and ambience kept independent.");
    }

    [Fact]
    public void AnotherMeBgmRidesTheSharedLifecycleCleanup()
    {
        var facade = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/AnotherMeBgmPlayback.cs"));
        var core = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/CardBgmPlayback.cs"));

        RegressionTestHarness.Require(
            facade.Contains("CardBgmPlayback.RegisterSharedLifecycle();", StringComparison.Ordinal)
            && core.Contains("SubscribeLifecycle<CombatEndedEvent>", StringComparison.Ordinal)
            && core.Contains("SubscribeLifecycle<RunEndedEvent>", StringComparison.Ordinal)
            && core.Contains("SubscribeLifecycle<MainMenuReadyEvent>", StringComparison.Ordinal)
            && core.Contains("StopAllTracks(restoreRunMusic: true)", StringComparison.Ordinal)
            && core.Contains("StopAllTracks(restoreRunMusic: false)", StringComparison.Ordinal),
            "Expected card BGM lifecycle cleanup to live once in the shared engine and cover every registered track.");
    }

    [Fact]
    public void CardBgmTriggerPreflightsTheProxyThenReplacesEveryActiveTrack()
    {
        var core = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/CardBgmPlayback.cs"));
        var tryPlayStart = core.IndexOf(
            "public void TryPlay(CardModel card)",
            StringComparison.Ordinal);
        var tryPlayEnd = core.IndexOf(
            "catch (Exception exception)",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            tryPlayStart >= 0 && tryPlayEnd > tryPlayStart,
            "Expected CardBgmPlayback to keep a guarded TryPlay body.");

        var tryPlayBody = core.Substring(tryPlayStart, tryPlayEnd - tryPlayStart);
        var proxyIndex = tryPlayBody.IndexOf("ResolveRunMusicProxy()", StringComparison.Ordinal);
        var stopAllIndex = tryPlayBody.IndexOf(
            "StopAllTracks(restoreRunMusic: false);",
            StringComparison.Ordinal);
        var playIndex = tryPlayBody.IndexOf(
            "GameAudioService.Shared.PlayMusic(",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            proxyIndex >= 0
            && proxyIndex < stopAllIndex
            && stopAllIndex < playIndex
            && tryPlayBody.Contains("MarkRunMusicStopped();", StringComparison.Ordinal),
            "Expected any card BGM trigger to preflight the native proxy, then replace every active card track before starting, so two mod songs can never layer and a failed trigger cannot leave vanilla music stopped.");
    }
}
