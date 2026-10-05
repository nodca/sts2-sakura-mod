using STS2RitsuLib.Audio;
using STS2RitsuLib.Settings;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode;
using System.Text.Json;

public sealed class SakuraVoiceCueSuite
{
    [Fact]
    public void VoiceSettingUsesOneDefaultOffRitsuToggle()
    {
        var page = SakuraModConfig.BuildSettingsPageForTests();
        var section = Assert.Single(
            page.Sections,
            static section => section.Id == SakuraModConfig.SectionId);
        var toggle = Assert.IsType<ToggleModSettingsEntryDefinition>(Assert.Single(section.Entries));
        var defaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(toggle.Binding);

        RegressionTestHarness.Require(
            !new SakuraModConfig().EnableSakuraVoice
            && !defaultBinding.CreateDefaultValue(),
            "Expected one RitsuLib-backed Sakura voice toggle with a false default.");

        foreach (var locale in new[] { "eng", "zhs" })
        {
            var relativePath = $"SakuraMod/localization/{locale}/settings_ui.json";
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
            RegressionTestHarness.Require(
                !string.IsNullOrWhiteSpace(settings[SakuraModConfig.VoiceTitleKey])
                && !string.IsNullOrWhiteSpace(settings[SakuraModConfig.VoiceDescriptionKey]),
                $"Expected {locale} Sakura voice setting label and description.");
        }
    }

    [Fact]
    public void VoiceCuesMapToIndependentPerCombatGroups()
    {
        RegressionTestHarness.Require(
            SakuraVoicePlayback.LineFor(new SpellSeal()) == SakuraVoiceLines.Seal
            && SakuraVoicePlayback.LineFor(new GrowingMagic()) == SakuraVoiceLines.Seal
            && SakuraVoicePlayback.LineFor(new SpellRelease()) is null
            && SakuraVoicePlayback.LineFor(new ClowArrow()) is null
            && SakuraVoiceLines.Release.ResourcePath == $"{MainFile.ResPath}/voices/dream_wand.ogg"
            && SakuraVoiceLines.Seal.ResourcePath == $"{MainFile.ResPath}/voices/stabilize.ogg"
            && SakuraVoiceLines.Release.Key != SakuraVoiceLines.Seal.Key,
            "Expected Seal/Growing Magic to share the Seal line and Spell Release to route through its own release entry.");

        var gate = new SakuraVoiceCueGate();
        var firstCombat = new object();
        var secondCombat = new object();
        var release = SakuraVoiceLines.Release.Key;
        var seal = SakuraVoiceLines.Seal.Key;
        RegressionTestHarness.Require(
            gate.CanPlay(firstCombat, release)
            && gate.CanPlay(firstCombat, release),
            "Expected an unplayed cue to remain available until playback succeeds.");

        gate.MarkPlayed(firstCombat, release);
        RegressionTestHarness.Require(
            !gate.CanPlay(firstCombat, release)
            && gate.CanPlay(firstCombat, seal),
            "Expected only successfully played cues to be consumed.");

        gate.MarkPlayed(firstCombat, seal);
        RegressionTestHarness.Require(
            !gate.CanPlay(firstCombat, seal)
            && gate.CanPlay(secondCombat, seal)
            && gate.CanPlay(secondCombat, release),
            "Expected two independent once-per-combat cue groups that reset for a new combat identity.");
    }

    [Fact]
    public void CardVoiceLinesBindToSourceIdentityAcrossEras()
    {
        var clowSword = SakuraCardVoiceCatalog.For(new ClowSword());
        var sakuraSword = SakuraCardVoiceCatalog.For(new SakuraSword());

        RegressionTestHarness.Require(
            clowSword is { } line
            && clowSword == sakuraSword
            && line.RelativePath == "voices/cards/sword.ogg"
            && SakuraCardVoiceCatalog.For(new ClowArrow()) is null
            && SakuraCardVoiceCatalog.For(new SpellRelease()) is null,
            "Expected ClowSword and SakuraSword to share the Sword line and cards without a line to map to none.");

        var keys = SakuraVoiceLines.All.Select(static line => line.Key).ToList();
        RegressionTestHarness.Require(
            keys.Count == keys.Distinct(StringComparer.Ordinal).Count()
            && SakuraCardVoiceCatalog.All.All(static line =>
                line.RelativePath.StartsWith("voices/cards/", StringComparison.Ordinal)
                && line.RelativePath.EndsWith(".ogg", StringComparison.Ordinal)),
            "Expected unique voice gate keys and per-card lines under voices/cards/.");
    }

    [Fact]
    public void ReleaseUsesEitherTheCardLineOrTheGenericLineNeverBoth()
    {
        var sword = SakuraCardVoiceCatalog.For(new ClowSword())!.Value;
        RegressionTestHarness.Require(
            SakuraVoicePlayback.ReleaseLineFor(new SakuraSword()) == sword
            && SakuraVoicePlayback.ReleaseLineFor(new ClowArrow()) == SakuraVoiceLines.Release
            && SakuraVoicePlayback.ReleaseLineFor(null) == SakuraVoiceLines.Release,
            "Expected a card with its own line to use only that line and every other release the generic line.");

        var gate = new SakuraVoiceCueGate();
        var combat = new object();
        gate.MarkPlayed(combat, sword.Key);
        RegressionTestHarness.Require(
            !gate.CanPlay(combat, sword.Key)
            && gate.CanPlay(combat, SakuraVoiceLines.Release.Key)
            && gate.CanPlay(new object(), sword.Key),
            "Expected card lines and the generic Release line to be gated independently and reset per combat.");

        var playback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraVoicePlayback.cs"));
        RegressionTestHarness.Require(
            playback.Contains("TryPlayGated(card, ReleaseLineFor(releasedTarget))", StringComparison.Ordinal)
            && !playback.Contains("foreach (var line in candidates)", StringComparison.Ordinal),
            "Expected a played card line to stay silent rather than fall back to the generic Release line.");
    }

    [Fact]
    public void CardLineGainTrimsTheEnvelopeWithoutMutingIt()
    {
        foreach (var line in SakuraVoiceLines.All)
            RegressionTestHarness.Require(
                line.Gain is > 0f and <= 1f,
                $"Expected voice line {line.Key} gain in (0, 1].");

        var playback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraVoicePlayback.cs"));
        RegressionTestHarness.Require(
            playback.Contains("volume * _activeGain * SakuraGameVolumeFollower.VoiceFactor()", StringComparison.Ordinal)
            && playback.Contains("_envelopeVolume * _activeGain * SakuraGameVolumeFollower.VoiceFactor()", StringComparison.Ordinal),
            "Expected both envelope writes and the per-frame refresh to apply the active line gain.");
    }

    [Fact]
    public void VoiceCuesUseOneNonOverlappingFadedCombatChannel()
    {
        foreach (var line in SakuraVoiceLines.All)
        {
            var options = SakuraVoicePlayback.CreatePlaybackOptions(line);
            RegressionTestHarness.Require(
                options.Volume == 0f
                && options.Scope == AudioLifecycleScope.Combat
                && options.Routing?.Channel == SakuraVoicePlayback.VoiceChannel
                && options.Routing.ChannelMode == AudioChannelMode.KeepExisting,
                $"Expected voice line {line.Key} to start silent on the shared keep-existing combat channel.");
        }

        RegressionTestHarness.Require(
            SakuraVoicePlayback.FadeInSeconds > 0f
            && SakuraVoicePlayback.FadeOutSeconds > 0f,
            "Expected voice lines to use a fade envelope.");
    }

    [Fact]
    public void VoiceCuesPlayLooseFilesResolvedBesideTheAssembly()
    {
        var playback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraVoicePlayback.cs"));
        var separator = Path.DirectorySeparatorChar;

        foreach (var line in SakuraVoiceLines.All)
        {
            var external = SakuraVoicePlayback.ExternalVoicePathFor(line);
            RegressionTestHarness.Require(
                Path.IsPathRooted(external)
                && external.EndsWith(line.RelativePath.Replace('/', separator), StringComparison.Ordinal),
                $"Expected voice line {line.Key} to resolve a loose package file beside the mod assembly.");
        }

        RegressionTestHarness.Require(
            playback.Contains("AudioSource.File(externalPath)", StringComparison.Ordinal)
            && !playback.Contains("ResourceSoundFileSource", StringComparison.Ordinal)
            && playback.IndexOf("File.Exists(externalPath)", StringComparison.Ordinal)
                < playback.IndexOf("AudioSource.File(externalPath)", StringComparison.Ordinal),
            "Expected FMOD playback to be gated on loose file presence.");
    }

    [Fact]
    public void EligibleCardsRequestTheirVoiceCueAtTheRightMoment()
    {
        var seal = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/SpellSeal.cs"));
        var release = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/SpellRelease.cs"));
        var ancientCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Ancients/GrowingMagic.cs"));

        RegressionTestHarness.Require(
            CountOccurrences(seal, "SakuraVoicePlayback.TryPlay(this);") == 1
            && CountOccurrences(ancientCard, "SakuraVoicePlayback.TryPlay(this);") == 1
            && CountOccurrences(release, "SakuraVoicePlayback.TryPlay(this);") == 0
            && CountOccurrences(release, "SakuraVoicePlayback.TryPlayRelease(this, selected);") == 1,
            "Expected SpellSeal and GrowingMagic to request the Seal cue and SpellRelease to use its release request only.");

        var select = release.IndexOf("CardSelectCmd.FromHand(", StringComparison.Ordinal);
        var apply = release.IndexOf("ApplyRelease(selected);", StringComparison.Ordinal);
        var voice = release.IndexOf("SakuraVoicePlayback.TryPlayRelease(this, selected);", StringComparison.Ordinal);
        var vulnerable = release.IndexOf("PowerCmd.Apply<VulnerablePower>(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            select >= 0 && select < apply && apply < voice && voice < vulnerable,
            "Expected Spell Release to request its voice after target selection and release, before Vulnerable.");
    }

    [Fact]
    public void VoicePlaybackRequiresTheCardOwnerToBeLocal()
    {
        var playback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraVoicePlayback.cs"));
        var localOwnerGuard = playback.IndexOf(
            "if (!LocalContext.IsMe(card.Owner)",
            StringComparison.Ordinal);
        var settingGuard = playback.IndexOf(
            "|| !SakuraModConfig.IsSakuraVoiceEnabled()",
            StringComparison.Ordinal);
        var cueGuard = playback.IndexOf(
            "if (CueGate.CanPlay(combatState, line.Key))",
            StringComparison.Ordinal);
        var markPlayed = playback.IndexOf(
            "CueGate.MarkPlayed(combatState, line.Key);",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            localOwnerGuard >= 0
            && localOwnerGuard < settingGuard
            && settingGuard < cueGuard
            && cueGuard < markPlayed,
            "Expected remote card plays to be rejected before reading local voice settings or claiming a combat cue.");
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
