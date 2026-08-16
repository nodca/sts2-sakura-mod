using System.Buffers.Binary;
using System.Text.Json;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.Acts;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Routing;

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
        Assert.Equal(SleepingAffliction.OverlayScenePath, new SleepingAffliction().AssetProfile.OverlayScenePath);

        var sleepingSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/CardState/WindSleepingCards.cs"));
        Assert.Contains("CardCmd.Afflict<SleepingAffliction>", sleepingSource);
        Assert.Contains("CardCmd.ClearAffliction(card)", sleepingSource);
        Assert.DoesNotContain("CardCapability", sleepingSource);
        Assert.DoesNotContain("AddCapability", sleepingSource);

        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/cards/overlays/sleeping_affliction.tscn"));
        Assert.Contains("SleepingAffliction", scene);
        Assert.Contains("LeftEyelid", scene);
        Assert.Contains("RightEyelid", scene);
        Assert.Contains("settle_breathe", scene);
        Assert.Contains("mouse_filter = 2", scene);
    }

    [Fact]
    public void AcceptedBaseAndAscensionValuesRemainStable()
    {
        Assert.Equal((220, 235, 8, 9, 3, 36, 42),
            (FlyMonster.BaseHp, FlyMonster.ToughHp, FlyMonster.BaseHighAttackDamage,
             FlyMonster.DeadlyHighAttackDamage, FlyMonster.HighAttackHits,
             FlyMonster.BaseDiveDamage, FlyMonster.DeadlyDiveDamage));
        Assert.Equal((210, 225, 18, 21, 2, 30, 36),
            (IllusionMonster.BaseHp, IllusionMonster.ToughHp, IllusionMonster.BaseBeguilingDamage,
             IllusionMonster.DeadlyBeguilingDamage, IllusionMonster.VulnerableAmount,
             IllusionMonster.BaseLuredFallDamage, IllusionMonster.DeadlyLuredFallDamage));
        Assert.Equal((420, 440, 5, 6, 5, 20, 24, 30, 36),
            (WindyMonster.BaseHp, WindyMonster.ToughHp, WindyMonster.BaseMultiDamage,
             WindyMonster.DeadlyMultiDamage, WindyMonster.MultiHits, WindyMonster.BaseSingleDamage,
             WindyMonster.DeadlySingleDamage, WindyMonster.BaseHeavyDamage, WindyMonster.DeadlyHeavyDamage));
        Assert.Equal((65, 70, 8, 10, 3, 4),
            (DashMonster.BaseHp, DashMonster.ToughHp, DashMonster.BaseDamage,
             DashMonster.DeadlyDamage, DashMonster.BaseGrowth, DashMonster.DeadlyGrowth));
        Assert.Equal((60, 65, 2), (FloatMonster.BaseHp, FloatMonster.ToughHp, FloatMonster.BlockPerDraw));
        Assert.Equal((55, 60), (SleepMonster.BaseHp, SleepMonster.ToughHp));
        Assert.Equal((1, 1),
            (new IllusionProjectionMonster().MinInitialHp, new IllusionProjectionMonster().MaxInitialHp));
    }

    [Fact]
    public void WindEncounterSetupAddsOnlyEnemyOwnedState()
    {
        AssertEnemyOwnedSetup(
            "SakuraModCode/FourthAct/Wind/Models/FlyMonster.cs",
            ["PowerCmd.Apply<SoarPower>"]);
        AssertEnemyOwnedSetup(
            "SakuraModCode/FourthAct/Wind/Models/IllusionMonsters.cs",
            ["PowerCmd.Apply<IllusionIdentityPower>"]);
        AssertEnemyOwnedSetup(
            "SakuraModCode/FourthAct/Wind/Models/WindyMonster.cs",
            ["PowerCmd.Apply<WindSovereigntyPower>", "PowerCmd.Apply<WindyBattlePower>"]);
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

    [Fact]
    public void WindGroundingKeepsTheEncounterCameraAndRooftopUnchanged()
    {
        WindEncounterTemplate[] encounters =
        [
            new FlyEncounter(),
            new IllusionEncounter(),
            new WindyEncounter()
        ];

        Assert.All(encounters, static encounter =>
        {
            var offset = encounter.GetCameraOffset();
            Assert.Equal(0f, offset.X);
            Assert.Equal(0f, offset.Y);
            Assert.True(WindCombatGrounding.AppliesTo(encounter));
        });

        var dark = new DarkEncounter();
        Assert.Equal(0f, dark.GetCameraOffset().Y);
        Assert.False(WindCombatGrounding.AppliesTo(dark));
        Assert.Equal(0f, WindCombatGrounding.AllyOffset.X);
        Assert.Equal(110f, WindCombatGrounding.AllyOffset.Y);
        Assert.Equal(80f, WindCombatGrounding.EnemyOffset.X);
        Assert.Equal(110f, WindCombatGrounding.EnemyOffset.Y);

        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/rooftop/rooftop_base.tscn"));
        Assert.Contains("offset_left = -1382.4", layerScene);
        Assert.Contains("offset_top = -648.0", layerScene);
        Assert.Contains("offset_right = 1382.4", layerScene);
        Assert.Contains("offset_bottom = 648.0", layerScene);

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Encounters/WindEncounters.cs"));
        Assert.Contains("Position = position + WindCombatGrounding.EnemyOffset", encounterSource);

        var groundingSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/WindCombatGrounding.cs"));
        Assert.Contains("AllyOffset = new(0f, 110f)", groundingSource);
        Assert.Contains("EnemyOffset = new(80f, 110f)", groundingSource);
        Assert.Contains("nameof(NCombatRoom.PositionPlayersAndPets)", groundingSource);
        Assert.Contains("node.Position += AllyOffset", groundingSource);
    }

    [Fact]
    public void WindyDazedUsesTheActiveCombatGeneratedCardLifecycle()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Powers/WindCombatPowers.cs"));

        Assert.Contains("CombatState.CreateCard<Dazed>(player)", source);
        Assert.Contains("SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(", source);
        Assert.DoesNotContain("RunState.CreateCard<Dazed>", source);
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
    public void WindWallFeedbackRunsOnlyForActualCappedDamage()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Powers/WindCombatPowers.cs"));
        var capStart = source.IndexOf("public override decimal ModifyDamageCap", StringComparison.Ordinal);
        var afterStart = source.IndexOf("public override async Task AfterModifyingDamageAmount", capStart, StringComparison.Ordinal);
        var nextPower = source.IndexOf("public sealed class WindyNextActionDamagePower", afterStart, StringComparison.Ordinal);

        Assert.DoesNotContain("BeginWindWallInterception", source[capStart..afterStart]);
        Assert.Contains("BeginWindWallInterception", source[afterStart..nextPower]);
    }

    [Fact]
    public void EveryWindyActionForecastsAndAppliesOneWindBindBatch()
    {
        var windy = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindyMonster.cs"));
        var attendants = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs"));

        Assert.Equal(7, windy.Split("new DebuffIntent(strong: true)").Length - 1);
        Assert.All(Enum.GetValues<WindyAction>(), static action =>
            Assert.Equal(WindEnemyRules.BindPerPlayer, WindEnemyRules.WindBindForAction(action)));
        Assert.Equal(0, WindEnemyRules.WindBindForAttendantAction());
        Assert.Equal(4, windy.Split("await ApplyWindBind(action);").Length - 1);
        Assert.Contains("silent: false", windy);
        Assert.DoesNotContain("WindBindPower", attendants);
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
    public void AcceptedWindAssetsAndImportsExist()
    {
        Assert.Equal(18, WindEnemyAssets.All.Distinct().Count());
        Assert.Contains(WindEnemyAssets.IllusionCast, WindEnemyAssets.All);
        Assert.Contains(WindEnemyAssets.WindyAction, WindEnemyAssets.All);
        Assert.Contains(WindEnemyAssets.DashAttack, WindEnemyAssets.All);
        Assert.Contains(WindEnemyAssets.SleepCast, WindEnemyAssets.All);
        foreach (var resourcePath in WindEnemyAssets.All)
        {
            var relativePath = resourcePath.Replace("res://SakuraMod/", "SakuraMod/", StringComparison.Ordinal);
            var file = RegressionTestHarness.FindRepoFile(relativePath);
            Assert.True(File.Exists(file), $"Missing {relativePath}.");
            Assert.True(File.Exists($"{file}.import"), $"Missing {relativePath}.import.");
        }
    }

    [Fact]
    public void FourthActEncountersAreScopedAndRegisterTheirCompleteMonsterSet()
    {
        Assert.Equal(7, WindEnemyCatalog.MonsterTypes.Distinct().Count());

        var registration = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraContentRegistration.cs"));
        Assert.Contains("registry.RegisterActEncounter(typeof(SakuraFourthAct), encounterType);", registration);
        Assert.Contains("WindEnemyCatalog.MonsterTypes.Concat(DarkEnemyCatalog.MonsterTypes)", registration);
        Assert.DoesNotContain("RegisterGlobalEncounter<WindyEncounter>", registration);
    }

    [Fact]
    public void EveryWindEncounterUsesOneStaticRooftopBackgroundLayer()
    {
        Assert.Equal(
            [FourthActCombatBackgrounds.WindRooftopLayerPath],
            FourthActCombatBackgrounds.WindRooftopLayers);

        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/rooftop/rooftop_base.tscn"));
        var texture = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/rooftop/rooftop_base.png");
        var textureImport = File.ReadAllText($"{texture}.import");
        var header = File.ReadAllBytes(texture).AsSpan(0, 26);

        Assert.Equal(
            "res://scenes/backgrounds/glory/glory_background.tscn",
            FourthActCombatBackgrounds.MainScenePath);
        Assert.Contains(FourthActCombatBackgrounds.WindRooftopTexturePath, layerScene);
        Assert.DoesNotContain("AnimationPlayer", layerScene);
        Assert.DoesNotContain("ShaderMaterial", layerScene);
        Assert.DoesNotContain("VideoStream", layerScene);
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(960, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(8, header[24]);
        Assert.Equal(2, header[25]);
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/rooftop/rooftop_base.png\"",
            textureImport);
        Assert.Contains("mipmaps/generate=false", textureImport);

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Encounters/WindEncounters.cs"));
        var baseStart = encounterSource.IndexOf(
            "public abstract class WindEncounterTemplate", StringComparison.Ordinal);
        var flyStart = encounterSource.IndexOf(
            "public sealed class FlyEncounter", StringComparison.Ordinal);
        var baseDeclaration = encounterSource[baseStart..flyStart];
        Assert.Contains("UseProgrammaticCombatBackground => true", baseDeclaration);
        Assert.Contains("FourthActCombatBackgrounds.CreateWindRooftop()", baseDeclaration);

        var windyDeclaration = encounterSource[encounterSource.IndexOf(
            "public sealed class WindyEncounter", StringComparison.Ordinal)..];
        Assert.DoesNotContain("UseProgrammaticCombatBackground", windyDeclaration);
        Assert.DoesNotContain("FourthActCombatBackgrounds.CreateWindRooftop()", windyDeclaration);
    }


    private static void AssertEnemyOwnedSetup(string relativePath, IReadOnlyList<string> expectedPowers)
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
        var start = source.IndexOf("public override async Task AfterAddedToRoom()", StringComparison.Ordinal);
        var end = source.IndexOf("protected override MonsterMoveStateMachine", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not isolate setup method in {relativePath}.");
        var setup = source[start..end];
        Assert.Contains("Creature, 1, Creature, null, true", setup);
        Assert.DoesNotContain("CombatState.Players", setup);
        foreach (var expected in expectedPowers)
            Assert.Contains(expected, setup);
    }

    [Fact]
    public void WindLocalizationIncludesEveryVisibleIdentityAndExactIllusionText()
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
        Assert.DoesNotContain(englishMonsters.Keys, static key => key.StartsWith("SAKURAMOD-WINDY_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-FLY_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-ILLUSION_", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-DASH_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-FLOAT_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-SLEEP_MONSTER", StringComparison.Ordinal));
        Assert.DoesNotContain(chineseMonsters.Keys, static key => key.StartsWith("SAKURAMOD-WINDY_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-FLY_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-ILLUSION_", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-DASH_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-FLOAT_MONSTER", StringComparison.Ordinal)
            || key.StartsWith("SAKURAMOD-SLEEP_MONSTER", StringComparison.Ordinal));

        const string illusionTitle = "SAKURA_MOD_POWER_ILLUSION_IDENTITY_POWER.title";
        const string illusionDescription = "SAKURA_MOD_POWER_ILLUSION_IDENTITY_POWER.description";
        Assert.Equal("Illusion", englishPowers[illusionTitle].GetString());
        Assert.Equal("幻象", chinesePowers[illusionTitle].GetString());
        Assert.True(englishPowers.ContainsKey(illusionDescription));
        Assert.Equal(
            "幻的真身或假身。假身不会造成伤害，其受到伤害或被给予状态后会消失。假身和真身的位置每回合可能交换。",
            chinesePowers[illusionDescription].GetString());
    }

    private static Dictionary<string, JsonElement> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))!;
}
