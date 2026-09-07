using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Audio;

public sealed class SpellTurnBgmSuite
{
    [Fact]
    public void SpellTurnBgmUsesItsOwnReplacingCombatMusicChannel()
    {
        var options = SpellTurnBgmPlayback.CreatePlaybackOptions();
        var routing = options.Routing;

        RegressionTestHarness.Require(
            options.Volume == 0f
            && SpellTurnBgmPlayback.MusicVolume > 0f
            && SpellTurnBgmPlayback.FadeInSeconds > 0f
            && SpellTurnBgmPlayback.FadeOutSeconds > 0f
            && options.Scope == AudioLifecycleScope.Combat
            && !options.AllowFadeOutOnStop
            && routing is
            {
                ChannelMode: AudioChannelMode.ReplaceExisting,
                AllowFadeOutOnReplace: false
            }
            && routing?.Channel == $"{MainFile.ModId}.SpellTurnBgm"
            && options.DebugName == $"{MainFile.ModId}.SpellTurnBgm",
            "Expected Spell Turn to fade in from silence on one replacing, combat-scoped music channel.");

        RegressionTestHarness.Require(
            SpellTurnBgmPlayback.MusicChannel != AnotherMeBgmPlayback.MusicChannel
            && SpellTurnBgmPlayback.RelativePath == "music/platinum.ogg"
            && SpellTurnBgmPlayback.ResourcePath == $"{MainFile.ResPath}/music/platinum.ogg",
            "Expected Spell Turn to own a dedicated platinum track instead of reusing Another Me's.");
    }

    [Fact]
    public void SpellTurnRequestsBgmAfterAllTransformationGuardsPass()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Spells/SpellTurn.cs"));
        var clowGuardIndex = source.IndexOf(
            "if (selected is not ClowCard { Identity: { } identity } selectedClow)",
            StringComparison.Ordinal);
        var canonicalGuardIndex = source.IndexOf(
            "if (canonicalSakura is null || SakuraSourceCardRules.HasSakuraIdentity(Owner, identity))",
            StringComparison.Ordinal);
        var deckGuardIndex = source.IndexOf(
            "if (deckCard is null || deckCard.Pile?.Type != PileType.Deck)",
            StringComparison.Ordinal);
        var playbackIndex = source.IndexOf(
            "SpellTurnBgmPlayback.TryPlay(this);",
            StringComparison.Ordinal);
        var vfxIndex = source.IndexOf(
            "SpellTurnTransformationVfx.TryCreate(selectedClow)",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            clowGuardIndex >= 0
            && clowGuardIndex < canonicalGuardIndex
            && canonicalGuardIndex < deckGuardIndex
            && deckGuardIndex < playbackIndex
            && playbackIndex < vfxIndex,
            "Expected Spell Turn BGM to start only after every transformation guard passes and before the transformation VFX.");
    }

    [Fact]
    public void SpellTurnBgmReusesTheSharedEngineWithoutForkingIt()
    {
        var facade = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SpellTurnBgmPlayback.cs"));
        var core = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/CardBgmPlayback.cs"));

        RegressionTestHarness.Require(
            facade.Contains("CardBgmPlayback.CreateTrack(", StringComparison.Ordinal)
            && facade.Contains("Track.TryPlay(card)", StringComparison.Ordinal)
            && !facade.Contains("CreateTween", StringComparison.Ordinal)
            && !facade.Contains("GameAudioService", StringComparison.Ordinal)
            && !facade.Contains("ProcessFrame", StringComparison.Ordinal)
            && !facade.Contains("NRunMusicController", StringComparison.Ordinal),
            "Expected the Spell Turn facade to stay a config over the shared engine, not a fork of it.");

        RegressionTestHarness.Require(
            core.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && core.Contains("!SakuraModConfig.IsCardBgmEnabled()", StringComparison.Ordinal)
            && core.Contains("AudioSource.StreamingMusic(path)", StringComparison.Ordinal)
            && core.Contains("AudioLifecycleScope.Combat", StringComparison.Ordinal)
            && core.Contains("AudioChannelMode.ReplaceExisting", StringComparison.Ordinal)
            && core.Contains("SakuraGameVolumeFollower.MusicFactor()", StringComparison.Ordinal),
            "Expected the shared engine to keep local-owner, preference, combat-scope, and volume-follower contracts.");
    }

    [Fact]
    public void SpellTurnBgmResourceRemainsComplete()
    {
        const string relativePath = "SakuraMod/music/platinum.ogg";
        var audioPath = RegressionTestHarness.FindRepoFile(relativePath);
        var bytes = File.ReadAllBytes(audioPath);
        var import = File.ReadAllText($"{audioPath}.import");

        RegressionTestHarness.Require(
            bytes.Length > 4 && bytes.AsSpan(0, 4).SequenceEqual("OggS"u8),
            "Expected the Spell Turn BGM to remain a non-empty OGG stream.");
        RegressionTestHarness.Require(
            import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
            && import.Contains("loop=false", StringComparison.Ordinal),
            "Expected the Spell Turn BGM import to remain tracked and non-looping as a Godot resource.");

        var csproj = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraMod.csproj"));
        RegressionTestHarness.Require(
            csproj.Contains(
                "SakuraExternalMusic Include=\"SakuraMod/music/**/*.ogg\"",
                StringComparison.Ordinal),
            "Expected the csproj to keep shipping SakuraMod/music OGG files as external package music.");
    }
}
