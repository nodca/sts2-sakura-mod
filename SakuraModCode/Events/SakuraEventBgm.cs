using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Events;

internal static class SakuraEventBgm
{
    private const float EventMusicVolume = 0.3f;
    private const float FadeInDuration = 0.8f;
    private const float FadeOutDuration = 0.6f;
    private const int FadeSteps = 12;

    private static GodotObject? _musicHandle;
    private static CancellationTokenSource? _fadeCts;
    private static float _currentVolume;
    private static bool _stoppedRunMusic;

    public static void Play(string relativePath)
    {
        if (TestMode.IsOn)
            return;

        var path = ResolveExternalMusicPath(relativePath);
        if (!File.Exists(path))
            return;

        StopImmediately(restoreRunMusic: false);
        NRunMusicController.Instance?.StopMusic();
#pragma warning disable CS0618
        var handle = FmodAudio.PlayMusic(path, 0f);
#pragma warning restore CS0618
        if (handle is null)
        {
            var musicController = NRunMusicController.Instance;
            musicController?.UpdateMusic();
            musicController?.UpdateTrack();
            return;
        }

        _musicHandle = handle;
        _currentVolume = 0f;
        _stoppedRunMusic = true;
        StartFade(handle, 0f, EventMusicVolume, FadeInDuration, releaseWhenComplete: false, restoreRunMusic: false);
    }

    public static void Stop() =>
        FadeOut(restoreRunMusic: true);

    public static void StopForRunCleanup() =>
        StopImmediately(restoreRunMusic: false);

    public static void StopForEventProceed() =>
        StopImmediately(restoreRunMusic: true);

    private static void FadeOut(bool restoreRunMusic)
    {
        var handle = _musicHandle;
        if (handle is null || !GodotObject.IsInstanceValid(handle))
        {
            StopImmediately(restoreRunMusic);
            return;
        }

        StartFade(handle, _currentVolume, 0f, FadeOutDuration, releaseWhenComplete: true, restoreRunMusic);
    }

    private static void StopImmediately(bool restoreRunMusic)
    {
        CancelFade();
        StopMusicHandle();
        FinishStop(restoreRunMusic);
    }

    private static void FinishStop(bool restoreRunMusic)
    {
        if (!_stoppedRunMusic)
            return;

        _stoppedRunMusic = false;
        _currentVolume = 0f;
        if (!restoreRunMusic)
            return;

        var musicController = NRunMusicController.Instance;
        musicController?.UpdateMusic();
        musicController?.UpdateTrack();
    }

    private static void StartFade(
        GodotObject handle,
        float fromVolume,
        float toVolume,
        float duration,
        bool releaseWhenComplete,
        bool restoreRunMusic)
    {
        CancelFade();
        _fadeCts = new CancellationTokenSource();
        TaskHelper.RunSafely(FadeVolume(
            handle,
            fromVolume,
            toVolume,
            duration,
            releaseWhenComplete,
            restoreRunMusic,
            _fadeCts.Token));
    }

    private static async Task FadeVolume(
        GodotObject handle,
        float fromVolume,
        float toVolume,
        float duration,
        bool releaseWhenComplete,
        bool restoreRunMusic,
        CancellationToken cancellationToken)
    {
        try
        {
            for (var step = 0; step <= FadeSteps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentHandle(handle))
                    return;

                var progress = step / (float)FadeSteps;
                SetVolume(handle, Mathf.Lerp(fromVolume, toVolume, progress));
                if (step < FadeSteps)
                    await Cmd.Wait(duration / FadeSteps, cancellationToken, ignoreCombatEnd: true);
            }

            if (!releaseWhenComplete || !IsCurrentHandle(handle))
                return;

            StopMusicHandle();
            FinishStop(restoreRunMusic);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool IsCurrentHandle(GodotObject handle) =>
        ReferenceEquals(_musicHandle, handle) && GodotObject.IsInstanceValid(handle);

    private static void SetVolume(GodotObject handle, float volume)
    {
        _currentVolume = volume;
        handle.Call("set_volume", volume);
    }

    private static void CancelFade()
    {
        _fadeCts?.Cancel();
        _fadeCts = null;
    }

    private static void StopMusicHandle()
    {
        var handle = _musicHandle;
        _musicHandle = null;
        if (handle is null || !GodotObject.IsInstanceValid(handle))
            return;

        handle.Call("stop");
        handle.Call("release");
    }

    private static string ResolveExternalMusicPath(string relativePath)
    {
        var modDirectory = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        return Path.Combine(
            modDirectory ?? AppContext.BaseDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class SakuraEventBgmProceedPatch
{
    private static void Prefix()
    {
        SakuraEventBgm.StopForEventProceed();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class SakuraEventBgmRunCleanupPatch
{
    private static void Prefix()
    {
        SakuraEventBgm.StopForRunCleanup();
    }
}
