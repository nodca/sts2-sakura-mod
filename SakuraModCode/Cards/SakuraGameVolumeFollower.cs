using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
///     Loose-file FMOD playback attaches to the low-level master channel group, so it bypasses the
///     game's <c>bus:/master</c> subtree where the volume sliders act. The FMOD add-on exposes no
///     channel-group rerouting, so mod audio must scale itself with the game bus volumes instead.
/// </summary>
internal static class SakuraGameVolumeFollower
{
    internal const float MaxFactor = 2f;

    /// <summary>
    ///     STS2 writes <c>slider²</c> into each volume bus, and master / sfx / music all default to
    ///     50%. Following buses raw therefore multiplies mod audio by
    ///     <c>0.25 × 0.25 = 0.0625</c> at stock settings. Compensate by
    ///     <c>1 / (0.5² × 0.5²) = 16</c> so default sliders restore authored mod levels while
    ///     still tracking relative slider changes the same way game audio does.
    /// </summary>
    internal const float DefaultVolumeCompensation = 16f;

    /// <summary>
    ///     Authored loose-file levels run a bit hot once default bus compensation is applied.
    ///     Keep a mild trim so stock settings sit under full authored gain.
    /// </summary>
    internal const float PreferredGain = 0.75f;

    // Test seams: swapped out by SakuraGameVolumeFollowerSuite to pin the fallback contract.
    internal static Func<string, bool> BusExists = DefaultBusExists;
    internal static Func<string, float> ReadBusVolume = FmodStudioBusAccess.TryGetVolume;

    internal static float VoiceFactor() =>
        BusFactor(FmodStudioRouting.MasterBus)
        * BusFactor(FmodStudioRouting.SfxBus)
        * DefaultVolumeCompensation
        * PreferredGain;

    internal static float MusicFactor() =>
        BusFactor(FmodStudioRouting.MasterBus)
        * BusFactor(FmodStudioRouting.MusicBus)
        * DefaultVolumeCompensation
        * PreferredGain;

    internal static void ResetSeams()
    {
        BusExists = DefaultBusExists;
        ReadBusVolume = FmodStudioBusAccess.TryGetVolume;
    }

    private static bool DefaultBusExists(string busPath) =>
        FmodStudioBusAccess.TryGetBus(busPath) is not null;

    private static float BusFactor(string busPath)
    {
        // FmodStudioBusAccess.TryGetVolume returns 0 both for a muted bus and for a failed lookup,
        // so the existence probe is what keeps an unresolved bus from muting mod audio.
        if (!BusExists(busPath))
            return 1f;

        var volume = ReadBusVolume(busPath);
        if (!float.IsFinite(volume) || volume < 0f)
            return 1f;

        return Math.Clamp(volume, 0f, MaxFactor);
    }
}
