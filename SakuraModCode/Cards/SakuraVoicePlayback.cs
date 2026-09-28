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

/// <summary>
/// One playable Sakura voice line. <see cref="Key"/> is the per-combat gate and failure-dedup key;
/// <see cref="RelativePath"/> names both the loose package file FMOD plays and its imported res:// twin;
/// <see cref="Gain"/> trims a line's authored loudness against the others.
/// </summary>
internal readonly record struct SakuraVoiceLine(string Key, string RelativePath, float Gain = 1f)
{
    // The imported res:// twin only supplies the authoritative envelope duration.
    public string ResourcePath => $"{MainFile.ResPath}/{RelativePath}";
}

internal static class SakuraVoiceLines
{
    internal static readonly SakuraVoiceLine Release = new("release", "voices/dream_wand.ogg");
    internal static readonly SakuraVoiceLine Seal = new("seal", "voices/stabilize.ogg");

    internal static IEnumerable<SakuraVoiceLine> All =>
        [Release, Seal, .. SakuraCardVoiceCatalog.All];
}

internal sealed class SakuraVoiceCueGate
{
    private object? _currentCombat;
    private readonly HashSet<string> _claimedCues = [];

    public bool CanPlay(object combatState, string key)
    {
        ResetIfCombatChanged(combatState);
        return !_claimedCues.Contains(key);
    }

    public void MarkPlayed(object combatState, string key)
    {
        ResetIfCombatChanged(combatState);
        _claimedCues.Add(key);
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
    internal const string VoiceChannel = $"{MainFile.ModId}.Voice";
    internal const float FadeInSeconds = 0.18f;
    internal const float FadeOutSeconds = 0.28f;
    private const string PreconditionFailureKey = "preconditions";

    private static readonly SakuraVoiceCueGate CueGate = new();
    private static readonly HashSet<string> ReportedFailures = [];
    private static AudioFileHandle? _activeHandle;
    private static Tween? _envelopeTween;
    private static float _envelopeVolume;
    private static float _activeGain = 1f;
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

    /// <summary>Plays the fixed cue of a Seal-group card (Spell Seal, Growing Magic).</summary>
    public static void TryPlay(CardModel card)
    {
        if (LineFor(card) is not { } line)
            return;

        TryPlayGated(card, line);
    }

    /// <summary>
    /// Plays one Spell Release line after target selection. A released card with its own line only ever
    /// uses that line (silent once played this combat); every other release uses the generic Release line.
    /// </summary>
    public static void TryPlayRelease(SpellRelease card, CardModel? releasedTarget) =>
        TryPlayGated(card, ReleaseLineFor(releasedTarget));

    internal static SakuraVoiceLine? LineFor(CardModel card) => card switch
    {
        SpellSeal or GrowingMagic => SakuraVoiceLines.Seal,
        _ => null
    };

    internal static SakuraVoiceLine ReleaseLineFor(CardModel? releasedTarget) =>
        releasedTarget is not null && SakuraCardVoiceCatalog.For(releasedTarget) is { } cardLine
            ? cardLine
            : SakuraVoiceLines.Release;

    private static void TryPlayGated(CardModel card, SakuraVoiceLine line)
    {
        if (TestMode.IsOn || card.CombatState is not ICombatState combatState)
            return;

        try
        {
            if (!LocalContext.IsMe(card.Owner)
                || !SakuraModConfig.IsSakuraVoiceEnabled()
                || IsChannelBusy())
                return;
        }
        catch (Exception exception)
        {
            ReportFailureOnce(PreconditionFailureKey, exception.Message);
            return;
        }

        if (CueGate.CanPlay(combatState, line.Key))
            TryPlayLine(combatState, line);
    }

    private static bool TryPlayLine(ICombatState combatState, SakuraVoiceLine line)
    {
        try
        {
            var externalPath = ExternalVoicePathFor(line);
            if (!File.Exists(externalPath))
            {
                ReportFailureOnce(line.Key, $"voice file not found: {externalPath}");
                return false;
            }

            var result = GameAudioService.Shared.PlayOneShot(
                AudioSource.File(externalPath),
                CreatePlaybackOptions(line));

            if (!result.Succeeded || result.Handle is not AudioFileHandle handle)
            {
                ReportFailureOnce(line.Key, $"{result.Status}: {result.Message ?? "no details"}");
                return false;
            }

            _activeHandle = handle;
            _activeGain = line.Gain;
            if (!TryStartEnvelope(handle, line.ResourcePath))
            {
                StopActivePlayback(handle);
                ReportFailureOnce(line.Key, "could not create the voice volume envelope");
                return false;
            }

            CueGate.MarkPlayed(combatState, line.Key);
            return true;
        }
        catch (Exception exception)
        {
            if (_activeHandle is { } handle)
                StopActivePlayback(handle);
            ReportFailureOnce(line.Key, exception.Message);
            return false;
        }
    }

    // FMOD reads the loose package file beside the mod assembly directly.
    internal static string ExternalVoicePathFor(SakuraVoiceLine line)
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            line.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    internal static AudioPlaybackOptions CreatePlaybackOptions(SakuraVoiceLine line) => new()
    {
        Volume = 0f,
        Scope = AudioLifecycleScope.Combat,
        DebugName = $"{VoiceChannel}.{line.Key}",
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
            handle.TrySetVolume(volume * _activeGain * SakuraGameVolumeFollower.VoiceFactor());
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
            handle.TrySetVolume(_envelopeVolume * _activeGain * SakuraGameVolumeFollower.VoiceFactor());
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

    private static void ReportFailureOnce(string key, string details)
    {
        if (ReportedFailures.Add(key))
            MainFile.Logger.Warn($"Sakura voice line {key} failed: {details}");
    }
}
