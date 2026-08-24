using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraVoiceCue
{
    Release,
    Seal
}

internal sealed class SakuraVoiceCueGate
{
    private object? _currentCombat;
    private readonly HashSet<SakuraVoiceCue> _claimedCues = [];

    public bool CanPlay(object combatState, SakuraVoiceCue cue)
    {
        ResetIfCombatChanged(combatState);
        return !_claimedCues.Contains(cue);
    }

    public void MarkPlayed(object combatState, SakuraVoiceCue cue)
    {
        ResetIfCombatChanged(combatState);
        _claimedCues.Add(cue);
    }

    private void ResetIfCombatChanged(object combatState)
    {
        if (ReferenceEquals(_currentCombat, combatState))
            return;

        _currentCombat = combatState;
        _claimedCues.Clear();
    }
}

public static class SakuraVoicePlayback
{
    internal const string ReleaseVoicePath = $"{MainFile.ResPath}/voices/dream_wand.ogg";
    internal const string SealVoicePath = $"{MainFile.ResPath}/voices/stabilize.ogg";
    // FMOD reads the loose package files directly; the imported res:// twins only supply the envelope duration.
    internal const string ReleaseVoiceRelativePath = "voices/dream_wand.ogg";
    internal const string SealVoiceRelativePath = "voices/stabilize.ogg";
    internal const string VoiceChannel = $"{MainFile.ModId}.Voice";
    internal const float FadeInSeconds = 0.18f;
    internal const float FadeOutSeconds = 0.28f;

    private static readonly SakuraVoiceCueGate CueGate = new();
    private static readonly HashSet<SakuraVoiceCue> ReportedFailures = [];
    private static AudioFileHandle? _activeHandle;
    private static Tween? _envelopeTween;
    private static float _envelopeVolume;
    private static SceneTree? _volumeRefreshTree;
    private static IDisposable? _combatEndedSubscription;
    private static IDisposable? _runEndedSubscription;
    private static IDisposable? _mainMenuReadySubscription;

    internal static bool LifecycleCleanupRegistered { get; private set; }

    public static void Register()
    {
        if (LifecycleCleanupRegistered)
            return;

        _combatEndedSubscription = RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(
            _ => StopForLifecycle(),
            replayCurrentState: false);
        _runEndedSubscription = RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            _ => StopForLifecycle(),
            replayCurrentState: false);
        _mainMenuReadySubscription = RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(
            _ => StopForLifecycle(),
            replayCurrentState: false);
        LifecycleCleanupRegistered = true;
    }

    public static void TryPlay(CardModel card)
    {
        if (TestMode.IsOn || card.CombatState is not ICombatState combatState)
            return;

        var cue = CueFor(card);
        if (cue is null)
            return;

        try
        {
            if (!LocalContext.IsMe(card.Owner)
                || !SakuraModConfig.IsSakuraVoiceEnabled()
                || !CueGate.CanPlay(combatState, cue.Value)
                || IsChannelBusy())
                return;

            var externalPath = ExternalVoicePathFor(cue.Value);
            if (!File.Exists(externalPath))
            {
                ReportFailureOnce(cue.Value, $"voice file not found: {externalPath}");
                return;
            }

            var result = GameAudioService.Shared.PlayOneShot(
                AudioSource.File(externalPath),
                CreatePlaybackOptions(cue.Value));

            if (!result.Succeeded || result.Handle is not AudioFileHandle handle)
            {
                ReportFailureOnce(cue.Value, $"{result.Status}: {result.Message ?? "no details"}");
                return;
            }

            _activeHandle = handle;
            if (!TryStartEnvelope(handle, PathFor(cue.Value)))
            {
                StopActivePlayback(handle);
                ReportFailureOnce(cue.Value, "could not create the voice volume envelope");
                return;
            }

            CueGate.MarkPlayed(combatState, cue.Value);
        }
        catch (Exception exception)
        {
            if (_activeHandle is { } handle)
                StopActivePlayback(handle);
            ReportFailureOnce(cue.Value, exception.Message);
        }
    }

    internal static SakuraVoiceCue? CueFor(CardModel card) => card switch
    {
        SpellRelease => SakuraVoiceCue.Release,
        SpellSeal or GrowingMagic => SakuraVoiceCue.Seal,
        _ => null
    };

    internal static string PathFor(SakuraVoiceCue cue) => cue switch
    {
        SakuraVoiceCue.Release => ReleaseVoicePath,
        SakuraVoiceCue.Seal => SealVoicePath,
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
    };

    internal static string ExternalVoicePathFor(SakuraVoiceCue cue) => cue switch
    {
        SakuraVoiceCue.Release => ResolveExternalVoicePath(ReleaseVoiceRelativePath),
        SakuraVoiceCue.Seal => ResolveExternalVoicePath(SealVoiceRelativePath),
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
    };

    private static string ResolveExternalVoicePath(string relativePath)
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    internal static AudioPlaybackOptions CreatePlaybackOptions(SakuraVoiceCue cue) => new()
    {
        Volume = 0f,
        Scope = AudioLifecycleScope.Combat,
        DebugName = $"{VoiceChannel}.{cue}",
        Routing = new AudioRoutingOptions
        {
            Channel = VoiceChannel,
            ChannelMode = AudioChannelMode.KeepExisting
        }
    };

    private static bool IsChannelBusy()
    {
        if (_activeHandle is null)
            return false;
        if (_activeHandle.IsValid)
            return true;

        StopActivePlayback(_activeHandle);
        return false;
    }

    private static bool TryStartEnvelope(AudioFileHandle handle, string path)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return false;

        var stream = ResourceLoader.Load<AudioStream>(path);
        var duration = stream?.GetLength() ?? 0d;
        if (duration <= 0d)
            return false;

        var holdSeconds = Math.Max(0d, duration - FadeInSeconds - FadeOutSeconds);
        var tween = tree.CreateTween();
        _envelopeTween = tween;
        tween.TweenMethod(
                Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume)),
                0f,
                1f,
                FadeInSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(holdSeconds);
        tween.TweenMethod(
                Callable.From<float>(volume => ApplyEnvelopeVolume(handle, volume)),
                1f,
                0f,
                FadeOutSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() => CompleteEnvelope(handle, tween)));
        _envelopeVolume = 0f;
        AttachVolumeRefresh(tree);
        return true;
    }

    private static void ApplyEnvelopeVolume(AudioFileHandle handle, float volume)
    {
        if (!ReferenceEquals(_activeHandle, handle))
            return;

        _envelopeVolume = volume;
        if (handle.IsValid)
            handle.TrySetVolume(volume * SakuraGameVolumeFollower.VoiceFactor());
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
        if (_activeHandle is { IsValid: true } handle)
            handle.TrySetVolume(_envelopeVolume * SakuraGameVolumeFollower.VoiceFactor());
    }

    private static void StopActivePlayback(AudioFileHandle handle)
    {
        if (!ReferenceEquals(_activeHandle, handle))
            return;

        KillEnvelope();
        _activeHandle = null;
        DetachVolumeRefresh();
        handle.TryStop(allowFadeOut: false);
        handle.Dispose();
    }

    private static void CompleteEnvelope(AudioFileHandle handle, Tween tween)
    {
        if (!ReferenceEquals(_envelopeTween, tween))
            return;

        _envelopeTween = null;
        if (!ReferenceEquals(_activeHandle, handle))
            return;

        _activeHandle = null;
        DetachVolumeRefresh();
        handle.TryStop(allowFadeOut: false);
        handle.Dispose();
    }

    private static void StopForLifecycle()
    {
        KillEnvelope();
        if (_activeHandle is not { } handle)
            return;

        _activeHandle = null;
        DetachVolumeRefresh();
        handle.TryStop(allowFadeOut: false);
        handle.Dispose();
    }

    private static void KillEnvelope()
    {
        if (_envelopeTween is { } tween && tween.IsValid())
            tween.Kill();
        _envelopeTween = null;
    }

    private static void ReportFailureOnce(SakuraVoiceCue cue, string details)
    {
        if (ReportedFailures.Add(cue))
            MainFile.Logger.Warn($"Sakura voice cue {cue} failed: {details}");
    }
}
