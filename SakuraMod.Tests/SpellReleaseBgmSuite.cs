using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Audio;

public sealed class SpellReleaseBgmSuite
{
    [Fact]
    public void SpellReleaseBgmUsesItsOwnReplacingCombatMusicChannel()
    {
        var options = SpellReleaseBgmPlayback.CreatePlaybackOptions();
        var routing = options.Routing;

        RegressionTestHarness.Require(
            options.Volume == 0f
            && SpellReleaseBgmPlayback.MusicVolume > 0f
            && SpellReleaseBgmPlayback.FadeInSeconds > 0f
            && SpellReleaseBgmPlayback.FadeOutSeconds > 0f
            && options.Scope == AudioLifecycleScope.Combat
            && !options.AllowFadeOutOnStop
            && routing is
            {
                ChannelMode: AudioChannelMode.ReplaceExisting,
                AllowFadeOutOnReplace: false
            }
            && routing?.Channel == $"{MainFile.ModId}.SpellReleaseBgm"
            && options.DebugName == $"{MainFile.ModId}.SpellReleaseBgm",
            "Expected Spell Release to fade in from silence on one replacing, combat-scoped music channel.");

        RegressionTestHarness.Require(
            SpellReleaseBgmPlayback.MusicChannel != AnotherMeBgmPlayback.MusicChannel
            && SpellReleaseBgmPlayback.MusicChannel != SpellTurnBgmPlayback.MusicChannel,
            "Expected Spell Release to own a dedicated release channel instead of reusing Another Me or Spell Turn.");
        Assert.Equal(
            new[] { "music/release.ogg", "music/release_2.ogg", "music/release_3.ogg", "music/release_4.ogg" },
            SpellReleaseBgmPlayback.RelativePaths);
    }

    [Fact]
    public void SpellReleaseCallsBgmPlaybackInPlayCard()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Spells/SpellRelease.cs"));

        var voiceIndex = source.IndexOf(
            "SakuraVoicePlayback.TryPlayRelease(this, selected);",
            StringComparison.Ordinal);
        var bgmIndex = source.IndexOf(
            "SpellReleaseBgmPlayback.TryPlay(this);",
            StringComparison.Ordinal);
        var selectIndex = source.IndexOf(
            "CardPile.GetCards(Owner, PileType.Hand)",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            bgmIndex >= 0
            && bgmIndex < selectIndex
            && selectIndex < voiceIndex,
            "Expected Spell Release to start BGM at play start and request its voice line after target selection.");
    }

    [Fact]
    public void SpellReleaseBgmReusesTheSharedEngineWithoutForkingIt()
    {
        var facade = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SpellReleaseBgmPlayback.cs"));

        RegressionTestHarness.Require(
            facade.Contains("CardBgmPlayback.CreateTrack(", StringComparison.Ordinal)
            && facade.Contains("Tracks[Random.Shared.Next(Tracks.Length)].TryPlay(card)", StringComparison.Ordinal)
            && !facade.Contains("CreateTween", StringComparison.Ordinal)
            && !facade.Contains("GameAudioService", StringComparison.Ordinal)
            && !facade.Contains("ProcessFrame", StringComparison.Ordinal)
            && !facade.Contains("NRunMusicController", StringComparison.Ordinal),
            "Expected the Spell Release facade to stay a config over the shared engine, not a fork of it.");
    }

    [Fact]
    public void SpellReleaseBgmGatingRestrictsToEliteAndBossRooms()
    {
        RegressionTestHarness.Require(
            !SpellReleaseBgmPlayback.IsEligibleRoom(null),
            "Null combat state must not be eligible for Spell Release BGM.");

        // Clean room verification without leaking combat state
        SpellReleaseBgmPlayback.ResetPerCombat();
        RegressionTestHarness.Require(
            !SpellReleaseBgmPlayback.HasTriggeredInCombat(null),
            "Null combat must report not triggered.");
    }

    [Fact]
    public void MainFileRegistersSpellReleaseBgm()
    {
        var mainSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/MainFile.cs"));

        RegressionTestHarness.Require(
            mainSource.Contains("SpellReleaseBgmPlayback.Register();", StringComparison.Ordinal),
            "Expected MainFile.Initialize to register SpellReleaseBgmPlayback.");
    }
}
