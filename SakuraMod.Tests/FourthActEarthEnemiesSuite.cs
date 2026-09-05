using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Earth;
using SakuraMod.SakuraModCode.FourthAct.Earth.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Earth.Models;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.Powers;

public sealed class FourthActEarthEnemiesSuite
{
    [Fact]
    public void EarthCatalogKeepsApprovedEncounterStructure()
    {
        Assert.Equal(
            [SourceCardIdentity.Shadow, SourceCardIdentity.Wood],
            EarthEnemyCatalog.EliteEncounters.Select(static encounter => encounter.RewardIdentity));
        Assert.Equal(
            [typeof(ShadowEncounter), typeof(WoodEncounter)],
            EarthEnemyCatalog.EliteEncounters.Select(static encounter => encounter.EncounterType));

        Assert.Equal(SourceCardIdentity.Earthy, EarthEnemyCatalog.BossEncounter.RewardIdentity);
        Assert.Equal(typeof(EarthyEncounter), EarthEnemyCatalog.BossEncounter.EncounterType);

        Assert.Equal(
            [typeof(ShadowMonster), typeof(WoodMonster), typeof(EarthyMonster)],
            EarthEnemyCatalog.MonsterTypes);
    }

    [Fact]
    public void EarthEncounterRoomTypesAndActsAreValid()
    {
        EarthEncounterTemplate[] encounters = [new ShadowEncounter(), new WoodEncounter(), new EarthyEncounter()];
        Assert.Equal([RoomType.Elite, RoomType.Elite, RoomType.Boss], encounters.Select(static encounter => encounter.RoomType));

        Assert.All(encounters, encounter =>
        {
            Assert.False(encounter.ShouldGiveRewards);
            Assert.True(encounter.IsValidForAct(new SakuraFourthAct()));
            Assert.False(encounter.IsValidForAct(new Glory()));
        });
    }

    [Fact]
    public void EarthEnemyCalibratedValuesMatchPRD()
    {
        // Shadow values
        Assert.Equal(255, EarthEnemyRules.ShadowHp);
        Assert.Equal(270, EarthEnemyRules.ShadowToughHp);
        Assert.Equal(7, EarthEnemyRules.ShadowClawsDamage);
        Assert.Equal(8, EarthEnemyRules.ShadowClawsA9Damage);
        Assert.Equal(3, EarthEnemyRules.ShadowClawsHits);
        Assert.Equal(16, EarthEnemyRules.ShadowVeilBlock);
        Assert.Equal(20, EarthEnemyRules.ShadowVeilA8Block);
        Assert.Equal(6, EarthEnemyRules.ShadowVeilHeal);
        Assert.Equal(8, EarthEnemyRules.ShadowVeilA9Heal);
        Assert.Equal(3, EarthEnemyRules.ShadowSurgeStrength);
        Assert.Equal(4, EarthEnemyRules.ShadowSurgeA9Strength);
        Assert.Equal(10, EarthEnemyRules.ShadowSurgeBlock);
        Assert.Equal(14, EarthEnemyRules.ShadowSurgeA8Block);
        Assert.Equal(28, EarthEnemyRules.ShadowBiteDamage);
        Assert.Equal(32, EarthEnemyRules.ShadowBiteA9Damage);

        // Wood values
        Assert.Equal(245, EarthEnemyRules.WoodHp);
        Assert.Equal(260, EarthEnemyRules.WoodToughHp);
        Assert.Equal(14, EarthEnemyRules.WoodStrikeBase);
        Assert.Equal(16, EarthEnemyRules.WoodStrikeA9Base);
        Assert.Equal(2, EarthEnemyRules.WoodStrikePerRoot);
        Assert.Equal(10, EarthEnemyRules.WoodSproutBaseBlock);
        Assert.Equal(12, EarthEnemyRules.WoodSproutA8BaseBlock);
        Assert.Equal(1, EarthEnemyRules.WoodSproutStrength);
        Assert.Equal(2, EarthEnemyRules.WoodSproutA9Strength);

        // Earthy values
        Assert.Equal(440, EarthEnemyRules.EarthyHp);
        Assert.Equal(465, EarthEnemyRules.EarthyToughHp);
        Assert.Equal(18, EarthEnemyRules.EarthyTremorBase);
        Assert.Equal(20, EarthEnemyRules.EarthyTremorA9Base);
        Assert.Equal(8, EarthEnemyRules.EarthyRockfallDamage);
        Assert.Equal(9, EarthEnemyRules.EarthyRockfallA9Damage);
        Assert.Equal(2, EarthEnemyRules.EarthyRockfallHits);
        Assert.Equal(2, EarthEnemyRules.EarthyChargeStrength);
        Assert.Equal(3, EarthEnemyRules.EarthyChargeA9Strength);
        Assert.Equal(16, EarthEnemyRules.EarthyChargeBlock);
        Assert.Equal(20, EarthEnemyRules.EarthyChargeA8Block);
        Assert.Equal(10, EarthEnemyRules.EarthyLandslideDamage);
        Assert.Equal(12, EarthEnemyRules.EarthyLandslideA9Damage);
    }

    [Theory]
    [InlineData(0, false, 14)]
    [InlineData(1, false, 16)]
    [InlineData(5, false, 24)]
    [InlineData(0, true, 16)]
    [InlineData(1, true, 18)]
    [InlineData(5, true, 26)]
    public void WoodStrikeDamageScalesWithRootedCount(int rooted, bool deadly, int expected) =>
        Assert.Equal(expected, EarthEnemyRules.WoodStrikeDamage(rooted, deadly));

    [Theory]
    [InlineData(0, false, 10)]
    [InlineData(3, false, 13)]
    [InlineData(0, true, 12)]
    [InlineData(3, true, 15)]
    public void WoodSproutBlockScalesWithRootedCount(int rooted, bool tough, int expected) =>
        Assert.Equal(expected, EarthEnemyRules.WoodSproutBlock(rooted, tough));

    [Theory]
    [InlineData(0, false, 18)]
    [InlineData(4, false, 22)]
    [InlineData(0, true, 20)]
    [InlineData(4, true, 24)]
    public void EarthyTremorDamageScalesWithSediment(int sediment, bool deadly, int expected) =>
        Assert.Equal(expected, EarthEnemyRules.EarthyTremorDamage(sediment, deadly));

    [Fact]
    public void EarthSovereigntyLocksEarthState()
    {
        Assert.Equal(SakuraElementSet.Earth, SakuraElementState.LocksFromSovereignty(false, false, false, false, false, earth: true));
        Assert.Equal(SakuraElementSet.Earth, SakuraElementState.LocksForPower(new ClassicEarthyPower()));
        Assert.Equal(SakuraElementSet.Earth, SakuraElementState.LocksForPower(new ClassicEarthyPermanentPower()));
    }

    [Fact]
    public void BuriedCardsTrackingLifecycle()
    {
        var dummyCard = new ClowWood();

        Assert.False(EarthCombatRules.IsBuried(dummyCard));
        EarthCombatRules.MarkBuried(dummyCard);
        Assert.True(EarthCombatRules.IsBuried(dummyCard));
        EarthCombatRules.UnmarkBuried(dummyCard);
        Assert.False(EarthCombatRules.IsBuried(dummyCard));

        EarthCombatRules.MarkBuried(dummyCard);
        Assert.True(EarthCombatRules.IsBuried(dummyCard));
        EarthCombatRules.ClearBuried();
        Assert.False(EarthCombatRules.IsBuried(dummyCard));
    }

    [Fact]
    public void BuriedCardShufflePatchTargetsCardPileCmdShuffle()
    {
        var patchAttr = typeof(EarthBuriedCardShufflePatch).GetCustomAttributes(typeof(HarmonyPatch), false)
            .Cast<HarmonyPatch>()
            .FirstOrDefault();

        Assert.NotNull(patchAttr);
        Assert.NotNull(typeof(EarthBuriedCardShufflePatch).GetMethod("Prefix"));
        Assert.NotNull(typeof(EarthBuriedCardShufflePatch).GetMethod("Postfix"));
    }

    [Fact]
    public void EarthPowersUseStandardContracts()
    {
        Assert.True(RegressionTestHarness.DeclaresMethod<EarthSovereigntyPower>("TryModifyPowerAmountReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<ShadowEchoPower>("AfterCardPlayed"));
        Assert.True(RegressionTestHarness.DeclaresMethod<ShadowEchoPower>("AfterSideTurnEnd"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WoodRootedPower>("AfterCardExhausted"));
        Assert.True(RegressionTestHarness.DeclaresMethod<EarthySedimentPower>("AfterCardPlayed"));
        Assert.True(RegressionTestHarness.DeclaresMethod<EarthySedimentPower>("AfterCardDiscarded"));
        Assert.True(RegressionTestHarness.DeclaresMethod<EarthySedimentPower>("AfterCardChangedPiles"));
        Assert.True(RegressionTestHarness.DeclaresMethod<EarthySedimentPower>("AfterDeath"));
    }
}
