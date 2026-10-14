using Godot;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using System.Buffers.Binary;

public sealed class FourthActStandeeActionSuite
{
    [Fact]
    public void FourthActStandeesAttachWithoutMotion()
    {
        var factory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));

        Assert.Contains("CreateFourthActEnemy", factory, StringComparison.Ordinal);
        Assert.Contains("motion: false", factory, StringComparison.Ordinal);
        Assert.Contains("if (_motion && !TestMode.IsOn)", controller, StringComparison.Ordinal);
        Assert.Contains("_playIdleMotion = motion && playIdleMotion", controller, StringComparison.Ordinal);
        Assert.Equal(
            [SakuraStandeePlaybackPriority.Idle, SakuraStandeePlaybackPriority.Action],
            Enum.GetValues<SakuraStandeePlaybackPriority>());
        foreach (var removed in new[] { "PlayActionAsync", "PlayHurt", "PlayDeath", "SpawnAfterimage", "HarmonyPatch" })
            Assert.DoesNotContain(removed, controller, StringComparison.Ordinal);
    }

    [Fact]
    public void BigAndLittleComposeWithoutMovingTheStandeeFoot()
    {
        Assert.Equal(1f, SakuraStandeeSizeRules.Multiplier(false, false));
        Assert.Equal(1.25f, SakuraStandeeSizeRules.Multiplier(true, false));
        Assert.Equal(0.78f, SakuraStandeeSizeRules.Multiplier(false, true));
        Assert.Equal(0.975f, SakuraStandeeSizeRules.Multiplier(true, true), 3);

        var basePosition = new Vector2(-10.56f, -174.08f);
        const float floorY = 1f;
        const float multiplier = 1.25f;
        var anchored = SakuraStandeeSizeRules.FootAnchoredPosition(
            basePosition,
            floorY,
            multiplier);
        var scaledFootY = anchored.Y + (floorY - basePosition.Y) * multiplier;

        Assert.Equal(basePosition.X, anchored.X);
        Assert.Equal(floorY, scaledFootY, 3);
        Assert.Equal(
            new Vector2(0.35f, 0.35f),
            SakuraStandeeSizeRules.RestScale(new Vector2(0.28f, 0.28f), multiplier));
    }

    [Fact]
    public void ActionOutranksIdleAndOnlyTheCurrentClipRecovers()
    {
        var playback = new SakuraStandeePlaybackState();

        Assert.Equal(SakuraStandeePlaybackPriority.Idle, playback.Priority);
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out var first));
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Idle, out _));
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out var second));

        Assert.False(playback.TryFinish(first));
        Assert.True(playback.TryFinish(second));
        Assert.Equal(SakuraStandeePlaybackPriority.Idle, playback.Priority);
    }

    [Fact]
    public void TreeExitInvalidatesPlaybackAndRejectsNewClips()
    {
        var playback = new SakuraStandeePlaybackState();
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out var generation));

        playback.Dispose();

        Assert.False(playback.IsCurrent(generation));
        Assert.False(playback.TryFinish(generation));
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out _));
    }

    [Fact]
    public void HurtAndDeathRideTheStandeeShaderThroughNativeHooks()
    {
        var material = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/Presentation/FourthActStandeeMaterial.cs"));
        var template = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/FourthActMonsterTemplate.cs"));

        Assert.Contains("nameof(NCreature.SetAnimationTrigger)", material, StringComparison.Ordinal);
        Assert.Contains("trigger != \"Hit\"", material, StringComparison.Ordinal);
        Assert.Contains("monster.TryPlayStandeeHit(__instance)", material, StringComparison.Ordinal);
        Assert.Contains("nameof(NCreature.StartDeathAnim)", material, StringComparison.Ordinal);
        Assert.Contains("SfxCmd.PlayDeath", material, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTween", material, StringComparison.Ordinal);
        Assert.Contains("DeathAnimLengthOverride => DissolveDuration", template, StringComparison.Ordinal);
        Assert.Contains("internal virtual float DissolveDuration => FourthActStandeeMaterial.DissolveDuration", template, StringComparison.Ordinal);
        Assert.Contains("ShouldFadeAfterDeath => false", template, StringComparison.Ordinal);
    }

    [Fact]
    public void FlyFramesStillSwapThroughTheSharedController()
    {
        var flyVisuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/FlyVisualController.cs"));
        var flyMonster = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs"));
        Assert.Contains("PlayTextureSequenceAsync", flyVisuals, StringComparison.Ordinal);
        Assert.Contains("PlayLandingAsync", flyMonster, StringComparison.Ordinal);
        Assert.Contains("PlayTakeoffAsync", flyMonster, StringComparison.Ordinal);
    }

    [Fact]
    public void PoseChangingActionFramesRemainFixedCanvasRgbaAssets()
    {
        var expected = new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal)
        {
            [WindEnemyAssets.IllusionCast] = (1536, 2048),
            [WindEnemyAssets.WindyAction] = (1536, 2048),
            [WindEnemyAssets.DashAttack] = (2048, 1536),
            [WindEnemyAssets.SleepCast] = (1536, 2048),
            [DarkEnemyAssets.Action] = (1536, 2048)
        };

        foreach (var (assetPath, dimensions) in expected)
        {
            var relativePath = assetPath.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
            var file = RegressionTestHarness.FindRepoFile(relativePath);
            var header = File.ReadAllBytes(file).AsSpan(0, 26);
            Assert.Equal(dimensions.Width, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal(dimensions.Height, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
            Assert.Equal(6, header[25]);
            Assert.True(File.Exists($"{file}.import"), $"Missing Godot import for {relativePath}.");
        }

        Assert.Equal([WindEnemyAssets.IllusionCast], WindEnemyAssets.ActionFramesFor(WindEnemyAssets.Illusion));
        Assert.Equal([WindEnemyAssets.WindyAction], WindEnemyAssets.ActionFramesFor(WindEnemyAssets.Windy));
        Assert.Equal([WindEnemyAssets.DashAttack], WindEnemyAssets.ActionFramesFor(WindEnemyAssets.Dash));
        Assert.Equal([WindEnemyAssets.SleepCast], WindEnemyAssets.ActionFramesFor(WindEnemyAssets.Sleep));
        Assert.Empty(WindEnemyAssets.ActionFramesFor(WindEnemyAssets.Float));
        Assert.Contains(DarkEnemyAssets.Action, new DarkMonster().AssetPaths);

        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));
        Assert.Contains("SetActionPose", controller, StringComparison.Ordinal);
        Assert.Contains("ActionTexturePath", controller, StringComparison.Ordinal);
        Assert.Contains("PreloadTextures", controller, StringComparison.Ordinal);
    }
}
