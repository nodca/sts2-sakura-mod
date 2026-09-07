using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode;
using STS2RitsuLib;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
///     Shared one-shot card BGM engine. Every track owns one <see cref="Config" />
///     and is registered in the shared track list at creation, so a single set of
///     run-lifecycle subscriptions stops every active track and restores vanilla
///     run music only after the last owner releases it. Triggers are mutually
///     exclusive: starting any track replaces every active one, so two mod songs
///     can never layer.
/// </summary>
internal sealed class CardBgmPlayback
{
    internal sealed record Config(
        string ResourcePath,
        string RelativePath,
        string MusicChannel,
        float MusicVolume,
        string TrackLabel);

    internal const float FadeInSeconds = 1.5f;
    internal const float FadeOutSeconds = 1.5f;
    private const string RunMusicProxyPath = "Proxy";
    private const string StopMusicMethod = "stop_music";

    private static readonly List<CardBgmPlayback> Tracks = [];
    private static int _runMusicStoppedTrackCount;
    private static bool _lifecycleRegistered;

    internal static bool LifecycleCleanupRegistered => _lifecycleRegistered;

    private readonly Config _config;
    private AudioMusicHandle? _musicHandle;
    private Tween? _completionTween;
    private float _envelopeVolume;
    private SceneTree? _volumeRefreshTree;
    private bool _stoppedRunMusic;
    private bool _reportedFailure;

    private CardBgmPlayback(Config config)
    {
        _config = config;
        Tracks.Add(this);
    }

    internal static CardBgmPlayback CreateTrack(Config config) => new(config);

    internal static void RegisterSharedLifecycle()
    {
        if (_lifecycleRegistered)
            return;

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(
            _ => StopAllTracks(restoreRunMusic: true),
            replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            _ => StopAllTracks(restoreRunMusic: false),
            replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(
            _ => StopAllTracks(restoreRunMusic: false),
            replayCurrentState: false);
        _lifecycleRegistered = true;
    }

    public void TryPlay(CardModel card)
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

            var duration = ResourceLoader.Load<AudioStream>(_config.ResourcePath)?.GetLength() ?? 0d;
            if (duration <= 0d)
            {
                ReportFailureOnce($"could not read a positive duration from {_config.ResourcePath}");
                return;
            }

            var proxy = ResolveRunMusicProxy();
            if (NRunMusicController.Instance is not null && proxy is null)
            {
                ReportFailureOnce("the native run-music proxy does not expose stop_music");
                return;
            }

            StopAllTracks(restoreRunMusic: false);
            if (proxy is not null)
            {
                proxy.Call(StopMusicMethod);
                MarkRunMusicStopped();
            }

            var handle = GameAudioService.Shared.PlayMusic(
                AudioSource.StreamingMusic(path),
                CreatePlaybackOptions());
            if (handle is null)
            {
                StopImmediately(restoreRunMusic: true);
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
                    _config.MusicVolume,
                    fadeInSeconds)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            completionTween.TweenInterval(holdSeconds);
            completionTween.TweenMethod(
                    Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume)),
                    _config.MusicVolume,
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

    internal AudioPlaybackOptions CreatePlaybackOptions() => new()
    {
        Volume = 0f,
        Scope = AudioLifecycleScope.Combat,
        AllowFadeOutOnStop = false,
        DebugName = _config.MusicChannel,
        Routing = new AudioRoutingOptions
        {
            Channel = _config.MusicChannel,
            ChannelMode = AudioChannelMode.ReplaceExisting,
            AllowFadeOutOnReplace = false
        }
    };

    private static void StopAllTracks(bool restoreRunMusic)
    {
        foreach (var track in Tracks.ToArray())
            track.StopImmediately(restoreRunMusic);
    }

    private void CompleteFirstPlay(AudioMusicHandle handle, Tween completionTween)
    {
        if (!ReferenceEquals(_completionTween, completionTween)
            || !ReferenceEquals(_musicHandle, handle))
            return;

        _completionTween = null;
        _musicHandle = null;
        DetachVolumeRefresh();
        Release(handle);
        ClearRunMusicStopped(restoreRunMusic: true);
    }

    private void ApplyEnvelopeVolume(AudioMusicHandle handle, float volume)
    {
        if (!ReferenceEquals(_musicHandle, handle))
            return;

        _envelopeVolume = volume;
        if (handle.IsValid)
            handle.TrySetVolume(volume * SakuraGameVolumeFollower.MusicFactor());
    }

    private void AttachVolumeRefresh(SceneTree tree)
    {
        if (_volumeRefreshTree is not null)
            return;

        _volumeRefreshTree = tree;
        tree.ProcessFrame += RefreshVolumeFromGameBuses;
    }

    private void DetachVolumeRefresh()
    {
        if (_volumeRefreshTree is not { } tree)
            return;

        _volumeRefreshTree = null;
        tree.ProcessFrame -= RefreshVolumeFromGameBuses;
    }

    private void RefreshVolumeFromGameBuses()
    {
        if (_musicHandle is { IsValid: true } handle)
            handle.TrySetVolume(_envelopeVolume * SakuraGameVolumeFollower.MusicFactor());
    }

    private void StopImmediately(bool restoreRunMusic)
    {
        KillCompletionTween();

        var handle = _musicHandle;
        _musicHandle = null;
        DetachVolumeRefresh();
        if (handle is not null)
            Release(handle);

        ClearRunMusicStopped(restoreRunMusic);
    }

    private void Release(AudioMusicHandle handle)
    {
        handle.TryStop(allowFadeOut: false);
        handle.TryRelease();
        handle.Dispose();
    }

    private void KillCompletionTween()
    {
        if (_completionTween is { } tween && tween.IsValid())
            tween.Kill();
        _completionTween = null;
    }

    private void MarkRunMusicStopped()
    {
        if (_stoppedRunMusic)
            return;

        _stoppedRunMusic = true;
        _runMusicStoppedTrackCount++;
    }

    private void ClearRunMusicStopped(bool restoreRunMusic)
    {
        if (!_stoppedRunMusic)
            return;

        _stoppedRunMusic = false;
        _runMusicStoppedTrackCount--;
        if (!restoreRunMusic || _runMusicStoppedTrackCount > 0)
            return;

        if (NRunMusicController.Instance is not { } controller)
            return;

        controller.StopCustomMusic();
        AudioVanillaBridge.RefreshTrackAndAmbience();
    }

    /// <summary>
    ///     Resolves the native run-music proxy without touching any audio state so
    ///     a missing proxy fails the presentation before any handle or vanilla
    ///     music is disturbed. A missing run-music controller is tolerated (there
    ///     is no native music to stop through the bridge), matching the historical
    ///     behavior.
    /// </summary>
    private static Node? ResolveRunMusicProxy()
    {
        if (NRunMusicController.Instance is not { } controller)
            return null;

        var proxy = controller.GetNodeOrNull<Node>(RunMusicProxyPath);
        return proxy is not null && proxy.HasMethod(StopMusicMethod) ? proxy : null;
    }

    private string ResolveExternalMusicPath()
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            _config.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private void ReportFailureOnce(string details)
    {
        if (_reportedFailure)
            return;

        _reportedFailure = true;
        MainFile.Logger.Warn($"{_config.TrackLabel} BGM playback failed: {details}");
    }
}
