using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Covers the Extra Effect activation gate. Three cards expose that gate to the
/// player — Arrow through targeting, Rain and Time through the result pile — and
/// all three must resolve it through the single predicate the hand highlight and
/// the card description already use.
/// </summary>
/// <remarks>
/// The positive half of each gate needs a live <c>Player</c> with a Magic Charge
/// power and the Red Cape relic, which the unit harness cannot build. Those
/// combinations are asserted in game; here the wiring is pinned so the old
/// ad-hoc predicates cannot come back.
/// </remarks>
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
    public void ActivationGatesDelegateToTheUnifiedPredicate()
    {
        var reworkedGates = new (string RelativePath, string RemovedToken)[]
        {
            ("SakuraModCode/Cards/ClowSakura/Arrow.cs", "SakuraMagicCharge.CanSpendMagic(Owner)"),
            ("SakuraModCode/Cards/ClowSakura/Rain.cs", "SakuraExtraEffectTransaction.CanActivate(Owner)"),
            ("SakuraModCode/Cards/ClowSakura/Time.cs", "SakuraExtraEffectTransaction.CanActivate(Owner)")
        };

        foreach (var (relativePath, removedToken) in reworkedGates)
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                source.Contains(
                    "SakuraExtraEffectTransaction.ShouldShowAsActive(this)",
                    StringComparison.Ordinal)
                && !source.Contains(removedToken, StringComparison.Ordinal),
                $"Expected {relativePath} to resolve its Extra Effect gate through "
                + $"SakuraExtraEffectTransaction.ShouldShowAsActive instead of {removedToken}.");
        }
    }

    [Fact]
    public void ClowThunderKeepsItsMagicChargePlayRequirement()
    {
        var source = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/ClowSakura/Thunder.cs"));
        RegressionTestHarness.Require(
            source.Contains("IsPlayable => SakuraMagicCharge.CanSpendMagic(Owner)", StringComparison.Ordinal),
            "Expected Thunder to keep requiring 10 Magic Charge to be played, matching its printed text.");

        var descriptions = new[]
        {
            ("SakuraMod/localization/zhs/cards.json", "[gold]地[/gold]\n只有拥有 10 点[gold]魔力充能[/gold]时才能打出。"),
            ("SakuraMod/localization/eng/cards.json", "[gold]Earthy[/gold]\nCan only be played with 10 [gold]Magic Charge[/gold].")
        };

        foreach (var (relativePath, expected) in descriptions)
        {
            var actual = ReadLocalization(relativePath)["SAKURA_MOD_CARD_CLOW_THUNDER.description"].GetString();
            RegressionTestHarness.Require(
                actual == expected,
                $"Expected {relativePath} to keep Thunder's Magic Charge play requirement, got '{actual}'.");
        }
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
            ClassicRedCapeRelic.CanActivateFreeExtraEffect(activatedThisCombat: false, ownerMatches: true, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(activatedThisCombat: true, ownerMatches: true, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(activatedThisCombat: false, ownerMatches: false, isEligible: true)
            && !ClassicRedCapeRelic.CanActivateFreeExtraEffect(activatedThisCombat: false, ownerMatches: true, isEligible: false),
            "Expected the Red Cape to free-activate exactly the first eligible Clow Card of the combat.");
    }

    private static PileType ResultPileForCardPlay(CardModel card) =>
        (PileType)typeof(CardModel)
            .GetMethod("GetResultPileTypeForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(card, null)!;

    private static Dictionary<string, JsonElement> ReadLocalization(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
        ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
}
