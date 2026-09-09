using SakuraMod.SakuraModCode.FourthAct.Water;
using SakuraMod.SakuraModCode.FourthAct.Water.Models;
using SakuraMod.SakuraModCode.FourthAct.Water.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.Acts;
using System.Text.Json;
using System.Buffers.Binary;

public sealed class WaterEnemyContractSuite
{
    [Fact]
    public void AquariumMountsAfterBackgroundSetupInsteadOfCreatureReady()
    {
        var patch = Assert.Single(typeof(WaterAquariumBackgroundPatch)
            .GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false)
            .Cast<HarmonyLib.HarmonyPatch>());
        Assert.Equal(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom), patch.info.declaringType);
        Assert.Equal(nameof(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.SetUpBackground), patch.info.methodName);
        var postfix = typeof(WaterAquariumBackgroundPatch).GetMethod("Postfix",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(postfix);
        Assert.True(postfix.IsDefined(typeof(HarmonyLib.HarmonyPostfix), false));
        var creatureFeedback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        Assert.DoesNotContain("WaterAquariumVisuals.Attach", creatureFeedback);
    }

    [Fact]
    public void UsesApprovedA9Values()
    {
        Assert.Equal((250, 265, 7, 8, 4, 14, 16, 8, 18, 20),
            (FreezeMonster.BaseHp, FreezeMonster.ToughHp, FreezeMonster.HeavyDamage,
             FreezeMonster.DeadlyHeavyDamage, FreezeMonster.HeavyHits, FreezeMonster.ColdDamage,
             FreezeMonster.DeadlyColdDamage, FreezeMonster.ColdBlock, FreezeMonster.IceBlock,
             FreezeMonster.DeadlyIceBlock));
        Assert.Equal((265, 280, 22, 25, 34, 40, 14, 16),
            (RainMonster.BaseHp, RainMonster.ToughHp, RainMonster.DownpourDamage,
             RainMonster.DeadlyDownpourDamage, RainMonster.FloodDamage, RainMonster.DeadlyFloodDamage,
             RainMonster.CoverBlock, RainMonster.DeadlyCoverBlock));
        Assert.Equal((440, 465, 14, 18, 16, 20, 27, 30, 14, 17),
            (WateryMonster.BaseHp, WateryMonster.ToughHp, WateryMonster.TidalMinimum,
             WateryMonster.TidalMaximum, WateryMonster.DeadlyTidalMinimum, WateryMonster.DeadlyTidalMaximum,
             WateryMonster.DragonDamage,
             WateryMonster.DeadlyDragonDamage, WateryMonster.FloodDamage, WateryMonster.DeadlyFloodDamage));
    }

    [Fact]
    public void ReservoirOnlyConsumesUnblockedWater()
    {
        Assert.Equal(6, WaterEnemyRules.RemainingReservoir(10, 4));
        Assert.Equal(10, WaterEnemyRules.RemainingReservoir(10, 0));
        Assert.Equal(0, WaterEnemyRules.RemainingReservoir(10, 20));
    }

    [Fact]
    public void TidalDamageIsIdempotentAndBounded()
    {
        var first = WaterEnemyRules.RollTidalDamage(123, 7, 2, 14, 18);
        Assert.Equal(first, WaterEnemyRules.RollTidalDamage(123, 7, 2, 14, 18));
        Assert.InRange(first, 14, 18);
        Assert.All(Enumerable.Range(0, 32), round =>
            Assert.InRange(WaterEnemyRules.RollTidalDamage(123, 7, round, 16, 20), 16, 20));
    }

    [Fact]
    public void EncountersAndRewardsStayScopedToTheWaterRoute()
    {
        Assert.Equal([typeof(FreezeEncounter), typeof(RainEncounter)],
            WaterEnemyCatalog.EliteEncounters.Select(static encounter => encounter.EncounterType));
        Assert.Equal([SourceCardIdentity.Freeze, SourceCardIdentity.Rain],
            WaterEnemyCatalog.EliteEncounters.Select(static encounter => encounter.RewardIdentity));
        Assert.Equal(typeof(WateryEncounter), WaterEnemyCatalog.BossEncounter.EncounterType);
        Assert.Equal(SourceCardIdentity.Watery, WaterEnemyCatalog.BossEncounter.RewardIdentity);

        WaterMonsterTemplate[] encounters = [new FreezeEncounter(), new RainEncounter(), new WateryEncounter()];
        Assert.Equal([RoomType.Elite, RoomType.Elite, RoomType.Boss], encounters.Select(static encounter => encounter.RoomType));
        Assert.All(encounters, encounter =>
        {
            Assert.False(encounter.ShouldGiveRewards);
            Assert.True(encounter.IsValidForAct(new SakuraMod.SakuraModCode.FourthAct.Routing.SakuraFourthAct()));
            Assert.False(encounter.IsValidForAct(new Glory()));
        });
    }

    [Fact]
    public void WaterPowersUseNativePlayerScopedHooks()
    {
        Assert.True(RegressionTestHarness.DeclaresMethod<WaterFrozenPower>("ShouldPlay"));
        Assert.True(RegressionTestHarness.DeclaresMethod<DrenchedPower>("TryModifyEnergyCostInCombat"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WaterSovereigntyPower>("TryModifyPowerAmountReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<FreezeMonster>("AfterDamageReceived"));
        Assert.False(new WaterFrozenPower().ShouldScaleInMultiplayer);
        Assert.False(new DrenchedPower().ShouldScaleInMultiplayer);
        Assert.False(new WaterReservoirPower().ShouldScaleInMultiplayer);
        Assert.Equal(SakuraElementSet.Water, SakuraElementState.LocksFromSovereignty(false, false, true));
    }

    [Fact]
    public void WaterMonstersUseApprovedRgbaStandees()
    {
        Assert.Equal(WaterEnemyAssets.Freeze, new FreezeMonster().CustomVisualsPath);
        Assert.Equal(WaterEnemyAssets.Rain, new RainMonster().CustomVisualsPath);
        Assert.Equal(WaterEnemyAssets.Watery, new WateryMonster().CustomVisualsPath);
        Assert.Contains(WaterEnemyAssets.Freeze, new FreezeMonster().AssetPaths);
        Assert.Contains(WaterEnemyAssets.Rain, new RainMonster().AssetPaths);
        Assert.Contains(WaterEnemyAssets.Watery, new WateryMonster().AssetPaths);

        foreach (var assetPath in new[] { WaterEnemyAssets.Freeze, WaterEnemyAssets.Rain, WaterEnemyAssets.Watery })
        {
            var relativePath = assetPath.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
            var file = RegressionTestHarness.FindRepoFile(relativePath);
            var header = File.ReadAllBytes(file).AsSpan(0, 26);
            Assert.Equal(1536, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
            Assert.Equal(6, header[25]);
            Assert.True(File.Exists($"{file}.import"), $"Missing Godot import for {relativePath}.");
        }
    }

    [Fact]
    public void WaterEncountersUseOneAlignedVideoAquariumBackgroundLayer()
    {
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/aquarium/aquarium_base.tscn"));
        var texture = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/aquarium/aquarium_base.png");
        var textureImport = File.ReadAllText($"{texture}.import");
        var header = File.ReadAllBytes(texture).AsSpan(0, 26);

        Assert.Contains(FourthActCombatBackgrounds.WaterAquariumTexturePath, layerScene);
        Assert.DoesNotContain("AnimationPlayer", layerScene);
        Assert.Contains(FourthActCombatBackgrounds.WaterAquariumVideoPath, layerScene);
        Assert.Contains(FourthActCombatBackgrounds.WaterAquariumMaskPath, layerScene);
        Assert.Contains(FourthActCombatBackgrounds.WaterAquariumShaderPath, layerScene);
        Assert.Contains("type=\"VideoStreamPlayer\" parent=\"AquariumPainting\"", layerScene);
        Assert.Contains("autoplay = true", layerScene);
        Assert.Contains("loop = true", layerScene);
        Assert.Contains("expand = true", layerScene);
        Assert.Contains("anchor_left = 0.3", layerScene);
        Assert.Contains("anchor_right = 0.7", layerScene);
        Assert.DoesNotContain(".cs\"", layerScene);
        Assert.Equal(2240, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(1120, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(8, header[24]);
        Assert.Equal(2, header[25]);
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/aquarium/aquarium_base.png\"",
            textureImport);
        Assert.Contains("mipmaps/generate=false", textureImport);
        var mask = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/aquarium/aquarium_tank_mask.png");
        var maskHeader = File.ReadAllBytes(mask).AsSpan(0, 26);
        Assert.Equal(896, BinaryPrimitives.ReadInt32BigEndian(maskHeader[16..20]));
        Assert.Equal(448, BinaryPrimitives.ReadInt32BigEndian(maskHeader[20..24]));
        Assert.Contains("mipmaps/generate=false", File.ReadAllText($"{mask}.import"));
        var video = File.ReadAllBytes(RegressionTestHarness.FindRepoFile(
            "SakuraMod/videos/fourth_act/aquarium_tank.ogv"));
        Assert.Equal("OggS", System.Text.Encoding.ASCII.GetString(video, 0, 4));

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Water/WaterMonsterTemplate.cs"));
        Assert.Contains("UseProgrammaticCombatBackground => true", encounterSource);
        Assert.Contains("FourthActCombatBackgrounds.CreateWaterAquarium()", encounterSource);
        Assert.DoesNotContain("FourthActCombatBackgrounds.CreateWindRooftop()", encounterSource);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2520, 1080)]
    [InlineData(1920, 1200)]
    public void AquariumOverscanCoversSupportedRatiosWithShakeMargin(float width, float height)
    {
        var scale = WaterAquariumVisuals.ImageScale(width, height);
        Assert.True(2240 * scale >= width + 96);
        Assert.True(1120 * scale >= height + 96);
        Assert.Equal(1.2f, scale, 5);
    }

    [Fact]
    public void WaterMoveAndLocalizationContractsAreComplete()
    {
        var freeze = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Water/Models/FreezeMonster.cs"));
        Assert.Contains("WithHitCount(HeavyHits)", freeze);
        Assert.Contains("_isHeavyStriking && dealer == Creature", freeze);

        var watery = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Water/Models/WateryMonster.cs"));
        Assert.Contains("new TidalDrawIntent", watery);
        Assert.Contains("new WaterBlockStealIntent", watery);
        Assert.Contains("DamageCmd.Attack(CurrentFlood)", watery);
        Assert.Contains("WaterReservoirPower", watery);
        Assert.Contains("SakuraPowerValueProps.Block", watery);

        foreach (var locale in new[] { "eng", "zhs" })
        {
            using var monsters = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                $"SakuraMod/localization/{locale}/monsters.json")));
            using var powers = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                $"SakuraMod/localization/{locale}/powers.json")));
            using var intents = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                $"SakuraMod/localization/{locale}/intents.json")));
            Assert.True(monsters.RootElement.TryGetProperty("SAKURA_MOD_MONSTER_WATERY_MONSTER.moves.FLOOD.title", out _));
            Assert.True(powers.RootElement.TryGetProperty("SAKURA_MOD_POWER_WATER_RESERVOIR_POWER.title", out _));
            Assert.True(intents.RootElement.TryGetProperty("SAKURA_MOD_FORMAT_DAMAGE_RANGE", out _));
            Assert.True(intents.RootElement.TryGetProperty("SAKURA_MOD_STEAL_BLOCK.description", out _));
        }
    }
}
