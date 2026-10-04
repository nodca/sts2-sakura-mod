using MegaCrit.Sts2.Core.Entities.Relics;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;

public sealed class SakuraRelicParitySuite
{
    [Fact]
    public void RelicCatalogRegistersTheCostumeRelicsWithoutExpandingCreateRewards()
    {
        var allTypes = SakuraRelicCatalog.AllRelicTypes();
        var rewardableTypes = SakuraRelicCatalog.RewardableRelicTypes;

        RegressionTestHarness.Require(
            allTypes.Length == 21
            && allTypes.Distinct().Count() == 21
            && allTypes.Contains(typeof(ClassicDarknessWandRelic))
            && allTypes.Contains(typeof(ClassicGemBroochRelic))
            && allTypes.Contains(typeof(ClassicRedCapeRelic))
            && allTypes.Contains(typeof(ClassicPinkTransformationCostumeRelic))
            && allTypes.Contains(typeof(ClassicFrogRaincoatRelic)),
            "Expected the relic registry to contain the event-exclusive Monster relic and all three Ancient costume relics exactly once.");
        RegressionTestHarness.Require(
            rewardableTypes.Count == 11
            && !rewardableTypes.Contains(typeof(ClassicDarknessWandRelic))
            && !rewardableTypes.Contains(typeof(ClassicGemBroochRelic))
            && !rewardableTypes.Contains(typeof(ClassicRedCapeRelic))
            && !rewardableTypes.Contains(typeof(ClassicPinkTransformationCostumeRelic))
            && !rewardableTypes.Contains(typeof(ClassicFrogRaincoatRelic)),
            "Expected special, event-exclusive, and costume relics to stay outside Sakura Create's Common/Uncommon/Rare relic list.");
        RegressionTestHarness.Require(
            new ClassicDarknessWandRelic().Rarity == RelicRarity.Ancient
            && new ClassicGemBroochRelic().Rarity == RelicRarity.Shop
            && new ClassicRedCapeRelic().Rarity == RelicRarity.Ancient
            && new ClassicPinkTransformationCostumeRelic().Rarity == RelicRarity.Ancient
            && new ClassicFrogRaincoatRelic().Rarity == RelicRarity.Ancient
            && new ClassicMonsterRelic().Rarity == RelicRarity.Rare,
            "Expected special and costume relics to retain their intended rarities.");
        RegressionTestHarness.Require(
            !rewardableTypes.Contains(typeof(ClassicMonsterRelic)),
            "Expected Monster to remain obtainable only through its dedicated event.");
    }

    [Fact]
    public void AncientCostumeEffectsRespectTheirEraAndTriggerBoundaries()
    {
        var clowEligible = new ClowSword();
        var clowWithoutExtraEffect = new ClowLight();
        var sakuraCard = new SakuraSword();

        RegressionTestHarness.Require(
            ClassicRedCapeRelic.IsEligible(clowEligible)
            && !ClassicRedCapeRelic.IsEligible(clowWithoutExtraEffect)
            && !ClassicRedCapeRelic.IsEligible(sakuraCard)
            && ClassicRedCapeRelic.CanActivateFreeExtraEffect(
                activatedThisCombat: false,
                ownerMatches: true,
                isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(
                activatedThisCombat: true,
                ownerMatches: true,
                isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(
                activatedThisCombat: false,
                ownerMatches: false,
                isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(
                activatedThisCombat: false,
                ownerMatches: true,
                isEligible: false),
            "Expected Red Cape to grant only eligible Clow Cards a free Extra Effect.");
        RegressionTestHarness.Require(
            SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: true, hasPinkTransformationCostume: false)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: true, hasPinkTransformationCostume: true)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: false, hasPinkTransformationCostume: false)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: false, hasPinkTransformationCostume: true),
            "Expected Pink Transformation Costume to suppress only normal Sakura-card Void generation.");

        var six = ClassicFrogRaincoatRelic.Accumulate(0, 6, 3);
        var seven = ClassicFrogRaincoatRelic.Accumulate(0, 7, 3);
        var carried = ClassicFrogRaincoatRelic.Accumulate(2, 2, 3);
        RegressionTestHarness.Require(
            six == (0, 2)
            && seven == (1, 2)
            && carried == (1, 1),
            "Expected Frog Raincoat to queue one Remind per three Memory entries and preserve its remainder.");
    }

    [Fact]
    public void FrogRaincoatReferencesTheRemindKeywordHoverTip()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicFrogRaincoatRelic.cs"));
        var zhs = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/relics.json"));
        var eng = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/relics.json"));

        RegressionTestHarness.Require(
            source.Contains("SakuraCardHoverTips.StaticTip(SakuraCardHoverTips.RemindTipKey)", StringComparison.Ordinal)
            && zhs.Contains("[gold]想起[/gold]", StringComparison.Ordinal)
            && eng.Contains("[gold]Remind[/gold]", StringComparison.Ordinal),
            "Expected Frog Raincoat to color Remind gold and expose its registered static hover tip.");
    }

    [Fact]
    public void WandChargePolicyCombinesEachEligibleBonusOnce()
    {
        RegressionTestHarness.Require(
            ClassicSealedWandRelic.ChargeGainForDeath(3, 2, 2, 2, false, false, false) == 3
            && ClassicSealedWandRelic.ChargeGainForDeath(3, 2, 2, 2, true, false, false) == 5
            && ClassicSealedWandRelic.ChargeGainForDeath(3, 2, 2, 2, false, true, false) == 5
            && ClassicSealedWandRelic.ChargeGainForDeath(3, 2, 2, 2, false, false, true) == 5
            && ClassicSealedWandRelic.ChargeGainForDeath(3, 2, 2, 2, true, true, true) == 9,
            "Expected base, Elite/Boss, Seal, and Darkness Wand charge bonuses to combine exactly once.");
    }

    [Fact]
    public void SealedWandDeathDedupeKeepsEachOwnerWhenCloneStateIsShared()
    {
        HashSet<(ulong PlayerNetId, uint CombatId)> sharedDeaths = [];

        RegressionTestHarness.Require(
            ClassicSealedWandRelic.TryRecordDeathForOwner(sharedDeaths, 1, 17)
            && ClassicSealedWandRelic.TryRecordDeathForOwner(sharedDeaths, 2, 17)
            && !ClassicSealedWandRelic.TryRecordDeathForOwner(sharedDeaths, 1, 17)
            && !ClassicSealedWandRelic.TryRecordDeathForOwner(sharedDeaths, 2, 17),
            "Expected shallow-cloned Sealed Wands to reward each owner once for the same enemy death.");
    }

    [Fact]
    public void SealedWandDeathRewardsUseOneDeterministicLifecyclePath()
    {
        var runHooks = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraRunHooks.cs"));

        RegressionTestHarness.Require(
            runHooks.Contains("SubscribeLifecycle<CreatureDiedEvent>", StringComparison.Ordinal)
            && runHooks.Contains("ApplyDeathCharge", StringComparison.Ordinal)
            && !runHooks.Contains("RitsuLibManagedNetActions", StringComparison.Ordinal)
            && !runHooks.Contains("DeferredDeathRewards", StringComparison.Ordinal),
            "Expected Sealed Wand death charge to derive locally from one lifecycle event without a second network action.");
    }

    [Fact]
    public void SealedWandConversionThresholdsIncreaseByTwenty()
    {
        RegressionTestHarness.Require(
            ClassicSealedWandRelic.TriggerThresholdFor(0) == 40
            && ClassicSealedWandRelic.TriggerThresholdFor(1) == 60
            && ClassicSealedWandRelic.TriggerThresholdFor(2) == 80,
            "Expected Sealed Wand conversion thresholds to be 40, 60, and 80 after each Sakura conversion.");
    }

    [Fact]
    public void EveryTurnGrantingRelicUsesTheOneSharedPersistentGrant()
    {
        var spellTurn = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Spells/SpellTurn.cs"));
        var moonBell = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicMoonBellRelic.cs"));
        var wand = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicSealedWandRelic.cs"));

        RegressionTestHarness.Require(
            spellTurn.Contains("internal static async Task GrantPersistentTurn(", StringComparison.Ordinal)
            && spellTurn.Contains("RunState.CreateCard<SpellTurn>(owner)", StringComparison.Ordinal)
            && spellTurn.Contains("CardPileCmd.Add(deckCard, PileType.Deck", StringComparison.Ordinal)
            && spellTurn.Contains("handCard.DeckVersion = deckCard", StringComparison.Ordinal),
            "Expected SpellTurn to own the one grant that creates a deck body plus a hand copy linked through DeckVersion.");

        RegressionTestHarness.Require(
            moonBell.Contains("SpellTurn.GrantPersistentTurn(", StringComparison.Ordinal)
            && !moonBell.Contains("CreateCard<SpellTurn>", StringComparison.Ordinal)
            && wand.Contains("SpellTurn.GrantPersistentTurn(", StringComparison.Ordinal)
            && !wand.Contains("CreateCard<SpellTurn>", StringComparison.Ordinal),
            "Expected Moon Bell and the wands to delegate to the shared grant instead of creating their own Turn copies.");

        Type[] wandTypes =
        [
            typeof(ClassicSealedWandRelic),
            typeof(ClassicStarWandRelic),
            typeof(ClassicUltimateWandRelic)
        ];
        const System.Reflection.BindingFlags DeclaredGrant =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        RegressionTestHarness.Require(
            wandTypes.All(type =>
                type.GetMethod("AddGeneratedTurnCard", DeclaredGrant)?.DeclaringType
                    == typeof(ClassicSealedWandRelic)),
            "Expected every wand to inherit the single grant path instead of adding a second Turn of its own.");
    }

    [Fact]
    public void EveryTurnGrantingRelicTextStatesTheUnplayedTurnIsKept()
    {
        static string LineFor(IEnumerable<string> lines, string key) =>
            lines.FirstOrDefault(line => line.Contains($"\"{key}\"", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing localization key {key}.");

        var zhsLines = File.ReadAllLines(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/relics.json"));
        var engLines = File.ReadAllLines(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/relics.json"));

        var keys = new[]
        {
            "SAKURA_MOD_RELIC_CLASSIC_SEALED_WAND_RELIC.description",
            "SAKURA_MOD_RELIC_CLASSIC_STAR_WAND_RELIC.description",
            "SAKURA_MOD_RELIC_CLASSIC_ULTIMATE_WAND_RELIC.description",
            "SAKURA_MOD_RELIC_CLASSIC_MOON_BELL_RELIC.description"
        };

        const string PersistenceClause = "保留到之后的战斗";
        string[] engPersistenceClauses = ["is kept for later combats", "carry over to later combats", "carries over to later combats"];

        RegressionTestHarness.Require(
            keys.All(key => LineFor(zhsLines, key).Contains(PersistenceClause, StringComparison.Ordinal))
            && keys.All(key => engPersistenceClauses.Any(clause => LineFor(engLines, key).Contains(clause, StringComparison.Ordinal))),
            "Expected every Turn-granting relic to state that an unplayed Turn is kept for later combats.");

        RegressionTestHarness.Require(
            keys.All(key => !LineFor(zhsLines, key).Contains("加入手牌和牌组", StringComparison.Ordinal))
            && keys.All(key => !LineFor(engLines, key).Contains("to your hand and deck", StringComparison.Ordinal)),
            "Expected no Turn-granting relic to describe its Turn as entering the hand and deck at once.");
    }

    [Fact]
    public void SakuraReturnRefundsSeventyFivePercentOfThePreviousConversionCost()
    {
        var returnCard = new SakuraReturn();

        RegressionTestHarness.Require(
            ClassicSealedWandRelic.DefaultReturnRechargeAmount == 30
            && returnCard.DynamicVars["Magic"].IntValue == 30
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(40, 40, 20) == 30
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(60, 40, 20) == 30
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(80, 40, 20) == 45
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(35, 35, 20) == 26
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(55, 35, 20) == 26,
            "Expected Sakura Return previews and runtime refunds to use 75% of the previous wand conversion cost.");
    }

    [Fact]
    public void GemBroochKeepsTheFirstSwordAndShieldAndRemovesLaterCopies()
    {
        var firstSword = new ClowSword();
        var secondSword = new ClowSword();
        var firstShield = new ClowShield();
        var secondShield = new ClowShield();
        var thirdShield = new ClowShield();

        var plan = ClassicGemBroochRelic.BuildDeckPlan(
            [firstSword, firstShield, secondSword, secondShield, thirdShield]);

        RegressionTestHarness.Require(
            plan.RetainedCards.SequenceEqual([firstSword, firstShield])
            && plan.Duplicates.SequenceEqual([secondSword, secondShield, thirdShield]),
            "Expected Gem Brooch to preserve stable deck order while retaining one Clow Sword and one Clow Shield.");

        var emptyPlan = ClassicGemBroochRelic.BuildDeckPlan([]);
        var swordOnlyPlan = ClassicGemBroochRelic.BuildDeckPlan([firstSword, secondSword]);
        RegressionTestHarness.Require(
            emptyPlan.RetainedCards.Count == 0
            && emptyPlan.Duplicates.Count == 0
            && swordOnlyPlan.RetainedCards.SequenceEqual([firstSword])
            && swordOnlyPlan.Duplicates.SequenceEqual([secondSword]),
            "Expected Gem Brooch to handle missing card identities without null placeholders.");
    }

    [Fact]
    public void AddedRelicAssetsAndLocalizationExist()
    {
        string[] assetPaths =
        [
            "SakuraMod/images/relics/darkness_wand.png",
            "SakuraMod/images/relics/darkness_wand_outline.png",
            "SakuraMod/images/relics/big/darkness_wand.png",
            "SakuraMod/images/relics/gem_brooch.png",
            "SakuraMod/images/relics/gem_brooch_outline.png",
            "SakuraMod/images/relics/big/gem_brooch.png",
            "SakuraMod/images/relics/red_cape.png",
            "SakuraMod/images/relics/red_cape_outline.png",
            "SakuraMod/images/relics/big/red_cape.png",
            "SakuraMod/images/relics/frog_raincoat.png",
            "SakuraMod/images/relics/frog_raincoat_outline.png",
            "SakuraMod/images/relics/big/frog_raincoat.png",
            "SakuraMod/images/relics/pink_transformation_costume.png",
            "SakuraMod/images/relics/pink_transformation_costume_outline.png",
            "SakuraMod/images/relics/big/pink_transformation_costume.png"
        ];

        RegressionTestHarness.Require(
            assetPaths.All(path =>
            {
                var asset = RegressionTestHarness.FindRepoFile(path);
                return File.Exists(asset) && File.Exists($"{asset}.import");
            }),
            "Expected added relics' normal, outline, and big art plus Godot imports to exist.");
        RegressionTestHarness.RequireRegisteredRelicLocalizationKeys(
            "SakuraMod/localization/eng/relics.json",
            [
                typeof(ClassicDarknessWandRelic),
                typeof(ClassicGemBroochRelic),
                typeof(ClassicRedCapeRelic),
                typeof(ClassicPinkTransformationCostumeRelic),
                typeof(ClassicFrogRaincoatRelic)
            ]);
        RegressionTestHarness.RequireRegisteredRelicLocalizationKeys(
            "SakuraMod/localization/zhs/relics.json",
            [
                typeof(ClassicDarknessWandRelic),
                typeof(ClassicGemBroochRelic),
                typeof(ClassicRedCapeRelic),
                typeof(ClassicPinkTransformationCostumeRelic),
                typeof(ClassicFrogRaincoatRelic)
            ]);
    }
}
