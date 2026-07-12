using SakuraMod.SakuraModCode;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Events;

internal static class SakuraEventBgm
{
    private const float EventMusicVolume = 0.3f;
    private const string EventMusicChannel = "SakuraMod.EventBgm";
    private const string EventMusicDebugName = "SakuraMod.EventBgm";

    private static AudioMusicHandle? _musicHandle;
    private static IDisposable? _roomExitedSubscription;
    private static IDisposable? _runEndedSubscription;
    private static bool _registeredLifecycleSubscriptions;
    private static bool _stoppedRunMusic;

    public static void Play(string relativePath)
    {
        if (TestMode.IsOn)
            return;

        var path = ResolveExternalMusicPath(relativePath);
        if (!File.Exists(path))
            return;

        EnsureLifecycleSubscriptions();
        StopImmediately(restoreRunMusic: false, allowFadeOut: false);

        _stoppedRunMusic = NRunMusicController.Instance is not null;
        NRunMusicController.Instance?.StopMusic();

        _musicHandle = GameAudioService.Shared.PlayMusic(
            AudioSource.StreamingMusic(path),
            new AudioPlaybackOptions
            {
                Volume = EventMusicVolume,
                Scope = AudioLifecycleScope.Run,
                AllowFadeOutOnStop = true,
                DebugName = EventMusicDebugName,
                Routing = new AudioRoutingOptions
                {
                    Channel = EventMusicChannel,
                    ChannelMode = AudioChannelMode.ReplaceExisting,
                    AllowFadeOutOnReplace = false
                }
            });

        if (_musicHandle is null)
            FinishStop(restoreRunMusic: true);
    }

    public static void Stop() =>
        StopImmediately(restoreRunMusic: true, allowFadeOut: true);

    public static void StopForRunCleanup() =>
        StopImmediately(restoreRunMusic: false, allowFadeOut: false);

    public static void StopForEventProceed() =>
        StopImmediately(restoreRunMusic: true, allowFadeOut: false);

    private static void StopImmediately(bool restoreRunMusic, bool allowFadeOut)
    {
        var handle = _musicHandle;
        _musicHandle = null;
        if (handle is not null)
        {
            handle.TryStop(allowFadeOut);
            handle.TryRelease();
            handle.Dispose();
        }

        FinishStop(restoreRunMusic);
    }

    private static void FinishStop(bool restoreRunMusic)
    {
        if (!_stoppedRunMusic)
            return;

        _stoppedRunMusic = false;
        if (!restoreRunMusic)
            return;

        AudioVanillaBridge.RefreshRunMusic();
    }

    private static string ResolveExternalMusicPath(string relativePath)
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void EnsureLifecycleSubscriptions()
    {
        if (_registeredLifecycleSubscriptions)
            return;

        _roomExitedSubscription = RitsuLibFramework.SubscribeLifecycle<RoomExitedEvent>(
            _ => StopForEventProceed(),
            replayCurrentState: false);
        _runEndedSubscription = RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            _ => StopForRunCleanup(),
            replayCurrentState: false);
        _registeredLifecycleSubscriptions = true;
    }
}
