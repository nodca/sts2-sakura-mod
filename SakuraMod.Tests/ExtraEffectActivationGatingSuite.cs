using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using System.Reflection;

public sealed class ExtraEffectActivationGatingSuite
{
    [Fact]
    public void UnifiedPredicateStaysInactiveWithoutACombatOwner()
    {
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.ShouldShowAsActive(new ClowArrow())
            && !SakuraExtraEffectTransaction.ShouldShowAsActive(new ClowRain())
            && !SakuraExtraEffectTransaction.ShouldShowAsActive(new ClowTime())
            && !SakuraExtraEffectTransaction.ShouldShowAsActive(null),
            "Expected canonical Clow cards and a null card not to report an active Extra Effect.");

        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.ShouldShowAsActive(RegressionTestHarness.MutableForCostTest(new ClowArrow()))
            && !SakuraExtraEffectTransaction.ShouldShowAsActive(RegressionTestHarness.MutableForCostTest(new ClowRain()))
            && !SakuraExtraEffectTransaction.ShouldShowAsActive(RegressionTestHarness.MutableForCostTest(new ClowTime())),
            "Expected a mutable Clow card without a combat owner to stay inactive, so previews keep the base behaviour.");
    }

    [Fact]
    public void OwnerlessInstancesKeepTheirNativeTargetingAndPile()
    {
        RegressionTestHarness.Require(
            new ClowArrow().TargetType == TargetType.None
            && RegressionTestHarness.MutableForCostTest(new ClowArrow()).TargetType == TargetType.None,
            "Expected Arrow to stay untargetable outside an active Extra Effect.");

        RegressionTestHarness.Require(
            ResultPileForCardPlay(RegressionTestHarness.MutableForCostTest(new ClowRain())) == PileType.Exhaust
            && ResultPileForCardPlay(RegressionTestHarness.MutableForCostTest(new ClowTime())) == PileType.Exhaust,
            "Expected Rain and Time to keep consuming themselves outside an active Extra Effect.");
    }

    [Fact]
    public void RedCapeFreeActivationStaysScopedToTheFirstEligibleClowCard()
    {
        RegressionTestHarness.Require(
            ClassicRedCapeRelic.IsEligible(new ClowArrow())
            && ClassicRedCapeRelic.IsEligible(new ClowRain())
            && ClassicRedCapeRelic.IsEligible(new ClowTime()),
            "Expected every reworked Clow card to stay eligible for the Red Cape's free Extra Effect.");

        RegressionTestHarness.Require(
            ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 1, activatedTurn: 0, turns: 3, ownerMatches: true, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 1, activatedTurn: 1, turns: 3, ownerMatches: true, isEligible: true)
            && ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 2, activatedTurn: 1, turns: 3, ownerMatches: true, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 4, activatedTurn: 0, turns: 3, ownerMatches: true, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 1, activatedTurn: 0, turns: 3, ownerMatches: false, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(turnNumber: 1, activatedTurn: 0, turns: 3, ownerMatches: true, isEligible: false),
            "Expected the Red Cape to free-activate the first eligible Clow Card of each of the first 3 turns.");
    }

    private static PileType ResultPileForCardPlay(CardModel card) =>
        ((CardLocation)typeof(CardModel)
            .GetMethod("GetResultLocationForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(card, null)!).pileType;
}
