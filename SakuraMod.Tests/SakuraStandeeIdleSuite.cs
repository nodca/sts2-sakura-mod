public sealed class SakuraStandeeIdleSuite
{
    [Fact]
    public void IdleLayersAreBuiltBeforeTheControllerEntersTheTree()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.DoesNotContain("public override void _Ready()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("parent.AddChild(_halfClosed);", source, StringComparison.Ordinal);
        Assert.Contains("body.AddChild(controller);", source, StringComparison.Ordinal);
        Assert.Contains("AddChild(layers);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ChibiWandTipIsAValidatedRigAnchorRatherThanAVfxChild()
    {
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraChibiStandeeIdleController.cs"));
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_chibi_combat_idle_rigged.tscn"));

        Assert.Contains(
            "[node name=\"WandTip\" type=\"Marker2D\" parent=\"CharacterRoot/ChestAttachmentRoot/HeldWandRoot/WandRoot\"]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains("TryGetWandPreludeRig", controller, StringComparison.Ordinal);
        Assert.Contains("missing its WandTip marker", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("NCard", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewAndCombatShareOneLayeredIdleScene()
    {
        const string layerScenePath = "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn";
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(layerScenePath));
        var previewScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_preview.tscn"));
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.Contains($"path=\"res://{layerScenePath}\"", previewScene, StringComparison.Ordinal);
        Assert.Contains("sakura_standee_idle_rigged.tscn", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewBackground", layerScene, StringComparison.Ordinal);
        Assert.Contains("type=\"Skeleton2D\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("type=\"Polygon2D\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"CanvasOrigin\" type=\"Marker2D\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"SkirtWaistRoot\" type=\"Node2D\"", layerScene, StringComparison.Ordinal);
        Assert.Contains(
            "[node name=\"SkirtAnchor\" type=\"Sprite2D\" parent=\"CharacterRoot/SkirtMotionRoot/SkirtWaistRoot\"",
            layerScene,
            StringComparison.Ordinal);
        Assert.Contains("BackTrainTipBone:rotation", layerScene, StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"idle_preview\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"breath_preview\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"blink_preview\"", layerScene, StringComparison.Ordinal);
        Assert.Contains("position = Vector2(960, 456)", previewScene, StringComparison.Ordinal);
    }

    [Fact]
    public void RiggedIdleRootKeepsCombatTransformNeutral()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));
        var rootStart = scene.IndexOf(
            "[node name=\"SakuraStandeeIdleLayers\" type=\"Node2D\"",
            StringComparison.Ordinal);
        var nextNode = scene.IndexOf(
            "[node name=\"CanvasOrigin\" type=\"Marker2D\"",
            rootStart,
            StringComparison.Ordinal);

        Assert.True(rootStart >= 0);
        Assert.True(nextNode > rootStart);
        var rootBlock = scene[rootStart..nextNode];
        Assert.DoesNotContain("position =", rootBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("scale =", rootBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void IdleMotionUsesSixSecondSkirtAndFourSecondBlinkLoops()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));

        Assert.Contains(
            "resource_name = \"idle_preview\"\nlength = 6.0",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "resource_name = \"blink_preview\"\nlength = 4.0",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 1.5, 3, 4.5, 6)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 1.76, 1.84, 1.96, 2.08, 4)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 1.84, 1.96, 4)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [-0.00392699, 0.00654498, -0.00523599, 0.00392699, -0.00392699]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [-0.0218166, 0.0436332, -0.0370882, 0.0327249, -0.0261799, -0.0218166]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [0.0109083, -0.0261799, 0.0218166, -0.019635, 0.0152716, 0.0109083]",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontSkirtFeathersUseTheExpandedSwayEnvelope()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));

        Assert.Contains(
            "\"values\": [-0.005673, 0.036869, 0.009927, -0.032616]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [0.0, 0.025526, 0.00709, -0.021272]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [0.0, 0.025526, 0.00709, -0.021272]",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontSkirtFeathersUseIndependentPeriodsAndVerticalLag()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));

        Assert.Contains(
            "NodePath(\"CharacterRoot/SkirtMotionRoot/SkirtLeftFrontRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0.18, 2.38, 4.58)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "NodePath(\"CharacterRoot/SkirtMotionRoot/SkirtCenterFrontRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0.36, 1.86, 3.36, 4.86)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "NodePath(\"CharacterRoot/SkirtMotionRoot/SkirtRightFrontRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0.58, 1.78, 2.98, 4.18, 5.38)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains("Vector2(-173.741, 57.051)", scene, StringComparison.Ordinal);
        Assert.Contains(
            "NodePath(\"CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/DaimaoRoot:rotation\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [0.0, 0.0, 0.06, -0.035, 0.0, 0.0]",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontBreathLayerFollowsTheBackLayerWithAQuietDelay()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));

        Assert.Contains(
            "NodePath(\"CharacterRoot/UpperBodyMotionRoot/BreathRoot/TorsoBackRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "NodePath(\"CharacterRoot/UpperBodyMotionRoot/BreathRoot/TorsoFrontRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 0.37, 1.77, 2.02, 3.77, 4.8)",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LowFrequencyMicroMotionUsesAnIndependentLoopAndAsymmetricRebound()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));

        Assert.Contains(
            "resource_name = \"micro_preview\"\nlength = 10.5",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "[node name=\"MicroMotionAnimationPlayer\" type=\"AnimationPlayer\" parent=\".\"",
            scene,
            StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"micro_preview\"", scene, StringComparison.Ordinal);
        Assert.Contains(
            "NodePath(\"CharacterRoot/SkirtMotionRoot/SkirtBackTrainRoot:position\")",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 6.7, 6.88, 7.08, 7.72, 10.5)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "Vector2(-249.542, 128.711), Vector2(-247.592, 128.061)",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LayeredIdleIsLimitedToActiveCombatVisuals()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));

        Assert.Contains("NCombatRoom.Instance?.Mode == CombatRoomMode.ActiveCombat", source, StringComparison.Ordinal);
        Assert.Contains("if (attachLayeredIdle && isActiveCombat)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SakuraDeathPermanentlySwitchesLayeredIdleBackToTheStaticStandee()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.Contains("nameof(NCreature.StartDeathAnim)", source, StringComparison.Ordinal);
        Assert.Contains("SakuraStarterCompatibility.IsKinomotoSakura(player)", source, StringComparison.Ordinal);
        Assert.Contains("controller.ShowStaticStandee();", source, StringComparison.Ordinal);
        Assert.Contains("Visible = false;", source, StringComparison.Ordinal);
        Assert.Contains("_body.SelfModulate = _originalSelfModulate;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LayeredIdleAssetsRemainFixedCanvasImportedTextures()
    {
        var expectedAssets = new[]
        {
            "body_upper_headless.png",
            "body_lower_static.png",
            "skirt_underlay.png",
            "skirt_seam_underlay.png",
            "skirt_anchor.png",
            "back-skirt.png",
            "skirt_left_front.png",
            "skirt_center_front.png",
            "skirt_right_front.png",
            "leg_underlay.png",
            "eye_half_closed.png",
            "eye_closed.png",
            "torso_underlay.png",
            "behind-breath.png",
            "front-breath.png",
            "daimao.png",
            "behind-hair.png",
            "face.png",
            "front_hair_clean.png",
            "head_accessories.png",
            "head_core_underlay.png"
        };

        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));
        foreach (var asset in expectedAssets)
        {
            var relativePath = $"SakuraMod/images/charui/standee_idle/{asset}";
            var path = RegressionTestHarness.FindRepoFile(relativePath);
            var png = File.ReadAllBytes(path);

            Assert.True(png.Length >= 26, $"Expected a complete PNG at {relativePath}.");
            Assert.Equal(941, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
            Assert.Equal(1672, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
            Assert.Equal(6, png[25]);
            Assert.True(File.Exists($"{path}.import"), $"Missing Godot import for {relativePath}.");
            Assert.Contains($"standee_idle/{asset}", scene, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RiggedIdleValidatesRenderedMeshesAndRestoresIdleAfterHurt()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.Contains("SkirtBackTrainMesh", source, StringComparison.Ordinal);
        Assert.Contains("SkirtBackTrainOuterTipMesh", source, StringComparison.Ordinal);
        Assert.Contains("BehindHairMesh", source, StringComparison.Ordinal);
        Assert.Contains("Polygon2D { Texture: not null }", source, StringComparison.Ordinal);
        Assert.Contains("nameof(NCreature.SetAnimationTrigger)", source, StringComparison.Ordinal);
        Assert.Contains("trigger == \"Hit\"", source, StringComparison.Ordinal);
        Assert.Contains("_primaryAnimationPlayer.Play(HurtAnimation);", source, StringComparison.Ordinal);
        Assert.Contains("_primaryAnimationPlayer.Play(IdleAnimation);", source, StringComparison.Ordinal);

        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));
        Assert.Contains(
            "\"values\": [0.0, -0.0122173, 0.00698132, -0.00261799, 0.0]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [Vector2(0, 0), Vector2(-3, -1.5), Vector2(1.2, 0.5), Vector2(-0.4, 0), Vector2(0, 0)]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"values\": [0.0, 0.0453786, -0.0771436, 0.0317649, 0.0]",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandardSakuraUsesLocalIdleWithoutMovingCombatAnchors()
    {
        var combatRoute = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraCombatVisuals.cs"));
        var visualFactory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var actionController = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));

        Assert.Contains("CreateWithLayeredIdle(standardVisualPath", combatRoute, StringComparison.Ordinal);
        Assert.Contains("SakuraCombatVisualPosition = CombatVisualCenter + Vector2.Down * 16f", visualFactory, StringComparison.Ordinal);
        Assert.Contains("playIdleMotion: false", visualFactory, StringComparison.Ordinal);
        Assert.Contains("attachLayeredIdle: true", visualFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("CenterPosition = SakuraCombatVisualPosition", visualFactory, StringComparison.Ordinal);
        Assert.Contains("if (!_playIdleMotion ||", actionController, StringComparison.Ordinal);
    }

    [Fact]
    public void StandeeSourceProjectsAndEditorBackupsStayOutOfThePck()
    {
        var exportPreset = File.ReadAllText(RegressionTestHarness.FindRepoFile("export_presets.cfg"));

        Assert.Contains("SakuraMod/**/*.kra", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/**/*~", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/images/charui/standee_idle/back_train_fill_mask.png*", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/images/charui/standee_idle/front-hair.png*", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/images/charui/standee_idle/neck-withbody.png*", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/images/charui/standee_idle/skirt_back_train_original.png*", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/scenes/charui/sakura_standee_idle_layers.tscn", exportPreset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/scenes/charui/*_test.tscn", exportPreset, StringComparison.Ordinal);
    }

    [Fact]
    public void StandeeScenesUseThePinnedGodot45AnimationLibraryFormat()
    {
        var project = File.ReadAllText(RegressionTestHarness.FindRepoFile("project.godot"));
        var riggedScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_rigged.tscn"));
        var previewTestScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/charui/sakura_standee_idle_preview_test.tscn"));

        Assert.Contains(
            "config/features=PackedStringArray(\"4.5\", \"C#\", \"Mobile\")",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("libraries/ =", riggedScene, StringComparison.Ordinal);
        Assert.DoesNotContain("libraries/ =", previewTestScene, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(riggedScene, "libraries = {"));
        Assert.Equal(1, CountOccurrences(previewTestScene, "libraries = {"));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
