using MegaCrit.Sts2.Core.Entities.Relics;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;

public sealed class SakuraRelicParitySuite
{
    [Fact]
    public void RelicCatalogKeepsSpecialAndCostumeRelicsOutsideRewardPool()
    {
        var allTypes = SakuraRelicCatalog.AllRelicTypes();
        var rewardableTypes = SakuraRelicCatalog.RewardableRelicTypes;

        RegressionTestHarness.Require(
            allTypes.Distinct().Count() == allTypes.Length
            && allTypes.Contains(typeof(ClassicDarknessWandRelic))
            && allTypes.Contains(typeof(ClassicGemBroochRelic))
            && allTypes.Contains(typeof(ClassicRedCapeRelic))
            && allTypes.Contains(typeof(ClassicPinkTransformationCostumeRelic))
            && allTypes.Contains(typeof(ClassicFrogRaincoatRelic)),
            "Expected the relic registry to contain unique relic types including special and costume relics.");
        RegressionTestHarness.Require(
            !rewardableTypes.Contains(typeof(ClassicDarknessWandRelic))
            && !rewardableTypes.Contains(typeof(ClassicGemBroochRelic))
            && !rewardableTypes.Contains(typeof(ClassicRedCapeRelic))
            && !rewardableTypes.Contains(typeof(ClassicPinkTransformationCostumeRelic))
            && !rewardableTypes.Contains(typeof(ClassicFrogRaincoatRelic))
            && !rewardableTypes.Contains(typeof(ClassicMonsterRelic))
            && !new ClassicMonsterRelic().IsAllowedInShops,
            "Expected special, event-exclusive, and costume relics to stay outside ordinary reward and shop pools.");
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
            && ClassicRedCapeRelic.CanActivateFreeExtraEffect(1, 0, 3, true, true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(1, 1, 3, true, true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(1, 0, 3, false, true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(1, 0, 3, true, false)
            && ClassicRedCapeRelic.CanActivateFreeExtraEffect(3, 2, 3, true, true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(4, 3, 3, true, true),
            "Expected Red Cape to grant the first eligible Clow Card of each active turn a free Extra Effect.");
        RegressionTestHarness.Require(
            SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: true, hasPinkTransformationCostume: false)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: true, hasPinkTransformationCostume: true)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: false, hasPinkTransformationCostume: false)
            && !SakuraFormVoid.ShouldAdd(addsVoidOnNormalSakuraPlay: false, hasPinkTransformationCostume: true),
            "Expected Pink Transformation Costume to suppress only normal Sakura-card Void generation.");
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
    public void SealedWandConversionAndReturnFormulasRemainConsistent()
    {
        RegressionTestHarness.Require(
            ClassicSealedWandRelic.TriggerThresholdFor(0) == 40
            && ClassicSealedWandRelic.TriggerThresholdFor(1) == 60
            && ClassicSealedWandRelic.TriggerThresholdFor(2) == 80
            && ClassicSealedWandRelic.TriggerThresholdFor(3, 35, 10) == 65
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(40, 40, 20) == 30
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(60, 40, 20) == 30
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(80, 40, 20) == 45
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(35, 35, 20) == 26
            && ClassicSealedWandRelic.ReturnRechargeAmountForThreshold(55, 35, 20) == 26,
            "Expected Sealed Wand conversion thresholds and Sakura Return refund calculations to match.");
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
}
