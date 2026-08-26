using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.Powers;
using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

public sealed class ResourceContractSuite
{
    [Fact]
    public void TomoyoAncientCostumeBackgroundRemainsReadyForEventIntegration()
    {
        const string imageRelativePath = "SakuraMod/images/events/tomoyo_ancient_costumes.png";
        const string sceneRelativePath = "SakuraMod/scenes/events/tomoyo_ancient_costumes_background.tscn";
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(sceneRelativePath));

        RequirePngWithImport(imageRelativePath, 1920, 1080);
        RegressionTestHarness.Require(
            scene.Contains($"path=\"res://{imageRelativePath}\"", StringComparison.Ordinal)
            && scene.Contains("anchors_preset = 15", StringComparison.Ordinal)
            && scene.Contains("expand_mode = 1", StringComparison.Ordinal)
            && scene.Contains("stretch_mode = 5", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected the Tomoyo Ancient costume background to remain a full-screen, input-transparent 16:9 scene.");

        RequirePngWithImport("SakuraMod/images/events/tomoyo_ancient_icon.png", 128, 128);
        RequirePngWithImport("SakuraMod/images/events/tomoyo_ancient_icon_outline.png", 128, 128);
    }

    [Fact]
    public void MonsterEventArtworkAndRelicIconsRemainPackaged()
    {
        const string imageRelativePath = "SakuraMod/images/events/monster_event.png";

        RequirePngWithImport(imageRelativePath, 1920, 1080);

        RequirePngWithImport("SakuraMod/images/relics/monster.png", 128, 128);
        RequirePngWithImport("SakuraMod/images/relics/monster_outline.png", 128, 128);
        RequirePngWithImport("SakuraMod/images/relics/big/monster.png", 128, 128);
    }

    [Fact]
    public void ThunderExtraEffectDescriptionsRemainLocalized()
    {
        const string thunderExtraKey = "SAKURA_MOD_CARD_CLOW_THUNDER.extraDescription";
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var relativePath = $"SakuraMod/localization/{locale}/cards.json";
            var cards = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

            RegressionTestHarness.Require(
                cards.TryGetValue(thunderExtraKey, out var description)
                && description.Contains("{Damage:diff()}", StringComparison.Ordinal)
                && description.Contains("{Magic:diff()}", StringComparison.Ordinal),
                $"Expected {locale} Thunder extra-effect description to be localized with its dynamic values.");
        }
    }

    [Fact]
    public void SakuraMerchantStandeeRemainsLargeAndBottomAligned()
    {
        const string sceneRelativePath = "SakuraMod/scenes/merchant/sakura_merchant_character.tscn";
        const string standeeRelativePath = "SakuraMod/images/charui/sakura_battle_standee.png";
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(sceneRelativePath));
        var standeePath = RegressionTestHarness.FindRepoFile(standeeRelativePath);

        RegressionTestHarness.Require(
            scene.Contains("position = Vector2(0, -468.16)", StringComparison.Ordinal)
            && scene.Contains("scale = Vector2(0.56, 0.56)", StringComparison.Ordinal),
            "Expected the Sakura merchant standee to use the approved large, bottom-aligned layout.");
        RegressionTestHarness.Require(
            scene.Contains($"path=\"res://{standeeRelativePath}\"", StringComparison.Ordinal)
            && File.Exists(standeePath)
            && File.Exists($"{standeePath}.import"),
            "Expected the Sakura merchant scene to retain its tracked standee texture.");
    }

    [Fact]
    public void SakuraMultiplayerHandsRemainNativeSizedAndWired()
    {
        var sakura = new ClassicSakura();
        var hands = new[]
        {
            (sakura.CustomArmPointingTexturePath, "multiplayer_hand_sakura_point.png"),
            (sakura.CustomArmRockTexturePath, "multiplayer_hand_sakura_rock.png"),
            (sakura.CustomArmPaperTexturePath, "multiplayer_hand_sakura_paper.png"),
            (sakura.CustomArmScissorsTexturePath, "multiplayer_hand_sakura_scissors.png")
        };
        var modRoot = Path.GetDirectoryName(
            RegressionTestHarness.FindRepoFile("SakuraMod/mod_image.png"))!;
        var allImportUids = Directory
            .EnumerateFiles(modRoot, "*.import", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .Where(static line => line.StartsWith("uid=\"uid://", StringComparison.Ordinal))
            .ToList();

        foreach (var (runtimePath, fileName) in hands)
        {
            var relativePath = $"SakuraMod/images/ui/hands/{fileName}";
            var path = RegressionTestHarness.FindRepoFile(relativePath);
            var header = File.ReadAllBytes(path).AsSpan(0, 26);
            var importLines = File.ReadAllLines($"{path}.import");
            var uidLine = importLines.Single(
                static line => line.StartsWith("uid=\"uid://", StringComparison.Ordinal));

            RequirePngWithImport(relativePath, 422, 1200);
            RegressionTestHarness.Require(
                header[25] == 6,
                $"Expected {relativePath} to retain an RGBA alpha channel.");
            RegressionTestHarness.Require(
                runtimePath.EndsWith(relativePath, StringComparison.Ordinal),
                $"Expected ClassicSakura to expose {relativePath} through its native RitsuLib hand override.");
            RegressionTestHarness.Require(
                importLines.Contains("process/channel_remap/alpha=3")
                && allImportUids.Count(line => line == uidLine) == 1,
                $"Expected {relativePath}.import to retain alpha and a globally unique Godot UID.");
        }
    }

    [Fact]
    public void SakuraRestSiteSceneRetainsItsAnimationAndLayerContract()
    {
        const string sceneRelativePath = "SakuraMod/scenes/rest_site/sakura_rest_site_character.tscn";
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(sceneRelativePath));

        RequirePngWithImport("SakuraMod/images/charui/rest_site/sakura_open.png", 1024, 1024);
        RequirePngWithImport("SakuraMod/images/charui/rest_site/sakura_closed.png", 1024, 1024);
        RequirePngWithImport("SakuraMod/images/charui/rest_site/kero_sleeping.png", 191, 253);
        RequirePngWithImport("SakuraMod/images/charui/rest_site/sakura_staff.png", 1024, 1024);

        foreach (var texturePath in new[]
        {
            "SakuraMod/images/charui/rest_site/sakura_open.png",
            "SakuraMod/images/charui/rest_site/sakura_closed.png",
            "SakuraMod/images/charui/rest_site/kero_sleeping.png",
            "SakuraMod/images/charui/rest_site/sakura_staff.png"
        })
        {
            var header = File.ReadAllBytes(RegressionTestHarness.FindRepoFile(texturePath)).AsSpan(0, 26);
            RegressionTestHarness.Require(
                header[25] == 6,
                $"Expected {texturePath} to retain its RGBA transparency.");
        }

        RegressionTestHarness.Require(
            scene.Contains("path=\"res://SakuraMod/images/charui/rest_site/sakura_open.png\"", StringComparison.Ordinal)
            && scene.Contains("path=\"res://SakuraMod/images/charui/rest_site/sakura_closed.png\"", StringComparison.Ordinal)
            && scene.Contains("path=\"res://SakuraMod/images/charui/rest_site/kero_sleeping.png\"", StringComparison.Ordinal)
            && scene.Contains("path=\"res://SakuraMod/images/charui/rest_site/sakura_staff.png\"", StringComparison.Ordinal),
            "Expected the Sakura rest-site scene to retain the approved dozing, Kero, and staff layers.");
        RegressionTestHarness.Require(
            scene.Contains("[node name=\"ControlRoot\" type=\"Control\" parent=\".\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Hitbox\" type=\"Control\" parent=\"ControlRoot\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"ThoughtBubbleLeft\" type=\"Control\" parent=\"ControlRoot\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"ThoughtBubbleRight\" type=\"Control\" parent=\"ControlRoot\"]", StringComparison.Ordinal),
            "Expected the Sakura rest-site scene to retain RitsuLib's interaction and thought-bubble anchors.");
        RegressionTestHarness.Require(
            scene.Contains("resource_name = \"rest_idle\"", StringComparison.Ordinal)
            && scene.Contains("length = 12.0", StringComparison.Ordinal)
            && scene.Contains("loop_mode = 1", StringComparison.Ordinal)
            && scene.Contains("Visuals/SakuraRoot/SakuraOpen:visible", StringComparison.Ordinal)
            && scene.Contains("Visuals/SakuraRoot/SakuraClosed:visible", StringComparison.Ordinal)
            && scene.Contains("Visuals/KeroRoot:position", StringComparison.Ordinal)
            && !scene.Contains("Visuals/SakuraRoot:scale", StringComparison.Ordinal)
            && !scene.Contains("Visuals/SakuraRoot:position", StringComparison.Ordinal),
            "Expected the dozing Sakura to remain still while Kero retains its float loop.");
        RegressionTestHarness.Require(
            scene.Contains("position = Vector2(110, -85)", StringComparison.Ordinal)
            && scene.Contains("offset_left = -176.0", StringComparison.Ordinal)
            && scene.Contains("offset_top = -345.0", StringComparison.Ordinal)
            && scene.Contains("offset_right = 396.0", StringComparison.Ordinal)
            && scene.Contains("offset_bottom = 180.0", StringComparison.Ordinal)
            && scene.Contains(
                "offset_left = -50.0\noffset_top = -305.0\noffset_right = -50.0\noffset_bottom = -305.0",
                StringComparison.Ordinal)
            && scene.Contains(
                "offset_left = 270.0\noffset_top = -305.0\noffset_right = 270.0\noffset_bottom = -305.0",
                StringComparison.Ordinal),
            "Expected the Sakura rest-site art and interaction anchors to retain the approved seated alignment.");
        RegressionTestHarness.Require(
            scene.Contains("[node name=\"StaffRoot\" type=\"Node2D\" parent=\"ControlRoot/Visuals\"]", StringComparison.Ordinal)
            && scene.Contains("position = Vector2(430, -40)", StringComparison.Ordinal)
            && scene.Contains("scale = Vector2(0.75, 0.75)", StringComparison.Ordinal)
            && scene.IndexOf("[node name=\"StaffRoot\"", StringComparison.Ordinal)
                < scene.IndexOf("[node name=\"SakuraRoot\"", StringComparison.Ordinal),
            "Expected the fixed staff to lean behind Sakura toward the rest-site log.");
        RegressionTestHarness.Require(
            scene.Contains(
                "[Vector2(-377.3, -55.5), Vector2(-373.3, -97.5), Vector2(-377.3, -55.5), "
                + "Vector2(-381.3, -40.5), Vector2(-377.3, -55.5)]",
                StringComparison.Ordinal),
            "Expected Kero's 31-pixel float to remain visibly legible after the scene's 0.55 visual scale.");
    }

    [Fact]
    public void SakuraAncientPortraitsAndPowerIconsRemainComplete()
    {
        RequirePngWithImport("SakuraMod/images/card_portraits/ancient/another_me.png", 606, 852);
        RequirePngWithImport("SakuraMod/images/card_portraits/ancient/growing_magic.png", 606, 852);
        RequirePngWithImport("SakuraMod/images/powers/another_me.png", 64, 64);
        RequirePngWithImport("SakuraMod/images/powers/big/another_me.png", 256, 256);
        RequirePngWithImport("SakuraMod/images/powers/erase.png", 64, 64);
        RequirePngWithImport("SakuraMod/images/powers/big/erase.png", 256, 256);

        RegressionTestHarness.RequireNoRemovedCardTypes(
            "Sakura assembly types",
            typeof(MainFile).Assembly.GetTypes(),
            RegressionTestData.RemovedAncientCardTypeNames);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/eng/cards.json",
            RegressionTestData.RemovedAncientCardLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/zhs/cards.json",
            RegressionTestData.RemovedAncientCardLocalizationPrefixes);
    }

    [Fact]
    public void VisibleTransparentPowersDoNotUseThePlaceholderIcon()
    {
        var iconProperty = typeof(SakuraPowerModel).GetProperty(
            "IconFileName",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not inspect Sakura Power icon ownership.");

        foreach (var (power, iconFileName) in new (SakuraPowerModel Power, string IconFileName)[]
                 {
                     (new KindnessPower(), "kindness.png"),
                     (new GravitationHoldPower(), "gravitation_hold.png"),
                     (new RepairRegenerationPower(), "repair_regeneration.png")
                 })
        {
            Assert.True(power.IsVisible);
            Assert.Equal(iconFileName, iconProperty.GetValue(power));
            Assert.NotEqual("power.png", iconFileName);
            RequirePngWithImport($"SakuraMod/images/powers/{iconFileName}", 64, 64);
            RequirePngWithImport($"SakuraMod/images/powers/big/{iconFileName}", 256, 256);
        }
    }

    [Fact]
    public void VisibleFourthActPowersUseTheVectorAuthoredIconFamily()
    {
        const string fourthActNamespaceSegment = ".FourthAct.";
        const string iconPrefix = "fourth_act/";
        var iconProperty = typeof(SakuraPowerModel).GetProperty(
            "IconFileName",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not inspect Sakura Power icon ownership.");
        var fourthActPowers = typeof(DarkLightPower).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && typeof(SakuraPowerModel).IsAssignableFrom(type)
                && type.FullName?.Contains(fourthActNamespaceSegment, StringComparison.Ordinal) == true)
            .Select(type => (SakuraPowerModel)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create {type.FullName}.")))
            .OrderBy(static power => power.GetType().Name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["IllusionProjectionPower", "WindyBattlePower"],
            fourthActPowers.Where(static power => !power.IsVisible)
                .Select(static power => power.GetType().Name));

        var visiblePowers = fourthActPowers.Where(static power => power.IsVisible).ToList();
        Assert.Equal(14, visiblePowers.Count);

        var svg = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "tools/fourth-act-power-icons/fourth_act_power_icons.svg"));
        var iconStems = new HashSet<string>(StringComparer.Ordinal);
        var importUids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var power in visiblePowers)
        {
            var iconFileName = iconProperty.GetValue(power) as string
                ?? throw new InvalidOperationException($"{power.GetType().Name} has no icon filename.");
            Assert.StartsWith(iconPrefix, iconFileName, StringComparison.Ordinal);
            Assert.NotEqual("power.png", Path.GetFileName(iconFileName));

            var stem = Path.GetFileNameWithoutExtension(iconFileName);
            Assert.True(iconStems.Add(stem), $"Duplicate fourth-act icon stem: {stem}.");
            Assert.Contains($"data-icon=\"{stem}\" data-size=\"large\"", svg, StringComparison.Ordinal);
            Assert.Contains($"data-icon=\"{stem}\" data-size=\"small\"", svg, StringComparison.Ordinal);

            foreach (var (relativePath, size) in new[]
                     {
                         ($"SakuraMod/images/powers/{iconFileName}", 64),
                         ($"SakuraMod/images/powers/big/{iconFileName}", 256)
                     })
            {
                RequirePngWithImport(relativePath, size, size);
                var importPath = $"{RegressionTestHarness.FindRepoFile(relativePath)}.import";
                var uid = File.ReadLines(importPath).Single(line => line.StartsWith("uid=", StringComparison.Ordinal));
                Assert.True(importUids.Add(uid), $"Duplicate fourth-act texture {uid}.");
            }
        }
    }

    [Fact]
    public void RetiredPlaceholderCardPortraitsStayAbsent()
    {
        var repoRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile("SakuraMod.csproj"))!;
        foreach (var relativePath in new[]
                 {
                     "SakuraMod/images/card_portraits/card.png",
                     "SakuraMod/images/card_portraits/card.png.import",
                     "SakuraMod/images/card_portraits/big/card.png",
                     "SakuraMod/images/card_portraits/big/card.png.import"
                 })
        {
            RegressionTestHarness.Require(
                !File.Exists(Path.Join(repoRoot, relativePath)),
                $"Expected retired placeholder portrait {relativePath} to stay removed.");
        }

        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/Cards/SakuraSourceCard.cs",
                     "SakuraModCode/Cards/SakuraCardFrameVisuals.cs",
                     "SakuraModCode/Extensions/StringExtensions.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                !source.Contains("card.png", StringComparison.Ordinal),
                $"Expected {relativePath} not to retain a placeholder portrait fallback.");
        }
    }

    [Fact]
    public void AquaWaterSphereVfxResourcesRemainComplete()
    {
        var rootScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/aqua_water_sphere_vfx.tscn"));
        var targetScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/aqua_water_sphere_target.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/aqua_water_sphere.gdshader"));
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Transparent/AquaWaterSphereVfx.cs"));
        var aqua = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Aqua.cs"));
        var mainFile = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/MainFile.cs"));
        var legacyVfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardPlayVfx.cs"));

        var backBufferIndex = rootScene.IndexOf("[node name=\"StableCombatFrame\" type=\"BackBufferCopy\"", StringComparison.Ordinal);
        var crestIndex = rootScene.IndexOf("[node name=\"Crest\" type=\"Node2D\"", StringComparison.Ordinal);
        var sphereIndex = rootScene.IndexOf("[node name=\"Spheres\" type=\"Node2D\"", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            backBufferIndex >= 0
            && crestIndex > backBufferIndex
            && sphereIndex > crestIndex
            && rootScene.Contains("[node name=\"CrestBody\" type=\"ColorRect\"", StringComparison.Ordinal)
            && rootScene.Contains("[node name=\"Debris\" type=\"Node2D\"", StringComparison.Ordinal)
            && rootScene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected one stable combat-frame copy before the crest, enclosure, and debris layers with ignored input.");
        RegressionTestHarness.Require(
            !rootScene.Contains("WaterBird", StringComparison.Ordinal)
            && !rootScene.Contains("BindingStreams", StringComparison.Ordinal),
            "Expected the water-bird and authored binding-stream layers to be retired with the SDF rebuild.");

        RegressionTestHarness.Require(
            targetScene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"WaterBody\" type=\"ColorRect\"", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"Droplets\" type=\"Node2D\"", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"FloorRipple\" type=\"Line2D\"", StringComparison.Ordinal)
            && targetScene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected each target scene to own a local water material plus ground-contact and debris anchors.");
        RegressionTestHarness.Require(
            !targetScene.Contains("CelRim", StringComparison.Ordinal)
            && !targetScene.Contains("HighlightArc", StringComparison.Ordinal)
            && !targetScene.Contains("[node name=\"Bubbles\"", StringComparison.Ordinal),
            "Expected no authored polyline to describe the water edge: the SDF is the single silhouette source.");

        foreach (var uniform in new[]
                 { "shape_mode", "formation", "impact", "freeze", "breakup", "elapsed", "seed", "opacity" })
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            shader.Contains("hint_screen_texture", StringComparison.Ordinal)
            && shader.Contains("filter_linear", StringComparison.Ordinal)
            && shader.Contains("step(", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal),
            "Expected restrained screen refraction, stepped cel bands, and controller-owned timing.");
        // The smooth-minimum union, derivative-width ink, and stepped clock now live
        // in the shared include. Aqua must consume them rather than restate them:
        // a second copy of the union operator cannot stay in step with the first.
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("cel_smin(", StringComparison.Ordinal)
            && shader.Contains("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && shader.Contains("cel_step_clock(elapsed)", StringComparison.Ordinal),
            "Expected Aqua to consume the shared union, ink, and stepped clock from cel_vfx.gdshaderinc.");
        RegressionTestHarness.Require(
            !shader.Contains("float smin(", StringComparison.Ordinal)
            && !shader.Contains("float hash11(", StringComparison.Ordinal)
            && !shader.Contains("float ellipse_field(", StringComparison.Ordinal)
            && !shader.Contains("uniform float ink_width", StringComparison.Ordinal),
            "Expected no card-local copy of shared mathematics or of a locked art-language value.");
        // fwidth/dFdx/dFdy stay in uniform control flow: GLSL leaves them undefined
        // across a branch boundary, and the silhouette is exactly where the ink line
        // needs a stable value. cel_ink therefore takes aa rather than deriving it.
        RegressionTestHarness.Require(
            shader.Contains("float aa = max(fwidth(d), 0.0001);", StringComparison.Ordinal)
            && shader.IndexOf("float aa = max(fwidth(d)", StringComparison.Ordinal)
                < shader.IndexOf("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal),
            "Expected derivatives to be taken once in uniform control flow before any ink call.");
        RegressionTestHarness.Require(
            shader.Contains("float squash_y = 1.0 / squash_x;", StringComparison.Ordinal),
            "Expected impact compression to conserve volume through a reciprocal vertical factor.");
        // The crest is a ground-anchored height field whose front radiates outward
        // from the caster's column, so player-left, player-right, and
        // player-centre arenas are all covered without branching per layout.
        RegressionTestHarness.Require(
            shader.Contains("uniform float crest_origin_x", StringComparison.Ordinal)
            && shader.Contains("float floor_y = half_size.y;", StringComparison.Ordinal)
            && shader.Contains("float offset = p.x - crest_origin_x;", StringComparison.Ordinal)
            && shader.Contains("CREST_SPREAD", StringComparison.Ordinal),
            "Expected the crest to rise from the floor line and lag with distance from the caster.");
        // A hard sign() flip at the caster's column would tear a vertical crack
        // through the water when the player stands between two enemy groups.
        RegressionTestHarness.Require(
            !shader.Contains("offset >= 0.0 ? 1.0 : -1.0", StringComparison.Ordinal)
            && shader.Contains("float dir = clamp(offset", StringComparison.Ordinal),
            "Expected the outward lean direction to ramp continuously through the caster's column.");

        // Budgets stay pinned to exact values; beat durations are art-tuning knobs
        // and are asserted by presence so retiming does not require a test edit.
        RegressionTestHarness.Require(
            controller.Contains("float CrestDuration =", StringComparison.Ordinal)
            && controller.Contains("float FormationDuration =", StringComparison.Ordinal)
            && controller.Contains("float TargetStagger =", StringComparison.Ordinal)
            && controller.Contains("float FreezeHold =", StringComparison.Ordinal)
            && controller.Contains("DropletCount = 8", StringComparison.Ordinal)
            && controller.Contains("ShardCount = 7", StringComparison.Ordinal)
            && controller.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && controller.Contains("Hitbox", StringComparison.Ordinal)
            && controller.Contains("Math.Clamp", StringComparison.Ordinal)
            && controller.Contains("ResourceLoader.Load<PackedScene>", StringComparison.Ordinal)
            && controller.Contains("MainFile.Logger.Error", StringComparison.Ordinal)
            && controller.Contains("Duplicate", StringComparison.Ordinal),
            "Expected bounded timing/geometry, safe presentation loading, and independent target material state.");
        RegressionTestHarness.Require(
            !controller.Contains("BirdDuration", StringComparison.Ordinal)
            && !controller.Contains("BubbleCount", StringComparison.Ordinal)
            && !controller.Contains("BuildBindingStreams", StringComparison.Ordinal),
            "Expected the bird beat, CPU bubble budget, and authored binding streams to be retired.");
        // Guards the reason the first crest pass read as a flat mass in flight: the
        // region was anchored above the enemies and the node itself was translated
        // across the line. The wave front now moves through a stationary region
        // whose bottom edge sits on the floor.
        RegressionTestHarness.Require(
            controller.Contains("floorY - size.Y * 0.5f", StringComparison.Ordinal)
            && controller.Contains("SetShaderParameter(\"crest_origin_x\", localOrigin)", StringComparison.Ordinal)
            && controller.Contains("ResolveCasterX", StringComparison.Ordinal)
            && !controller.Contains("CrestTravel", StringComparison.Ordinal)
            && !controller.Contains("CrestRise", StringComparison.Ordinal),
            "Expected the crest region to be floor-anchored and stationary, with the wave front driven by the caster's column.");
        // Guards the root cause of ink weight drifting with enemy size: the old
        // implementation scaled the target root non-uniformly instead of telling
        // the shader how large its region is.
        RegressionTestHarness.Require(
            controller.Contains("Root.Scale = Vector2.One;", StringComparison.Ordinal)
            && controller.Contains("SetShaderParameter(\"region_size\", geometry.Size)", StringComparison.Ordinal),
            "Expected target roots to keep uniform scale and pass region_size so ink stays constant in screen pixels.");
        RegressionTestHarness.Require(
            mainFile.Contains("AquaWaterSphereVfx.PreloadResources();", StringComparison.Ordinal)
            && controller.Contains("private static PackedScene? _rootScene;", StringComparison.Ordinal)
            && controller.Split("catch (OperationCanceledException)", StringSplitOptions.None).Length == 3,
            "Expected Aqua resources to warm before combat use and normal tree-exit frame cancellation to stay silent.");

        RegressionTestHarness.Require(
            aqua.Contains("AquaWaterSphereVfx.TryCreate(targets, Owner.Creature)", StringComparison.Ordinal)
            && aqua.Contains("await waterVfx.PlayPrelude()", StringComparison.Ordinal)
            && aqua.Contains("waterVfx?.Impact(enemy)", StringComparison.Ordinal)
            && aqua.Contains("finally", StringComparison.Ordinal)
            && aqua.Contains("waterVfx?.Release()", StringComparison.Ordinal)
            && aqua.Contains("await SakuraActions.Attack", StringComparison.Ordinal),
            "Expected Aqua to wrap its unchanged attack loop in the focused presentation session.");
        // The freeze beat must start inside the try, because the finally disposes
        // the session before the reward awaits run. It must also stay after the
        // attack loop, since attacks can kill the enemy holding the maximum.
        var attackIndex = aqua.IndexOf("await SakuraActions.Attack", StringComparison.Ordinal);
        var frostbiteIndex = aqua.IndexOf("AquaRules.FrostbiteEnemyCount(targets)", StringComparison.Ordinal);
        var freezeIndex = aqua.IndexOf("waterVfx?.PlayFreeze(frozen)", StringComparison.Ordinal);
        var releaseIndex = aqua.IndexOf("waterVfx?.Release()", StringComparison.Ordinal);
        var energyIndex = aqua.IndexOf("PlayerCmd.GainEnergy", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            attackIndex >= 0
            && frostbiteIndex > attackIndex
            && freezeIndex > frostbiteIndex
            && releaseIndex > freezeIndex
            && energyIndex > releaseIndex,
            "Expected Frostbite to be read after every attack, the freeze beat to start before release, and the rewards to stay after release.");
        RegressionTestHarness.Require(
            !legacyVfx.Contains("PlayAqua", StringComparison.Ordinal)
            && !legacyVfx.Contains("BuildAqua", StringComparison.Ordinal)
            && !legacyVfx.Contains("AnimateAqua", StringComparison.Ordinal)
            && !legacyVfx.Contains("AquaDuration", StringComparison.Ordinal)
            && !legacyVfx.Contains("AquaColor", StringComparison.Ordinal),
            "Expected the legacy wave/ripple Aqua owner to be fully retired.");
        RegressionTestHarness.Require(
            !legacyVfx.Contains("PlayHail", StringComparison.Ordinal)
            && !legacyVfx.Contains("BuildHail", StringComparison.Ordinal)
            && !legacyVfx.Contains("AnimateHail", StringComparison.Ordinal)
            && !legacyVfx.Contains("HailDuration", StringComparison.Ordinal)
            && !legacyVfx.Contains("IceColor", StringComparison.Ordinal),
            "Expected the legacy diamond/streak Hail owner to be fully retired.");
        RegressionTestHarness.Require(
            !legacyVfx.Contains("PlayBlaze", StringComparison.Ordinal)
            && !legacyVfx.Contains("BuildBlaze", StringComparison.Ordinal)
            && !legacyVfx.Contains("AnimateBlaze", StringComparison.Ordinal)
            && !legacyVfx.Contains("BlazeDuration", StringComparison.Ordinal)
            && !legacyVfx.Contains("FlameColor", StringComparison.Ordinal)
            && !legacyVfx.Contains("FlameGoldColor", StringComparison.Ordinal),
            "Expected the legacy polygon-flame Blaze owner to be fully retired.");
        // CreateDiamond served only the legacy Hail and Blaze builders. Hail left it
        // for Blaze, the later of the two, so it goes out with this rebuild; the
        // remaining helpers still have callers and stay.
        // QuadraticPoints followed the same rule one card later: Gale was its only
        // remaining caller, so rebuilding Gale on the shared cel layer orphaned it.
        // AddEllipse still serves Time and Gravitation, so it stays — the pair is
        // asserted together to keep "orphaned" meaning "has no caller" rather than
        // "belongs to a retired card".
        RegressionTestHarness.Require(
            !legacyVfx.Contains("CreateDiamond", StringComparison.Ordinal)
            && !legacyVfx.Contains("QuadraticPoints(", StringComparison.Ordinal)
            && legacyVfx.Contains("AddEllipse(", StringComparison.Ordinal),
            "Expected the orphaned diamond and quadratic helpers to be removed while helpers with live callers remain.");
        RegressionTestHarness.Require(
            !legacyVfx.Contains("CreateGaleWindBlade", StringComparison.Ordinal)
            && !legacyVfx.Contains("BuildGaleWindBlade", StringComparison.Ordinal)
            && !legacyVfx.Contains("AnimateGaleWindBlade", StringComparison.Ordinal)
            && !legacyVfx.Contains("GaleDuration", StringComparison.Ordinal)
            && !legacyVfx.Contains("GaleEdgeColor", StringComparison.Ordinal)
            && !legacyVfx.Contains("GaleBodyColor", StringComparison.Ordinal)
            && !legacyVfx.Contains("GaleTrailColor", StringComparison.Ordinal),
            "Expected the legacy crescent-and-streak Gale owner to be fully retired.");
    }

    [Fact]
    public void HailIceShardConsumesSharedCelLayerWithIntersectionSilhouette()
    {
        var rootScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/hail_ice_shard_vfx.tscn"));
        var targetScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/hail_ice_shard_target.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/hail_ice_shard.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Transparent/HailIceShardVfx.cs"));
        var hail = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Hail.cs"));

        var backBufferIndex = rootScene.IndexOf(
            "[node name=\"StableCombatFrame\" type=\"BackBufferCopy\"", StringComparison.Ordinal);
        var shardsIndex = rootScene.IndexOf("[node name=\"Shards\" type=\"Node2D\"", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            backBufferIndex >= 0
            && shardsIndex > backBufferIndex
            && rootScene.Contains("[node name=\"Debris\" type=\"Node2D\"", StringComparison.Ordinal)
            && rootScene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected one stable combat-frame copy before the crystal and debris layers with ignored input.");
        RegressionTestHarness.Require(
            targetScene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"ShardBody\" type=\"ColorRect\"", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"Fragments\" type=\"Node2D\"", StringComparison.Ordinal)
            && targetScene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected each target scene to own a local ice material plus a fragment anchor.");

        foreach (var uniform in new[] { "elapsed", "held", "held_at", "seed", "formation", "crack", "shatter", "opacity" })
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);

        // Ice is an intersection of half-planes; water is a smooth union. Calling
        // cel_smin here would round every corner and collapse the two cards onto one
        // silhouette, so its absence is the executable form of that art decision.
        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("cel_smin(", StringComparison.Ordinal)),
            "Expected Hail's ice to stay an intersection: no smooth-union call may build the crystal.");
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("cel_bands3(", StringComparison.Ordinal)
            && shader.Contains("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && shader.Contains("cel_step_clock_held(elapsed, held, held_at)", StringComparison.Ordinal)
            && shader.Contains("CEL_REFRACT_MAX_PX", StringComparison.Ordinal),
            "Expected Hail to consume shared bands, ink, refraction budget, and the held stepped clock.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("const float CEL_", StringComparison.Ordinal))
            && !shader.Contains("TIME", StringComparison.Ordinal),
            "Expected no card-local art-language constant and no shader-owned clock.");
        RegressionTestHarness.Require(
            shader.Contains("float aa = max(fwidth(d), 0.0001);", StringComparison.Ordinal)
            && shader.IndexOf("float aa = max(fwidth(d)", StringComparison.Ordinal)
                < shader.IndexOf("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal),
            "Expected derivatives to be taken once in uniform control flow before any ink call.");

        // The clock pulls Materials, so the subclass field backing it is still empty
        // during the base constructor. Starting the clock there would read nothing.
        RegressionTestHarness.Require(
            session.Contains("session.StartClock();", StringComparison.Ordinal)
            && session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("BeginHold();", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.AddBallisticDebris(", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.BallisticOffset(", StringComparison.Ordinal),
            "Expected the session to derive from the shared skeleton, start its clock explicitly, hold a frame, and fall under the shared parabola.");

        // Fragments must wait out the hold: BeginHold freezes shader time, not the
        // tween clock, so launching them inside it shows a still crystal shedding
        // moving debris.
        RegressionTestHarness.Require(
            session.Contains("HoldDuration", StringComparison.Ordinal)
            && session.Contains("CreateShatterTween(_debris, HoldDuration)", StringComparison.Ordinal),
            "Expected the shatter beat to be delayed by the hold rather than overlapping it.");

        Assert.Contains(
            "HailIceShardVfx.PlayOrResolveAsync(this, Owner.Creature, targets",
            hail,
            StringComparison.Ordinal);
        var loopIndex = hail.IndexOf("foreach (var target in targets)", StringComparison.Ordinal);
        // Session creation, prelude, fail-open, and cleanup are behavioral contracts
        // of CelVfxSession.PlayOrResolveAsync and are covered through its in-memory
        // playback interface in CelVfxOrchestrationSuite.
        // Impact lives in the Hit helper below PlayCard, so its ordering is asserted
        // against the damage call it must precede rather than against file position:
        // the damage number has to land on the frame the crystal strikes.
        var hitIndex = hail.IndexOf("private async Task Hit(", StringComparison.Ordinal);
        var impactIndex = hail.IndexOf("cues.Impact(target)", hitIndex, StringComparison.Ordinal);
        var damageIndex = hail.IndexOf("CreatureCmd.Damage(", hitIndex, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            hitIndex >= 0 && impactIndex > hitIndex && damageIndex > impactIndex,
            "Expected each hit to show its ice impact before the damage command resolves.");
        // The VFX session and the hit loop must walk the same snapshot, taken before
        // any damage resolves.
        RegressionTestHarness.Require(
            hail.Contains("var targets = CombatState!.HittableEnemies.ToList();", StringComparison.Ordinal)
            && loopIndex > hail.IndexOf("var targets = CombatState!.HittableEnemies.ToList();", StringComparison.Ordinal),
            "Expected one hittable-enemy snapshot shared by the hit loop and its visual cues.");
    }

    [Fact]
    public void BlazeFireColumnConsumesSharedCelLayerWithTurbulentSilhouette()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/blaze_fire_column_vfx.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/blaze_fire_column.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Transparent/BlazeFireColumnVfx.cs"));
        var blaze = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Blaze.cs"));

        // One scene, not Aqua's root-plus-target split: that split lets a single
        // BackBufferCopy serve N target copies, and Blaze's N is always one.
        var backBufferIndex = scene.IndexOf(
            "[node name=\"StableCombatFrame\" type=\"BackBufferCopy\"", StringComparison.Ordinal);
        var columnIndex = scene.IndexOf("[node name=\"ColumnBody\" type=\"ColorRect\"", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            backBufferIndex >= 0
            && columnIndex > backBufferIndex
            && scene.Contains("[node name=\"Embers\" type=\"Node2D\"", StringComparison.Ordinal)
            && scene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected one stable combat-frame copy before the column and ember layers with a local material and ignored input.");

        foreach (var uniform in new[] { "elapsed", "held", "held_at", "seed", "ignite", "rise", "burnout", "opacity" })
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        // Three cards, three field operators: water unions with cel_smin, ice
        // intersects with max, fire warps its coordinate domain with turbulence. A
        // smooth union here would pull the silhouette back toward water, so its
        // absence is the executable form of that art decision — and the guard that
        // keeps a later fire card from quietly reverting to the fluid look.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("cel_smin(", StringComparison.Ordinal)),
            "Expected Blaze's fire to stay a turbulence warp: no smooth-union call may build the column.");
        RegressionTestHarness.Require(
            shaderCode.Any(static line => line.Contains("cel_fbm(", StringComparison.Ordinal)),
            "Expected the fire field to be built from the shared turbulence primitive.");
        // Fire is a reaction front, not an incompressible body. Aqua's reciprocal
        // squash pairing conserves volume, which would make fire behave like a fluid
        // balloon.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("squash", StringComparison.Ordinal)),
            "Expected no volume-conservation pairing on a card whose body is a reaction front.");
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("cel_bands3(", StringComparison.Ordinal)
            && shader.Contains("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && shader.Contains("cel_step_clock_held(elapsed, held, held_at)", StringComparison.Ordinal)
            && shader.Contains("CEL_REFRACT_MAX_PX", StringComparison.Ordinal),
            "Expected Blaze to consume shared bands, ink, refraction budget, and the held stepped clock.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("const float CEL_", StringComparison.Ordinal))
            && !shader.Contains("TIME", StringComparison.Ordinal),
            "Expected no card-local art-language constant and no shader-owned clock.");
        RegressionTestHarness.Require(
            shader.Contains("float aa = max(fwidth(d), 0.0001);", StringComparison.Ordinal)
            && shader.IndexOf("float aa = max(fwidth(d)", StringComparison.Ordinal)
                < shader.IndexOf("cel_ink(d, aa, CEL_INK_WIDTH)", StringComparison.Ordinal),
            "Expected derivatives to be taken once in uniform control flow before any ink call.");
        // Screen-pixel budgets convert through SCREEN_PIXEL_SIZE. Dividing a pixel
        // count by region_size yields region UV, so adding it to SCREEN_UV both
        // overshoots the budget and grows as the region shrinks.
        RegressionTestHarness.Require(
            shaderCode.Any(static line => line.Contains("SCREEN_PIXEL_SIZE", StringComparison.Ordinal)),
            "Expected the heat shimmer to convert its pixel budget through SCREEN_PIXEL_SIZE.");

        RegressionTestHarness.Require(
            session.Contains("session.StartClock();", StringComparison.Ordinal)
            && session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("BeginHold();", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.AddBallisticDebris(", StringComparison.Ordinal),
            "Expected the session to derive from the shared skeleton, start its clock explicitly, and hold a frame.");
        // Embers must wait out the hold: BeginHold freezes shader time, not the tween
        // clock, so launching them inside it shows a still column shedding movement.
        RegressionTestHarness.Require(
            session.Contains("HoldDuration", StringComparison.Ordinal)
            && session.Contains("SetDelay(HoldDuration)", StringComparison.Ordinal),
            "Expected the burnout beat and the embers to be delayed by the hold rather than overlapping it.");
        // Light embers, not rock. Gravity is a parameter of the shared integrator, so
        // a slower fall must not become a second integration.
        RegressionTestHarness.Require(
            session.Contains("EmberGravity", StringComparison.Ordinal),
            "Expected embers to fall under a card-tuned gravity parameter rather than the default.");
        // Blaze's damage scales with the exhaust pile; its fire must not. Reading a
        // gameplay value here would open a channel from mechanics into presentation
        // for no gain beyond "bigger number, taller flame".
        RegressionTestHarness.Require(
            !session.Contains("PileType", StringComparison.Ordinal)
            && !session.Contains("ExhaustedCardMultiplier", StringComparison.Ordinal)
            && !session.Contains("CalculatedDamage", StringComparison.Ordinal),
            "Expected the fire column to stay at fixed strength rather than reading a gameplay value.");

        Assert.Contains(
            "BlazeFireColumnVfx.PlayOrResolveAsync(this, Owner.Creature, target",
            blaze,
            StringComparison.Ordinal);
        var impactIndex = blaze.IndexOf("cues.Impact()", StringComparison.Ordinal);
        var attackIndex = blaze.IndexOf("SakuraActions.Attack(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            impactIndex >= 0 && attackIndex > impactIndex,
            "Expected Blaze to show its family cue before the attack resolves.");
        // Single target: RequiredTarget is a pure read, so unlike Hail there is no
        // snapshot-timing question. The gameplay call must stay untouched.
        RegressionTestHarness.Require(
            blaze.Contains("var target = RequiredTarget(play);", StringComparison.Ordinal)
            && blaze.Contains(
                "SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage)",
                StringComparison.Ordinal)
            && blaze.Contains("BlazeRules.ExhaustedCardMultiplier", StringComparison.Ordinal),
            "Expected Blaze's gameplay path to stay verbatim behind the visual rebuild.");
    }

    [Fact]
    public void GaleWindBladeConsumesSharedCelLayerAsForwardFacingCrescent()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/gale_wind_blade_vfx.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/gale_wind_blade.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Transparent/GaleWindBladeVfx.cs"));
        var gale = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Gale.cs"));

        var wakeIndex = scene.IndexOf(
            "[node name=\"Wake\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal);
        var carrierIndex = scene.IndexOf(
            "[node name=\"BladeCarrier\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            wakeIndex >= 0
            && carrierIndex > wakeIndex
            && scene.Split("[node name=\"CrescentBody\"", StringSplitOptions.None).Length == 2
            && scene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && scene.Contains("width = 12.0", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 2", StringComparison.Ordinal)
            && !scene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected one crescent carrier above one independent wake, with a local material, ignored input, and no screen capture.");

        foreach (var uniform in new[]
                 {
                     "elapsed", "held", "held_at", "seed", "formation", "impact", "dissolve", "opacity"
                 })
        {
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        }
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        // The subtractive ellipse is shifted toward local -X. The surviving
        // crescent therefore lies on +X, so its convex face meets the enemy while
        // its concave opening receives the wake from Sakura.
        RegressionTestHarness.Require(
            shader.Contains("INNER_OFFSET", StringComparison.Ordinal)
            && shader.Contains("float outer = cel_ellipse(p, OUTER_RADII);", StringComparison.Ordinal)
            && shader.Contains("float inner = cel_ellipse(p - INNER_OFFSET, INNER_RADII);", StringComparison.Ordinal)
            && shader.Contains("float crescent = max(outer, -inner)", StringComparison.Ordinal),
            "Expected Gale's sole silhouette to be a subtractive crescent whose convex side faces local +X.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line =>
                line.Contains("cel_bipointed_stroke(", StringComparison.Ordinal)
                || line.Contains("cel_smin(", StringComparison.Ordinal)
                || line.Contains("cel_fbm(", StringComparison.Ordinal)),
            "Expected Gale to remain a clean ellipse-subtraction crescent rather than a spearhead, fluid union, or turbulent mass.");

        // Gale's near-white air edge is the approved local exception to dark ink.
        // Its light remains a bounded field band, not a scene read or global bloom.
        RegressionTestHarness.Require(
            shader.Contains("cel_step_clock_held(elapsed, held, held_at)", StringComparison.Ordinal)
            && shader.Contains("float aa = max(fwidth(d), 0.0001);", StringComparison.Ordinal)
            && !shader.Contains("cel_ink(", StringComparison.Ordinal)
            && !shader.Contains("ink_color", StringComparison.OrdinalIgnoreCase)
            && !shader.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal),
            "Expected a held stepped crescent with a bounded local halo, no dark shell, screen sampling, or shader-owned clock.");

        RegressionTestHarness.Require(
            session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("BladeRegion", StringComparison.Ordinal)
            && session.Contains("root.Scale = Vector2.One;", StringComparison.Ordinal)
            && session.Contains("session.StartClock();", StringComparison.Ordinal)
            && session.Contains("PlayCelPrelude(card, caster)", StringComparison.Ordinal)
            && session.Contains("BeginHold();", StringComparison.Ordinal)
            && session.Contains("FlightDuration", StringComparison.Ordinal)
            && session.Contains("FormationDuration", StringComparison.Ordinal)
            && session.Contains("SetDelay(HoldDuration)", StringComparison.Ordinal),
            "Expected Gale to retain the shared session, fixed region, three-step flight, explicit formation, hold, and delayed pass-through.");
        RegressionTestHarness.Require(
            session.Contains("private readonly Line2D _wake;", StringComparison.Ordinal)
            && session.Contains("ConfigureWake();", StringComparison.Ordinal)
            && session.Contains("UpdateWakePath(position, wakeOpacity);", StringComparison.Ordinal)
            && session.Contains("_flightEnd = target.Center - _direction * CrescentCenterOffsetPx;", StringComparison.Ordinal)
            && session.Contains("_carrier.Rotation = _direction.Angle();", StringComparison.Ordinal)
            && !session.Contains("TweenProperty(_carrier, \"rotation\"", StringComparison.Ordinal)
            && !session.Contains("TipOffsetPx", StringComparison.Ordinal)
            && !session.Contains("DebrisCount", StringComparison.Ordinal)
            && !session.Contains("AddBallisticDebris", StringComparison.Ordinal),
            "Expected one fixed-attitude crescent aligned by its visible centre, with an independent wake and no spear-tip or burst debris path.");

        Assert.Contains(
            "GaleWindBladeVfx.PlayOrResolveAsync(this, Owner.Creature, target",
            gale,
            StringComparison.Ordinal);
        var impactIndex = gale.IndexOf("cues.Impact()", StringComparison.Ordinal);
        var attackIndex = gale.IndexOf("SakuraActions.AttackCommand(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            impactIndex >= 0
            && attackIndex > impactIndex
            && gale.Contains("ApplyExtraEffect(choiceContext)", StringComparison.Ordinal)
            && gale.Contains("AfterCardPlayed", StringComparison.Ordinal),
            "Expected Gale to preserve its attack, Extra copies, draw cadence, and cue synchronization.");
    }

    [Fact]
    public void CloudRainWeatherSharesOneCelFieldWithoutScreenSampling()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/cloud_rain_weather_vfx.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cloud_rain_weather.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/CloudRainWeatherVfx.cs"));
        var cloud = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Cloud.cs"));
        var rain = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Rain.cs"));
        var include = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"WeatherBody\" type=\"ColorRect\"", StringComparison.Ordinal)
            && scene.Contains("unique_name_in_owner = true", StringComparison.Ordinal)
            && scene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && scene.Contains("shader_parameter/formation = 0.0", StringComparison.Ordinal)
            && scene.Contains("shader_parameter/rain = 0.0", StringComparison.Ordinal)
            && scene.Split("mouse_filter = 2", StringSplitOptions.None).Length - 1 == 2
            && !scene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected one local ColorRect weather region that ships in its start state, ignores input, and never copies the screen.");

        foreach (var uniform in new[]
                 {
                     "elapsed", "held", "held_at", "seed", "formation", "rain", "rain_origin",
                     "splash", "opacity"
                 })
        {
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        }
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        var includeMass = ExtractGlslFunction(include, "float cel_scalloped_mass(");
        var canopyField = ExtractGlslFunction(shader, "float canopy_field(");
        var canopyShade = ExtractGlslFunction(shader, "vec4 shade_canopy(");
        var rainField = ExtractGlslFunction(shader, "float rain_field(");
        var rainShade = ExtractGlslFunction(shader, "vec4 shade_rain_streaks(");
        var splashShade = ExtractGlslFunction(shader, "vec4 shade_splash_crowns(");

        RegressionTestHarness.Require(
            includeMass.Contains("d = min(d, length(p - lobe_c) - radius);", StringComparison.Ordinal)
            && includeMass.Contains("return max(d, p.y - base_y);", StringComparison.Ordinal)
            && !includeMass.Contains("cel_smin(", StringComparison.Ordinal)
            && !includeMass.Contains("cel_fbm(", StringComparison.Ordinal),
            "Expected the shared scalloped mass to union circles with min and cut the larger-y base, never a smooth-union neck.");
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && canopyField.Contains("cel_scalloped_mass(", StringComparison.Ordinal)
            && canopyShade.Contains("cel_ink(", StringComparison.Ordinal)
            && canopyShade.Contains("cel_bands3(", StringComparison.Ordinal)
            && (canopyShade.Contains("cel_step_clock", StringComparison.Ordinal)
                || shader.Contains("cel_step_clock_held(elapsed, held, held_at)", StringComparison.Ordinal)),
            "Expected the canopy path to consume the shared scalloped mass, ink, bands, and stepped clock.");
        RegressionTestHarness.Require(
            !rainShade.Contains("cel_ink", StringComparison.Ordinal)
            && !rainShade.Contains("cel_bands3", StringComparison.Ordinal)
            && !rainShade.Contains("cel_step_clock", StringComparison.Ordinal)
            && !splashShade.Contains("cel_ink", StringComparison.Ordinal)
            && !splashShade.Contains("cel_bands3", StringComparison.Ordinal)
            && !splashShade.Contains("cel_step_clock", StringComparison.Ordinal)
            && shader.Contains("float rain_time = max(elapsed - rain_origin, 0.0);", StringComparison.Ordinal)
            && shader.Contains("rain_field(p, rain_time)", StringComparison.Ordinal)
            && rainField.Contains("p0 + vel * local", StringComparison.Ordinal),
            "Expected rain streaks and splash crowns to shade with a bright core and foam rim, falling from the moment rain starts.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line =>
                line.Contains("cel_smin(", StringComparison.Ordinal)
                || line.Contains("cel_fbm(", StringComparison.Ordinal)
                || line.Contains("TIME", StringComparison.Ordinal)
                || line.Contains("hint_screen_texture", StringComparison.Ordinal)
                || line.Contains("const float CEL_", StringComparison.Ordinal)
                || line.Contains("FACE_BUDGET", StringComparison.Ordinal)),
            "Expected no Aqua neck, fire turbulence, Hail crystal loop, shader clock, screen sample, or restated art-language constants.");

        RegressionTestHarness.Require(
            session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("session.StartClock();", StringComparison.Ordinal)
            && session.Contains("PlayCelPrelude(card, caster)", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.ResolveCaster(", StringComparison.Ordinal)
            && session.Contains("room.CombatVfxContainer.AddChildSafely(root)", StringComparison.Ordinal)
            && session.Contains("root.Scale = Vector2.One;", StringComparison.Ordinal)
            && session.Contains("private const int VfxZIndex = 1;", StringComparison.Ordinal)
            && session.Contains("caster.FacingSign * FacingOffsetPx", StringComparison.Ordinal)
            && session.Contains("BeginHold();", StringComparison.Ordinal)
            && session.Contains("SetShaderParameter(\"rain_origin\"", StringComparison.Ordinal)
            && !session.Contains("CountWateryCards", StringComparison.Ordinal)
            && !session.Contains("ReleasedBlock", StringComparison.Ordinal),
            "Expected a single caster-side session that starts its clock after construction and never reads watery-card counts.");
        RegressionTestHarness.Require(
            session.Contains("BeginHold();", StringComparison.Ordinal)
            && session.Contains("SetDelay(HoldDuration)", StringComparison.Ordinal)
            && CloudRainWeatherVfx.CloudFieldIsShorterThanRain(),
            "Expected the cloud field to stay shorter than rain, with splash delayed past the hold.");

        Assert.Contains(
            "CloudRainWeatherVfx.PlayOrResolveAsync(",
            cloud,
            StringComparison.Ordinal);
        Assert.Contains(
            "CloudRainWeatherVfx.PlayOrResolveAsync(",
            rain,
            StringComparison.Ordinal);
        RegressionTestHarness.Require(
            cloud.Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 3
            && rain.Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 3,
            "Expected Clow Cloud, Sakura Cloud, Clow Rain's two paths, and Sakura Rain to each wrap one session.");

        var activatedIndex = cloud.IndexOf("Task PlayActivatedCard(", StringComparison.Ordinal);
        var helperIndex = cloud.IndexOf("private async Task ResolveCloudMechanics(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            activatedIndex >= 0
            && helperIndex > activatedIndex
            && cloud[activatedIndex..helperIndex].Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 1
            && !cloud[activatedIndex..helperIndex].Contains("PlayCard(", StringComparison.Ordinal)
            && cloud[activatedIndex..helperIndex].Contains("ExtraBlock", StringComparison.Ordinal)
            && !cloud.Contains("await PlayCard(", StringComparison.Ordinal),
            "Expected Clow Cloud's activated path to wrap one session around the mechanical helper plus extra block.");

        foreach (var (source, cueAfter) in new[]
                 {
                     (cloud, "ResolveCloudMechanics("),
                     (rain, "ReduceHandCosts(")
                 })
        {
            var impactIndex = source.IndexOf("cues.Impact()", StringComparison.Ordinal);
            var actionIndex = source.IndexOf(cueAfter, StringComparison.Ordinal);
            RegressionTestHarness.Require(
                impactIndex >= 0 && actionIndex > impactIndex,
                $"Expected {cueAfter} to resolve after the weather impact cue.");
        }
    }

    [Fact]
    public void SnowBlizzardScenesShipInputTransparentStartStateMaterials()
    {
        var curtainScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/snow_blizzard_vfx.tscn"));
        var crystalScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/snow_crystal_target.tscn"));

        // One curtain root, one crystal root: pure ColorRect regions with a local
        // material and no screen capture — the enemy side never refracts, so the
        // whole budget stays on the drawn shapes. The body names are load-bearing
        // (the session fetches them as %SnowfallBody / %CrystalBody), which is
        // what unique_name_in_owner records.
        RegressionTestHarness.Require(
            curtainScene.Contains("[node name=\"SnowfallBody\" type=\"ColorRect\" parent=\".\"]", StringComparison.Ordinal)
            && curtainScene.Contains("unique_name_in_owner = true", StringComparison.Ordinal)
            && curtainScene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && curtainScene.Split("mouse_filter = 2", StringSplitOptions.None).Length - 1 == 2
            && !curtainScene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected one local ColorRect curtain region whose every control ignores input and never copies the screen.");
        RegressionTestHarness.Require(
            crystalScene.Contains("[node name=\"CrystalBody\" type=\"ColorRect\" parent=\".\"]", StringComparison.Ordinal)
            && crystalScene.Contains("unique_name_in_owner = true", StringComparison.Ordinal)
            && crystalScene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && crystalScene.Contains("mouse_filter = 2", StringComparison.Ordinal)
            && !crystalScene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected one local ColorRect crystal region that ignores input and never copies the screen.");

        // Both scenes ship the shader's start state: no beat has begun and the
        // curtain would draw nothing as delivered. A scene shipped at its finished
        // state would flash a completed blizzard on spawn, so the parameters are
        // asserted including the zero values the forensic scripts must scan.
        foreach (var (scene, layerParameter, name) in new[]
                 {
                     (curtainScene, "shader_parameter/layer = 0", "curtain"),
                     (crystalScene, "shader_parameter/layer = 1", "crystal")
                 })
        {
            RegressionTestHarness.Require(
                scene.Contains(layerParameter, StringComparison.Ordinal)
                && scene.Contains("shader_parameter/curtain = 0.0", StringComparison.Ordinal)
                && scene.Contains("shader_parameter/dart = 0.0", StringComparison.Ordinal)
                && scene.Contains("shader_parameter/dart_spin = 0.0", StringComparison.Ordinal)
                && scene.Contains("shader_parameter/bloom = 0.0", StringComparison.Ordinal)
                && scene.Contains("shader_parameter/frost = 0.0", StringComparison.Ordinal)
                && scene.Contains("shader_parameter/opacity = 1.0", StringComparison.Ordinal),
                $"Expected the {name} scene to ship its ShaderMaterial in the start state with {layerParameter}.");
        }
    }

    [Fact]
    public void SnowBlizzardFoldsTheCelWheelWithoutNeighbouringCardSilhouettes()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/snow_blizzard.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SnowBlizzardVfx.cs"));

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        var snowfallField = ExtractGlslFunction(shader, "float snowfall_field(");
        var snowfallShade = ExtractGlslFunction(shader, "vec4 shade_snowfall(");
        var beatSteps = ExtractGlslFunction(shader, "float snow_beat_steps(");
        var fragmentBody = ExtractGlslFunction(shader, "void fragment()");

        // Shared includes first: the fold, bands, ink, and stepped clock live in
        // cel_vfx.gdshaderinc, and a card-local copy of any of them could not stay
        // in step with every other card.
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc\"", StringComparison.Ordinal),
            "Expected Snow to consume the shared cel mathematics and signature includes.");

        // Snow's silhouette operator is the polar fold, so every neighbouring
        // card's operator stays absent: cel_smin is Aqua's union, cel_fbm is
        // Blaze's turbulence, cel_scalloped_mass is the weather canopy, and Hail's
        // crystal is a half-plane facet loop. Screen sampling and a shader-owned
        // clock are forbidden card-wide. Checked against the comment-stripped
        // source, because the shader's own commentary names hint_screen_texture
        // while explaining why it is forbidden here.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line =>
                line.Contains("cel_smin(", StringComparison.Ordinal)
                || line.Contains("cel_fbm(", StringComparison.Ordinal)
                || line.Contains("cel_scalloped_mass(", StringComparison.Ordinal)
                || line.Contains("cel_facet(", StringComparison.Ordinal)
                || line.Contains("FACE_BUDGET", StringComparison.Ordinal)
                || line.Contains("TIME", StringComparison.Ordinal)
                || line.Contains("hint_screen_texture", StringComparison.Ordinal)),
            "Expected no water union, fire turbulence, cloud canopy, Hail facet loop, shader clock, or screen sample in the snow field.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("const float CEL_", StringComparison.Ordinal)),
            "Expected no card-local restatement of the locked band, ink, or step constants.");

        // The crystal path is the fold's consumer: the frost lace and the dart
        // wheel each fold into one fundamental sector, and both shade with the
        // shared bands and ink on the stepped clock.
        RegressionTestHarness.Require(
            fragmentBody.Contains("cel_radial_fold(", StringComparison.Ordinal)
            && fragmentBody.Split("cel_radial_fold(", StringSplitOptions.None).Length - 1 == 2
            && fragmentBody.Contains("cel_bands3(", StringComparison.Ordinal)
            && fragmentBody.Contains("cel_ink(d_dart, aa_dart, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && fragmentBody.Contains("cel_ink(d_frost, aa_frost, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && beatSteps.Contains("cel_step_clock(", StringComparison.Ordinal),
            "Expected the frost lace and the dart wheel to share the polar fold and shade through the shared bands, ink, and stepped clock.");

        // Curtain grains are traces, not drawn bodies — the cloud/rain ruling on
        // precipitation gives them a bright core and foam rim on continuous time,
        // never ink, bands, or a stepped clock. Their fall is terminal drift:
        // driven by the session-pushed elapsed, with a shared noise gust and a
        // per-grain sway, so the field cannot collapse into Rain's straight
        // streaks or Hail's accelerating shards.
        RegressionTestHarness.Require(
            !snowfallShade.Contains("cel_ink", StringComparison.Ordinal)
            && !snowfallShade.Contains("cel_bands3", StringComparison.Ordinal)
            && !snowfallShade.Contains("cel_step_clock", StringComparison.Ordinal)
            && !snowfallField.Contains("cel_ink", StringComparison.Ordinal)
            && !snowfallField.Contains("cel_bands3", StringComparison.Ordinal)
            && !snowfallField.Contains("cel_step_clock", StringComparison.Ordinal)
            && shader.Contains("snowfall_field(p, elapsed, half_size)", StringComparison.Ordinal)
            && snowfallField.Contains("t * fall", StringComparison.Ordinal)
            && snowfallField.Contains("cel_noise2(", StringComparison.Ordinal)
            && snowfallField.Contains("* SWAY_AMP", StringComparison.Ordinal),
            "Expected curtain grains to shade as bright-core traces whose fall runs on the pushed clock with noise gusts and per-grain sway.");

        // The bloom's sparkle stays in the shader's bright rim. Hail's ballistic
        // debris reads as shattered shards, which is exactly what a snowflake
        // must not do — it settles and sublimates.
        RegressionTestHarness.Require(
            !session.Contains("AddBallisticDebris", StringComparison.Ordinal),
            "Expected no ballistic debris path in the snow session: snowflake bloom is a shader rim, never thrown fragments.");
    }

    [Fact]
    public void SnowBlizzardBeatCurveCompressesOntoItsFloorInsideTheLifetimeCap()
    {
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SnowBlizzardVfx.cs"));

        // The floor is three stepped formation frames plus the 0.12 s terminal
        // fall plus one bloom frame at the shared 12 Hz stepped clock. The curve
        // must actually reach it as beats accumulate — a bound that is never
        // attained would let every beat stay uncompressed.
        var floor = 3f / 12f + 0.12f + 1f / 12f;
        var beats = Enumerable.Range(0, SnowBlizzardVfx.WorstCaseBeats * 2)
            .Select(SnowBlizzardVfx.BeatSeconds)
            .ToList();
        RegressionTestHarness.Require(
            beats.All(beat => beat >= floor - 1e-3f)
            && beats[0] > floor + 1e-3f
            && beats.Zip(beats.Skip(1), (current, next) => next <= current + 1e-6f)
                .All(static compressed => compressed)
            && Math.Abs(beats[^1] - floor) < 1e-3f,
            "Expected beat durations to compress monotonically onto the three-frame, 0.12 s, one-bloom-frame floor as beats accumulate.");

        // MaximumLifetime is a wall-clock safety net, not a beat timer: it is
        // sized for the worst case (prelude, curtain, twelve floor beats, the
        // held finale volley, and the fade), so the whole envelope at the worst
        // beat count must clear it with room to spare.
        RegressionTestHarness.Require(
            session.Contains("MaximumLifetime", StringComparison.Ordinal)
            && float.IsFinite(SnowBlizzardVfx.TotalEnvelopeSeconds(SnowBlizzardVfx.WorstCaseBeats))
            && SnowBlizzardVfx.TotalEnvelopeSeconds(SnowBlizzardVfx.WorstCaseBeats) < 15f,
            "Expected the worst-case envelope to stay finite and under the session's 15 s lifetime cap.");

        // A zero-count play still lowers the curtain once, but must not idle: with
        // no beats the envelope is its shortest and stays a short show.
        RegressionTestHarness.Require(
            float.IsFinite(SnowBlizzardVfx.TotalEnvelopeSeconds(0))
            && SnowBlizzardVfx.TotalEnvelopeSeconds(0) < SnowBlizzardVfx.TotalEnvelopeSeconds(1)
            && SnowBlizzardVfx.TotalEnvelopeSeconds(0) < 2.5f,
            "Expected a zero-beat play to run the shortest envelope: the snow falls once, no dart is thrown, nothing idles.");
    }

    [Fact]
    public void SnowCardsWrapEveryEntryPointOnceAroundSnowMechanics()
    {
        var snow = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Snow.cs"));

        // Three entry points, three wrappings: ClowSnow's base play, its
        // activated extra-effect play, and SakuraSnow's play each own exactly one
        // session. A fourth would be a second blizzard over the same card play.
        RegressionTestHarness.Require(
            snow.Split("SnowBlizzardVfx.PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 3,
            "Expected exactly one Snow blizzard session per card entry point.");

        var activatedIndex = snow.IndexOf("Task PlayActivatedCard(", StringComparison.Ordinal);
        var helperIndex = snow.IndexOf("private async Task ResolveSnowMechanics(", StringComparison.Ordinal);
        var sakuraIndex = snow.IndexOf("class SakuraSnow", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            activatedIndex >= 0
            && helperIndex > activatedIndex
            && sakuraIndex > helperIndex,
            "Expected ClowSnow's activated path, the shared mechanical helper, then SakuraSnow's declaration.");

        // The activated path must not re-enter PlayCard: that method creates its
        // own session, and the extra-effect play would run the whole blizzard
        // twice. It wraps the mechanical helper directly, cues its finale, and
        // only then resolves the extra all-enemy damage.
        var finaleIndex = snow.IndexOf("cues.Finale();", activatedIndex, StringComparison.Ordinal);
        var extraDamageIndex = snow.IndexOf("await DealDamageToEnemies(", activatedIndex, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            snow[activatedIndex..helperIndex].Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 1
            && !snow[activatedIndex..helperIndex].Contains("PlayCard(", StringComparison.Ordinal)
            && snow[activatedIndex..helperIndex].Contains("ResolveSnowMechanics(", StringComparison.Ordinal)
            && finaleIndex > activatedIndex
            && finaleIndex < helperIndex
            && extraDamageIndex > finaleIndex
            && !snow.Contains("await PlayCard(", StringComparison.Ordinal),
            "Expected ClowSnow's activated path to wrap one session around the mechanical helper and fire its finale before the extra damage resolves.");

        // Every Impact must land its dart before the damage it visualises.
        var impactIndex = snow.IndexOf("cues.Impact(target)", helperIndex, StringComparison.Ordinal);
        var damageIndex = snow.IndexOf("await DealDamage(", helperIndex, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            impactIndex > helperIndex && damageIndex > impactIndex,
            "Expected each snow segment to show its impact cue before the damage command resolves.");

        // SakuraSnow wraps its own play once, with every snapshot target taking a
        // dart before the shared all-enemy attack lands.
        var sakuraImpactIndex = snow.IndexOf("cues.Impact(target)", sakuraIndex, StringComparison.Ordinal);
        var sakuraAttackIndex = snow.IndexOf("await DealDamageToEnemies(", sakuraIndex, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            snow[sakuraIndex..].Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 1
            && sakuraImpactIndex > sakuraIndex
            && sakuraAttackIndex > sakuraImpactIndex,
            "Expected SakuraSnow to wrap one session and cue its whole-line impact before the attack resolves.");
    }

    [Fact]
    public void SnowCardsRouteThroughTheBlizzardAssetGroup()
    {
        var assets = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraCardVfxAssets.cs"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SnowBlizzardVfx.cs"));

        // Both snow forms share one group built from the session's own paths plus
        // the shared cel assets, so adding a snow scene means adding it once.
        RegressionTestHarness.Require(
            assets.Contains("ClowSnow or SakuraSnow => SnowPaths", StringComparison.Ordinal)
            && assets.Contains("[.. SnowBlizzardVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths]", StringComparison.Ordinal),
            "Expected both Snow cards to route through one blizzard asset group built from the session's own paths.");
        // The group is warmed before combat, so the first blizzard never loads
        // synchronously on the play path.
        RegressionTestHarness.Require(
            session.Contains("PreloadManager.Cache", StringComparison.Ordinal)
            && !session.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the blizzard session to consume native run assets without synchronous playback-path loads.");
    }

    [Fact]
    public void FreezeCageScenesShipInputTransparentStartStateMaterials()
    {
        var rootScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/freeze_cage_vfx.tscn"));
        var targetScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/freeze_cage_target.tscn"));

        // One root scene is instantiated once in each native combat VFX layer.
        // The target stays a pure ColorRect region with a local material and no
        // screen capture: the rear spines and front shards get their depth from
        // the game's existing BackCombatVfxContainer/CombatVfxContainer split.
        RegressionTestHarness.Require(
            rootScene.Contains("[node name=\"Cages\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal)
            && rootScene.Contains("unique_name_in_owner = true", StringComparison.Ordinal)
            && rootScene.Contains("mouse_filter = 2", StringComparison.Ordinal)
            && !rootScene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected a reusable cage container root whose pass-through control ignores input and never copies the screen.");
        RegressionTestHarness.Require(
            targetScene.Contains("[node name=\"CageBody\" type=\"ColorRect\" parent=\".\"]", StringComparison.Ordinal)
            && targetScene.Contains("unique_name_in_owner = true", StringComparison.Ordinal)
            && targetScene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && targetScene.Contains("mouse_filter = 2", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/layer_mode = 0.0", StringComparison.Ordinal)
            && !targetScene.Contains("BackBufferCopy", StringComparison.Ordinal),
            "Expected one local ColorRect cage region with a rear-layer default that ignores input and never copies the screen.");

        // The target scene ships the shader's start state: no beat has begun,
        // so rise = 0 leaves nothing between the ground and growth planes and
        // the scene draws no cage at all as delivered. A scene shipped at its
        // finished state would flash a completed prison on spawn.
        RegressionTestHarness.Require(
            targetScene.Contains("shader_parameter/rise = 0.0", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/shatter = 0.0", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/glint = 0.0", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/opacity = 1.0", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/weight = 0.5", StringComparison.Ordinal),
            "Expected the cage scene to ship its ShaderMaterial in the start state, rise at zero included.");
    }

    [Fact]
    public void FreezeCageUsesGroundedSpinesWithoutNeighbouringCardSilhouettes()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/freeze_cage.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/FreezeCageVfx.cs"));

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        var fragmentBody = ExtractGlslFunction(shader, "void fragment()");

        // Shared includes first: the bands, ink, hash, and stepped clock live in
        // cel_vfx.gdshaderinc, and a card-local copy of any of them could not
        // stay in step with every other card.
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc\"", StringComparison.Ordinal),
            "Expected Freeze to consume the shared cel mathematics and signature includes.");

        // Freeze's silhouette operator is its own grounded tapered-spine array.
        // Neighbouring card operators stay absent: cel_smin is Aqua's union,
        // cel_fbm is Blaze's turbulence, cel_radial_fold is Snow's repeated
        // wheel, cel_facet is the rounded/faceted crystal language, and
        // cel_scalloped_mass is the weather canopy. Screen sampling and a
        // shader-owned clock are forbidden card-wide. Checked against the
        // comment-stripped source because the shader commentary documents the
        // visual distinction but must not satisfy the contract by itself.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line =>
                line.Contains("cel_smin(", StringComparison.Ordinal)
                || line.Contains("cel_fbm(", StringComparison.Ordinal)
                || line.Contains("cel_radial_fold(", StringComparison.Ordinal)
                || line.Contains("cel_facet(", StringComparison.Ordinal)
                || line.Contains("cel_scalloped_mass(", StringComparison.Ordinal)
                || line.Contains("hint_screen_texture", StringComparison.Ordinal)
                || line.Contains("TIME", StringComparison.Ordinal)),
            "Expected no water union, fire turbulence, snow fold, facet flattening, cloud canopy, shader clock, or screen sample in the cage field.");
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("const float CEL_", StringComparison.Ordinal)),
            "Expected no card-local restatement of the locked band, ink, or step constants.");

        // The operator itself is a fixed-budget set of floor-anchored tapered
        // segments. The two layer modes share one shader: the rear owns seven
        // possible tall spines, the front owns four possible low shards, and
        // the weight selects the Medium/Heavy count without creating nodes.
        // Shading goes through the shared bands and ink with band edges against
        // fwidth(depth) + 0.002 rather than the silhouette field's derivative.
        // rise arrives already floored onto whole growth frames by the session,
        // so the shader holds no second quantizer.
        RegressionTestHarness.Require(
            shader.Contains("const int BACK_SPINE_BUDGET = 7", StringComparison.Ordinal)
            && shader.Contains("const int FRONT_SPINE_BUDGET = 4", StringComparison.Ordinal)
            && shader.Contains("uniform float layer_mode", StringComparison.Ordinal)
            && fragmentBody.Contains("back_base_fraction", StringComparison.Ordinal)
            && fragmentBody.Contains("front_base_fraction", StringComparison.Ordinal)
            && fragmentBody.Contains("cel_tapered_segment", StringComparison.Ordinal)
            && fragmentBody.Contains("float spine_count", StringComparison.Ordinal)
            && fragmentBody.Contains("float front_count", StringComparison.Ordinal)
            && fragmentBody.Contains("float ring_gate = foreground", StringComparison.Ordinal)
            && fragmentBody.Contains("cel_bands3(", StringComparison.Ordinal)
            && fragmentBody.Contains("cel_ink(d, aa_d, CEL_INK_WIDTH)", StringComparison.Ordinal)
            && fragmentBody.Contains("fwidth(depth) + 0.002", StringComparison.Ordinal)
            && session.Contains("Mathf.Floor(Mathf.Clamp(value, 0f, 1f) * steps) / steps", StringComparison.Ordinal)
            && !fragmentBody.Contains("cel_step_clock(", StringComparison.Ordinal),
            "Expected the enclosure to use grounded tapered spine arrays, layered foreground control, and shared bands and ink, with the growth floor session-owned.");

        // The scatter stays one session-driven progress consumed by the shader:
        // no ballistic formula may be retyped outside BallisticOffset, and this
        // card has no ballistic motion at all — the wedges fly at terminal
        // velocity, linear in that progress.
        RegressionTestHarness.Require(
            !session.Contains("AddBallisticDebris", StringComparison.Ordinal)
            && !session.Contains("BallisticOffset", StringComparison.Ordinal),
            "Expected no ballistic path in the freeze session: the scatter is shader-side linear travel off one progress.");

        RegressionTestHarness.Require(
            session.Contains("BackCombatVfxContainer", StringComparison.Ordinal)
            && session.Contains("FrontMaterial", StringComparison.Ordinal)
            && session.Contains("BackMaterial", StringComparison.Ordinal)
            && session.Contains("DisposePresentation", StringComparison.Ordinal)
            && session.Contains("ReleaseBackRoot", StringComparison.Ordinal),
            "Expected one Freeze session to own independent rear/front materials and idempotent back-layer cleanup.");
    }

    [Fact]
    public void FreezeCageBeatsStayWholeFramesInsideTheLifetimeCap()
    {
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/FreezeCageVfx.cs"));

        // Every beat is counted in whole frames of the shared stepped clock,
        // never in seconds: growth needs at least three frames to read as drawn,
        // the glint and the hold are the standard two, and the burst keeps four.
        RegressionTestHarness.Require(
            FreezeCageVfx.MediumGrowthSteps == 5
            && FreezeCageVfx.HeavyGrowthSteps == 6
            && FreezeCageVfx.GrowthSteps(FreezeWeight.Medium) >= 3
            && FreezeCageVfx.GrowthSteps(FreezeWeight.Heavy) >= 3
            && FreezeCageVfx.GrowthSteps(FreezeWeight.Heavy) > FreezeCageVfx.GrowthSteps(FreezeWeight.Medium)
            && FreezeCageVfx.GlintSteps == 2
            && FreezeCageVfx.HoldSteps == 2
            && FreezeCageVfx.ShatterBurstSteps >= 3,
            "Expected every freeze beat to be whole stepped frames, with the heavy tier buying one growth frame.");

        // MaximumLifetime is a wall-clock safety net, not a beat timer: sized
        // for the worst case (prelude, growth, hold, glint, five serialized
        // bursts, residue sublimation, fade) and asserted from the curve so a
        // later re-tune fails here instead of truncating in combat.
        RegressionTestHarness.Require(
            session.Contains("MaximumLifetime", StringComparison.Ordinal)
            && float.IsFinite(FreezeCageVfx.TotalEnvelopeSeconds(FreezeCageVfx.WorstCaseTargets))
            && FreezeCageVfx.TotalEnvelopeSeconds(FreezeCageVfx.WorstCaseTargets) < 9f,
            "Expected the worst-case envelope to stay finite and under the session's 9 s lifetime cap.");
        RegressionTestHarness.Require(
            float.IsFinite(FreezeCageVfx.TotalEnvelopeSeconds(0))
            && FreezeCageVfx.TotalEnvelopeSeconds(0) < FreezeCageVfx.TotalEnvelopeSeconds(1)
            && FreezeCageVfx.TotalEnvelopeSeconds(0) < 2.5f,
            "Expected a single-target play to run the shortest envelope: the cage rises once, nothing idles.");
    }

    [Fact]
    public void FreezeCardsWrapEveryEntryPointOnceAroundFreezeMechanics()
    {
        var freeze = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Freeze.cs"));

        // Three entry points, three wrappings: ClowFreeze's base play, its
        // activated extra-effect play, and SakuraFreeze's play each own exactly
        // one session. A fourth would be a second prison over the same play.
        RegressionTestHarness.Require(
            freeze.Split("FreezeCageVfx.PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 3,
            "Expected exactly one Freeze cage session per card entry point.");

        // Gameplay stays verbatim inside the wrapper: block first, then the
        // propagation-suppressed block enumerating its own targets exactly as it
        // did before the session existed. The prelude snapshot outside it is a
        // separate enumeration for presentation, not a second gameplay pass.
        RegressionTestHarness.Require(
            freeze.Split("WithPropagationSuppressed", StringSplitOptions.None).Length - 1 == 3
            && freeze.Split("foreach (var target in SakuraThroughResolution.TargetsFor(play))", StringSplitOptions.None).Length - 1 == 3
            && freeze.Split("await GainBlock(play, ReleasedBlock());", StringSplitOptions.None).Length - 1 == 3,
            "Expected every entry point to keep its block-then-suppressed-loop structure and its own in-loop target enumeration.");

        // The weight axis: plain Clow is the medium tier, the activated path and
        // the Sakura form share the heavy one — one orchestration, no branches.
        RegressionTestHarness.Require(
            freeze.Split("FreezeWeight.Medium", StringSplitOptions.None).Length - 1 == 1
            && freeze.Split("FreezeWeight.Heavy", StringSplitOptions.None).Length - 1 == 2,
            "Expected Medium on the plain Clow play and Heavy on the activated and Sakura plays.");

        // Every shatter must land its burst on the frame of the damage it
        // visualises: each cue sits immediately before its own DealDamage.
        var segments = freeze.Split("cues.Shatter(target)", StringSplitOptions.None);
        RegressionTestHarness.Require(
            segments.Length == 4
            && segments.Skip(1).All(static segment =>
                segment.Contains("await DealDamage(", StringComparison.Ordinal)),
            "Expected each freeze segment to fire its shatter cue immediately before its damage command resolves.");
    }

    [Fact]
    public void FreezeCardsRouteThroughTheCageAssetGroup()
    {
        var assets = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraCardVfxAssets.cs"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/FreezeCageVfx.cs"));

        // Both freeze forms share one group built from the session's own paths
        // plus the shared cel assets, so adding a cage scene means adding it
        // once.
        RegressionTestHarness.Require(
            assets.Contains("ClowFreeze or SakuraFreeze => FreezePaths", StringComparison.Ordinal)
            && assets.Contains("[.. FreezeCageVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths]", StringComparison.Ordinal),
            "Expected both Freeze cards to route through one cage asset group built from the session's own paths.");
        // The group is warmed before combat, so the first prison never loads
        // synchronously on the play path.
        RegressionTestHarness.Require(
            session.Contains("PreloadManager.Cache", StringComparison.Ordinal)
            && !session.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the cage session to consume native run assets without synchronous playback-path loads.");
    }

    [Fact]
    public void ShieldCardsHaveNoDedicatedCombatVfx()
    {
        var shield = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Shield.cs"));
        RegressionTestHarness.Require(
            !shield.Contains("SakuraShieldPlateVfx", StringComparison.Ordinal)
            && !shield.Contains("SakuraShieldPlate", StringComparison.Ordinal),
            "Expected both shield cards to resolve gameplay without a dedicated VFX session.");
        var assets = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraCardVfxAssets.cs"));
        RegressionTestHarness.Require(
            !assets.Contains("ShieldPaths", StringComparison.Ordinal)
            && !assets.Contains("ClowShield or SakuraShield", StringComparison.Ordinal),
            "Expected shield cards to be absent from the dedicated card VFX asset route.");
        RegressionTestHarness.Require(
            shield.Contains("await GainBlock(play, CurrentBlock())", StringComparison.Ordinal)
            && shield.Contains("await GainBlock(play, ReleasedBlock())", StringComparison.Ordinal)
            && shield.Contains("CurrentHpBlock(Owner.Creature.CurrentHp", StringComparison.Ordinal),
            "Expected shield gameplay values and the Sakura current-HP conversion to remain in place.");
    }

    [Fact]
    public void SwordBladeDrawsHybridArcSweepWithoutWeaponOrResidue()
    {
        var rootScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/sakura_sword_vfx.tscn"));
        var targetScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/card_vfx/sakura_sword_target.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_sword_blade.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SakuraSwordBladeVfx.cs"));
        var sword = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Sword.cs"));
        var blade = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Blade.cs"));

        var shaderCode = shader
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        // No screen copy and no screen sampling. The mark is additive-looking but composited
        // normally, so nothing here needs the scene behind it.
        RegressionTestHarness.Require(
            !rootScene.Contains("BackBufferCopy", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_TEXTURE", StringComparison.Ordinal),
            "Expected the slash to carry no screen copy and sample no screen texture.");

        // One parent, and no debris parent. The effect leaves nothing on screen after it
        // ends, so there is nothing for a second parent to hold.
        RegressionTestHarness.Require(
            rootScene.Contains("[node name=\"Slashes\" type=\"Node2D\"", StringComparison.Ordinal)
            && !rootScene.Contains("Debris", StringComparison.Ordinal)
            && rootScene.Contains("mouse_filter = 2", StringComparison.Ordinal),
            "Expected the root scene to expose only the slash parent and ignore input.");

        // One rect and one local material, where the previous revision had a pivoted weapon
        // rect, a separate wound rect, and a fragment parent. The weapon is gone, so the
        // pivot that swung it and the anchor that held its wound are gone with it.
        RegressionTestHarness.Require(
            targetScene.Contains("[node name=\"SlashAnchor\" type=\"Node2D\"", StringComparison.Ordinal)
            && targetScene.Contains("[node name=\"SlashBody\" type=\"ColorRect\"", StringComparison.Ordinal)
            && targetScene.Split("resource_local_to_scene = true", StringSplitOptions.None).Length - 1 == 1
            && !targetScene.Contains("BladePivot", StringComparison.Ordinal)
            && !targetScene.Contains("BladeBody", StringComparison.Ordinal)
            && !targetScene.Contains("CutBody", StringComparison.Ordinal)
            && !targetScene.Contains("Fragments", StringComparison.Ordinal),
            "Expected one slash rect with one local material, and no weapon pivot, wound rect, or fragment parent.");

        foreach (var uniform in new[]
                 {
                     "arc_count", "cross_tilt_deg", "single_tilt_deg", "weight", "flash", "opacity"
                 })
        {
            Assert.Contains($"uniform float {uniform}", shader, StringComparison.Ordinal);
        }
        Assert.Contains("uniform vec4 arc_phase", shader, StringComparison.Ordinal);
        Assert.Contains("uniform vec2 region_size", shader, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            shader.Contains("region_size = vec2(240.0, 200.0)", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/region_size = Vector2(240, 200)", StringComparison.Ordinal),
            "Expected the shader's default region to match the scene's, so a preview draws what combat draws.");

        // Every parameter the scene ships must be driven, including the ones whose shipped
        // value draws almost nothing. The previous revision shipped `extend` at 0 — which
        // collapsed the blade to zero length — and nothing drove it, so a bare hilt swung
        // through the target and passed every check here. The rule outlived the parameter:
        // arc_phase ships at zero, which draws no stroke at all.
        RegressionTestHarness.Require(
            targetScene.Contains("shader_parameter/arc_phase = Vector4(0, 0, 0, 0)", StringComparison.Ordinal)
            && session.Contains("\"arc_phase\"", StringComparison.Ordinal)
            && session.Contains("Callable.From<float>(value => SetPhase(arc, value))", StringComparison.Ordinal),
            "Expected the launch beat to drive arc_phase, since the scene ships it at zero and draws nothing there.");

        // The hybrid rendering language, matching the accepted wind and fire marks rather
        // than the dark-ink cel cards. Gale reached the same place first and for the same
        // reason: a constant-width dark outline around a thin bright arc reads as weight,
        // which is the opposite of speed, and a 0.3s effect has too few stepped frames to
        // read as anything but a hitch. Asserted as absences because a copy-paste from one
        // of the four ink cards would silently reintroduce the whole language.
        RegressionTestHarness.Require(
            !shader.Contains("cel_ink(", StringComparison.Ordinal)
            && !shader.Contains("cel_bands3(", StringComparison.Ordinal)
            && !shader.Contains("cel_step_clock", StringComparison.Ordinal)
            && !shader.Contains("cel_vfx.gdshaderinc", StringComparison.Ordinal)
            && !shader.Contains("cel_signature.gdshaderinc", StringComparison.Ordinal)
            && !shaderCode.Any(static line => line.Contains("const float CEL_", StringComparison.Ordinal)),
            "Expected the hybrid language: a bright core with a transparent falloff, no ink line, no hard bands, no stepped clock.");
        // Light stays a bounded local field. No additive blend mode and no full-frame bloom,
        // which is the same limit Gale's halo accepts.
        // Checked against the stripped source, because the shader's own commentary names
        // both of these while explaining why it does not use them.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("blend_add", StringComparison.Ordinal))
            && !shaderCode.Any(static line => line.Contains("bloom", StringComparison.OrdinalIgnoreCase)),
            "Expected brightness to come from a bounded local field rather than an additive or bloom pass.");
        // No shader-owned clock, unlike the hybrid state marks. Those persist for many turns
        // and ride the engine clock; every beat here belongs to one card play, and the freeze
        // has to be able to actually stop the drawn detail.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("TIME", StringComparison.Ordinal)),
            "Expected the session to own every beat, since an engine-clocked field could not be frozen on impact.");

        // The weapon is gone. All six parts and the wound are asserted absent by their own
        // named constants, because this is the change the rework exists to make and a
        // partial revert would otherwise still pass every assertion above.
        foreach (var part in new[]
                 {
                     "BLADE_ROOT_HALF", "BLADE_TIP_HALF", "FULLER_", "WING_", "GEM_", "GRIP_",
                     "POMMEL_", "TIP_Y", "CUT_DEG", "CUT_SPAN", "CUT_NOTCH", "draw_cut",
                     "cut_open", "blade_count", "extend"
                 })
        {
            RegressionTestHarness.Require(
                !shader.Contains(part, StringComparison.Ordinal),
                $"Expected no weapon or wound geometry in the slash shader, but found {part}.");
        }

        // Six cards, six silhouette operators. This one is a polar arc sweep, so both
        // operators that would pull it toward another card's reading stay absent: cel_smin
        // is water's mass union and cel_fbm is fire's torn outline.
        RegressionTestHarness.Require(
            !shaderCode.Any(static line => line.Contains("cel_smin(", StringComparison.Ordinal))
            && !shaderCode.Any(static line => line.Contains("cel_fbm(", StringComparison.Ordinal)),
            "Expected the slash to stay a polar arc sweep: neither smooth union nor turbulence may build it.");
        RegressionTestHarness.Require(
            shader.Contains("float ring = length(rel) - radius;", StringComparison.Ordinal)
            && shader.Contains("float radius = (chord * 0.5) / sin(HALF_SPAN)", StringComparison.Ordinal),
            "Expected the stroke to be laid along a circular arc whose radius follows from its chord and angular span.");

        float Constant(string name)
        {
            var line = shaderCode.FirstOrDefault(candidate =>
                candidate.StartsWith($"const float {name} =", StringComparison.Ordinal));
            RegressionTestHarness.Require(line is not null, $"Expected {name} to be a named constant.");
            var declaration = line ?? string.Empty;
            var value = declaration[(declaration.IndexOf('=') + 1)..].TrimEnd(';', ' ');
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        // The one axis that separates this from Gale with colour removed. Gale is a closed
        // crescent area — thick through the belly, with a concave bite — while this is a
        // line. Hue separates them too, but hue is never allowed to be the only difference,
        // so the ratio is what gets measured. Evaluated at the smallest region the budget
        // allows, since that is where the stroke is shortest relative to its width.
        var span = Constant("SPAN");
        var heaviestHalf = Constant("MID_HALF_HEAVY");
        RegressionTestHarness.Require(
            session.Contains("MinHeight: 140f", StringComparison.Ordinal),
            "Expected the geometry budget's floor to stay in step with the aspect-ratio gate below.");
        var shortestChord = span * 140f;
        RegressionTestHarness.Require(
            shortestChord / (heaviestHalf * 2f) > 6f,
            "Expected the stroke to stay a line rather than a crescent area, so it never reads as Gale's blade.");
        RegressionTestHarness.Require(
            span > 1f,
            "Expected the stroke to leave the frame at both ends rather than floating inside its own rect.");
        RegressionTestHarness.Require(
            Constant("MID_HALF_LIGHT") < heaviestHalf
            && Constant("LAY_FAST") < Constant("LAY_SLOW")
            && Constant("CORE_FRAC") is > 0f and < 1f,
            "Expected the weight axis to buy width and lay time together, with a core narrower than the stroke.");
        // Arrive fast, leave slow.
        RegressionTestHarness.Require(
            Constant("RISE_END") < 1f - Constant("DECAY_START"),
            "Expected the stroke to reach full presence faster than it decays.");

        // One constant per side, kept in step: the X the strokes trace has to be the X the
        // session specified, and the previous revision's wound angle is the same 24 degrees.
        RegressionTestHarness.Require(
            shader.Contains("cross_tilt_deg = 24.0", StringComparison.Ordinal)
            && session.Contains("CrossTiltDegrees = 24f", StringComparison.Ordinal)
            && targetScene.Contains("shader_parameter/cross_tilt_deg = 24.0", StringComparison.Ordinal),
            "Expected the session, scene, and shader to agree on the crossing angle.");

        RegressionTestHarness.Require(
            session.Contains("session.StartClock();", StringComparison.Ordinal)
            && session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.Resolve(room, creature, index, Budget)", StringComparison.Ordinal)
            && session.Contains("room.CombatVfxContainer.AddChildSafely(root)", StringComparison.Ordinal),
            "Expected the session to derive from the shared skeleton, start its clock explicitly, and size through the shared resolver.");

        // The hit-stop pauses the launch tween rather than calling BeginHold. That helper
        // stops the base stepped clock and pushes `held` to shaders that read it; this card
        // declares no clock uniforms and moves every stroke by tween, and the base class is
        // explicit that a hold does not pause tween time. Calling it here would have
        // satisfied the shape of a freeze while freezing nothing, so its absence is asserted
        // alongside the pause that replaced it.
        RegressionTestHarness.Require(
            session.Contains("launch.Pause()", StringComparison.Ordinal)
            && session.Contains("launch.Play()", StringComparison.Ordinal)
            && !session.Contains("BeginHold(", StringComparison.Ordinal),
            "Expected impact to pause the stroke's own tween, since BeginHold cannot freeze tween-driven motion.");

        // No lagging wound, and nothing left behind. The previous revision delayed its wound
        // two stepped frames so the damage number would land on it, which stretched the whole
        // chain to accommodate a decoration; the game already owns damage numbers and health
        // bars, so the freeze and flash land on the same beat as them instead.
        foreach (var removed in new[]
                 {
                     "CutLagSteps", "CutOpenFraction", "CutOpensWithinEnvelope", "cut_open",
                     "SampleAfterimages", "Afterimage", "AddBallisticDebris", "FragmentPoints",
                     "SetExtend"
                 })
        {
            RegressionTestHarness.Require(
                !session.Contains(removed, StringComparison.Ordinal),
                $"Expected the session to carry no wound, afterimage, or debris machinery, but found {removed}.");
        }

        // Total length is the point of the rework, so it is asserted on the constants rather
        // than left to be noticed by eye. The previous revision ran about 1.15s per target
        // for a Basic attack.
        RegressionTestHarness.Require(
            SakuraSwordBladeVfx.StrokeFitsBudget(),
            "Expected one play to stay inside its time budget.");
        RegressionTestHarness.Require(
            SakuraSwordBladeVfx.SingleTargetSeconds() < 0.6f
            && SakuraSwordBladeVfx.DualTargetSeconds() < 0.9f,
            "Expected a single stroke and a full flurry to stay short enough to bear repetition.");

        // Extra hits buy crossings inside a fixed envelope, not extra wall time, and the
        // count is bounded because past four the strokes stop being tellable apart.
        RegressionTestHarness.Require(
            SakuraSwordBladeVfx.ArcCount(SwordMode.Single, 4) == 1
            && SakuraSwordBladeVfx.ArcCount(SwordMode.Dual, 2) == 2
            && SakuraSwordBladeVfx.ArcCount(SwordMode.Dual, 9) == 4
            && SakuraSwordBladeVfx.ArcCount(SwordMode.Dual, 0) == 1,
            "Expected the arc count to follow the hit count for Blade, stay single for Sword, and clamp at four.");
        RegressionTestHarness.Require(
            session.Contains("CrossingEnvelope", StringComparison.Ordinal)
            && session.Contains("var each = _arcCount > 1 ? envelope / _arcCount : 0f;", StringComparison.Ordinal),
            "Expected crossings to pack into a fixed envelope so four read as denser rather than longer.");

        // The intensity axis is one number, so the tiers cannot drift into separate code
        // paths. Frequency picks the tier: the Basic attack is lightest and the release token
        // is heaviest.
        RegressionTestHarness.Require(
            SakuraSwordBladeVfx.WeightValue(SlashWeight.Light)
                < SakuraSwordBladeVfx.WeightValue(SlashWeight.Medium)
            && SakuraSwordBladeVfx.WeightValue(SlashWeight.Medium)
                < SakuraSwordBladeVfx.WeightValue(SlashWeight.Heavy),
            "Expected the weight tiers to stay ordered on one axis.");

        // ClowSword's two paths share one orchestration and differ only by tier. A second
        // PlayOrResolveAsync in the activated path would be a second source of truth for
        // what the card looks like.
        var activatedIndex = sword.IndexOf("Task PlayActivatedCard(", StringComparison.Ordinal);
        var nextClassIndex = sword.IndexOf("class SakuraSword", StringComparison.Ordinal);
        // Bounded to the activated method's own body, which ends where the shared helper
        // begins. Running the slice to the next class instead would swallow PlayStroke,
        // whose whole job is to hold the one orchestration both paths call.
        var strokeHelperIndex = sword.IndexOf("private async Task PlayStroke(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            activatedIndex >= 0
            && strokeHelperIndex > activatedIndex
            && nextClassIndex > strokeHelperIndex,
            "Expected ClowSword's activated path, then the shared helper, then SakuraSword's declaration.");
        RegressionTestHarness.Require(
            sword.Contains("PlayStroke(choiceContext, play, SlashWeight.Light)", StringComparison.Ordinal)
            && sword.Contains("PlayStroke(choiceContext, play, SlashWeight.Medium)", StringComparison.Ordinal)
            && sword.Contains("ExtraHpLoss", StringComparison.Ordinal)
            && !sword[activatedIndex..strokeHelperIndex].Contains("PlayOrResolveAsync(", StringComparison.Ordinal),
            "Expected both ClowSword paths to share one orchestration and differ only by weight tier.");
        RegressionTestHarness.Require(
            sword.Split("PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 2,
            "Expected exactly two orchestrations in Sword.cs: ClowSword's shared stroke and SakuraSword's own.");

        foreach (var card in new[] { "ClowSword", "SakuraSword" })
        {
            var classIndex = sword.IndexOf($"class {card}", StringComparison.Ordinal);
            RegressionTestHarness.Require(classIndex >= 0, $"Expected {card} to be present.");
            var body = sword[classIndex..];
            var impactIndex = body.IndexOf("cues.Impact(target)", StringComparison.Ordinal);
            var damageIndex = body.IndexOf("await DealDamage(choiceContext, target", StringComparison.Ordinal);
            RegressionTestHarness.Require(
                body.Contains("SwordMode.Single", StringComparison.Ordinal)
                && impactIndex >= 0
                && damageIndex > impactIndex,
                $"Expected {card} to use the single-stroke presentation and show its cue before damage resolves.");
        }
        // The release token is the rarest of the three plays, so it takes the heaviest tier.
        RegressionTestHarness.Require(
            sword.IndexOf("SlashWeight.Heavy", StringComparison.Ordinal) > nextClassIndex,
            "Expected SakuraSword to take the heaviest tier, since a release token is the rarest play here.");
        // Enumerated once, and the loop walks that same copy. Enumerating twice could
        // diverge: the second pass would drop targets the first pass killed.
        RegressionTestHarness.Require(
            sword.Split("SakuraThroughResolution.TargetsFor(play)", StringSplitOptions.None).Length - 1 == 1
            && sword.Contains("var targets = SakuraThroughResolution.TargetsFor(play).ToList();", StringComparison.Ordinal)
            && sword.Contains("foreach (var target in targets)", StringComparison.Ordinal),
            "Expected SakuraSword to snapshot its targets exactly once and drive both the visuals and the hit loop from that copy.");

        Assert.Contains("SakuraSwordBladeVfx.PlayOrResolveAsync(", blade, StringComparison.Ordinal);
        var bladeHitsIndex = blade.IndexOf("var hits = BladeRules.HitCount(this);", StringComparison.Ordinal);
        var bladeImpactIndex = blade.IndexOf("cues.Impact(target)", StringComparison.Ordinal);
        var bladeAttackIndex = blade.IndexOf("await SakuraActions.Attack(", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            bladeHitsIndex >= 0
            && bladeImpactIndex > bladeHitsIndex
            && bladeAttackIndex > bladeImpactIndex
            && blade.Contains("SwordMode.Dual", StringComparison.Ordinal)
            && blade.Contains("SlashWeight.Medium", StringComparison.Ordinal)
            && blade.Contains("crossings: hits", StringComparison.Ordinal),
            "Expected Blade to read its hit count once, use the dual presentation, and show its cue before the attack.");
        // The hit count is read once and handed over as a resolved number. A second read
        // for presentation could disagree with the one the attack used.
        RegressionTestHarness.Require(
            blade.Split("BladeRules.HitCount(this)", StringSplitOptions.None).Length - 1 == 1
            && blade.Contains("hitCount: hits", StringComparison.Ordinal),
            "Expected Blade's hit count to be read once and shared by the attack and the visuals.");
    }

    [Fact]
    public void CelVfxSharedLayerRemainsSingleSourceOfTruth()
    {
        var includePath = RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc");
        var include = File.ReadAllText(includePath);

        foreach (var export in new[]
                 {
                     "float cel_smin(", "float cel_ellipse(", "float cel_hash11(",
                     "float cel_facet(", "vec3 cel_bands3(", "float cel_ink(",
                     "float cel_body(", "vec3 cel_quantize(", "float cel_step_clock(",
                     "float cel_noise2(", "float cel_fbm(",
                     "float cel_tapered_segment(",
                     "float cel_scalloped_mass("
                 })
            Assert.Contains(export, include, StringComparison.Ordinal);

        // The locked art language: band boundaries, ink weight, step rate, and the
        // refraction ceiling. A card shader that restates any of them has forked the
        // shared visual language, so each must be declared exactly once repo-wide.
        var shaderRoot = Path.GetFullPath(Path.Combine(includePath, "..", ".."));
        var shaderSources = Directory
            .EnumerateFiles(shaderRoot, "*.gdshader*", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".uid", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
        foreach (var constant in new[]
                 {
                     "CEL_BAND_LIGHT_MID", "CEL_BAND_MID_DEEP",
                     "CEL_INK_WIDTH", "CEL_STEP_HZ", "CEL_REFRACT_MAX_PX"
                 })
        {
            var declarations = shaderSources
                .Count(source => source.Contains($"const float {constant} =", StringComparison.Ordinal));
            RegressionTestHarness.Require(
                declarations == 1,
                $"Expected {constant} to be declared exactly once across all shaders, found {declarations}.");
        }

        // Comments in this file discuss "uniform control flow", so scan declarations
        // rather than raw substrings.
        var includeCode = include
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        // Uniforms in the shared include would force every consumer to carry
        // parameters it never reads, and Godot reports no error for an unused one.
        RegressionTestHarness.Require(
            !includeCode.Any(static line => line.StartsWith("uniform ", StringComparison.Ordinal)),
            "Expected the shared mathematics include to stay stateless and uniform-free.");

        // cel_ink taking aa is the whole point: hiding fwidth inside it would look
        // safe while still being undefined when called from a pixel-varying branch.
        RegressionTestHarness.Require(
            !includeCode.Any(static line =>
                line.Contains("fwidth(", StringComparison.Ordinal)
                || line.Contains("dFdx(", StringComparison.Ordinal)
                || line.Contains("dFdy(", StringComparison.Ordinal)),
            "Expected no screen-space derivative call inside the shared include; callers pass aa in.");

        // A pixel budget divided by region_size is region UV, and SCREEN_UV does not
        // speak it: the offset overshoots CEL_REFRACT_MAX_PX by the viewport-to-region
        // ratio, grows as the region shrinks, and skews direction on a non-square
        // region. Asserted repo-wide rather than per card, because the failure mode is
        // a later card copying the older form from one that still had it.
        foreach (var path in Directory.EnumerateFiles(shaderRoot, "*.gdshader", SearchOption.AllDirectories))
            RequireScreenOffsetsUsePixelSize(path);
    }

    private static string ExtractGlslFunction(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        RegressionTestHarness.Require(start >= 0, $"Expected GLSL function {signature}.");
        var brace = source.IndexOf('{', start);
        RegressionTestHarness.Require(brace > start, $"Expected a body for {signature}.");
        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Unbalanced braces in {signature}.");
    }

    /// <summary>
    /// A pixel budget divided by <c>region_size</c> is region UV, and <c>SCREEN_UV</c>
    /// does not speak it: the offset overshoots <c>CEL_REFRACT_MAX_PX</c> by the
    /// viewport-to-region ratio, grows as the region shrinks, and skews direction on a
    /// non-square region.
    /// </summary>
    /// <remarks>
    /// The offset may be written inline or held in a local, so a local is resolved back
    /// to its assignment before being judged. Checking one line at a time would pass
    /// the two-line form, where the divide and the <c>SCREEN_UV</c> sample sit on
    /// separate lines.
    /// </remarks>
    private static void RequireScreenOffsetsUsePixelSize(string shaderPath)
    {
        const string marker = "SCREEN_UV +";
        var name = Path.GetFileName(shaderPath);
        var lines = File.ReadAllLines(shaderPath)
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            var at = lines[i].IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                continue;

            var offset = lines[i][(at + marker.Length)..].TrimStart();
            if (offset.Contains("SCREEN_PIXEL_SIZE", StringComparison.Ordinal))
                continue;

            var local = new string(offset
                .TakeWhile(static character => char.IsLetterOrDigit(character) || character == '_')
                .ToArray());
            RegressionTestHarness.Require(
                local.Length > 0,
                $"Expected {name} to offset SCREEN_UV through SCREEN_PIXEL_SIZE or a named local.");

            var assignment = lines
                .Take(i)
                .LastOrDefault(candidate => candidate.Contains($" {local} =", StringComparison.Ordinal));
            RegressionTestHarness.Require(
                assignment is not null
                && assignment.Contains("SCREEN_PIXEL_SIZE", StringComparison.Ordinal)
                && !assignment.Contains("region_size", StringComparison.Ordinal),
                $"Expected {name} to convert screen offset '{local}' through SCREEN_PIXEL_SIZE, not region UV.");
        }
    }

    [Fact]
    public void MagicCircleReferenceProgramBakesSharedRotatableMasks()
    {
        const string referenceRelativePath =
            "research/cel-vfx/magic-circle-refs/ref_star.py";
        var referencePath = RegressionTestHarness.FindRepoFile(referenceRelativePath);
        var generator = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "scripts/generate_cel_magic_circles.py"));
        var adapter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "scripts/ref_star_cairo.py"));
        var verifier = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "scripts/verify_cel_magic_circle_masks.gd"));
        var signature = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc"));
        var prelude = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cel_wand_prelude.gdshader"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/CelVfxSession.cs"));
        var presenter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraMagicCirclePresenter.cs"));
        var main = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/MainFile.cs"));

        RegressionTestHarness.Require(
            File.Exists(referencePath),
            $"Expected the permanent magic-circle geometry source at {referenceRelativePath}.");

        foreach (var scriptRelativePath in new[]
        {
            "scripts/generate_cel_magic_circles.py",
            "scripts/_analyze_ref_topology.py",
            "scripts/_crop_ref_cartouches.py"
        })
        {
            var script = File.ReadAllText(RegressionTestHarness.FindRepoFile(scriptRelativePath));
            RegressionTestHarness.Require(
                script.Contains("research/cel-vfx/magic-circle-refs", StringComparison.Ordinal)
                && !script.Contains(".trellis/tasks/", StringComparison.Ordinal),
                $"Expected {scriptRelativePath} to consume permanent research inputs, not task-local evidence.");
        }

        RegressionTestHarness.Require(
            generator.Contains("SIZE = 1024", StringComparison.Ordinal)
            && generator.Contains("render_ref_star(", StringComparison.Ordinal)
            && generator.Contains("ink_coverages", StringComparison.Ordinal)
            && generator.Contains("knockout_masks", StringComparison.Ordinal)
            && generator.Contains("The textures contain no era colour", StringComparison.Ordinal),
            "Expected one high-resolution, colourless ref_star.py mask pair shared by all eras.");

        RegressionTestHarness.Require(
            adapter.Contains("ZODIAC_GLYPHS = \"♒♓♈♉♌♍♎♏\"", StringComparison.Ordinal)
            && adapter.Contains("ZODIAC_FONT = \"Noto Sans Symbols Light\"", StringComparison.Ordinal)
            && adapter.Contains("DIRECTION_CENTRES", StringComparison.Ordinal)
            && adapter.Contains("knockout_masks", StringComparison.Ordinal),
            "Expected the headless Turtle adapter to preserve the eight glyphs, fixed fonts, radial directions, and black overdraw.");

        RegressionTestHarness.Require(
            !signature.Contains("cel_seal_", StringComparison.Ordinal)
            && !signature.Contains("float cel_magic_circle(", StringComparison.Ordinal)
            && !signature.Contains("float cel_ring_ticks(", StringComparison.Ordinal)
            && !signature.Contains("float cel_fold_polar(", StringComparison.Ordinal),
            "Expected no duplicate magic-circle geometry or obsolete SDF sampler in the shared shader include.");

        RegressionTestHarness.Require(
            prelude.Contains("uniform sampler2D magic_circle_ink", StringComparison.Ordinal)
            && prelude.Contains("uniform sampler2D magic_circle_knockout", StringComparison.Ordinal)
            && prelude.Contains("uniform float speed_lines_enabled = 1.0", StringComparison.Ordinal)
            && prelude.Contains("vec2 magic_circle_uv(", StringComparison.Ordinal)
            && prelude.Contains("composed_seal * (1.0 - knockout_layers", StringComparison.Ordinal)
            && prelude.Contains("magic_circle_colour", StringComparison.Ordinal)
            && prelude.Contains("uniform vec4 magic_circle_layer_phases", StringComparison.Ordinal)
            && prelude.Contains("vec4 phases = magic_circle_layer_phases;", StringComparison.Ordinal)
            && !prelude.Contains("magic_circle_layer_speeds", StringComparison.Ordinal)
            && prelude.Contains("float magic_circle_composed(", StringComparison.Ordinal)
            && prelude.Contains("MAGIC_CIRCLE_HALO_RADIUS_PX", StringComparison.Ordinal)
            && prelude.Contains("MAGIC_CIRCLE_HALO_OPACITY", StringComparison.Ordinal),
            "Expected the real wand prelude to rotate, erase, composite, colour, and locally halo the four direct line stages.");

        foreach (var name in new[] { "magic_circle_ink.png", "magic_circle_knockout.png" })
        {
            var relativePath = $"SakuraMod/images/card_vfx/magic_circles/{name}";
            var path = RegressionTestHarness.FindRepoFile(relativePath);
            var import = File.ReadAllText($"{path}.import");
            RegressionTestHarness.Require(
                new FileInfo(path).Length > 0
                && import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
                && import.Contains("compress/mode=0", StringComparison.Ordinal)
                && import.Contains("mipmaps/generate=false", StringComparison.Ordinal)
                && import.Contains("process/fix_alpha_border=false", StringComparison.Ordinal),
                $"Expected imported, non-mipmapped shared magic-circle mask {name}.");
        }

        RegressionTestHarness.Require(
            verifier.Contains("source.get_data() != imported.get_data()", StringComparison.Ordinal)
            && verifier.Contains("EXPECTED_SIZE := Vector2i(1024, 1024)", StringComparison.Ordinal),
            "Expected pinned Godot to verify the imported masks remain pixel-exact RGBA data.");

        RegressionTestHarness.Require(
            presenter.Contains("MagicCircleInkPath", StringComparison.Ordinal)
            && presenter.Contains("MagicCircleKnockoutPath", StringComparison.Ordinal)
            && presenter.Contains("LoadResources", StringComparison.Ordinal)
            && session.Contains("SakuraMagicCirclePresenter.AssetPaths", StringComparison.Ordinal)
            && presenter.Contains(
                "PreloadManager.Cache.GetAsset<Shader>(WandPreludeShaderPath)",
                StringComparison.Ordinal)
            && presenter.Contains(
                "PreloadManager.Cache.GetAsset<Texture2D>(MagicCircleInkPath)",
                StringComparison.Ordinal)
            && presenter.Contains(
                "PreloadManager.Cache.GetAsset<Texture2D>(MagicCircleKnockoutPath)",
                StringComparison.Ordinal)
            && !presenter.Contains("private static Shader?", StringComparison.Ordinal)
            && !presenter.Contains("private static Texture2D?", StringComparison.Ordinal)
            && !presenter.Contains("ResourceLoader.Load", StringComparison.Ordinal)
            && !presenter.Contains("PreloadResources()", StringComparison.Ordinal)
            && !session.Contains("PreloadResources()", StringComparison.Ordinal)
            && presenter.Contains("MagicCircleDiameter", StringComparison.Ordinal)
            && presenter.Contains("MagicCircleRadius", StringComparison.Ordinal)
            && presenter.Contains("MagicCircleZIndex", StringComparison.Ordinal)
            && presenter.Contains("new Color(1f, 0.94f, 0.62f)", StringComparison.Ordinal)
            && presenter.Contains("new Color(1f, 0.78f, 0.94f)", StringComparison.Ordinal)
            && presenter.Contains("new Color(0.88f, 1f, 0.8f)", StringComparison.Ordinal)
            && !main.Contains("CelVfxSession.PreloadResources();", StringComparison.Ordinal),
            "Expected every era to resolve cache-owned masks without process-static Godot resources and select only ivory gold, pale violet-pink, or moonlit mint ink.");
    }

    [Fact]
    public void CardVfxPreferenceGatesApprovedPresentationWithoutOwningSpellTurn()
    {
        var config = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/SakuraModConfig.cs"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/CelVfxSession.cs"));
        var presenter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraMagicCirclePresenter.cs"));
        var simpleVfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardPlayVfx.cs"));
        var bigLittle = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/BigLittleStandeeVfx.cs"));
        var standee = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));
        var spellTurn = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SpellTurnTransformationVfx.cs"));

        var disabledIndex = bigLittle.IndexOf(
            "if (!SakuraModConfig.IsCardVfxEnabled())",
            StringComparison.Ordinal);
        var applyStateIndex = bigLittle.IndexOf(
            "ApplySizeEffectState(caster, effect);",
            disabledIndex,
            StringComparison.Ordinal);
        var resolveIndex = bigLittle.IndexOf(
            "await ResolveOnce();",
            applyStateIndex,
            StringComparison.Ordinal);

        RegressionTestHarness.Require(
            config.Contains("EnableCardVfxBinding", StringComparison.Ordinal)
            && config.Contains("IsCardVfxEnabled", StringComparison.Ordinal)
            && new SakuraModConfig().EnableCardVfx
            && session.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && session.Contains("if (presentationEnabled)", StringComparison.Ordinal)
            && presenter.Contains("IsCardVfxEnabled", StringComparison.Ordinal)
            && simpleVfx.Contains("if (!SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal),
            "Expected one local card-VFX preference to gate the shared session and approved presentation owners.");
        RegressionTestHarness.Require(
            disabledIndex >= 0
            && applyStateIndex > disabledIndex
            && resolveIndex > applyStateIndex
            && standee.Contains("ApplySizeEffectState", StringComparison.Ordinal)
            && !spellTurn.Contains("IsCardVfxEnabled", StringComparison.Ordinal)
            && !spellTurn.Contains("EnableCardVfxBinding", StringComparison.Ordinal),
            "Expected Big/Little state to be preserved before gameplay and Spell Turn to stay untouched by the preference.");
    }

    [Fact]
    public void CelVfxSessionOwnsGeometryClockCleanupAndTheRealCardPrelude()
    {
        var geometry = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/CelVfxGeometry.cs"));
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/CelVfxSession.cs"));
        var presenter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraMagicCirclePresenter.cs"));
        var transaction = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraExtraEffectTransaction.cs"));
        var chibiController = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraChibiStandeeIdleController.cs"));
        var standardController = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));
        var preludeShader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/cel_wand_prelude.gdshader"));

        RegressionTestHarness.Require(
            geometry.Contains("record struct GeometryBudget(", StringComparison.Ordinal)
            && geometry.Contains("TargetGeometry Resolve(", StringComparison.Ordinal)
            && geometry.Contains("hitbox.GetGlobalRect()", StringComparison.Ordinal)
            && geometry.Contains("GetBottomOfHitbox()", StringComparison.Ordinal)
            && geometry.Contains("ResolveCaster(", StringComparison.Ordinal)
            && geometry.Contains("DuplicateMaterial(", StringComparison.Ordinal)
            && geometry.Contains("AddBallisticDebris(", StringComparison.Ordinal)
            && geometry.Contains("BallisticOffset(", StringComparison.Ordinal),
            "Expected the shared geometry owner to cover hitbox-first placement, caster resolution, material isolation, and ballistic debris.");

        // Caster-side anchoring has exactly one implementation. It lived as a private
        // helper in the session while the magic circle was session-owned. The room
        // presenter now owns only the circle's vertical bias and still reads raw hitbox
        // facts through the shared geometry owner.
        RegressionTestHarness.Require(
            presenter.Contains("CelVfxGeometry.ResolveCaster(casterNode)", StringComparison.Ordinal)
            && !presenter.Contains("GetBottomOfHitbox()", StringComparison.Ordinal)
            && !presenter.Contains("VfxSpawnPosition", StringComparison.Ordinal)
            && geometry.Contains("float FacingSign", StringComparison.Ordinal),
            "Expected caster anchoring to have a single owner in CelVfxGeometry, with the presenter holding only the circle's vertical bias.");

        // The flip sign is read as a sign, never as the controller's transform. Both
        // idle controllers publish their flip by negating their own Scale, so an
        // effect parented into that subtree would have its ink width and region size
        // mirrored along with its position.
        RegressionTestHarness.Require(
            chibiController.Contains("FacingSign", StringComparison.Ordinal)
            && standardController.Contains("FacingSign", StringComparison.Ordinal)
            && chibiController.Contains("_body.FlipH ? -1f : 1f", StringComparison.Ordinal)
            && standardController.Contains("_body.FlipH ? -1f : 1f", StringComparison.Ordinal),
            "Expected both idle controllers to publish facing as a sign a caster-side effect can multiply its own offset by.");

        // The parabola is extracted so a consumer whose body already exists — a
        // shader-driven rect, say — reuses the trajectory instead of retyping it.
        // Once retyped, the copies drift silently.
        //
        // Aqua is excluded by name, not by accident: its session predates the shared
        // skeleton and its duplication is the deliberate boundary recorded in the cel
        // VFX guide. Every other card must route through the shared helper, which is
        // what this count enforces.
        var integrations = Directory
            .EnumerateFiles(
                Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
                    "SakuraModCode/Cards/Visuals/CelVfxGeometry.cs"))!,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith("AquaWaterSphereVfx.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .Count(static source => source.Contains("0.5f * gravity * time * time", StringComparison.Ordinal));
        RegressionTestHarness.Require(
            integrations == 1,
            $"Expected exactly one ballistic integration outside Aqua's grandfathered session, found {integrations}.");

        var constructorIndex = session.IndexOf("protected CelVfxSession(", StringComparison.Ordinal);
        var startClockIndex = session.IndexOf("internal void StartClock()", StringComparison.Ordinal);
        var clockLaunchIndex = session.IndexOf("TaskHelper.RunSafely(DriveClock());", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            constructorIndex >= 0
            && startClockIndex > constructorIndex
            && clockLaunchIndex > startClockIndex
            && session.Contains("_wallElapsed += delta;", StringComparison.Ordinal)
            && session.Contains("if (_holdRemaining > 0f)", StringComparison.Ordinal)
            && session.Contains("_elapsed += delta;", StringComparison.Ordinal)
            && session.Contains("_wallElapsed < MaximumLifetime", StringComparison.Ordinal),
            "Expected explicit post-construction clock startup, a frozen visual clock, and a lifetime cap that continues on wall time.");

        RegressionTestHarness.Require(
            presenter.Contains("EnterDuration", StringComparison.Ordinal)
            && presenter.Contains("FadeOutStart", StringComparison.Ordinal)
            && presenter.Contains("Lifetime", StringComparison.Ordinal)
            && presenter.Contains("_triggerAge = 0f;", StringComparison.Ordinal)
            && presenter.Contains("_entryVisibility = _visibility;", StringComparison.Ordinal)
            && presenter.Contains("return _triggerAge < Lifetime;", StringComparison.Ordinal),
            "Expected each trigger to renew one shared circle from its current visibility, then sustain and fade on a fresh 1.15-second envelope.");

        RegressionTestHarness.Require(
            session.Contains("_preludeHoldRemaining", StringComparison.Ordinal)
            && session.Contains("_preludeElapsed += delta;", StringComparison.Ordinal)
            && session.Contains("foreach (var material in Materials)", StringComparison.Ordinal)
            && session.Contains(
                "ApplyClockUniforms(_preludeLineMaterial, _preludeElapsed, preludeHeld, _preludeHoldAt);",
                StringComparison.Ordinal)
            && !session.Contains("ApplyClockUniforms(_magicCircleMaterial", StringComparison.Ordinal)
            && presenter.Contains("public override void _Process(double delta)", StringComparison.Ordinal),
            "Expected the prelude hold to affect only its session lines while the room presenter advances the circle independently.");

        RegressionTestHarness.Require(
            session.Contains("if (_disposed)", StringComparison.Ordinal)
            && session.Contains("private void OnCombatEnded(CombatRoom _) => Dispose();", StringComparison.Ordinal)
            && session.Contains("Dispose(queueFree: false);", StringComparison.Ordinal)
            && session.Contains("clock failed and was disposed", StringComparison.Ordinal)
            && session.Contains("Dispose();", StringComparison.Ordinal),
            "Expected normal, combat-end, tree-exit, exception, and lifetime cleanup to converge on one idempotent disposer.");

        RegressionTestHarness.Require(
            session.Contains("ShouldPlayCelPrelude", StringComparison.Ordinal)
            && session.Contains("metadata.Era.HasValue", StringComparison.Ordinal)
            && !session.Contains("metadata.VisualRoute", StringComparison.Ordinal)
            && session.Contains("SakuraChibiStandeeIdleController.TryGet(casterNode)", StringComparison.Ordinal)
            && session.Contains("SakuraStandeeIdleController.TryGet(casterNode)", StringComparison.Ordinal)
            && session.Contains("NCard.FindOnTable(card)", StringComparison.Ordinal)
            && session.Contains("ui.PlayContainer.IsAncestorOf(foundCard)", StringComparison.Ordinal)
            && !session.Contains("NCard.Create(card)", StringComparison.Ordinal)
            && !session.Contains("SuppressNativePlayedCard", StringComparison.Ordinal)
            && !session.Contains("rig.Tip.GlobalPosition", StringComparison.Ordinal)
            && !session.Contains("nativeCard.Visible = false", StringComparison.Ordinal)
            && session.Contains("cardCenter - lines.Size * 0.5f", StringComparison.Ordinal),
            "Expected era-backed cards on either standee art to leave the native play-area card visible on the vanilla route, with no wand-tip clone, suppression, or rig anchoring.");

        var standardPreludeIndex = session.IndexOf(
            "private async Task<bool> PlayStandardPrelude(",
            StringComparison.Ordinal);
        var standardPreludeEndIndex = session.IndexOf(
            "protected async Task<bool> WaitActive(",
            Math.Max(0, standardPreludeIndex),
            StringComparison.Ordinal);
        var standardPrelude = standardPreludeIndex >= 0 && standardPreludeEndIndex > standardPreludeIndex
            ? session[standardPreludeIndex..standardPreludeEndIndex]
            : string.Empty;
        RegressionTestHarness.Require(
            session.Contains("SakuraStandeeIdleController.TryGet(casterNode)", StringComparison.Ordinal)
            && standardPrelude.Contains("TryFindNativePlayedCard(card, out var nativeCard)", StringComparison.Ordinal)
            && standardPrelude.Contains("CreateStandardPrelude(nativeCard);", StringComparison.Ordinal)
            && standardPrelude.Contains("WaitActive(StandardPreludeLeadDuration)", StringComparison.Ordinal)
            && standardPrelude.Contains("BeginWandPreludeHold();", StringComparison.Ordinal)
            && standardPrelude.Contains("TaskHelper.RunSafely(RetireWandPrelude());", StringComparison.Ordinal)
            && !standardPrelude.Contains("NCard.Create", StringComparison.Ordinal)
            && !standardPrelude.Contains("SuppressNativePlayedCard", StringComparison.Ordinal)
            && !standardPrelude.Contains("Visible = false", StringComparison.Ordinal)
            && session.Contains("nativeCard.GetGlobalTransform()", StringComparison.Ordinal)
            && session.Contains("nativeCard.GetCurrentSize() * 0.5f", StringComparison.Ordinal),
            "Expected both standee arts to reuse and track the native play-area card for their concentration-line prelude, without creating, tapping, or hiding a card.");

        var prepareGuardIndex = session.IndexOf(
            "if (TestMode.IsOn",
            session.IndexOf("protected static bool TryPrepare", StringComparison.Ordinal),
            StringComparison.Ordinal);
        var prepareLoadIndex = session.IndexOf("var loaded = loadResources();", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            prepareGuardIndex >= 0
            && prepareLoadIndex > prepareGuardIndex
            && session.Contains("currentRoom.CombatVfxContainer is not { } currentContainer", StringComparison.Ordinal),
            "Expected the shared creation helper to reject TestMode and missing combat containers before invoking any card-specific resource loader.");

        RegressionTestHarness.Require(
            preludeShader.Contains(
                "#include \"res://SakuraMod/shaders/card_vfx/cel_signature.gdshaderinc\"",
                StringComparison.Ordinal)
            && preludeShader.Contains("cel_speed_lines(", StringComparison.Ordinal)
            && preludeShader.Contains("cel_step_clock_held(", StringComparison.Ordinal)
            && preludeShader.Contains("uniform sampler2D magic_circle_ink", StringComparison.Ordinal)
            && preludeShader.Contains("uniform sampler2D magic_circle_knockout", StringComparison.Ordinal)
            && preludeShader.Contains("uniform float speed_lines_enabled = 1.0", StringComparison.Ordinal)
            && preludeShader.Contains("uniform float magic_circle_visibility = 0.0", StringComparison.Ordinal)
            && preludeShader.Contains("uniform vec4 magic_circle_layer_phases = vec4(0.0);", StringComparison.Ordinal)
            && preludeShader.Contains("step(0.5, held)", StringComparison.Ordinal)
            && preludeShader.Contains(
                "float seal_gate = clamp(magic_circle_visibility, 0.0, 1.0)",
                StringComparison.Ordinal)
            && !preludeShader.Contains(
                "float seal_gate = held_gate * step(0.5, magic_circle_enabled)",
                StringComparison.Ordinal)
            && preludeShader.Contains("halo_sample = (", StringComparison.Ordinal)
            && preludeShader.Contains("MAGIC_CIRCLE_CORE_WHITE_MIX", StringComparison.Ordinal)
            && !preludeShader.Contains("hint_screen_texture", StringComparison.Ordinal),
            "Expected the held beat to reveal shared concentration lines plus a bounded local seal halo, without a card-local radial implementation or full-screen post-process.");

        var recordExtraIndex = transaction.IndexOf(
            "() => SakuraActions.RecordExtraEffectTriggeredThisTurn(choiceContext, play)",
            StringComparison.Ordinal);
        var showCircleIndex = transaction.IndexOf(
            "TryShowMagicCircle(card, activation);",
            Math.Max(0, recordExtraIndex),
            StringComparison.Ordinal);
        var playExtraIndex = transaction.IndexOf(
            "extra.PlayWithExtraEffect(choiceContext, play, activation)",
            Math.Max(0, showCircleIndex),
            StringComparison.Ordinal);
        RegressionTestHarness.Require(
            recordExtraIndex >= 0
            && showCircleIndex > recordExtraIndex
            && playExtraIndex > showCircleIndex
            && transaction.Contains("SakuraMagicCirclePresenter.TryShowOrRefresh(card.Owner?.Creature, era);", StringComparison.Ordinal)
            && transaction.Contains("MagicCircleEraFor(card, activation)", StringComparison.Ordinal)
            && presenter.Contains("TryShowOrRefresh", StringComparison.Ordinal)
            && presenter.Contains("if (!_showFailureLogged)", StringComparison.Ordinal)
            && session.Contains("SakuraMagicCirclePresenter.LoadResources();", StringComparison.Ordinal)
            && !session.Contains("SakuraMagicCirclePresenter.ShowOrRefresh(", StringComparison.Ordinal)
            && !session.Contains("Name = \"SakuraCelWandPreludeMagicCircle\"", StringComparison.Ordinal)
            && presenter.Contains("Name = $\"SakuraCelWandPreludeMagicCircleAnchor_", StringComparison.Ordinal)
            && presenter.Contains("Name = \"SakuraCelWandPreludeMagicCircle\"", StringComparison.Ordinal)
            && presenter.Contains("Size = Vector2.One * MagicCircleDiameter", StringComparison.Ordinal)
            && presenter.Contains("ZIndex = MagicCircleZIndex", StringComparison.Ordinal)
            && presenter.Contains("_material.SetShaderParameter(\"speed_lines_enabled\", 0f);", StringComparison.Ordinal)
            && session.Contains("_preludeLineMaterial.SetShaderParameter(\"magic_circle_enabled\", 0f);", StringComparison.Ordinal)
            && presenter.Contains("_anchor.GlobalPosition = ResolveMagicCircleCenter(_casterNode);", StringComparison.Ordinal)
            && session.Contains("lines.GlobalPosition = cardCenter - lines.Size * 0.5f;", StringComparison.Ordinal),
            "Expected the completed card-play transaction to trigger one fail-open room presenter while session lines remain independent and anchored to the native played card.");

        RegressionTestHarness.Require(
            presenter.Contains("MagicCircleEnterScale", StringComparison.Ordinal)
            && presenter.Contains("MagicCirclePulseScale", StringComparison.Ordinal)
            && presenter.Contains("MagicCircleExitScale", StringComparison.Ordinal)
            && presenter.Contains("PivotOffset = Vector2.One * MagicCircleDiameter * 0.5f", StringComparison.Ordinal)
            && presenter.Contains("Position = Vector2.One * MagicCircleDiameter * -0.5f", StringComparison.Ordinal)
            && presenter.Contains("_isRetrigger = _visibility > 0.001f;", StringComparison.Ordinal),
            "Expected the shared circle to enter, pulse on renewal, and retire around one stable centre pivot.");

        RegressionTestHarness.Require(
            presenter.Contains("new(ReferenceEqualityComparer.Instance);", StringComparison.Ordinal)
            && presenter.Contains("if (_states.TryGetValue(caster, out var existing))", StringComparison.Ordinal)
            && presenter.Contains("existing.Refresh(ColourFor(era));", StringComparison.Ordinal)
            && presenter.Contains("_spinAge = 0f;", StringComparison.Ordinal)
            && presenter.Contains("_phases += SettleLayerSpeeds * delta", StringComparison.Ordinal)
            && !presenter.Contains("_phases = Vector4.Zero;", StringComparison.Ordinal)
            && presenter.Contains("ColourTransitionDuration", StringComparison.Ordinal)
            && presenter.Contains("_colourStart = _colour;", StringComparison.Ordinal)
            && presenter.Contains("_colour = _colourStart.Lerp(_colourTarget, progress);", StringComparison.Ordinal),
            "Expected one renewable state per caster to preserve accumulated phase, reapply its spin impulse, and blend to the latest era colour.");

        RegressionTestHarness.Require(
            presenter.Contains("SpinDecayDuration", StringComparison.Ordinal)
            && presenter.Contains("var decayIntegral = SpinDecayDuration", StringComparison.Ordinal)
            && presenter.Contains("Mathf.Exp(-nextSpinAge / SpinDecayDuration)", StringComparison.Ordinal)
            && presenter.Contains("InitialLayerSpeeds", StringComparison.Ordinal)
            && presenter.Contains("SettleLayerSpeeds", StringComparison.Ordinal)
            && presenter.Contains("_spinAge = nextSpinAge;", StringComparison.Ordinal)
            && !presenter.Contains("StepDuration", StringComparison.Ordinal)
            && !presenter.Contains("_stepRemainder", StringComparison.Ordinal)
            && presenter.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded;", StringComparison.Ordinal)
            && presenter.Contains("public override void _ExitTree()", StringComparison.Ordinal)
            && presenter.Contains("state.Dispose(queueFreeChildren);", StringComparison.Ordinal)
            && presenter.Split("_anchor.QueueFreeSafely();", StringSplitOptions.None).Length - 1 >= 2,
            "Expected frame-continuous exponential deceleration and room/combat cleanup to belong to the shared presenter rather than a card session.");
    }

    [Fact]
    public void BigAndLittleReuseTheEraColouredPreludeAndComposeStandeeSize()
    {
        var consumer = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/BigLittleStandeeVfx.cs"));
        var big = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Big.cs"));
        var little = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Little.cs"));
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));
        var factory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var sharedPrelude = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/CelVfxSession.cs"));
        var circlePresenter = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/SakuraMagicCirclePresenter.cs"));
        var transaction = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraExtraEffectTransaction.cs"));

        RegressionTestHarness.Require(
            consumer.Contains("sealed class BigLittleStandeeVfx : CelVfxSession", StringComparison.Ordinal)
            && consumer.Contains("session.PlayCelPrelude(card, caster)", StringComparison.Ordinal)
            && consumer.Contains("session.StartClock();", StringComparison.Ordinal)
            && consumer.Contains("finally", StringComparison.Ordinal)
            && consumer.Contains("await ResolveOnce();", StringComparison.Ordinal)
            && consumer.Contains("session.Dispose();", StringComparison.Ordinal)
            && !consumer.Contains("ResourceLoader.Load", StringComparison.Ordinal)
            && !consumer.Contains("PackedScene", StringComparison.Ordinal),
            "Expected one resource-free Big/Little consumer to reuse the shared prelude and resolve gameplay during cleanup.");

        RegressionTestHarness.Require(
            big.Split("BigLittleStandeeVfx.PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 2
            && little.Split("BigLittleStandeeVfx.PlayOrResolveAsync(", StringSplitOptions.None).Length - 1 == 2
            && big.Split("SakuraStandeeSizeEffect.Big", StringSplitOptions.None).Length - 1 == 2
            && little.Split("SakuraStandeeSizeEffect.Little", StringSplitOptions.None).Length - 1 == 2,
            "Expected both Clow and Sakura versions of Big and Little to use the shared size presentation.");

        RegressionTestHarness.Require(
            controller.Contains("private bool _bigActive;", StringComparison.Ordinal)
            && controller.Contains("private bool _littleActive;", StringComparison.Ordinal)
            && controller.Contains("SakuraStandeeSizeRules.Multiplier(_bigActive, _littleActive)", StringComparison.Ordinal)
            && controller.Contains("_body.Position = RestPosition;", StringComparison.Ordinal)
            && controller.Contains("_body.Scale = RestScale;", StringComparison.Ordinal)
            && factory.Contains("layout.Bounds.Position.Y + layout.Bounds.Size.Y", StringComparison.Ordinal),
            "Expected persistent size flags to compose with every rest transform around the standee layout floor.");

        RegressionTestHarness.Require(
            !sharedPrelude.Contains("SakuraMagicCirclePresenter.ShowOrRefresh(", StringComparison.Ordinal)
            && transaction.Contains("era == SourceEraClass.Sakura", StringComparison.Ordinal)
            && transaction.Contains("card.Type == CardType.Power", StringComparison.Ordinal)
            && circlePresenter.Contains("SourceEraClass.Clow =>", StringComparison.Ordinal)
            && circlePresenter.Contains("SourceEraClass.Sakura =>", StringComparison.Ordinal)
            && !sharedPrelude.Contains("rig.Tip.GlobalPosition", StringComparison.Ordinal)
            && !sharedPrelude.Contains("NCard.Create", StringComparison.Ordinal),
            "Expected the transaction to route Clow Power and Sakura plays to one era-coloured circle while the session prelude tracks the native played card on the vanilla route.");
    }

    [Fact]
    public void SpellTurnTransformationResourcesRemainComplete()
    {
        const string luminRelativePath = "SakuraMod/images/vfx/spell_turn_lumin.png";
        var luminPath = RegressionTestHarness.FindRepoFile(luminRelativePath);
        var header = File.ReadAllBytes(luminPath).AsSpan(0, 26);
        var luminImport = File.ReadAllText($"{luminPath}.import");
        RegressionTestHarness.Require(
            header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 10
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 85
            && header[24] == 8
            && header[25] == 6,
            "Expected the Spell Turn lumin asset to remain an 8-bit 10x85 RGBA PNG.");
        RegressionTestHarness.Require(
            luminImport.Contains($"source_file=\"res://{luminRelativePath}\"", StringComparison.Ordinal)
            && luminImport.Contains("mipmaps/generate=false", StringComparison.Ordinal),
            "Expected the Spell Turn lumin import to remain tracked and non-mipmapped.");

        var audioFiles = new[]
        {
            "SOTE_SFX_PlayerTurn_v4_1.ogg",
            "SOTE_SFX_Buff_1_v1.ogg",
            "SOTE_SFX_Buff_2_v1.ogg",
            "SOTE_SFX_Buff_3_v1.ogg",
            "STS_SFX_Guardian3Destroy_v2.ogg"
        };
        foreach (var fileName in audioFiles)
        {
            var relativePath = $"SakuraMod/sfx/spell_turn/{fileName}";
            var audioPath = RegressionTestHarness.FindRepoFile(relativePath);
            var bytes = File.ReadAllBytes(audioPath);
            var import = File.ReadAllText($"{audioPath}.import");
            RegressionTestHarness.Require(
                bytes.Length > 4 && bytes.AsSpan(0, 4).SequenceEqual("OggS"u8),
                $"Expected {relativePath} to remain a non-empty OGG stream.");
            RegressionTestHarness.Require(
                import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
                && import.Contains("loop=false", StringComparison.Ordinal),
                $"Expected {relativePath}.import to remain tracked and non-looping.");
        }

        const string sceneRelativePath = "SakuraMod/scenes/combat/spell_turn_transformation_vfx.tscn";
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(sceneRelativePath));
        foreach (var required in new[]
                 {
                     "spell_turn_lumin.png",
                     "silhouette.png",
                     "name=\"OldClip\"",
                     "name=\"NewCard\"",
                     "name=\"GatherParticles\"",
                     "name=\"DiffusionParticles\"",
                     "name=\"PrimaryAudio\"",
                     "name=\"AccentAudio\"",
                     "stretch_mode = 3"
                 })
        {
            RegressionTestHarness.Require(
                scene.Contains(required, StringComparison.Ordinal),
                $"Expected Spell Turn VFX scene to retain {required}.");
        }
    }

    [Fact]
    public void SakuraVoiceResourcesRemainComplete()
    {
        foreach (var relativePath in new[]
                 {
                     "SakuraMod/voices/dream_wand.ogg",
                     "SakuraMod/voices/stabilize.ogg"
                 })
        {
            var audioPath = RegressionTestHarness.FindRepoFile(relativePath);
            var bytes = File.ReadAllBytes(audioPath);
            var import = File.ReadAllText($"{audioPath}.import");
            RegressionTestHarness.Require(
                bytes.Length > 4 && bytes.AsSpan(0, 4).SequenceEqual("OggS"u8),
                $"Expected {relativePath} to remain a non-empty OGG stream.");
            RegressionTestHarness.Require(
                import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
                && import.Contains("loop=false", StringComparison.Ordinal),
                $"Expected {relativePath}.import to remain tracked and non-looping.");
        }
    }

    [Fact]
    public void AnotherMeBgmResourceRemainsComplete()
    {
        const string relativePath = "SakuraMod/music/another_me.ogg";
        var audioPath = RegressionTestHarness.FindRepoFile(relativePath);
        var bytes = File.ReadAllBytes(audioPath);
        var import = File.ReadAllText($"{audioPath}.import");

        RegressionTestHarness.Require(
            bytes.Length > 4 && bytes.AsSpan(0, 4).SequenceEqual("OggS"u8),
            "Expected Another Me BGM to remain a non-empty OGG stream.");
        RegressionTestHarness.Require(
            import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
            && import.Contains("loop=false", StringComparison.Ordinal),
            "Expected the Another Me BGM import to remain tracked and non-looping as a Godot resource.");

        RegressionTestHarness.Require(
            File.Exists(RegressionTestHarness.FindRepoFile("artifacts/.gdignore")),
            "Expected generated test package mirrors to stay outside Godot resource import scanning.");
    }

    [Fact]
    public void OptionCardClearArtAndImportsRemainComplete()
    {
        foreach (var cardType in SakuraOptionCardCatalog.CardTypes)
        {
            var fileName = Path.GetFileName(ClearCardVisualAssets.ArtPath(cardType));
            var relativePath = Path.Join("SakuraMod/images/cards/clear_cards", fileName);
            var imagePath = RegressionTestHarness.FindRepoFile(relativePath);
            var header = File.ReadAllBytes(imagePath).AsSpan(0, 26);
            var import = File.ReadAllText($"{imagePath}.import");

            RegressionTestHarness.Require(
                header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 787
                && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 1717
                && header[24] == 8
                && header[25] == 6,
                $"Expected {relativePath} to remain an 8-bit 787x1717 RGBA PNG.");
            RegressionTestHarness.Require(
                import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
                && import.Contains("mipmaps/generate=false", StringComparison.Ordinal),
                $"Expected {relativePath}.import to remain tracked and non-mipmapped.");
        }
    }

    [Theory]
    [InlineData("eng")]
    [InlineData("zhs")]
    public void AncientDialogueLinesProvideNextButtonKeys(string locale)
    {
        var relativePath = $"SakuraMod/localization/{locale}/ancients.json";
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        // The game's AncientDialogueSet.PopulateLocKeys assigns every non-final dialogue line
        // a "{line}.next" continue-button key; a missing entry renders the raw key on the button.
        var linePattern = new System.Text.RegularExpressions.Regex(
            @"^(?<dialogue>.+\.talk\.(?:firstVisitEver|ANY|[A-Z0-9_]+)\.\d+-)(?<line>\d+)(?<repeat>r?)\.(?:ancient|char)$");
        var linesByDialogue = new Dictionary<string, List<(int Index, bool Repeat)>>();
        foreach (var key in entries.Keys)
        {
            var match = linePattern.Match(key);
            if (!match.Success)
                continue;
            var dialogue = match.Groups["dialogue"].Value;
            var line = (int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
                match.Groups["repeat"].Value.Length > 0);
            if (!linesByDialogue.TryGetValue(dialogue, out var lines))
                linesByDialogue[dialogue] = lines = [];
            lines.Add(line);
        }

        foreach (var (dialogue, lines) in linesByDialogue)
        {
            var finalLine = lines.Max(line => line.Index);
            foreach (var (index, repeat) in lines)
            {
                if (index == finalLine)
                    continue;
                var nextKey = $"{dialogue}{index}{(repeat ? "r" : "")}.next";
                RegressionTestHarness.Require(
                    entries.ContainsKey(nextKey),
                    $"Missing {nextKey} in {relativePath}: non-final ancient dialogue lines need a continue-button key.");
            }
        }
    }

    [Fact]
    public void RemovedHostAndRelicResourcesStayAbsent()
    {
        RegressionTestHarness.RequireNoRemovedCardTypes(
            "Sakura assembly types",
            typeof(MainFile).Assembly.GetTypes(),
            RegressionTestData.RemovedLegacyRelicTypeNames);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/eng/relics.json",
            RegressionTestData.RemovedLegacyRelicLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/zhs/relics.json",
            RegressionTestData.RemovedLegacyRelicLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/eng/characters.json",
            RegressionTestData.RemovedLegacyHostLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/zhs/characters.json",
            RegressionTestData.RemovedLegacyHostLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/eng/ancients.json",
            RegressionTestData.RemovedLegacyHostLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/zhs/ancients.json",
            RegressionTestData.RemovedLegacyHostLocalizationPrefixes);

    }

    [Fact]
    public void ClearCardDescriptionsAvoidRedundantText() => RequireClearCardDescriptionsAvoidRedundantText();

    [Fact]
    public void ForgottenTerminologyKeepsGeneralAndSpecialRulesSeparated()
    {
        var memoryIcon = RegressionTestHarness.FindRepoFile("SakuraMod/images/powers/record.png");
        RegressionTestHarness.Require(
            File.Exists(memoryIcon) && File.Exists($"{memoryIcon}.import"),
            "Expected the Memory pile to reuse the tracked Record icon resource.");

        foreach (var locale in new[] { "eng", "zhs" })
        {
            var tipsPath = $"SakuraMod/localization/{locale}/static_hover_tips.json";
            var tips = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(tipsPath)))
                ?? throw new InvalidOperationException($"Could not parse {tipsPath}.");
            var forgottenTitle = locale == "zhs" ? "遗忘" : "Forgotten";
            var oldStateTitle = locale == "zhs" ? "临时" : "Temporary";
            var termComparison = locale == "zhs"
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            var forgotten = tips["SAKURAMOD-TEMPORARY.description"];
            var memoryDescription = tips["SAKURA_MOD_CARDPILE_MEMORY.description"];
            var remind = tips["SAKURAMOD-REMIND.description"];

            RegressionTestHarness.Require(
                tips["SAKURAMOD-TEMPORARY.title"] == forgottenTitle,
                $"Expected {locale} to display the Temporary state as {forgottenTitle}.");
            RegressionTestHarness.Require(
                tips.ContainsKey("SAKURA_MOD_CARDPILE_MEMORY.title")
                && tips.ContainsKey("SAKURA_MOD_CARDPILE_MEMORY.description")
                && tips.ContainsKey("SAKURA_MOD_CARDPILE_MEMORY.empty"),
                $"Expected {locale} Memory pile title, description, and empty text.");
            RegressionTestHarness.Require(
                forgotten.Contains(forgottenTitle, StringComparison.Ordinal)
                && (forgotten.Contains("Memory", StringComparison.Ordinal)
                    || forgotten.Contains("记忆区", StringComparison.Ordinal))
                && (forgotten.Contains("Exhaust", StringComparison.Ordinal)
                    || forgotten.Contains("消耗牌堆", StringComparison.Ordinal))
                && (memoryDescription.Contains("Exhaust", StringComparison.Ordinal)
                    || memoryDescription.Contains("消耗牌堆", StringComparison.Ordinal))
                && !forgotten.Contains("Remind", StringComparison.Ordinal)
                && !forgotten.Contains("想起", StringComparison.Ordinal),
                $"Expected {locale} Forgotten hover text to explain only its general Memory rule.");
            RegressionTestHarness.Require(
                locale == "zhs"
                    ? remind.Contains("不会进入", StringComparison.Ordinal)
                    : remind.Contains("do not enter", StringComparison.Ordinal),
                $"Expected {locale} Remind hover text to own its no-return exception.");

            var stateReferences = new Dictionary<string, string[]>
            {
                ["cards.json"] =
                [
                    "SAKURAMOD-GENERIC.temporaryCardPrompt",
                    "SAKURA_MOD_CARD_DREAMING.description",
                    "SAKURA_MOD_CARD_EXCHANGE.description",
                    "SAKURA_MOD_CARD_SPIRAL.description",
                    "SAKURA_MOD_CARD_BLANK.description",
                    "SAKURA_MOD_CARD_TRUE_OR_FALSE.description",
                    "SAKURA_MOD_CARD_TRUE_OR_FALSE_DRAW_CHOICE.description"
                ],
                ["powers.json"] =
                [
                    "SAKURA_MOD_POWER_DREAMING_POWER.description",
                    "SAKURA_MOD_POWER_DREAMING_POWER.smartDescription"
                ],
                ["card_keywords.json"] =
                [
                    "SAKURAMOD-STABILIZE.description",
                    "SAKURAMOD-MANIFEST.description"
                ],
                ["static_hover_tips.json"] =
                [
                    "SAKURAMOD-TEMPORARY.title",
                    "SAKURAMOD-TEMPORARY.description",
                    "SAKURA_MOD_CARDPILE_MEMORY.description",
                    "SAKURAMOD-REMIND.description"
                ]
            };
            if (locale == "eng")
            {
                stateReferences["characters.json"] =
                ["SAKURA_MOD_CHARACTER_CLASSIC_SAKURA.description"];
            }

            foreach (var (fileName, keys) in stateReferences)
            {
                var relativePath = $"SakuraMod/localization/{locale}/{fileName}";
                var localization = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                    ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
                foreach (var key in keys)
                {
                    RegressionTestHarness.Require(
                        localization[key].Contains(forgottenTitle, StringComparison.Ordinal)
                        && !localization[key].Contains(oldStateTitle, termComparison),
                        $"Expected {locale} {key} to use {forgottenTitle} instead of the old state name.");
                }
            }

            var powersPath = $"SakuraMod/localization/{locale}/powers.json";
            var powers = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(powersPath)))
                ?? throw new InvalidOperationException($"Could not parse {powersPath}.");
            RegressionTestHarness.Require(
                powers["SAKURA_MOD_POWER_SAKURA_TEMPORARY_DEXTERITY_POWER.title"]
                    .Contains(oldStateTitle, termComparison)
                && powers["SAKURA_MOD_POWER_CLASSIC_TEMPORARY_STRENGTH_POWER.title"]
                    .Contains(oldStateTitle, termComparison)
                && powers["SAKURA_MOD_POWER_CLASSIC_TEMPORARY_STRENGTH_LOSS_POWER.title"]
                    .Contains(oldStateTitle, termComparison)
                && powers["SAKURA_MOD_POWER_CLASSIC_DREAM_POWER.description"]
                    .Contains(oldStateTitle, termComparison),
                $"Expected {locale} generic duration wording to remain unchanged.");
        }

        var cardStateSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/CardStateModifiers.cs"));
        RegressionTestHarness.Require(
            cardStateSource.Contains("[red]遗忘[/red]", StringComparison.Ordinal)
            && cardStateSource.Contains("[red]Forgotten[/red]", StringComparison.Ordinal)
            && !cardStateSource.Contains("[red]临时[/red]", StringComparison.Ordinal)
            && !cardStateSource.Contains("[red]Temporary[/red]", StringComparison.Ordinal),
            "Expected dynamically rendered Temporary state labels to use Forgotten terminology in both locales.");
    }

    [Fact]
    public void CharacterPresentationAssetsAndSizingAdapterRemainComplete()
    {
        RegressionTestHarness.RequireClassicEnergyCounterScene(
            "SakuraMod/scenes/combat/energy_counters/sakura_energy_counter.tscn");
        var classicSelectBackground = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/charui/sakura_char_select_background.png");
        var classicSelectScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/screens/char_select/sakura_character_select_background.tscn"));
        RegressionTestHarness.Require(
            File.Exists($"{classicSelectBackground}.import")
            && classicSelectScene.Contains(
                "res://SakuraMod/images/charui/sakura_char_select_background.png",
                StringComparison.Ordinal),
            "Expected the Classic Sakura character-select scene and tracked background asset.");
        var selectBackgroundPatch = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCharacterSelectBackgroundPatch.cs"));
        RegressionTestHarness.Require(
            selectBackgroundPatch.Contains("IsKinomotoSakuraCharacter", StringComparison.Ordinal)
            && selectBackgroundPatch.Contains("ToLocalRect(bgContainer, viewportRect)", StringComparison.Ordinal)
            && selectBackgroundPatch.Contains("ApplyFullRect(background)", StringComparison.Ordinal),
            "Expected the Sakura-scoped background adapter to size the root and texture layers without replacing scene layout.");
        RegressionTestHarness.RequireClassicFullFaceAssetsExist(SakuraSourceCardRules.AllCardTypes());

    }

    [Fact]
    public void ClassicBigFullFacesRemainSourceOnly()
    {
        const string bigRelativePath = "SakuraMod/images/cards/classic/full_faces/big";
        const string bigExportPattern = $"{bigRelativePath}/**";

        var bigRoot = RegressionTestHarness.FindRepoDirectory(bigRelativePath);
        var sourceImages = Directory.GetFiles(bigRoot, "*.png", SearchOption.AllDirectories);
        RegressionTestHarness.Require(
            sourceImages.Length == 116 && sourceImages.All(path => File.Exists($"{path}.import")),
            "Expected all 116 Classic big full-face source PNGs and imports to remain in the repository.");

        var exportPreset = File.ReadAllText(RegressionTestHarness.FindRepoFile("export_presets.cfg"));
        RegressionTestHarness.Require(
            exportPreset.Contains("include_filter=\"SakuraMod/**\"", StringComparison.Ordinal)
            && exportPreset.Contains(bigExportPattern, StringComparison.Ordinal),
            "Expected the Godot export to include SakuraMod resources while excluding Classic big full faces.");

        var assetPathSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraSourceCard.cs"));
        RegressionTestHarness.Require(
            !assetPathSource.Contains("BigClassicClowArtPath", StringComparison.Ordinal)
            && !assetPathSource.Contains("BigClassicSakuraArtPath", StringComparison.Ordinal)
            && !assetPathSource.Contains("BigClassicSpellArtPath", StringComparison.Ordinal),
            "Expected runtime code not to expose paths for package-excluded Classic big full faces.");
    }

    [Fact]
    public void SakuraHighlightsArePrebuiltRgbaHalfResources()
    {
        var highlightPaths = new[]
        {
            "SakuraMod/images/cards/highlights/clear_card_frame_sdf.exr",
            "SakuraMod/images/cards/highlights/classic_card_frame_sdf.exr"
        };
        foreach (var relativePath in highlightPaths)
        {
            var path = RegressionTestHarness.FindRepoFile(relativePath);
            var import = File.ReadAllText($"{path}.import");
            RegressionTestHarness.Require(
                import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
                && import.Contains("mipmaps/generate=false", StringComparison.Ordinal),
                $"Expected {relativePath} to have a tracked, non-mipmapped Godot import.");
        }

        var generator = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "scripts/generate_sakura_highlights.gd"));
        RegressionTestHarness.Require(
            generator.Contains("Vector2i(567, 1097)", StringComparison.Ordinal)
            && generator.Contains("Vector2i(578, 1134)", StringComparison.Ordinal)
            && generator.Contains("Image.FORMAT_RGBAH", StringComparison.Ordinal)
            && generator.Contains("--verify", StringComparison.Ordinal),
            "Expected a deterministic RGBAH generator and validator for both Sakura highlight layouts.");

        var owner = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardHighlightResources.cs"));
        RegressionTestHarness.Require(
            highlightPaths.All(path => owner.Contains($"res://{path}", StringComparison.Ordinal))
            && owner.Contains("SakuraCardTextureResource.FromPath", StringComparison.Ordinal)
            && owner.Contains("IsSakuraHighlight", StringComparison.Ordinal),
            "Expected one shared path-backed owner for both Sakura highlight resources.");

        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/Cards/ClearCardVisualPatch.cs",
                     "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                source.Contains("SakuraCardHighlightResources", StringComparison.Ordinal)
                && !source.Contains("Image.CreateEmpty", StringComparison.Ordinal)
                && !source.Contains("Image.Format.Rgbaf", StringComparison.Ordinal)
                && !source.Contains("SetPixel", StringComparison.Ordinal)
                && !source.Contains("FromFactory", StringComparison.Ordinal)
                && !source.Contains("string.IsNullOrEmpty(texture!.ResourcePath)", StringComparison.Ordinal),
                $"Expected {relativePath} to load shared highlights without runtime generation or empty-path ownership.");
        }
    }

    [Fact]
    public void LabyrinthAssetsLocalizationAndReleaseContractRemainComplete()
    {
        var intentIcon = RegressionTestHarness.FindRepoFile("SakuraMod/images/intents/labyrinth.png");
        RegressionTestHarness.Require(File.Exists($"{intentIcon}.import"), "Expected the Labyrinth intent icon import to exist.");
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var localization = File.ReadAllText(RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/intents.json"));
            RegressionTestHarness.Require(
                localization.Contains("SAKURA_MOD_LABYRINTH.title", StringComparison.Ordinal)
                && localization.Contains("SAKURA_MOD_LABYRINTH.description", StringComparison.Ordinal)
                && localization.Contains("SAKURA_MOD_LABYRINTH_RELEASE_WARNING.title", StringComparison.Ordinal)
                && localization.Contains("SAKURA_MOD_LABYRINTH_RELEASE_WARNING.description", StringComparison.Ordinal),
                $"Expected {locale} Labyrinth intent localization.");

            var cards = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/cards.json")))
                ?? throw new InvalidOperationException($"Could not parse {locale} card localization.");
            var staticTips = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/static_hover_tips.json")))
                ?? throw new InvalidOperationException($"Could not parse {locale} static hover-tip localization.");
            RegressionTestHarness.Require(
                cards["SAKURA_MOD_CARD_LABYRINTH.description"].Count(character => character == '\n') == 0
                && staticTips.ContainsKey("SAKURAMOD-ENTER_LABYRINTH.title")
                && staticTips.ContainsKey("SAKURAMOD-ENTER_LABYRINTH.description"),
                $"Expected {locale} Labyrinth card text to keep its rules in the Enter the Labyrinth hover tip.");
        }

        var labyrinthCardSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Labyrinth.cs"));
        var cardVfxSource = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraCardPlayVfx.cs"));
        var labyrinthMoveSource = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/SakuraLabyrinthMove.cs"));
        var powersSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/LabyrinthPower.cs"));
        RegressionTestHarness.Require(
            !labyrinthCardSource.Contains("PlayLabyrinth", StringComparison.Ordinal)
            && !cardVfxSource.Contains("Labyrinth", StringComparison.Ordinal),
            "Expected the legacy Labyrinth line VFX to be removed.");
        RegressionTestHarness.Require(
            labyrinthMoveSource.Contains("Concat(_coveredMove.Intents)", StringComparison.Ordinal)
            && powersSource.Contains("revealCoveredIntent: enemy == _pendingReleaseEnemy", StringComparison.Ordinal)
            && powersSource.Contains("IsTrapped(_pendingReleaseEnemy) ? _pendingReleaseEnemy", StringComparison.Ordinal),
            "Expected the release warning to reveal the covered move and release the enemy selected at player-turn start.");
    }

    [Fact]
    public void TheGlowUsesGlowPowerAndMagicChargeOnly()
    {
        var visual = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraGlowVisual.cs"));
        var charge = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicCharge.cs"));
        var character = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/ClassicSakura.cs"));
        var shaderPath = RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_glow_mote.gdshader");
        var shader = File.ReadAllText(shaderPath);

        RegressionTestHarness.Require(
            visual.Contains("ClassicGlowPower", StringComparison.Ordinal)
            && visual.Contains("NotifyMagicChargeGained", StringComparison.Ordinal)
            && visual.Contains("CombatVfxContainer", StringComparison.Ordinal)
            && visual.Contains("CelVfxGeometry.ResolveCaster", StringComparison.Ordinal)
            && visual.Contains("PreloadManager.Cache.GetAsset<Shader>", StringComparison.Ordinal)
            && !visual.Contains("ClassicLightPower", StringComparison.Ordinal)
            && !visual.Contains("ClassicLightSakuraPower", StringComparison.Ordinal)
            && !visual.Contains("StatusOrCurse", StringComparison.Ordinal)
            && charge.Contains("SakuraGlowVisual.NotifyMagicChargeGained", StringComparison.Ordinal)
            && character.Contains(".. SakuraGlowVisual.AssetPaths", StringComparison.Ordinal),
            "Expected The Glow to follow ClassicGlowPower and the central Magic Charge owner, not the separate Light Power or Status/Curse paths.");
        RegressionTestHarness.Require(
            File.Exists(shaderPath)
            && shader.Contains("shader_type canvas_item", StringComparison.Ordinal)
            && shader.Contains("uniform float elapsed", StringComparison.Ordinal)
            && shader.Contains("uniform float phase", StringComparison.Ordinal)
            && shader.Contains("core_color", StringComparison.Ordinal)
            && shader.Contains("halo_color", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal),
            "Expected The Glow to be a project-owned 2D shader with explicit clocks and no screen-reading or Shader TIME.");
    }

    [Fact]
    public void TransferVfxCoversEveryThroughTargetWithoutOwningGameplay()
    {
        var session = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Transparent/TransferVfx.cs"));
        var card = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Transfer.cs"));

        var cueIndex = card.IndexOf("cues.Exchange(target)", StringComparison.Ordinal);
        var powerIndex = card.IndexOf("PowerCmd.Apply<StrengthPower>", cueIndex, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            session.Contains(": CelVfxSession", StringComparison.Ordinal)
            && session.Contains("PlayCelPrelude", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.ResolveCaster", StringComparison.Ordinal)
            && session.Contains("CelVfxGeometry.Resolve(room, target, index, Budget)", StringComparison.Ordinal)
            && session.Contains("PairVisual", StringComparison.Ordinal)
            && session.Contains("Connection", StringComparison.Ordinal)
            && session.Contains("AfterCaster", StringComparison.Ordinal)
            && !session.Contains("ResourceLoader.Load", StringComparison.Ordinal)
            && !session.Contains("TIME", StringComparison.Ordinal)
            && card.Contains("var targets = SakuraThroughResolution.TargetsFor(play)", StringComparison.Ordinal)
            && card.Contains("TransferVfx.PlayOrResolveAsync", StringComparison.Ordinal)
            && cueIndex >= 0
            && powerIndex > cueIndex,
            "Expected Transfer to render one paired exchange cue for every Through target before the existing PowerCmd loop, without moving gameplay ownership into VFX.");
    }

    private static void RequireClearCardDescriptionsAvoidRedundantText()
    {
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var relativePath = $"SakuraMod/localization/{locale}/cards.json";
            var cards = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

            var remind = cards["SAKURA_MOD_CARD_REMIND.description"];
            RegressionTestHarness.Require(
                cards.Values.All(description =>
                    !description.Contains("Upgrade:", StringComparison.Ordinal)
                    && !description.Contains("升级：", StringComparison.Ordinal)),
                $"Expected {locale} card descriptions not to include explicit upgrade notes.");
            RegressionTestHarness.Require(
                !remind.Contains("[gold]Exhaust[/gold]", StringComparison.Ordinal)
                && !remind.Contains("[gold]消耗[/gold]", StringComparison.Ordinal),
                $"Expected {locale} Remind description not to duplicate its native Exhaust keyword.");
        }

        var chineseCardsPath = RegressionTestHarness.FindRepoFile("SakuraMod/localization/zhs/cards.json");
        var chineseCards = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(chineseCardsPath))
            ?? throw new InvalidOperationException("Could not parse zhs cards localization.");
        RegressionTestHarness.Require(
            !chineseCards["SAKURA_MOD_CARD_TRUE_OR_FALSE.description"]
                .Contains("[gold]消耗[/gold]", StringComparison.Ordinal),
            "Expected Chinese True or False description not to duplicate its native Exhaust keyword.");
    }

    private static void RequirePngWithImport(string relativePath, int width, int height)
    {
        var path = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(path).AsSpan(0, 26);
        var import = File.ReadAllText($"{path}.import");
        RegressionTestHarness.Require(
            header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == width
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == height
            && header[24] == 8,
            $"Expected {relativePath} to remain an 8-bit {width}x{height} PNG.");
        RegressionTestHarness.Require(
            import.Contains($"source_file=\"res://{relativePath}\"", StringComparison.Ordinal)
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal),
            $"Expected {relativePath}.import to remain tracked and non-mipmapped.");
    }
}
