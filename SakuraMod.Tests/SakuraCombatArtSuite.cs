using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Settings;

public sealed class SakuraCombatArtSuite
{
    private const string SelectedSourceHash =
        "1eed348e3755cbbad01ced2f0141ec611ad0d9013f226dc3fd33792805275ed7";
    private const string TransparentRuntimeHash =
        "d3d7d243d8c3ae7248c103a7ce32774b0e81293227a7162fe6e09e43ca417584";

    [Fact]
    public void CharacterSelectOnlyPreferencesUseDefaultsWithoutAddingSettingsPageEntries()
    {
        var page = SakuraModConfig.BuildSettingsPageForTests();
        var section = Assert.Single(
            page.Sections,
            static section => section.Id == SakuraModConfig.SectionId);
        var defaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(
            SakuraModConfig.UseChibiCombatArtBinding);
        var cardBgmDefaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(
            SakuraModConfig.EnableCardBgmBinding);
        var cardVfxDefaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(
            SakuraModConfig.EnableCardVfxBinding);

        RegressionTestHarness.Require(
            !new SakuraModConfig().UseChibiCombatArt
            && !defaultBinding.CreateDefaultValue()
            && new SakuraModConfig().EnableCardBgm
            && cardBgmDefaultBinding.CreateDefaultValue()
            && new SakuraModConfig().EnableCardVfx
            && cardVfxDefaultBinding.CreateDefaultValue()
            && section.Entries.Count == 1
            && section.Entries[0].Id == SakuraModConfig.VoiceToggleId,
            "Expected combat art, card BGM, and card VFX to keep their character-select defaults without duplicate settings-page entries.");
    }

    [Fact]
    public void PerPlayerRunPreferenceUsesSynchronizedLobbyDataWithStandardFallback()
    {
        var options = SakuraCombatArtPreference.CreateOptions();

        RegressionTestHarness.Require(
            SakuraCombatArtPreference.RunSavedDataKey == "combat_art_v1"
            && options.SchemaVersion == 1
            && options.WritePolicy == RunSavedDataWritePolicy.WhenSet
            && options.SyncLobbyOnChange
            && !new SakuraCombatArtState().UseChibi
            && !SakuraCombatArtPreference.IsChibi((SakuraCombatArtState?)null)
            && SakuraCombatArtPreference.IsChibi(new SakuraCombatArtState { UseChibi = true }),
            "Expected a synchronized per-player lobby slot with deterministic Standard fallback.");
    }

    [Fact]
    public void CharacterSelectOptionsAreAvailableForEligibleSakuraSelection()
    {
        RegressionTestHarness.Require(
            SakuraCombatArtFeature.IsEnabled
            && SakuraCharacterSelectOptionsPatch.ShouldShow(
                isSakura: true,
                isRandom: false,
                isLocked: false)
            && SakuraCharacterSelectOptionsPatch.IsEligibleSelection(
                isSakura: true,
                isRandom: false,
                isLocked: false)
            && !SakuraCharacterSelectOptionsPatch.IsEligibleSelection(
                isSakura: false,
                isRandom: false,
                isLocked: false)
            && !SakuraCharacterSelectOptionsPatch.IsEligibleSelection(
                isSakura: true,
                isRandom: true,
                isLocked: false)
            && !SakuraCharacterSelectOptionsPatch.IsEligibleSelection(
                isSakura: true,
                isRandom: false,
                isLocked: true),
            "Expected Sakura's options group to appear only for an eligible Sakura selection.");
    }

    [Fact]
    public void CharacterSelectLocalizationMatchesTheApprovedLabels()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["eng"] = ["Combat Art", "Standard", "Chibi", "Card BGM", "Card VFX", "On", "Off"],
            ["zhs"] = ["战斗立绘", "标准", "Q版", "卡牌 BGM", "卡牌特效", "开启", "关闭"]
        };

        foreach (var (locale, values) in expected)
        {
            var relativePath = $"SakuraMod/localization/{locale}/settings_ui.json";
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

            RegressionTestHarness.Require(
                settings[SakuraCharacterSelectOptionsPatch.CombatArtLabelKey] == values[0]
                && settings[SakuraCharacterSelectOptionsPatch.StandardKey] == values[1]
                && settings[SakuraCharacterSelectOptionsPatch.ChibiKey] == values[2]
                && settings[SakuraCharacterSelectOptionsPatch.CardBgmLabelKey] == values[3]
                && settings[SakuraCharacterSelectOptionsPatch.CardVfxLabelKey] == values[4]
                && settings[SakuraCharacterSelectOptionsPatch.VoiceOnKey] == values[5]
                && settings[SakuraCharacterSelectOptionsPatch.VoiceOffKey] == values[6],
                $"Expected {locale} character-select option labels to match the approved copy.");
        }
    }

    [Fact]
    public void CharacterSelectOptionsUseStableTwoRowGeometryAndInteractionStates()
    {
        var options = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCharacterSelectOptions.cs"));

        RegressionTestHarness.Require(
            options.Contains("GroupOffset", StringComparison.Ordinal)
            && options.Contains("CustomMinimumSize", StringComparison.Ordinal),
            "Expected the two-row group geometry to keep its stable offset and minimum sizes.");
        RegressionTestHarness.Require(
            options.Contains("PresentationChoices", StringComparison.Ordinal)
            && options.Contains("AddThemeConstantOverride", StringComparison.Ordinal)
            && options.Contains("TooltipText", StringComparison.Ordinal)
            && options.Contains("PanelContainer", StringComparison.Ordinal)
            && options.Contains("AddThemeStyleboxOverride", StringComparison.Ordinal)
            && options.Contains("SetAnchorsAndOffsetsPreset", StringComparison.Ordinal),
            "Expected one styled panel group hosting the compact presentation toggles.");
        RegressionTestHarness.Require(
            options.Contains("Focused", StringComparison.Ordinal)
            && options.Contains("MousePressed", StringComparison.Ordinal)
            && options.Contains("MouseReleased", StringComparison.Ordinal)
            && options.Contains("Darkened", StringComparison.Ordinal),
            "Expected native focus and mouse interaction feedback with a pressed surface tint.");
    }

    [Fact]
    public void PresentationChoicesUseExistingLocalBindingsAndThreeColumnFocusGraph()
    {
        var options = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCharacterSelectOptions.cs"));

        RegressionTestHarness.Require(
            CountOccurrences(
                options,
                "[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]") == 1
            && options.Contains("EnableSakuraVoiceBinding", StringComparison.Ordinal)
            && options.Contains("EnableCardBgmBinding", StringComparison.Ordinal)
            && options.Contains("EnableCardVfxBinding", StringComparison.Ordinal)
            && !options.Contains("RunSavedData", StringComparison.Ordinal)
            && !options.Contains("RegisterPerPlayer", StringComparison.Ordinal)
            && options.Contains("SetFocusNeighbors", StringComparison.Ordinal)
            && options.Contains("FocusNeighborTop", StringComparison.Ordinal)
            && options.Contains("RestoreFocusNeighbors", StringComparison.Ordinal),
            "Expected one patch to reuse all three local presentation bindings and connect the two-row option grid to native focus neighbors.");
    }

    [Fact]
    public void ChibiKeepsTheApprovedStaticFallbackAndFiveRigLayers()
    {
        const string selectedRelativePath =
            "research/sakura-chibi-combat/selected/clow-wand-source.png";
        const string transparentRelativePath =
            "artwork/sakura-chibi-combat/sakura_chibi_combat_clow_wand.png";
        const string runtimeRelativePath =
            "SakuraMod/images/charui/chibi_combat/sakura_clow_wand_body.png";

        var selected = RegressionTestHarness.FindRepoFile(selectedRelativePath);
        var transparent = RegressionTestHarness.FindRepoFile(transparentRelativePath);
        var runtime = RegressionTestHarness.FindRepoFile(runtimeRelativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");

        RegressionTestHarness.Require(
            Sha256(selected) == SelectedSourceHash,
            "Expected the selected source to remain byte-identical to Image #1.");
        RegressionTestHarness.Require(
            Sha256(transparent) == TransparentRuntimeHash
            && Sha256(runtime) == TransparentRuntimeHash,
            "Expected artwork and runtime chibi PNGs to remain the approved transparent Image #1 derivative.");
        RegressionTestHarness.Require(
            BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1254
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1254
            && header[24] == 8
            && header[25] == 6,
            "Expected the chibi runtime asset to remain a 1254x1254 8-bit RGBA PNG.");
        RegressionTestHarness.Require(
            import.Contains("mipmaps/generate=false", StringComparison.Ordinal),
            "Expected the chibi runtime asset to retain its tracked non-mipmapped import.");

        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(runtime)!, "../../../.."));
        var layerFiles = new[]
        {
            "body_core_completed.png",
            "head.png",
            "screen_left_arm.png",
            "screen_right_arm.png",
            "wand_completed_regenerated.png"
        };
        RegressionTestHarness.Require(
            layerFiles.All(file =>
            {
                var path = Path.Combine(
                    repoRoot,
                    "SakuraMod/images/charui/chibi_combat/layers",
                    file);
                if (!File.Exists(path) || !File.Exists($"{path}.import"))
                    return false;

                var layerImport = File.ReadAllText($"{path}.import");
                return layerImport.Contains(
                    "mipmaps/generate=false",
                    StringComparison.Ordinal);
            })
            && File.Exists(Path.Combine(
                repoRoot,
                "SakuraMod/scenes/charui/sakura_chibi_combat_idle_rigged.tscn"))
            && !File.Exists(Path.Combine(
                repoRoot,
                "SakuraMod/images/charui/chibi_combat/sakura_face_blink.png")),
            "Expected one approved static fallback plus five imported layered rig textures without a blink draft.");
    }

    [Fact]
    public void ChibiVisualBranchExplicitlyUsesTheLayeredStandeeFactory()
    {
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var standeeFactory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.ChibiVisualPath.EndsWith(
                "SakuraMod/images/charui/chibi_combat/sakura_clow_wand_body.png",
                StringComparison.Ordinal)
            && adapter.Contains("CreateWithChibiLayeredIdle", StringComparison.Ordinal)
            && adapter.Contains("combatArtFeatureEnabled && useChibi", StringComparison.Ordinal)
            && adapter.Contains("ChibiScale", StringComparison.Ordinal)
            && standeeFactory.Contains("attachChibiLayeredIdle", StringComparison.Ordinal)
            && standeeFactory.Contains("playIdleMotion", StringComparison.Ordinal)
            && standeeFactory.Contains("SakuraChibiStandeeIdleController.Attach", StringComparison.Ordinal),
            "Expected Q版 to use its layered idle while preserving the approved static texture as factory input.");
    }

    [Fact]
    public void FrogRaincoatAppliesOnlyToTheStandardVisualBranch()
    {
        RegressionTestHarness.Require(
            SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: false,
                hasRedCape: false,
                hasFrogRaincoat: false) == SakuraCombatVisualVariant.Standard
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: false,
                hasRedCape: false,
                hasFrogRaincoat: true) == SakuraCombatVisualVariant.FrogRaincoat
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: true,
                hasRedCape: false,
                hasFrogRaincoat: false) == SakuraCombatVisualVariant.Chibi
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: true,
                hasRedCape: false,
                hasFrogRaincoat: true) == SakuraCombatVisualVariant.FrogRaincoatChibi
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: false,
                useChibi: true,
                hasRedCape: false,
                hasFrogRaincoat: true) == SakuraCombatVisualVariant.FrogRaincoat
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: false,
                hasRedCape: false,
                hasFrogRaincoat: false,
                hasPinkTransformationCostume: true) == SakuraCombatVisualVariant.PinkTransformation
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: false,
                hasRedCape: false,
                hasFrogRaincoat: true,
                hasPinkTransformationCostume: true) == SakuraCombatVisualVariant.FrogRaincoat
            && SakuraCombatVisuals.ResolveVariant(
                combatArtFeatureEnabled: true,
                useChibi: true,
                hasRedCape: false,
                hasFrogRaincoat: false,
                hasPinkTransformationCostume: true) == SakuraCombatVisualVariant.PinkTransformationChibi,
            "Expected completed Chibi costumes to replace the base Chibi standee while preserving body-style priority.");
    }

    [Fact]
    public void RedCapeUsesDedicatedWholeSpriteArtAndTakesCostumePriority()
    {
        const string relativePath = "SakuraMod/images/charui/outfits/red_cape_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var patch = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatArtPreference.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.RedCapeVisualPath.EndsWith(relativePath, StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1024
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1312
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("RedCapeScale", StringComparison.Ordinal)
            && adapter.Contains("RedCapeBounds", StringComparison.Ordinal)
            && patch.Contains("GetRelic<ClassicRedCapeRelic>", StringComparison.Ordinal)
            && SakuraCombatVisuals.ResolveVariant(true, false, true, false, false)
                == SakuraCombatVisualVariant.RedCape
            && SakuraCombatVisuals.ResolveVariant(true, false, true, true, true)
                == SakuraCombatVisualVariant.RedCape
            && SakuraCombatVisuals.ResolveVariant(true, true, true, false, false)
                == SakuraCombatVisualVariant.RedCapeChibi,
            "Expected Red Cape to select the dedicated Standard or Chibi standee for the active body style.");
    }

    [Fact]
    public void RedCapeChibiUsesTheApprovedTransparentWholeSpriteStandee()
    {
        const string relativePath = "SakuraMod/images/charui/outfits/red_cape_chibi_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.RedCapeChibiVisualPath.EndsWith(relativePath, StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1024
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1536
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("RedCapeChibiScale", StringComparison.Ordinal)
            && adapter.Contains("CreateWithWholeSpriteIdle", StringComparison.Ordinal),
            "Expected the approved Red Cape Chibi RGBA standee to use its own whole-sprite branch.");
    }

    [Fact]
    public void FrogRaincoatUsesOneTransparentWholeSpriteStandee()
    {
        const string relativePath =
            "SakuraMod/images/charui/outfits/frog_raincoat_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var factory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var patch = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatArtPreference.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.FrogRaincoatVisualPath.EndsWith(relativePath, StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 978
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1343
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("CreateWithWholeSpriteIdle", StringComparison.Ordinal)
            && factory.Contains("CreateWithWholeSpriteIdle", StringComparison.Ordinal)
            && patch.Contains("GetRelic<ClassicFrogRaincoatRelic>", StringComparison.Ordinal),
            "Expected Frog Raincoat to use one imported RGBA standee with whole-sprite idle and relic-aware dispatch.");
    }

    [Fact]
    public void FrogRaincoatChibiUsesTheApprovedTransparentWholeSpriteStandee()
    {
        const string relativePath =
            "SakuraMod/images/charui/outfits/frog_raincoat_chibi_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.FrogRaincoatChibiVisualPath.EndsWith(
                relativePath,
                StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1024
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1536
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("FrogRaincoatChibiScale", StringComparison.Ordinal)
            && adapter.Contains("FrogRaincoatChibiBounds", StringComparison.Ordinal)
            && SakuraCombatVisuals.ResolveVariant(true, true, false, true, false)
                == SakuraCombatVisualVariant.FrogRaincoatChibi,
            "Expected the approved Frog Raincoat Chibi RGBA standee to use its own whole-sprite branch.");
    }

    [Fact]
    public void PinkTransformationUsesOneTransparentWholeSpriteStandee()
    {
        const string relativePath =
            "SakuraMod/images/charui/outfits/pink_transformation_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var patch = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatArtPreference.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.PinkTransformationVisualPath.EndsWith(relativePath, StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 980
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1492
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("PinkTransformationTextureFile", StringComparison.Ordinal)
            && adapter.Contains("PinkTransformationScale", StringComparison.Ordinal)
            && adapter.Contains("PinkTransformationBounds", StringComparison.Ordinal)
            && adapter.Contains("CreateWithWholeSpriteIdle", StringComparison.Ordinal)
            && patch.Contains("GetRelic<ClassicPinkTransformationCostumeRelic>", StringComparison.Ordinal),
            "Expected Pink Transformation Costume to use one imported RGBA standee with whole-sprite idle and relic-aware dispatch.");
    }

    [Fact]
    public void PinkTransformationChibiUsesTheApprovedTransparentWholeSpriteStandee()
    {
        const string relativePath =
            "SakuraMod/images/charui/outfits/pink_transformation_chibi_standee.png";
        var runtime = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));

        RegressionTestHarness.Require(
            SakuraCombatVisuals.PinkTransformationChibiVisualPath.EndsWith(
                relativePath,
                StringComparison.Ordinal)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1024
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1536
            && header[24] == 8
            && header[25] == 6
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
            && adapter.Contains("PinkTransformationChibiScale", StringComparison.Ordinal)
            && adapter.Contains("PinkTransformationChibiBounds", StringComparison.Ordinal)
            && SakuraCombatVisuals.ResolveVariant(true, true, false, false, true)
                == SakuraCombatVisualVariant.PinkTransformationChibi,
            "Expected the approved Pink Transformation Chibi RGBA standee to use its own whole-sprite branch.");
    }

    [Fact]
    public void PlayerAwareDispatchLeavesOtherCharactersOnTheirOriginalVisualPath()
    {
        var preference = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatArtPreference.cs"));
        var options = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCharacterSelectOptions.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var main = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/MainFile.cs"));

        RegressionTestHarness.Require(
            !SakuraPlayerCombatVisualPatch.ShouldOverride(isTestMode: true, isSakuraPlayer: true)
            && !SakuraPlayerCombatVisualPatch.ShouldOverride(isTestMode: false, isSakuraPlayer: false)
            && SakuraPlayerCombatVisualPatch.ShouldOverride(isTestMode: false, isSakuraPlayer: true)
            && preference.Contains("CreateVisuals", StringComparison.Ordinal)
            && preference.Contains("lobby.LocalPlayer.id", StringComparison.Ordinal)
            && preference.Contains("RegisterPerPlayer", StringComparison.Ordinal)
            && preference.Contains("SyncLobbyOnChange", StringComparison.Ordinal)
            && options.Contains("SetLocalLobbyPreference", StringComparison.Ordinal)
            && options.Contains("GetOrInitializeLocalLobbyPreference", StringComparison.Ordinal)
            && !visuals.Contains("SakuraModConfig", StringComparison.Ordinal)
            && main.IndexOf("SakuraCombatArtPreference.Register();", StringComparison.Ordinal)
                > main.IndexOf("SakuraModConfig.Register();", StringComparison.Ordinal),
            "Expected local-player lobby writes and Sakura-only run-state visual dispatch without changing mixed-character visuals.");
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
