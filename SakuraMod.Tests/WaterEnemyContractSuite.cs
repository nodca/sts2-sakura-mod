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

public sealed class WaterEnemyContractSuite
{
    [Fact]
    public void AquariumMountsAfterBackgroundSetup()
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
    public void WaterLocalizationContractsAreComplete()
    {
        foreach (var locale in new[] { "eng", "zhs", "jpn" })
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
