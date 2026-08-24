using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode;
using STS2RitsuLib;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

internal static class AnotherMeBgmPlayback
{
    internal const string ResourcePath = $"{MainFile.ResPath}/music/another_me.ogg";
    internal const string RelativePath = "music/another_me.ogg";
    internal const string MusicChannel = $"{MainFile.ModId}.AnotherMeBgm";
    internal const float MusicVolume = 0.3f;
    internal const float FadeInSeconds = 1.5f;
    internal const float FadeOutSeconds = 1.5f;
    private const string RunMusicProxyPath = "Proxy";
    private const string StopMusicMethod = "stop_music";

    private static AudioMusicHandle? _musicHandle;
    private static Tween? _completionTween;
    private static float _envelopeVolume;
    private static SceneTree? _volumeRefreshTree;
    private static IDisposable? _combatEndedSubscription;
    private static IDisposable? _runEndedSubscription;
    private static IDisposable? _mainMenuReadySubscription;
    private static bool _stoppedRunMusic;
    private static bool _reportedFailure;

    internal static bool LifecycleCleanupRegistered { get; private set; }

    public static void Register()
    {
        if (LifecycleCleanupRegistered)
            return;

        _combatEndedSubscription = RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(
            _ => StopImmediately(restoreRunMusic: true),
            replayCurrentState: false);
        _runEndedSubscription = RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            _ => StopImmediately(restoreRunMusic: false),
            replayCurrentState: false);
        _mainMenuReadySubscription = RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(
            _ => StopImmediately(restoreRunMusic: false),
            replayCurrentState: false);
        LifecycleCleanupRegistered = true;
    }

    public static void TryPlay(CardModel card)
    {
        if (TestMode.IsOn
            || card.CombatState is null
            || !LocalContext.IsMe(card.Owner)
            || !SakuraModConfig.IsCardBgmEnabled())
            return;

        try
        {
            var path = ResolveExternalMusicPath();
            if (!File.Exists(path))
            {
                ReportFailureOnce($"file not found: {path}");
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                ReportFailureOnce("the active Godot main loop is not a SceneTree");
                return;
            }

            var duration = ResourceLoader.Load<AudioStream>(ResourcePath)?.GetLength() ?? 0d;
            if (duration <= 0d)
            {
                ReportFailureOnce($"could not read a positive duration from {ResourcePath}");
                return;
            }

            StopImmediately(restoreRunMusic: false);
            if (!TryStopRunMusicPreservingAmbience())
                return;

            var handle = GameAudioService.Shared.PlayMusic(
                AudioSource.StreamingMusic(path),
                CreatePlaybackOptions());
            if (handle is null)
            {
                FinishStop(restoreRunMusic: true);
                ReportFailureOnce("RitsuLib rejected the streaming music request");
                return;
            }

            _musicHandle = handle;
            _envelopeVolume = 0f;
            AttachVolumeRefresh(tree);
            var completionTween = tree.CreateTween();
            _completionTween = completionTween;
            completionTween.SetIgnoreTimeScale();
            completionTween.SetPauseMode(Tween.TweenPauseMode.Process);
            var fadeInSeconds = Math.Min(FadeInSeconds, duration / 2d);
            var fadeOutSeconds = Math.Min(FadeOutSeconds, duration - fadeInSeconds);
            var holdSeconds = Math.Max(0d, duration - fadeInSeconds - fadeOutSeconds);
            completionTween.TweenMethod(
                    Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume)),
                    0f,
                    MusicVolume,
                    fadeInSeconds)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            completionTween.TweenInterval(holdSeconds);
            completionTween.TweenMethod(
                    Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume)),
                    MusicVolume,
                    0f,
                    fadeOutSeconds)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.In);
            completionTween.TweenCallback(Callable.From(
                () => CompleteFirstPlay(handle, completionTween)));
        }
        catch (Exception exception)
        {
            StopImmediately(restoreRunMusic: true);
            ReportFailureOnce(exception.Message);
        }
    }

    internal static AudioPlaybackOptions CreatePlaybackOptions() => new()
    {
        Volume = 0f,
        Scope = AudioLifecycleScope.Combat,
        AllowFadeOutOnStop = false,
        DebugName = MusicChannel,
        Routing = new AudioRoutingOptions
        {
            Channel = MusicChannel,
            ChannelMode = AudioChannelMode.ReplaceExisting,
            AllowFadeOutOnReplace = false
        }
    };

    private static void CompleteFirstPlay(AudioMusicHandle handle, Tween completionTween)
    {
        if (!ReferenceEquals(_completionTween, completionTween)
            || !ReferenceEquals(_musicHandle, handle))
            return;

        _completionTween = null;
        _musicHandle = null;
        DetachVolumeRefresh();
        Release(handle);
        FinishStop(restoreRunMusic: true);
    }

    private static void ApplyEnvelopeVolume(AudioMusicHandle handle, float volume)
    {
        if (!ReferenceEquals(_musicHandle, handle))
            return;

        _envelopeVolume = volume;
        if (handle.IsValid)
            handle.TrySetVolume(volume * SakuraGameVolumeFollower.MusicFactor());
    }

    private static void AttachVolumeRefresh(SceneTree tree)
    {
        if (_volumeRefreshTree is not null)
            return;

        _volumeRefreshTree = tree;
        tree.ProcessFrame += RefreshVolumeFromGameBuses;
    }

    private static void DetachVolumeRefresh()
    {
        if (_volumeRefreshTree is not { } tree)
            return;

        _volumeRefreshTree = null;
        tree.ProcessFrame -= RefreshVolumeFromGameBuses;
    }

    private static void RefreshVolumeFromGameBuses()
    {
        if (_musicHandle is { IsValid: true } handle)
            handle.TrySetVolume(_envelopeVolume * SakuraGameVolumeFollower.MusicFactor());
    }

    private static void StopImmediately(bool restoreRunMusic)
    {
        KillCompletionTween();

        var handle = _musicHandle;
        _musicHandle = null;
        DetachVolumeRefresh();
        if (handle is not null)
            Release(handle);

        FinishStop(restoreRunMusic);
    }

    private static void Release(AudioMusicHandle handle)
    {
        handle.TryStop(allowFadeOut: false);
        handle.TryRelease();
        handle.Dispose();
    }

    private static void KillCompletionTween()
    {
        if (_completionTween is { } tween && tween.IsValid())
            tween.Kill();
        _completionTween = null;
    }

    private static void FinishStop(bool restoreRunMusic)
    {
        if (!_stoppedRunMusic)
            return;

        _stoppedRunMusic = false;
        if (!restoreRunMusic || NRunMusicController.Instance is not { } controller)
            return;

        controller.StopCustomMusic();
        AudioVanillaBridge.RefreshTrackAndAmbience();
    }

    private static bool TryStopRunMusicPreservingAmbience()
    {
        var controller = NRunMusicController.Instance;
        if (controller is null)
            return true;

        var proxy = controller.GetNodeOrNull<Node>(RunMusicProxyPath);
        if (proxy is null || !proxy.HasMethod(StopMusicMethod))
        {
            ReportFailureOnce("the native run-music proxy does not expose stop_music");
            return false;
        }

        proxy.Call(StopMusicMethod);
        _stoppedRunMusic = true;
        return true;
    }

    private static string ResolveExternalMusicPath()
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ReportFailureOnce(string details)
    {
        if (_reportedFailure)
            return;

        _reportedFailure = true;
        MainFile.Logger.Warn($"Another Me BGM playback failed: {details}");
    }
}
