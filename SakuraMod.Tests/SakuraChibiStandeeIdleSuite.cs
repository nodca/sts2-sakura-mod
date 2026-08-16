public sealed class SakuraChibiStandeeIdleSuite
{
    private const string RigScenePath =
        "SakuraMod/scenes/charui/sakura_chibi_combat_idle_rigged.tscn";

    [Fact]
    public void RigUsesAWeightedGroundedBodyAndSharedHeldWandHierarchy()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(RigScenePath));

        Assert.Contains("[node name=\"BodySkeleton\" type=\"Skeleton2D\"", scene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"HipBone\" type=\"Bone2D\"", scene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"ChestBone\" type=\"Bone2D\"", scene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"BodyMesh\" type=\"Polygon2D\"", scene, StringComparison.Ordinal);
        Assert.Contains("skeleton = NodePath(\"../BodySkeleton\")", scene, StringComparison.Ordinal);
        Assert.Contains("bones = [\"HipBone\"", scene, StringComparison.Ordinal);
        Assert.Contains(
            "[node name=\"HeldWandRoot\" type=\"Node2D\" parent=\"CharacterRoot/ChestAttachmentRoot\"]",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "parent=\"CharacterRoot/ChestAttachmentRoot/HeldWandRoot/ScreenRightArmRoot\"",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "parent=\"CharacterRoot/ChestAttachmentRoot/HeldWandRoot/ScreenLeftArmRoot\"",
            scene,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CharacterRoot:position", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void RigUsesTheApprovedSixSecondAndCostumeCadenceLoops()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(RigScenePath));

        Assert.Contains("resource_name = \"chibi_idle\"\nlength = 6.0\nloop_mode = 1", scene, StringComparison.Ordinal);
        Assert.Contains(
            "resource_name = \"chibi_micro\"\nlength = 14.666667\nloop_mode = 1",
            scene,
            StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"chibi_idle\"", scene, StringComparison.Ordinal);
        Assert.Contains("autoplay = &\"chibi_micro\"", scene, StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 1.5, 3, 4.5, 6)",
            scene,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"times\": PackedFloat32Array(0, 3.666667, 7.333333, 11, 14.666667)",
            scene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RigUsesPinnedGodotAnimationLibrariesAndAcceptedRuntimeTextures()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(RigScenePath));
        var expectedTextures = new[]
        {
            "body_core_completed.png",
            "head.png",
            "screen_left_arm.png",
            "screen_right_arm.png",
            "wand_completed_regenerated.png"
        };

        Assert.DoesNotContain("libraries/ =", scene, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(scene, "libraries = {"));
        Assert.All(expectedTextures, texture => Assert.Contains(
            $"path=\"res://SakuraMod/images/charui/chibi_combat/layers/{texture}\"",
            scene,
            StringComparison.Ordinal));
        Assert.DoesNotContain("wand_completed.png\"", scene, StringComparison.Ordinal);
        Assert.Contains("[node name=\"CanvasOrigin\" type=\"Marker2D\"", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerKeepsStaticFallbackAndDoesNotMoveCombatAnchors()
    {
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraChibiStandeeIdleController.cs"));
        var standardController = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.Contains("baseSprite.SelfModulate = hidden;", controller, StringComparison.Ordinal);
        Assert.Contains("_body.SelfModulate = _originalSelfModulate;", controller, StringComparison.Ordinal);
        Assert.Contains("GetAlignmentPosition(body, layers)", controller, StringComparison.Ordinal);
        Assert.Contains("SyncFlip();", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("%Bounds", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("%CenterPos", controller, StringComparison.Ordinal);
        Assert.Contains(
            "SakuraChibiStandeeIdleController.ShowStaticStandeeForDeath(body);",
            standardController,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DraftLayersAndPreviewStayOutsideThePublishedPack()
    {
        var preset = File.ReadAllText(RegressionTestHarness.FindRepoFile("export_presets.cfg"));

        Assert.Contains("chibi_combat/layers/body_core.png*", preset, StringComparison.Ordinal);
        Assert.Contains("chibi_combat/layers/body_core_inpaint_work*", preset, StringComparison.Ordinal);
        Assert.Contains("chibi_combat/layers/wand.png*", preset, StringComparison.Ordinal);
        Assert.Contains("chibi_combat/layers/wand_completed.png*", preset, StringComparison.Ordinal);
        Assert.Contains("SakuraMod/scenes/charui/*_test.tscn", preset, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
