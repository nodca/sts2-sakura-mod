using STS2RitsuLib.Audio;
using STS2RitsuLib.Settings;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode;
using System.Text.Json;

public sealed class SakuraVoiceCueSuite
{
    [Fact]
    public void VoiceSettingUsesOneDefaultOnRitsuToggle()
    {
        var page = SakuraModConfig.BuildSettingsPageForTests();
        var section = Assert.Single(
            page.Sections,
            static section => section.Id == SakuraModConfig.SectionId);
        var toggle = Assert.IsType<ToggleModSettingsEntryDefinition>(Assert.Single(section.Entries));
        var defaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(toggle.Binding);

        RegressionTestHarness.Require(
            new SakuraModConfig().EnableSakuraVoice
            && page.ModId == MainFile.ModId
            && page.Id == SakuraModConfig.PageId
            && section.Id == SakuraModConfig.SectionId
            && toggle.Id == SakuraModConfig.VoiceToggleId
            && defaultBinding.CreateDefaultValue(),
            "Expected one RitsuLib-backed Sakura voice toggle with a true default.");

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
            SakuraVoicePlayback.CueFor(new SpellRelease()) == SakuraVoiceCue.Release
            && SakuraVoicePlayback.CueFor(new SpellSeal()) == SakuraVoiceCue.Seal
            && SakuraVoicePlayback.CueFor(new GrowingMagic()) == SakuraVoiceCue.Seal
            && SakuraVoicePlayback.CueFor(new ClowArrow()) is null
            && SakuraVoicePlayback.PathFor(SakuraVoiceCue.Release) == SakuraVoicePlayback.ReleaseVoicePath
            && SakuraVoicePlayback.PathFor(SakuraVoiceCue.Seal) == SakuraVoicePlayback.SealVoicePath,
            "Expected Release to use its own cue and Seal/Growing Magic to share the Seal cue.");

        var gate = new SakuraVoiceCueGate();
        var firstCombat = new object();
        var secondCombat = new object();
        RegressionTestHarness.Require(
            gate.CanPlay(firstCombat, SakuraVoiceCue.Release)
            && gate.CanPlay(firstCombat, SakuraVoiceCue.Release),
            "Expected an unplayed cue to remain available until playback succeeds.");

        gate.MarkPlayed(firstCombat, SakuraVoiceCue.Release);
        RegressionTestHarness.Require(
            !gate.CanPlay(firstCombat, SakuraVoiceCue.Release)
            && gate.CanPlay(firstCombat, SakuraVoiceCue.Seal),
            "Expected only successfully played cues to be consumed.");

        gate.MarkPlayed(firstCombat, SakuraVoiceCue.Seal);
        RegressionTestHarness.Require(
            !gate.CanPlay(firstCombat, SakuraVoiceCue.Seal)
            && gate.CanPlay(secondCombat, SakuraVoiceCue.Seal)
            && gate.CanPlay(secondCombat, SakuraVoiceCue.Release),
            "Expected two independent once-per-combat cue groups that reset for a new combat identity.");
    }

    [Fact]
    public void VoiceCuesUseOneNonOverlappingFadedCombatChannel()
    {
        var releaseOptions = SakuraVoicePlayback.CreatePlaybackOptions(SakuraVoiceCue.Release);
        var sealOptions = SakuraVoicePlayback.CreatePlaybackOptions(SakuraVoiceCue.Seal);

        RegressionTestHarness.Require(
            releaseOptions.Volume == 0f
            && sealOptions.Volume == 0f
            && releaseOptions.Scope == AudioLifecycleScope.Combat
            && sealOptions.Scope == AudioLifecycleScope.Combat
            && releaseOptions.Routing?.Channel == SakuraVoicePlayback.VoiceChannel
            && sealOptions.Routing?.Channel == SakuraVoicePlayback.VoiceChannel
            && releaseOptions.Routing.ChannelMode == AudioChannelMode.KeepExisting
            && sealOptions.Routing.ChannelMode == AudioChannelMode.KeepExisting
            && SakuraVoicePlayback.FadeInSeconds > 0f
            && SakuraVoicePlayback.FadeOutSeconds > 0f,
            "Expected both cues to start silent on one keep-existing combat channel with a fade envelope.");
    }

    [Fact]
    public void EligibleCardsRequestTheirVoiceCueAtPlayStart()
    {
        var spellCards = string.Join(
            '\n',
            File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/SpellSeal.cs")),
            File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/SpellRelease.cs")));
        var ancientCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Ancients/GrowingMagic.cs"));

        RegressionTestHarness.Require(
            CountOccurrences(spellCards, "SakuraVoicePlayback.TryPlay(this);") == 2
            && CountOccurrences(ancientCard, "SakuraVoicePlayback.TryPlay(this);") == 1,
            "Expected SpellRelease, SpellSeal, and GrowingMagic to be the only card play bodies requesting Sakura voice cues.");
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
            "|| !CueGate.CanPlay(combatState, cue.Value)",
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            localOwnerGuard >= 0
            && localOwnerGuard < settingGuard
            && settingGuard < cueGuard,
            "Expected remote card plays to be rejected before reading local voice settings or claiming a combat cue.");
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
