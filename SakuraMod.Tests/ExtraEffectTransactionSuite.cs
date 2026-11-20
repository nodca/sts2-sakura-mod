using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

public sealed class ExtraEffectTransactionSuite
{
    [Fact]
    public void ActivationThresholdAndLockPaymentRemainStable()
    {
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.CanActivate(9, isLocked: false)
            && SakuraExtraEffectTransaction.CanActivate(10, isLocked: false)
            && !SakuraExtraEffectTransaction.CanActivate(10, isLocked: true),
            "Expected standard activation to require 10 Magic Charge and no Lock.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.ActivationCost(hasLockSakura: false)
                == SakuraExtraEffectActivationCost.MagicCharge
            && SakuraExtraEffectTransaction.ActivationCost(hasLockSakura: true)
                == SakuraExtraEffectActivationCost.LockSakura,
            "Expected Lock Sakura to be consumed instead of Magic Charge for the next activation.");
    }

    [Fact]
    public void MagicCircleTriggerMatrixUsesEraTypeAndCompletedActivation()
    {
        var inactive = new SakuraExtraEffectActivation(false);
        var active = new SakuraExtraEffectActivation(true);

        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.MagicCircleEraFor(new ClowSword(), inactive) is null
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new ClowShield(), active)
                == SourceEraClass.Clow
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new ClowLight(), inactive)
                == SourceEraClass.Clow,
            "Expected ordinary Clow attacks to stay quiet while activated Extra skills and Clow Powers show the Clow circle.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.MagicCircleEraFor(new SakuraSword(), inactive)
                == SourceEraClass.Sakura
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new SakuraMirror(), inactive)
                == SourceEraClass.Sakura
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new SakuraLight(), inactive)
                == SourceEraClass.Sakura,
            "Expected Sakura Attacks, Skills, and Powers to show one Sakura circle, including overlapping Power triggers.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.MagicCircleEraFor(new Aqua(), inactive) is null
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new Aqua(), active)
                == SourceEraClass.Clear
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new Dreaming(), inactive)
                == SourceEraClass.Clear,
            "Expected ordinary Transparent attacks to stay quiet while activated Extra cards and Transparent Powers show the Clear circle.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.MagicCircleEraFor(new AnotherMe(), inactive) is null
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new SpellSeal(), active) is null
            && SakuraExtraEffectTransaction.MagicCircleEraFor(null, active) is null,
            "Expected era-neutral, Spell, and missing cards to remain outside the magic-circle trigger domain.");
    }

    [Fact]
    public void ClassicExtraEffectSupportMatchesBaseClass()
    {
        var assembly = typeof(SakuraCardModel).Assembly;
        foreach (var type in assembly.GetTypes().Where(static type =>
                     !type.IsAbstract && typeof(SakuraSourceCard).IsAssignableFrom(type)))
        {
            var card = (CardModel)Activator.CreateInstance(type)!;
            RegressionTestHarness.Require(
                SakuraExtraEffectTransaction.Supports(card) == typeof(ClowExtraEffectCard).IsAssignableFrom(type),
                $"Expected {type.Name} to have exactly one capability declaration matching its Classic Extra family.");
        }
    }

    [Fact]
    public void DescriptionPolicyShowsReferenceAndHidesInactiveCombatExtraCards()
    {
        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(new ClowSword())
            && TransparentCard.ShouldShowMagicChargeExtraEffectDescription(new Gale()),
            "Expected canonical reference cards to show their complete Extra Effect descriptions.");

        var classic = RegressionTestHarness.MutableForCostTest(new ClowSword());
        var transparent = RegressionTestHarness.MutableForCostTest(new Gale());
        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(classic)
            && TransparentCard.ShouldShowMagicChargeExtraEffectDescription(transparent),
            "Expected mutable non-combat cards to show their complete Extra Effect descriptions.");
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.ShouldShowDescription(classic, isInCombat: true)
            && !SakuraExtraEffectTransaction.ShouldShowDescription(transparent, isInCombat: true),
            "Expected inactive mutable combat cards to hide their Extra Effect descriptions.");
    }
}
