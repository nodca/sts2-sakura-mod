using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Audio;

public sealed class SakuraGameVolumeFollowerSuite
{
    [Fact]
    public void FactorsFollowTheirGameVolumeBuses()
    {
        using var stub = StubBuses(
            master: 0.5f,
            sfx: 0.25f,
            music: 0.8f);

        RegressionTestHarness.Require(
            Math.Abs(SakuraGameVolumeFollower.VoiceFactor() - 2f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f
            && Math.Abs(SakuraGameVolumeFollower.MusicFactor() - 6.4f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f,
            "Expected the voice factor to scale with master × sfx × default compensation × preferred gain and the music factor with master × music × default compensation × preferred gain.");
    }

    [Fact]
    public void DefaultGameSlidersApplyPreferredAuthoredGain()
    {
        // SettingsSave defaults are 0.5 for master/sfx/music; NAudioManager writes slider².
        using var stub = StubBuses(
            master: 0.25f,
            sfx: 0.25f,
            music: 0.25f);

        RegressionTestHarness.Require(
            Math.Abs(SakuraGameVolumeFollower.VoiceFactor() - SakuraGameVolumeFollower.PreferredGain) < 1e-6f
            && Math.Abs(SakuraGameVolumeFollower.MusicFactor() - SakuraGameVolumeFollower.PreferredGain) < 1e-6f,
            "Expected stock 50% sliders (bus volumes 0.25) to land at preferred authored gain.");
    }

    [Fact]
    public void MutedGameBusesSilenceModAudio()
    {
        using var stub = StubBuses(
            master: 0f,
            sfx: 1f,
            music: 1f);

        RegressionTestHarness.Require(
            SakuraGameVolumeFollower.VoiceFactor() == 0f
            && SakuraGameVolumeFollower.MusicFactor() == 0f,
            "Expected a muted master bus to silence both mod audio kinds.");
    }

    [Fact]
    public void UnresolvedBusesFallBackToUnityGain()
    {
        SakuraGameVolumeFollower.BusExists = _ => false;
        SakuraGameVolumeFollower.ReadBusVolume = _ => 0f;
        try
        {
            RegressionTestHarness.Require(
                Math.Abs(SakuraGameVolumeFollower.VoiceFactor() - 16f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f
                && Math.Abs(SakuraGameVolumeFollower.MusicFactor() - 16f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f,
                "Expected unresolved buses to leave mod audio unattenuated and still apply default compensation and preferred gain.");
        }
        finally
        {
            SakuraGameVolumeFollower.ResetSeams();
        }
    }

    [Fact]
    public void InvalidBusVolumesFallBackToUnityGainPerBus()
    {
        SakuraGameVolumeFollower.BusExists = _ => true;
        SakuraGameVolumeFollower.ReadBusVolume = path =>
            path == FmodStudioRouting.SfxBus ? float.NaN
                : path == FmodStudioRouting.MusicBus ? -1f
                : 0.5f;
        try
        {
            RegressionTestHarness.Require(
                Math.Abs(SakuraGameVolumeFollower.VoiceFactor() - 8f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f
                && Math.Abs(SakuraGameVolumeFollower.MusicFactor() - 8f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f,
                "Expected NaN or negative bus reads to contribute unity gain while the master bus, default compensation, and preferred gain still apply.");
        }
        finally
        {
            SakuraGameVolumeFollower.ResetSeams();
        }
    }

    [Fact]
    public void BusVolumesAreClampedAboveTwo()
    {
        using var stub = StubBuses(
            master: 5f,
            sfx: 1f,
            music: 1f);

        RegressionTestHarness.Require(
            Math.Abs(SakuraGameVolumeFollower.VoiceFactor() - 32f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f
            && Math.Abs(SakuraGameVolumeFollower.MusicFactor() - 32f * SakuraGameVolumeFollower.PreferredGain) < 1e-6f,
            "Expected boosted bus volumes to be clamped before default compensation and preferred gain are applied.");
    }

    [Fact]
    public void ResetSeamsRestoresDefaultReaders()
    {
        Func<string, bool> stubExists = _ => false;
        Func<string, float> stubVolume = _ => 0f;
        SakuraGameVolumeFollower.BusExists = stubExists;
        SakuraGameVolumeFollower.ReadBusVolume = stubVolume;

        SakuraGameVolumeFollower.ResetSeams();

        RegressionTestHarness.Require(
            !ReferenceEquals(SakuraGameVolumeFollower.BusExists, stubExists)
            && !ReferenceEquals(SakuraGameVolumeFollower.ReadBusVolume, stubVolume),
            "Expected ResetSeams to restore the real FMOD bus readers for the remaining suites.");
    }

    [Fact]
    public void PlaybackVolumesRouteThroughTheGameVolumeFollower()
    {
        var bgm = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/AnotherMeBgmPlayback.cs"));
        var voice = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraVoicePlayback.cs"));

        RegressionTestHarness.Require(
            bgm.Contains("SakuraGameVolumeFollower.MusicFactor()", StringComparison.Ordinal)
            && voice.Contains("SakuraGameVolumeFollower.VoiceFactor()", StringComparison.Ordinal)
            && bgm.Contains("ProcessFrame += RefreshVolumeFromGameBuses", StringComparison.Ordinal)
            && voice.Contains("ProcessFrame += RefreshVolumeFromGameBuses", StringComparison.Ordinal)
            && bgm.Contains("ProcessFrame -= RefreshVolumeFromGameBuses", StringComparison.Ordinal)
            && voice.Contains("ProcessFrame -= RefreshVolumeFromGameBuses", StringComparison.Ordinal)
            && CountOccurrences(bgm, "ProcessFrame +=") == 1
            && CountOccurrences(voice, "ProcessFrame +=") == 1,
            "Expected both playbacks to scale envelope writes with the follower and to detach their frame refresh on every stop path.");
    }

    private static VolumeBusStub StubBuses(float master, float sfx, float music) =>
        new(path =>
            path == FmodStudioRouting.MasterBus ? master
                : path == FmodStudioRouting.SfxBus ? sfx
                : path == FmodStudioRouting.MusicBus ? music
                : 1f);

    private sealed class VolumeBusStub : IDisposable
    {
        public VolumeBusStub(Func<string, float> readVolume)
        {
            SakuraGameVolumeFollower.BusExists = _ => true;
            SakuraGameVolumeFollower.ReadBusVolume = readVolume;
        }

        public void Dispose() =>
            SakuraGameVolumeFollower.ResetSeams();
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
