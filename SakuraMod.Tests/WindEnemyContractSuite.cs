using System.Text.Json;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.Acts;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

public sealed class WindEnemyContractSuite
{
    [Fact]
    public void SleepingUsesOneAfflictionStateAndBlocksOnlyManualPlay()
    {
        var sleeping = new SakuraMod.SakuraModCode.FourthAct.Dark.Cards.MicroLight();
        var other = new SakuraMod.SakuraModCode.FourthAct.Dark.Cards.MicroLight();

        Assert.True(SleepingAffliction.ShouldBlockPlay(sleeping, sleeping, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType.None));
        Assert.False(SleepingAffliction.ShouldBlockPlay(other, sleeping, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType.None));
        Assert.False(SleepingAffliction.ShouldBlockPlay(sleeping, sleeping, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType.Default));
    }

    [Fact]
    public void EncountersRemainExplicitAndScopedToTheFourthAct()
    {
        var fly = new FlyEncounter();
        var illusion = new IllusionEncounter();
        var windy = new WindyEncounter();
        Assert.Equal(RoomType.Elite, fly.RoomType);
        Assert.Equal(RoomType.Elite, illusion.RoomType);
        Assert.Equal(RoomType.Boss, windy.RoomType);
        Assert.All(new WindEncounterTemplate[] { fly, illusion, windy }, encounter =>
        {
            Assert.False(encounter.ShouldGiveRewards);
            Assert.True(encounter.IsValidForAct(new SakuraFourthAct()));
            Assert.False(encounter.IsValidForAct(new Glory()));
        });
        Assert.Equal([typeof(FlyEncounter), typeof(IllusionEncounter)], WindEnemyCatalog.EliteEncounterTypes);
        Assert.Equal(typeof(WindyEncounter), WindEnemyCatalog.BossEncounterType);
        Assert.Equal(
            [SourceCardIdentity.Fly, SourceCardIdentity.Illusion],
            WindEnemyCatalog.EliteEncounters.Select(static encounter => encounter.RewardIdentity));
        Assert.Equal(SourceCardIdentity.Windy, WindEnemyCatalog.BossEncounter.RewardIdentity);
        Assert.Equal(["CENTER"], fly.Slots);
        Assert.Equal(["LEFT", "CENTER", "RIGHT"], illusion.Slots);
        Assert.Equal(["ATTENDANT", "BOSS"], windy.Slots);
        Assert.All(new WindEncounterTemplate[] { fly, illusion, windy }, static encounter => Assert.True(encounter.HasScene));
        Assert.Equal(7, WindEnemyCatalog.MonsterTypes.Distinct().Count());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public void BindConversionRoundsEachPlayerUp(int unresolved, int wall) =>
        Assert.Equal(wall, WindEnemyRules.WallFromUnresolvedBind(unresolved));

    [Fact]
    public void MultiplayerWallUsesStableParticipantCapWithoutRemovingExistingLayers()
    {
        Assert.Equal(2, WindEnemyRules.AggregateWall(0, [5], 1));
        Assert.Equal(2, WindEnemyRules.AggregateWall(1, [2], 1));
        Assert.Equal(4, WindEnemyRules.AggregateWall(1, [3, 2], 2));
        Assert.Equal(5, WindEnemyRules.AggregateWall(2, [1, 4, 0], 3));
        Assert.Equal(4, WindEnemyRules.AggregateWall(4, [5], 1));
        Assert.Equal(6, WindEnemyRules.AggregateWall(4, [5], 3));
        Assert.Equal(7, WindEnemyRules.FailedBindAttackBonus([2, 5, 0]));
    }

    [Fact]
    public void HighRiskWindMechanicsRemainOnTheirNativeHookBoundaries()
    {
        Assert.True(RegressionTestHarness.DeclaresMethod<IllusionIdentityPower>("AfterPlayerTurnStart"));
        Assert.True(RegressionTestHarness.DeclaresMethod<IllusionIdentityPower>("AfterDamageReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<IllusionProjectionPower>("TryModifyPowerAmountReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<IllusionProjectionPower>("ModifyDamageCap"));
        Assert.True(RegressionTestHarness.DeclaresMethod<IllusionProjectionPower>("AfterDamageReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindWallPower>("ModifyDamageCap"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindWallPower>("AfterModifyingDamageAmount"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindyNextActionDamagePower>("ModifyDamageAdditive"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindyNextActionDamagePower>("AfterSideTurnEnd"));
        Assert.False(RegressionTestHarness.DeclaresMethod<WindyBattlePower>("BeforeHandDraw"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindyBattlePower>("BeforeSideTurnEnd"));
        Assert.True(RegressionTestHarness.DeclaresMethod<FloatDrawCounterPower>("AfterCardDrawn"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindSleepSelectionPower>("AfterPlayerTurnStart"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindSleepWakePower>("AfterDamageReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<WindSleepWakePower>("AfterDeath"));
    }

    [Fact]
    public void WindPowerAmountsDoNotUseGenericMultiplayerScaling()
    {
        MegaCrit.Sts2.Core.Models.PowerModel[] powers =
        [
            new IllusionIdentityPower(),
            new IllusionProjectionPower(),
            new WindSovereigntyPower(),
            new WindBindPower(),
            new WindWallPower(),
            new WindyNextActionDamagePower(),
            new WindyBattlePower(),
            new FloatDrawCounterPower(),
            new WindSleepSelectionPower(),
            new WindSleepWakePower()
        ];

        Assert.All(powers, static power => Assert.False(power.ShouldScaleInMultiplayer));
    }

    [Fact]
    public void WindLocalizationIncludesEveryVisibleIdentity()
    {
        var englishMonsters = ReadJson("SakuraMod/localization/eng/monsters.json");
        var chineseMonsters = ReadJson("SakuraMod/localization/zhs/monsters.json");
        var englishPowers = ReadJson("SakuraMod/localization/eng/powers.json");
        var chinesePowers = ReadJson("SakuraMod/localization/zhs/powers.json");
        var monsterEntries = WindEnemyCatalog.MonsterTypes
            .Select(RegressionTestHarness.RegisteredModelEntry)
            .ToArray();
        Assert.All(monsterEntries, entry =>
        {
            Assert.True(englishMonsters.ContainsKey($"{entry}.name"), $"Missing English {entry}.name.");
            Assert.True(chineseMonsters.ContainsKey($"{entry}.name"), $"Missing Chinese {entry}.name.");
            Assert.Contains(englishMonsters.Keys, key => key.StartsWith($"{entry}.moves.", StringComparison.Ordinal));
            Assert.Contains(chineseMonsters.Keys, key => key.StartsWith($"{entry}.moves.", StringComparison.Ordinal));
        });

        const string illusionTitle = "SAKURA_MOD_POWER_ILLUSION_IDENTITY_POWER.title";
        const string illusionDescription = "SAKURA_MOD_POWER_ILLUSION_IDENTITY_POWER.description";
        Assert.True(englishPowers.ContainsKey(illusionTitle));
        Assert.True(chinesePowers.ContainsKey(illusionTitle));
        Assert.True(englishPowers.ContainsKey(illusionDescription));
        Assert.True(chinesePowers.ContainsKey(illusionDescription));
    }

    private static Dictionary<string, JsonElement> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))!;
}
