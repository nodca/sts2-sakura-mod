using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

public sealed class ExtraEffectTransactionSuite
{
    private static readonly IReadOnlySet<Type> TransparentExtraCards = new HashSet<Type>
    {
        typeof(SakuraMod.SakuraModCode.Cards.Action),
        typeof(Appear),
        typeof(Aqua),
        typeof(Blade),
        typeof(Blank),
        typeof(Blaze),
        typeof(Break),
        typeof(Choice),
        typeof(Exchange),
        typeof(Flight),
        typeof(Gale),
        typeof(Gravitation),
        typeof(Hail),
        typeof(Kindness),
        typeof(Lucid),
        typeof(Mirage),
        typeof(Mirror),
        typeof(Promise),
        typeof(SakuraMod.SakuraModCode.Cards.Record),
        typeof(Reflect),
        typeof(Repair),
        typeof(Reversal),
        typeof(Rewind),
        typeof(Shade),
        typeof(Siege),
        typeof(Snooze),
        typeof(Spiral),
        typeof(Struggle),
        typeof(Swing),
        typeof(Time),
        typeof(Transfer),
        typeof(TrueOrFalse)
    };

    [Fact]
    public void ActivationAndPostPlayPoliciesRemainStable()
    {
        var mirror = new Mirror();
        RegressionTestHarness.Require(
            mirror.DynamicVars["Repeat"].IntValue == 1
            && mirror.DynamicVars["ExtraRepeat"].IntValue == 1,
            "Expected Mirror to grant 1 Replay this combat and 1 additional Replay with Extra.");

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

        RegressionTestHarness.Require(
            SakuraExtraEffectPostPlayPlan.ForGameplay(new SakuraExtraEffectActivation(true)).ApplyExtraElementStates
            && !SakuraExtraEffectPostPlayPlan.ForGameplay(new SakuraExtraEffectActivation(false)).ApplyExtraElementStates,
            "Expected Extra Effect to apply missing Element States only when the play is activated.");
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
            SakuraExtraEffectTransaction.MagicCircleEraFor(new Hail(), inactive) is null
            && SakuraExtraEffectTransaction.MagicCircleEraFor(new Hail(), active)
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
    public void CapabilityCoverageMatchesReviewedCardFamilies()
    {
        var assembly = typeof(SakuraCardModel).Assembly;
        var actualTransparentCards = assembly.GetTypes()
            .Where(static type => !type.IsAbstract && typeof(TransparentExtraEffectCard).IsAssignableFrom(type))
            .ToHashSet();

        RegressionTestHarness.Require(
            actualTransparentCards.SetEquals(TransparentExtraCards),
            "Expected the Transparent Extra Effect capability set to match the reviewed full-card inventory.");

        foreach (var type in assembly.GetTypes().Where(static type =>
                     !type.IsAbstract && typeof(SakuraSourceCard).IsAssignableFrom(type)))
        {
            var card = (CardModel)Activator.CreateInstance(type)!;
            RegressionTestHarness.Require(
                SakuraExtraEffectTransaction.Supports(card) == typeof(ClowExtraEffectCard).IsAssignableFrom(type),
                $"Expected {type.Name} to have exactly one capability declaration matching its Classic Extra family.");
        }

        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.Supports(new SakuraSword())
            && !SakuraExtraEffectTransaction.Supports(new ClowReturn())
            && !SakuraExtraEffectTransaction.Supports(new SpellSeal())
            && !SakuraExtraEffectTransaction.Supports(new Remind()),
            "Expected Sakura-form, non-Extra Clow, Spell, and non-Extra Transparent cards to stay outside Extra Effect.");
    }

    [Fact]
    public void DescriptionPolicyShowsReferenceAndHidesInactiveCombatExtraCards()
    {
        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(new ClowSword())
            && SakuraCardModel.ShouldShowMagicChargeExtraEffectDescription(new Gale()),
            "Expected canonical reference cards to show their complete Extra Effect descriptions.");

        var classic = RegressionTestHarness.MutableForCostTest(new ClowSword());
        var transparent = RegressionTestHarness.MutableForCostTest(new Gale());
        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(classic)
            && SakuraCardModel.ShouldShowMagicChargeExtraEffectDescription(transparent),
            "Expected mutable non-combat cards to show their complete Extra Effect descriptions.");
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.ShouldShowDescription(classic, isInCombat: true)
            && !SakuraExtraEffectTransaction.ShouldShowDescription(transparent, isInCombat: true),
            "Expected inactive mutable combat cards to hide their Extra Effect descriptions.");
    }

    [Fact]
    public void RedCapeAvailabilityFeedsTheSharedExtraEffectPresentationPolicy()
    {
        var transaction = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraExtraEffectTransaction.cs"));
        var redCape = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicRedCapeRelic.cs"));

        RegressionTestHarness.Require(
            transaction.Contains(
                "owner.GetRelic<ClassicRedCapeRelic>()?.CanActivateFreeExtraEffect(card) == true",
                StringComparison.Ordinal)
            && redCape.Contains("if (!CanActivateFreeExtraEffect(card))", StringComparison.Ordinal),
            "Expected Red Cape preview and consumption to share one eligibility rule so its first Clow card uses the normal gold highlight and Extra Effect description.");
    }

    [Fact]
    public void ExtraEffectExecuteIsReservedForExtraEffectCards()
    {
        var sourceCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraSourceCard.cs"));
        var cardModel = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardModel.cs"));
        var transaction = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraExtraEffectTransaction.cs"));

        RegressionTestHarness.Require(
            sourceCard.Contains("if (this is ISakuraExtraEffectCard)", StringComparison.Ordinal)
            && cardModel.Contains("if (this is ISakuraExtraEffectCard)", StringComparison.Ordinal)
            && sourceCard.Contains("SakuraFormVoid.AfterCardPlayed", StringComparison.Ordinal)
            && !transaction.Contains("AddVoidToDrawPile", StringComparison.Ordinal)
            && !transaction.Contains("ShouldAddSakuraVoid", StringComparison.Ordinal)
            && !transaction.Contains("ExecuteCoreForTests", StringComparison.Ordinal)
            && !transaction.Contains("internal static async Task ExecuteCore", StringComparison.Ordinal)
            && transaction.Contains("private static async Task ExecuteCore", StringComparison.Ordinal),
            "Expected Extra Effect to execute only Extra Effect cards, leave Sakura-Form Void outside the transaction, and keep ExecuteCore private.");
    }

    [Fact]
    public void RetiredExtraEffectApisHaveNoProductionCallSites()
    {
        string[] retiredTerms =
        [
            "IExtra" + "EffectCard",
            "HasExtra" + "Effect",
            "IsUsing" + "ExtraEffect",
            "Trigger" + "ExtraEffect",
            "Play" + "Normal",
            "Play" + "Extra"
        ];
        var sourceRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile("SakuraMod.csproj"))!;
        var productionFiles = Directory.EnumerateFiles(
            Path.Combine(sourceRoot, "SakuraModCode"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in productionFiles)
        {
            var source = File.ReadAllText(file);
            foreach (var term in retiredTerms)
            {
                RegressionTestHarness.Require(
                    !source.Contains(term, StringComparison.Ordinal),
                    $"Expected retired Extra Effect API '{term}' to have no production call sites.");
            }
        }
    }
}
