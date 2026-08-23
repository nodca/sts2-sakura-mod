using System.Buffers.Binary;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Afflictions;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;

public sealed class DarkEndpointSuite
{
    [Fact]
    public void AcceptedBossNumbersAndCurvesRemainStable()
    {
        Assert.Equal((520, 545, 50, 55, 3, 2, 3, 3, 0.75m, 0.25m, 0.6m, 2, 5),
            (DarkEnemyRules.BaseHp, DarkEnemyRules.ToughHp,
             DarkEnemyRules.BaseUltimateDamage, DarkEnemyRules.DeadlyUltimateDamage,
             DarkEnemyRules.MicroLightsPerDraw, DarkEnemyRules.MaxHandSizeReduction,
             DarkEnemyRules.InitialVeilLayers, DarkEnemyRules.MicroLightThreshold,
             DarkEnemyRules.InitialVeilDamageReduction,
             DarkEnemyRules.VeilDamageReductionPerLayer,
             DarkEnemyRules.TransitionHpRatio, DarkEnemyRules.VeilBreakPlayerSides,
             DarkEnemyRules.MaximumNight));

        Assert.Equal([0.25m, 0.5m, 0.75m, 1m],
            new[] { 3, 2, 1, 0 }.Select(DarkEnemyRules.VeilDamageMultiplier));
        Assert.Equal(1m, DarkEnemyRules.VeilDamageMultiplier(-1));
        Assert.Equal(0.25m, DarkEnemyRules.VeilDamageMultiplier(4));
        Assert.Equal(8, DarkEnemyRules.ModifyMaxHandSize(10));
        Assert.Equal(0, DarkEnemyRules.ModifyMaxHandSize(1));
        Assert.Equal(16, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 1, false));
        Assert.Equal(31, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 4, false));
        Assert.Equal(47, DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, 5, true));
        Assert.Equal([12, 15, 18, 21, 24], Enumerable.Range(1, 5).Select(DarkEnemyRules.Block));
        Assert.Equal((0, 0), DarkEnemyRules.ConfinementDebuffs(1));
        Assert.Equal((1, 0), DarkEnemyRules.ConfinementDebuffs(2));
        Assert.Equal((1, 1), DarkEnemyRules.ConfinementDebuffs(4));
        Assert.Equal([0, 1, 2, 3, 4, 5, 5], Enumerable.Range(0, 7).Select(DarkEnemyRules.VisibleNightRegions));
        Assert.Equal(DarkVeilVisualMode.Membrane,
            DarkVeilVisualProjection.Resolve(DarkPhase.Veiled, 3, 0));
        Assert.Equal(DarkVeilVisualMode.RetainCurrent,
            DarkVeilVisualProjection.Resolve(DarkPhase.Veiled, 0, 0));
        Assert.Equal(DarkVeilVisualMode.Remnants,
            DarkVeilVisualProjection.Resolve(DarkPhase.Veiled, 0, 2));
        Assert.Equal(DarkVeilVisualMode.None,
            DarkVeilVisualProjection.Resolve(DarkPhase.TransitionPending, 0, 0));
    }

    [Fact]
    public void MicroLightConsumptionPreservesRemainder()
    {
        Assert.Equal(3, DarkEnemyRules.ConsumeMicroLight(9, 6));
        Assert.Equal(0, DarkEnemyRules.ConsumeMicroLight(2, 6));
        Assert.Equal(DarkRegularAction.NonConfinement, DarkEnemyRules.Toggle(DarkRegularAction.Confinement));
        Assert.Equal(DarkRegularAction.Confinement, DarkEnemyRules.Toggle(DarkRegularAction.NonConfinement));
    }

    [Fact]
    public void OpeningPressureAndFadePoliciesAreExplicit()
    {
        Assert.All(Enum.GetValues<DarkRegularAction>(), static action =>
        {
            Assert.Equal(0, DarkEnemyRules.MicroLightsFromAction(action));
            Assert.False(DarkEnemyRules.AdvertisesStatusIntent(action));
        });

        Assert.True(SakuraFadeCardLifecycle.IsEligible(true, true, true));
        Assert.False(SakuraFadeCardLifecycle.IsEligible(false, true, true));
        Assert.False(SakuraFadeCardLifecycle.IsEligible(true, false, true));
        Assert.False(SakuraFadeCardLifecycle.IsEligible(true, true, false));

        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraFadeCardLifecycle.cs"));
        Assert.Contains("CardPile.GetCards(player, PileType.Hand).ToArray()", source);
        Assert.Contains("Hook.ShouldEtherealTrigger(combatState, card)", source);
        Assert.Contains("TemporaryDissolveVfx.PlayFade(card)", source);
        Assert.Contains("CardPileCmd.RemoveFromCombat(card, skipVisuals: true)", source);
        Assert.DoesNotContain("CardCmd.Exhaust", source);
    }

    [Fact]
    public void MicroLightIsRegisteredCombatOnlyClearLayoutStatus()
    {
        var card = new MicroLight();
        Assert.Equal(1, card.EnergyCost.Canonical);
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal(CardRarity.Basic, card.Rarity);
        Assert.Equal(TargetType.Self, card.TargetType);
        Assert.False(card.CanBeGeneratedInCombat);
        Assert.Equal([SakuraKeywords.Fade, CardKeyword.Exhaust], card.CanonicalKeywords);
        Assert.Equal(1, card.DynamicVars["DarkVeilPower"].IntValue);
        Assert.Equal(1, card.DynamicVars["DarkLightPower"].IntValue);
        Assert.Equal(CardType.Skill, ((ISakuraClearLayoutCard)card).DescriptionShapeCardType);
        Assert.IsAssignableFrom<ISakuraForgottenImmune>(card);
        card.MakeTemporary();
        Assert.False(card.IsTemporary());
        Assert.Contains(typeof(MicroLight), SakuraContentRegistration.AllCardTypesForRegistration());
        Assert.Contains(typeof(MicroLight), SakuraContentRegistration.ClearLayoutOnlyCardTypes);
        Assert.DoesNotContain(typeof(MicroLight), ClassicSakuraCardPool.AllCardTypesForPool());
    }

    [Fact]
    public void EncounterAndPowerHookContractsAreExplicit()
    {
        var encounter = new DarkEncounter();
        Assert.Equal(RoomType.Boss, encounter.RoomType);
        Assert.False(encounter.ShouldGiveRewards);
        Assert.True(encounter.IsValidForAct(new SakuraFourthAct()));
        Assert.False(encounter.IsValidForAct(new Glory()));
        Assert.Equal(["BOSS"], encounter.Slots);
        Assert.Equal(typeof(DarkEncounter), DarkEnemyCatalog.EndpointEncounterType);
        Assert.Equal([typeof(DarkMonster)], DarkEnemyCatalog.MonsterTypes);

        Assert.True(RegressionTestHarness.DeclaresMethod<DarkBattlePower>("BeforeHandDraw"));
        Assert.True(RegressionTestHarness.DeclaresMethod<DarkBattlePower>("AfterDamageReceived"));
        Assert.True(RegressionTestHarness.DeclaresMethod<DarkBattlePower>("BeforeSideTurnEnd"));
        Assert.True(RegressionTestHarness.DeclaresMethod<DarkBattlePower>("AfterSideTurnEnd"));
        Assert.True(RegressionTestHarness.DeclaresMethod<DarkConfinementSelectionPower>("AfterPlayerTurnStart"));
        Assert.All(new PowerModel[]
        {
            new DarkLightPower(), new DarkNightPower(), new DarkVeilPower(),
            new DarkSovereigntyPower(), new DarkBattlePower(), new DarkConfinementSelectionPower()
        }, static power => Assert.False(power.ShouldScaleInMultiplayer));
        Assert.IsAssignableFrom<IMaxHandSizeModifier>(new DarkBattlePower());
        Assert.Equal(PowerType.Debuff, new DarkLightPower().Type);
    }

    [Fact]
    public void DarkEncounterUsesOneApprovedStaticStageLayer()
    {
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/fourth_act/school_stage/dark_base.tscn"));
        var texture = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/school_stage/dark_base.png");
        var textureImport = File.ReadAllText($"{texture}.import");
        var header = File.ReadAllBytes(texture).AsSpan(0, 26);

        Assert.Contains(FourthActCombatBackgrounds.DarkStageTexturePath, layerScene);
        Assert.Contains(FourthActCombatBackgrounds.EternalNightRegionMaskPath, layerScene);
        Assert.Contains(FourthActCombatBackgrounds.EternalNightShaderPath, layerScene);
        Assert.Contains("EternalNightOverlay", layerScene);
        Assert.Contains("ShaderMaterial", layerScene);
        Assert.Contains("texture_filter = 1", layerScene);
        Assert.DoesNotContain("AnimationPlayer", layerScene);
        Assert.DoesNotContain("VideoStream", layerScene);
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(960, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(8, header[24]);
        Assert.Equal(2, header[25]);
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/school_stage/dark_base.png\"",
            textureImport);
        Assert.Contains("mipmaps/generate=false", textureImport);

        var regionMask = RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/backgrounds/fourth_act/school_stage/eternal_night_regions.png");
        var regionHeader = File.ReadAllBytes(regionMask).AsSpan(0, 26);
        var regionImport = File.ReadAllText($"{regionMask}.import");
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(regionHeader[16..20]));
        Assert.Equal(960, BinaryPrimitives.ReadInt32BigEndian(regionHeader[20..24]));
        Assert.Equal(8, regionHeader[24]);
        Assert.Equal(0, regionHeader[25]);
        Assert.Contains(
            "source_file=\"res://SakuraMod/images/backgrounds/fourth_act/school_stage/eternal_night_regions.png\"",
            regionImport);
        Assert.Contains("mipmaps/generate=false", regionImport);

        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/fourth_act/eternal_night.gdshader"));
        Assert.Contains("uniform float night_progress", shader);
        Assert.DoesNotContain("TIME", shader);

        var feedbackVisuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        Assert.Contains("SetEternalNightProgress(0f)", feedbackVisuals);
        Assert.Contains("_eternalNightOverlay.Visible = false", feedbackVisuals);

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Dark/Encounters/DarkEncounter.cs"));
        Assert.Contains("UseProgrammaticCombatBackground => true", encounterSource);
        Assert.Contains("FourthActCombatBackgrounds.CreateDarkStage()", encounterSource);
    }

    [Fact]
    public void DarkSetupOwnsVisibleTrialAndThreeLayerVeilWithoutPlayerMarker()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Dark/Models/DarkMonster.cs"));
        var start = source.IndexOf("public override async Task AfterAddedToRoom()", StringComparison.Ordinal);
        var end = source.IndexOf("public async Task BeginTransition()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var setup = source[start..end];
        Assert.Contains("PowerCmd.Apply<DarkSovereigntyPower>(context, Creature", setup);
        Assert.Contains("PowerCmd.Apply<DarkVeilPower>(context, Creature, DarkEnemyRules.InitialVeilLayers", setup);
        Assert.Contains("PowerCmd.Apply<DarkBattlePower>(context, Creature", setup);
        Assert.DoesNotContain("CombatState.Players", setup);
        Assert.True(new DarkBattlePower().IsVisible);
        Assert.True(new DarkVeilPower().IsVisible);
    }

    [Fact]
    public void SakuraLayoutsKeepNativeAfflictionOverlaysAlive()
    {
        var clear = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClearCardVisualPatch.cs"));
        var classic = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs"));
        var patches = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardVisualPatches.cs"));

        Assert.DoesNotContain("\"_cardOverlay\"", clear);
        Assert.DoesNotContain("\"_overlayContainer\"", clear);
        Assert.DoesNotContain("\"_cardOverlay\"", classic);
        Assert.DoesNotContain("\"_overlayContainer\"", classic);
        Assert.Contains("PatchTarget.Method<NCard>(\"OnAfflictionChanged\"", patches);
        Assert.Contains("AfterNativeAfflictionChanged", patches);
    }

    [Fact]
    public void DarkResourcesAndBilingualLocalizationAreComplete()
    {
        var standee = RegressionTestHarness.FindRepoFile("SakuraMod/images/monsters/fourth_act/dark/dark.png");
        var header = File.ReadAllBytes(standee).AsSpan(0, 26);
        Assert.True(header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.Equal(1536, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
        Assert.Equal(2048, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        Assert.Equal(6, header[25]);

        var portrait = RegressionTestHarness.FindRepoFile("SakuraMod/images/cards/clear_cards/MICRO_LIGHT.png");
        var portraitHeader = File.ReadAllBytes(portrait).AsSpan(0, 26);
        Assert.Equal(787, BinaryPrimitives.ReadInt32BigEndian(portraitHeader[16..20]));
        Assert.Equal(1717, BinaryPrimitives.ReadInt32BigEndian(portraitHeader[20..24]));

        var overlay = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/cards/overlays/dark_confinement.tscn"));
        Assert.Contains("TopShutter", overlay);
        Assert.Contains("BottomShutter", overlay);
        Assert.Contains("LeftShutter", overlay);
        Assert.Contains("RightShutter", overlay);
        Assert.Contains("autoplay = \"pulse\"", overlay);
        Assert.Equal(DarkEnemyAssets.ConfinementOverlay, new DarkConfinementAffliction().AssetProfile.OverlayScenePath);

        foreach (var locale in new[] { "eng", "zhs" })
        {
            var cards = ReadJson($"SakuraMod/localization/{locale}/cards.json");
            var monsters = ReadJson($"SakuraMod/localization/{locale}/monsters.json");
            var powers = ReadJson($"SakuraMod/localization/{locale}/powers.json");
            var keywords = ReadJson($"SakuraMod/localization/{locale}/card_keywords.json");
            var afflictions = ReadJson($"SakuraMod/localization/{locale}/afflictions.json");
            Assert.Contains("SAKURA_MOD_CARD_MICRO_LIGHT.title", cards.Keys);
            Assert.Contains("SAKURA_MOD_MONSTER_DARK_MONSTER.moves.P2_ULTIMATE.title", monsters.Keys);
            Assert.Contains("SAKURA_MOD_POWER_DARK_CONFINEMENT_SELECTION_POWER.selectionPrompt", powers.Keys);
            Assert.Contains("SAKURAMOD-FADE.title", keywords.Keys);
            Assert.Contains("SAKURAMOD-FADE.description", keywords.Keys);
            Assert.Contains("SAKURA_MOD_AFFLICTION_DARK_CONFINEMENT_AFFLICTION.title", afflictions.Keys);

            var microLightDescription = cards["SAKURA_MOD_CARD_MICRO_LIGHT.description"];
            Assert.DoesNotContain(locale == "zhs" ? "消逝" : "Fade", microLightDescription);
            Assert.DoesNotContain(locale == "zhs" ? "消耗" : "Exhaust", microLightDescription);
            if (locale == "zhs")
            {
                Assert.Equal("每层使受到的伤害降低 25%。层数降为 0 时，获得 1 层易伤。",
                    powers["SAKURA_MOD_POWER_DARK_VEIL_POWER.description"]);
                Assert.Equal("达到 3 层时，使永夜降低 1 层。",
                    powers["SAKURA_MOD_POWER_DARK_LIGHT_POWER.description"]);
                Assert.Equal("你的手牌上限减少 2。每回合抽牌前，将 3 张微光加入你的手牌。暗的生命降至 60% 以下时，进入永夜。",
                    powers["SAKURA_MOD_POWER_DARK_BATTLE_POWER.description"]);
                Assert.Equal("回合结束时，若此牌在手牌中，将其移出本场战斗。",
                    keywords["SAKURAMOD-FADE.description"]);
            }
        }
    }

    private static Dictionary<string, string> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
        ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
}
