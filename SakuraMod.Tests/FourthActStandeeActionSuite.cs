using Godot;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using System.Buffers.Binary;

public sealed class FourthActStandeeActionSuite
{
    [Fact]
    public void SharedBridgeOwnsTheStaticStandeeAttackContract()
    {
        var fourthActRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActEnemyActionCmd.cs"))!;
        fourthActRoot = Directory.GetParent(fourthActRoot)!.FullName;
        var fourthActSources = Directory
            .EnumerateFiles(fourthActRoot, "*.cs", SearchOption.AllDirectories)
            .ToDictionary(static path => path, File.ReadAllText);
        var bridge = fourthActSources.Single(static pair =>
            pair.Key.EndsWith("FourthActEnemyActionCmd.cs", StringComparison.Ordinal)).Value;

        Assert.Equal(1, fourthActSources.Values.Sum(static source =>
            CountOccurrences(source, ".WithNoAttackerAnim()")));
        Assert.Contains(".WithNoAttackerAnim()", bridge, StringComparison.Ordinal);
        Assert.Contains(".OnlyPlayAnimOnce()", bridge, StringComparison.Ordinal);
        Assert.Contains(".WithAttackerFx(null, attackerSfx)", bridge, StringComparison.Ordinal);
        Assert.Contains(".WithHitFx(hitVfx)", bridge, StringComparison.Ordinal);
        Assert.Contains("PerformAsync(attacker, SakuraStandeeClip.Attack", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFourthActAttackKeepsTheNativeAttackCommandPath()
    {
        var modelsRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs"))!;
        var fourthActRoot = Directory.GetParent(Directory.GetParent(modelsRoot)!.FullName)!.FullName;
        var modelSources = Directory
            .EnumerateFiles(fourthActRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => path.Contains("/Models/", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();

        var nativeAttackCount = modelSources.Sum(static source =>
            CountOccurrences(source, "DamageCmd.Attack("));
        var bridgedAttackCount = modelSources.Sum(static source =>
            CountOccurrences(source, "FourthActEnemyActionCmd.AttackAsync("));

        Assert.Equal(12, nativeAttackCount);
        Assert.Equal(nativeAttackCount, bridgedAttackCount);
        Assert.All(modelSources, static source =>
            Assert.DoesNotContain("CreatureCmd.Damage(", source, StringComparison.Ordinal));

        var bridge = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActEnemyActionCmd.cs"));
        Assert.Contains("() => command.Execute(null)", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void FourthActAudioUsesExplicitVanillaCues()
    {
        var expected = new Dictionary<FourthActAudioCue, string>
        {
            [FourthActAudioCue.WindAttack] =
                "event:/sfx/enemy/enemy_attacks/living_fog/living_fog_attack_blow",
            [FourthActAudioCue.HeavyWindAttack] =
                "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_wave",
            [FourthActAudioCue.IllusionAttack] =
                "event:/sfx/enemy/enemy_attacks/obscura/obscura_attack",
            [FourthActAudioCue.DarkAttack] =
                "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash",
            [FourthActAudioCue.WindTakeoff] =
                "event:/sfx/enemy/enemy_attacks/thieving_hopper/thieving_hopper_take_off",
            [FourthActAudioCue.WindySummon] =
                "event:/sfx/enemy/enemy_attacks/obscura/obscura_summon",
            [FourthActAudioCue.IllusionReweave] =
                "event:/sfx/enemy/enemy_attacks/obscura/obscura_summon",
            [FourthActAudioCue.DarkTransition] =
                "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_hex",
            [FourthActAudioCue.FlyLanding] =
                "res://SakuraMod/sfx/fourth_act/fly_landing.ogg",
            [FourthActAudioCue.SleepCast] =
                "res://SakuraMod/sfx/fourth_act/sleep_cast.ogg",
            [FourthActAudioCue.WindWallBlock] =
                "res://SakuraMod/sfx/fourth_act/wind_wall_block.ogg",
            [FourthActAudioCue.DarkVeilBreak] =
                "res://SakuraMod/sfx/fourth_act/dark_veil_break.ogg"
        };

        Assert.Equal(expected.Count, Enum.GetValues<FourthActAudioCue>().Length);
        foreach (var (cue, path) in expected)
            Assert.Equal(path, FourthActEnemyAudio.PathFor(cue));

        var audio = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActEnemyAudio.cs"));
        Assert.Contains("if (TestMode.IsOn)", audio, StringComparison.Ordinal);
        Assert.Contains("ResourceSoundFileSource", audio, StringComparison.Ordinal);
        Assert.Contains("GameAudioService.Shared.PlayOneShot", audio, StringComparison.Ordinal);
        Assert.Contains("SfxCmd.Play(path)", audio, StringComparison.Ordinal);
        Assert.Contains("MainFile.Logger.Warn", audio, StringComparison.Ordinal);

        foreach (var cue in new[]
                 {
                     FourthActAudioCue.FlyLanding,
                     FourthActAudioCue.SleepCast,
                     FourthActAudioCue.WindWallBlock,
                     FourthActAudioCue.DarkVeilBreak
                 })
        {
            var file = RegressionTestHarness.FindRepoFile(
                FourthActEnemyAudio.PathFor(cue).Replace("res://", string.Empty, StringComparison.Ordinal));
            Assert.True(File.Exists(file), $"Missing local audio resource for {cue}: {file}");
        }

        var fly = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs"));
        var sleep = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs"));
        var feedback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        Assert.Contains("FourthActAudioCue.FlyLanding", fly, StringComparison.Ordinal);
        Assert.Contains("FourthActAudioCue.SleepCast", sleep, StringComparison.Ordinal);
        Assert.Contains("FourthActAudioCue.WindWallBlock", feedback, StringComparison.Ordinal);
        Assert.Contains("FourthActAudioCue.DarkVeilBreak", feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void FourthActAudioGapListDocumentsUnmatchedActions()
    {
        var gaps = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            ".trellis/tasks/08-19-fourth-act-enemy-audio/research/original-audio-gap-list.md"));

        foreach (var action in new[]
                 {
                     "飞行落地", "催眠施法", "浮空转移", "风墙拦截", "风缚转化", "暗幕破裂"
                 })
        {
            Assert.Contains(action, gaps, StringComparison.Ordinal);
        }

        Assert.Contains("v0.107.1", gaps, StringComparison.Ordinal);
        Assert.Contains("本轮接入了可靠的 STS2 原版事件", gaps, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedControllerOwnsStandeeLifecycleAndGameplayCannotBeSkipped()
    {
        var factory = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));

        Assert.Contains("SakuraStandeeActionController.Attach(", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("StartCombatStandeeAnimation", factory, StringComparison.Ordinal);
        Assert.Equal(
            [SakuraStandeeClip.Attack, SakuraStandeeClip.Cast, SakuraStandeeClip.Buff, SakuraStandeeClip.Summon],
            Enum.GetValues<SakuraStandeeClip>());
        Assert.Equal(
            [
                SakuraStandeePlaybackPriority.Idle,
                SakuraStandeePlaybackPriority.Action,
                SakuraStandeePlaybackPriority.Hurt,
                SakuraStandeePlaybackPriority.Death
            ],
            Enum.GetValues<SakuraStandeePlaybackPriority>());
        Assert.Contains("await resolveAtContact();", controller, StringComparison.Ordinal);
        Assert.Contains("nameof(NCreature.SetAnimationTrigger)", controller, StringComparison.Ordinal);
        Assert.Contains("nameof(NCreature.StartDeathAnim)", controller, StringComparison.Ordinal);
        Assert.Contains("SfxCmd.PlayDeath(monster)", controller, StringComparison.Ordinal);
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
    public void PlaybackPriorityIsDeathThenHurtThenActionThenIdle()
    {
        var playback = new SakuraStandeePlaybackState();

        Assert.Equal(SakuraStandeePlaybackPriority.Idle, playback.Priority);
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out _));
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Idle, out _));
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Hurt, out _));
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out _));
        Assert.True(playback.TryBeginDeath(out _));
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Hurt, out _));
    }

    [Fact]
    public void InterruptedPlaybackOnlyLetsTheCurrentClipRecoverToIdle()
    {
        var playback = new SakuraStandeePlaybackState();

        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out var actionGeneration));
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Hurt, out var hurtGeneration));

        Assert.False(playback.IsCurrent(actionGeneration));
        Assert.False(playback.TryFinish(actionGeneration));
        Assert.Equal(SakuraStandeePlaybackPriority.Hurt, playback.Priority);
        Assert.True(playback.TryFinish(hurtGeneration));
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
        Assert.False(playback.TryBegin(SakuraStandeePlaybackPriority.Death, out _));
    }

    [Fact]
    public void DeathIsTerminalAndNeverRecoversToIdle()
    {
        var playback = new SakuraStandeePlaybackState();
        Assert.True(playback.TryBegin(SakuraStandeePlaybackPriority.Action, out _));

        Assert.True(playback.TryBeginDeath(out var deathGeneration));

        Assert.True(playback.IsDead);
        Assert.Equal(SakuraStandeePlaybackPriority.Death, playback.Priority);
        Assert.False(playback.TryFinish(deathGeneration));
        Assert.Equal(SakuraStandeePlaybackPriority.Death, playback.Priority);
        Assert.False(playback.CanPlayNonDeath);
        Assert.False(playback.TryBeginDeath(out _));
    }

    [Fact]
    public void FourthActModelsRouteActionsAndFlyFramesThroughTheSharedController()
    {
        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs",
                     "SakuraModCode/FourthAct/Wind/Models/IllusionMonsters.cs",
                     "SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs",
                     "SakuraModCode/FourthAct/Wind/Models/WindyMonster.cs",
                     "SakuraModCode/FourthAct/Dark/Models/DarkMonster.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            Assert.Contains("FourthActEnemyActionCmd.", source, StringComparison.Ordinal);
        }

        var flyVisuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/FlyVisualController.cs"));
        var flyMonster = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs"));
        Assert.Contains("PlayTextureSequenceAsync(", flyVisuals, StringComparison.Ordinal);
        Assert.Contains("await FlyVisualController.PlayLandingAsync(Creature);", flyMonster, StringComparison.Ordinal);
        Assert.Contains("await FlyVisualController.PlayTakeoffAsync(Creature);", flyMonster, StringComparison.Ordinal);

        var expectedActionClips = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["SakuraModCode/FourthAct/Wind/Models/IllusionMonsters.cs"] =
                ["SakuraStandeeClip.Summon"],
            ["SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs"] =
                ["SakuraStandeeClip.Buff", "SakuraStandeeClip.Cast"],
            ["SakuraModCode/FourthAct/Wind/Models/WindyMonster.cs"] =
                ["SakuraStandeeClip.Summon"],
            ["SakuraModCode/FourthAct/Dark/Models/DarkMonster.cs"] =
                ["SakuraStandeeClip.Cast"]
        };
        foreach (var (relativePath, clips) in expectedActionClips)
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            foreach (var clip in clips)
            {
                Assert.Contains(
                    $"FourthActEnemyActionCmd.PerformAsync(Creature, {clip}",
                    source,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void HurtAndDeathFeedbackExtendTheNativeCreatureLifecycle()
    {
        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));
        var windTemplate = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindMonsterTemplate.cs"));
        var darkMonster = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Dark/Models/DarkMonster.cs"));
        var windAttendants = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs"));

        Assert.Contains("nameof(NCreature.SetAnimationTrigger)", controller, StringComparison.Ordinal);
        Assert.Contains("trigger == \"Hit\"", controller, StringComparison.Ordinal);
        Assert.Contains("?.PlayHurt();", controller, StringComparison.Ordinal);
        Assert.Contains("nameof(NCreature.StartDeathAnim)", controller, StringComparison.Ordinal);
        Assert.Contains("controller.PlayDeath()", controller, StringComparison.Ordinal);
        Assert.Contains("SfxCmd.PlayDeath(monster)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatureCmd.Damage(", controller, StringComparison.Ordinal);

        foreach (var source in new[] { windTemplate, darkMonster })
        {
            Assert.Contains("public override bool HasDeathSfx => true;", source, StringComparison.Ordinal);
            Assert.Contains("public override string DeathSfx", source, StringComparison.Ordinal);
            Assert.Contains("public override string? HurtSfx", source, StringComparison.Ordinal);
            Assert.Contains(
                "public override float DeathAnimLengthOverride => SakuraStandeeActionController.DeathDuration;",
                source,
                StringComparison.Ordinal);
        }

        Assert.Contains("await CreatureCmd.GainBlock(", windAttendants, StringComparison.Ordinal);
        Assert.Contains("await CreatureCmd.GainBlock(", darkMonster, StringComparison.Ordinal);
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

        var windTemplate = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindMonsterTemplate.cs"));
        Assert.Contains(".Concat(WindEnemyAssets.ActionFramesFor(StandeePath))", windTemplate, StringComparison.Ordinal);

        var controller = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));
        Assert.Contains("if (!ApplyActionTexture(clip))", controller, StringComparison.Ordinal);
        Assert.Contains("ActionTexturePath(SakuraStandeeClip clip)", controller, StringComparison.Ordinal);
        Assert.Contains("PreloadTextures(restTexturePath);", controller, StringComparison.Ordinal);
        Assert.Contains("_textures.TryGetValue(path, out var texture)", controller, StringComparison.Ordinal);
        Assert.Contains("SpawnAfterimage(clip);", controller, StringComparison.Ordinal);
        Assert.Contains("ClearAfterimages();", controller, StringComparison.Ordinal);
        Assert.Contains("ApplyRestTexture();", controller, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
