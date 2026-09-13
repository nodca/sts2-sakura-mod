using System.Buffers.Binary;
using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;

public sealed class FireEnemyRulesSuite
{
    [Fact]
    public void FireCatalogKeepsTheAcceptedElitePoolAndEndpoint()
    {
        Assert.Equal([SourceCardIdentity.Sword, SourceCardIdentity.Libra], FireEnemyCatalog.EliteEncounters.Select(e => e.RewardIdentity));
        Assert.Equal(SourceCardIdentity.Firey, FireEnemyCatalog.BossEncounter.RewardIdentity);
        Assert.Equal(typeof(LightEncounter), FireEnemyCatalog.EndpointEncounterType);
    }

    [Theory]
    [InlineData(0, false, 12)]
    [InlineData(10, false, 42)]
    [InlineData(10, true, 52)]
    public void JudgmentUsesResolutionHandSize(int handSize, bool empowered, int expected) =>
        Assert.Equal(expected, FireEnemyRules.JudgmentDamage(handSize, empowered));

    [Theory]
    [InlineData(312, 520, true)]
    [InlineData(313, 520, false)]
    [InlineData(327, 545, true)]
    [InlineData(328, 545, false)]
    [InlineData(600, 1001, true)]
    [InlineData(601, 1001, false)]
    public void LightEmpowermentIncludesSixtyPercentWithoutRoundingUp(int hp, int maxHp, bool expected) =>
        Assert.Equal(expected, FireEnemyRules.IsLightEmpowered(hp, maxHp));

    [Theory]
    [InlineData(5, 5, 4, 1, 9)]
    [InlineData(5, 5, -4, 9, 1)]
    [InlineData(1, 9, 4, 0, 10)]
    public void PendulumSwingClampsAtTheLethalEdges(int left, int right, int vote, int expectedLeft, int expectedRight)
    {
        var result = FireEnemyRules.Swing(left, right, vote);
        Assert.Equal((expectedLeft, expectedRight), result);
    }

    [Fact]
    public void BalanceMovesBothPansOneStepTowardCenter() =>
        Assert.Equal((4, 6), FireEnemyRules.Recenter(3, 7));

    [Fact]
    public void FacingVoteResolvesBeforeTheSeparateImbalancePoint()
    {
        var result = FireEnemyRules.ResolveLibraTurn(5, 5, 1, "RIGHT");

        Assert.Equal((4, 6), result.Vote);
        Assert.Equal((4, 7), result.Final);
        Assert.Equal(result.Vote, FireEnemyRules.ResolveLibraTurn(5, 5, 1, null).Final);
    }

    [Theory]
    [InlineData(0, 300f, 0.85f, 560f)]
    [InlineData(5, 300f, 0.85f, 680f)]
    [InlineData(10, 300f, 0.85f, 800f)]
    [InlineData(0, 706f, 0.62f, 560f)]
    [InlineData(5, 706f, 0.62f, 680f)]
    [InlineData(10, 706f, 0.62f, 761.14f)]
    public void LibraPanCentersUseTheSafeVisibleInterval(
        int points,
        float height,
        float scale,
        float expected)
    {
        var scaledHeight = height * scale;
        var center = LibraVisualLayout.PanCenterY(points, scaledHeight);

        Assert.InRange(center, expected - 0.01f, expected + 0.01f);
        Assert.True(center - scaledHeight * 0.5f >= LibraVisualLayout.SafeTop - 0.01f);
        Assert.True(center + scaledHeight * 0.5f <= LibraVisualLayout.SafeBottom + 0.01f);
    }

    [Fact]
    public void LibraUsesThreeTrimmedRgbaRuntimeAssets()
    {
        var expected = new[]
        {
            (LibraEnemyAssets.Central, 626, 535),
            (LibraEnemyAssets.Moon, 300, 300),
            (LibraEnemyAssets.Sun, 544, 706)
        };

        Assert.Equal(expected.Select(static item => item.Item1), LibraEnemyAssets.All);
        Assert.Equal(LibraEnemyAssets.Moon, new LibraPanMonster().CustomVisualsPath);
        Assert.Equal(LibraEnemyAssets.All, new LibraPanMonster().AssetPaths);
        foreach (var (assetPath, width, height) in expected)
        {
            var relativePath = assetPath.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
            var file = RegressionTestHarness.FindRepoFile(relativePath);
            var header = File.ReadAllBytes(file).AsSpan(0, 26);
            Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
            Assert.Equal(6, header[25]);
            Assert.True(File.Exists($"{file}.import"), $"Missing Godot import for {relativePath}.");
        }
    }

    [Fact]
    public void FireyUsesTheExtractedRgbaRuntimeAsset()
    {
        var relativePath = FireEnemyAssets.Firey.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
        var file = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(file).AsSpan(0, 26);

        Assert.Equal(1536, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(6, header[25]);
        Assert.Equal(FireEnemyAssets.Firey, new FireyMonster().CustomVisualsPath);
        Assert.Equal([FireEnemyAssets.Firey], new FireyMonster().AssetPaths);
        Assert.True(File.Exists($"{file}.import"), $"Missing Godot import for {relativePath}.");
    }

    [Fact]
    public void SwordUsesTheExtractedRgbaRuntimeAsset()
    {
        var relativePath = FireEnemyAssets.Sword.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
        var file = RegressionTestHarness.FindRepoFile(relativePath);
        var header = File.ReadAllBytes(file).AsSpan(0, 26);

        Assert.Equal(1536, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(6, header[25]);
        Assert.Equal(FireEnemyAssets.Sword, new SwordMonster().CustomVisualsPath);
        Assert.Equal([FireEnemyAssets.Sword], new SwordMonster().AssetPaths);
        Assert.True(File.Exists($"{file}.import"), $"Missing Godot import for {relativePath}.");
    }

    [Fact]
    public void LibraPresentationKeepsTwoNativeBodiesAndRepeatedTierResolution()
    {
        var encounter = new LibraEncounter();
        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Encounters/FireEncounters.cs"));
        var powerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Powers/FireCombatPowers.cs"));
        var imbalanceStart = powerSource.IndexOf("public sealed class LibraImbalancePower", StringComparison.Ordinal);

        Assert.Equal(["LEFT", "RIGHT"], encounter.Slots);
        Assert.Equal(2, encounterSource.Split("ModelDb.Monster<LibraPanMonster>().ToMutable()", StringSplitOptions.None).Length - 1);
        Assert.Contains("root.AddChild(new LibraVisualController())", encounterSource);
        Assert.DoesNotContain("if (currentPosition == _lastEffectPosition)", powerSource);
        Assert.Contains("Notify(LibraPresentationCause.TierResolved", powerSource);
        Assert.DoesNotContain("AfterSideTurnEnd", powerSource[imbalanceStart..]);

        var controllerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Visuals/LibraVisualController.cs"));
        Assert.Contains("ZIndex = 0;", controllerSource);
        Assert.DoesNotContain("ZIndex = -1;", controllerSource);
        Assert.DoesNotContain("ZIndex = -2;", controllerSource);
    }

    [Fact]
    public void FireRouteEncountersKeepTokyoTowerAndLightUsesItsOwnAnimatedBackground()
    {
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/tokyo_tower/tokyo_tower_base.tscn"));
        var texture = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/tokyo_tower/tokyo_tower_base.png");
        var textureImport = File.ReadAllText($"{texture}.import");
        var header = File.ReadAllBytes(texture).AsSpan(0, 26);

        Assert.Contains(FourthActCombatBackgrounds.FireTokyoTowerTexturePath, layerScene);
        Assert.DoesNotContain("AnimationPlayer", layerScene);
        Assert.DoesNotContain("ShaderMaterial", layerScene);
        Assert.DoesNotContain("VideoStream", layerScene);
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(960, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(8, header[24]);
        Assert.Equal(2, header[25]);
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/tokyo_tower/tokyo_tower_base.png\"",
            textureImport);
        Assert.Contains("mipmaps/generate=false", textureImport);

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Encounters/FireEncounters.cs"));
        Assert.Contains("UseProgrammaticCombatBackground => true", encounterSource);
        Assert.Contains("FourthActCombatBackgrounds.CreateFireTokyoTower()", encounterSource);

        const string backgroundMethod = "BuildProgrammaticCombatBackground";
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        foreach (var type in new[] { typeof(SwordEncounter), typeof(LibraEncounter), typeof(FireyEncounter) })
            Assert.Equal(typeof(FireEncounterTemplate), type.GetMethod(backgroundMethod, flags)!.DeclaringType);
        Assert.Equal(typeof(LightEncounter), typeof(LightEncounter).GetMethod(backgroundMethod, flags)!.DeclaringType);
        Assert.Equal(typeof(LightEncounter), SakuraMod.SakuraModCode.FourthAct.Earth.EarthEnemyCatalog.EndpointEncounterType);
        var lightDeclaration = encounterSource[encounterSource.IndexOf("public sealed class LightEncounter", StringComparison.Ordinal)..];
        Assert.Contains("FourthActCombatBackgrounds.CreateLightEternalDay()", lightDeclaration);
        Assert.DoesNotContain("CreateFireTokyoTower", lightDeclaration);

        var lightLayer = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            FourthActCombatBackgrounds.LightEternalDayLayerPath["res://".Length..]));
        var lightTexture = RegressionTestHarness.FindRepoFile(
            FourthActCombatBackgrounds.LightEternalDayTexturePath["res://".Length..]);
        var lightImport = File.ReadAllText($"{lightTexture}.import");
        var lightHeader = File.ReadAllBytes(lightTexture).AsSpan(0, 26);
        Assert.Contains(FourthActCombatBackgrounds.LightEternalDayTexturePath, lightLayer);
        Assert.Contains(StaticCombatBackgroundVisuals.LightPaintingNodeName, lightLayer);
        Assert.Equal(2 * BinaryPrimitives.ReadInt32BigEndian(lightHeader[20..24]),
            BinaryPrimitives.ReadInt32BigEndian(lightHeader[16..20]));
        Assert.Contains($"source_file=\"{FourthActCombatBackgrounds.LightEternalDayTexturePath}\"", lightImport);
        Assert.Contains("mipmaps/generate=false", lightImport);
        Assert.Contains("resource_local_to_scene = true", lightLayer);
        Assert.Contains(FourthActCombatBackgrounds.LightEternalDayShaderPath, lightLayer);
        Assert.Contains(FourthActCombatBackgrounds.LightEternalDaySunShaderPath, lightLayer);
        foreach (var path in new[] { FourthActCombatBackgrounds.LightEternalDayTexturePath,
                     FourthActCombatBackgrounds.LightEternalDayEmpoweredPath,
                     FourthActCombatBackgrounds.LightEternalDayForegroundMaskPath,
                     FourthActCombatBackgrounds.LightEternalDaySunPath })
        {
            Assert.Contains(path, lightLayer);
            var file = RegressionTestHarness.FindRepoFile(path["res://".Length..]);
            var png = File.ReadAllBytes(file).AsSpan(0, 26);
            Assert.Equal(2720, BinaryPrimitives.ReadInt32BigEndian(png[16..20]));
            Assert.Equal(1360, BinaryPrimitives.ReadInt32BigEndian(png[20..24]));
            Assert.Contains($"source_file=\"{path}\"", File.ReadAllText(file + ".import"));
        }
    }

    [Fact]
    public void EarthEncountersUsePenguinParkBackgroundLayer()
    {
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/penguin_park/penguin_park_base.tscn"));
        var texture = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/penguin_park/penguin_park_base.png");
        var textureImport = File.ReadAllText($"{texture}.import");
        var header = File.ReadAllBytes(texture).AsSpan(0, 26);

        Assert.Contains(FourthActCombatBackgrounds.EarthPenguinParkTexturePath, layerScene);
        Assert.Contains(StaticCombatBackgroundVisuals.EarthPenguinParkPaintingNodeName, layerScene);
        Assert.Equal((int)StaticCombatBackgroundVisuals.CanvasWidth, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal((int)StaticCombatBackgroundVisuals.CanvasHeight, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(2 * BinaryPrimitives.ReadInt32BigEndian(header[20..24]),
            BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/penguin_park/penguin_park_base.png\"",
            textureImport);
        foreach (var animatedNode in new[] { "AnimationPlayer", "ShaderMaterial", "VideoStream" })
            Assert.DoesNotContain(animatedNode, layerScene);

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Earth/Encounters/EarthEncounters.cs"));
        Assert.Contains("UseProgrammaticCombatBackground => true", encounterSource);
        Assert.Contains("FourthActCombatBackgrounds.CreateEarthPenguinPark()", encounterSource);
    }

    [Fact]
    public void LibraAndSurroundedCombatSynchronizesPlayerFacing()
    {
        var powerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Powers/FireCombatPowers.cs"));
        var controllerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Visuals/LibraVisualController.cs"));
        var visualsSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeVisuals.cs"));
        var patchSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraStandeeIdleController.cs"));

        Assert.Contains("SakuraStandeeVisuals.SetFacing(player, side == \"LEFT\");", powerSource);
        Assert.Contains("UpdatePlayerFacing(presentation.Side);", controllerSource);
        Assert.Contains("UpdatePlayerFacing(\"RIGHT\");", controllerSource);
        Assert.Contains("internal static void SetFacing(NCreature? creatureNode, bool faceLeft)", visualsSource);
        Assert.Contains("SakuraCombatFacingCardPatch", patchSource);
        Assert.Contains("SakuraCombatFacingPotionPatch", patchSource);
    }
}
