using MegaCrit.Sts2.Core.Entities.Cards;
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

        var classicExtra = SakuraExtraEffectPostPlayPlan.ForGameplay(
            new ClowSword(),
            new SakuraExtraEffectActivation(true));
        var sakuraNormal = SakuraExtraEffectPostPlayPlan.ForGameplay(
            new SakuraSword(),
            new SakuraExtraEffectActivation(false));
        var transparentExtra = SakuraExtraEffectPostPlayPlan.ForGameplay(
            new Gale(),
            new SakuraExtraEffectActivation(true));
        var transparentAfterPlay = SakuraExtraEffectPostPlayPlan.ForAfterCardPlayed(new Gale());

        RegressionTestHarness.Require(
            classicExtra.ApplyExtraElementStates && !classicExtra.AddSakuraVoid,
            "Expected an activated Clow card to apply its Classic element state without adding Void.");
        RegressionTestHarness.Require(
            !sakuraNormal.ApplyExtraElementStates && sakuraNormal.AddSakuraVoid,
            "Expected a normal Sakura-form play to add Void without applying an Extra element state.");
        RegressionTestHarness.Require(
            transparentExtra.ApplyExtraElementStates && !transparentExtra.AddSakuraVoid,
            "Expected an activated Transparent card to share the Extra element post-play path.");
        RegressionTestHarness.Require(
            transparentAfterPlay.GainTransparentMagic && !transparentAfterPlay.MayGainClassicMagic,
            "Expected a Transparent card to retain its after-play Magic Charge path.");
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
    }

    [Fact]
    public void ActiveTransactionPreservesOrderingAndMarker()
    {
        var card = new Gale();
        var play = CardPlayFor(card);
        var order = new List<string>();

        RunCore(
            card,
            play,
            active: true,
            () => order.Add("spend"),
            () => order.Add("record"),
            () =>
            {
                order.Add("gameplay");
                RegressionTestHarness.Require(
                    SakuraExtraEffectTransaction.IsActivelyProjected(card),
                    "Expected the visual projection to be active during gameplay.");
            },
            () => order.Add("post"));

        RegressionTestHarness.Require(
            order.SequenceEqual(["spend", "record", "gameplay", "post"]),
            "Expected paid Extra Effect transaction ordering to remain stable.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.DidActivate(play)
            && SakuraExtraEffectTransaction.DidSpendMagicCharge(play)
            && !SakuraExtraEffectTransaction.IsActivelyProjected(card),
            "Expected the completed play marker to survive while the active projection is cleared.");
    }

    [Fact]
    public void InactiveTransactionSkipsSpendAndProjection()
    {
        var card = new Gale();
        var play = CardPlayFor(card);
        var order = new List<string>();

        RunCore(
            card,
            play,
            active: false,
            () => order.Add("spend"),
            () => order.Add("record"),
            () => order.Add("gameplay"),
            () => order.Add("post"));

        RegressionTestHarness.Require(
            order.SequenceEqual(["gameplay", "post"])
            && !SakuraExtraEffectTransaction.DidActivate(play)
            && !SakuraExtraEffectTransaction.DidSpendMagicCharge(play)
            && !SakuraExtraEffectTransaction.IsActivelyProjected(card),
            "Expected inactive plays to skip Extra spend and bookkeeping without installing a projection.");
    }

    [Fact]
    public void DescriptionPolicyShowsReferenceAndActiveCombatExtraCards()
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

        RunCore(
            classic,
            CardPlayFor(classic),
            active: true,
            static () => { },
            static () => { },
            () => RegressionTestHarness.Require(
                SakuraExtraEffectTransaction.ShouldShowDescription(classic, isInCombat: true),
                "Expected an active mutable Classic card to show its Extra Effect description."),
            static () => { });
        RunCore(
            transparent,
            CardPlayFor(transparent),
            active: true,
            static () => { },
            static () => { },
            () => RegressionTestHarness.Require(
                SakuraExtraEffectTransaction.ShouldShowDescription(transparent, isInCombat: true),
                "Expected an active mutable Transparent Card to show its Extra Effect description."),
            static () => { });

        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(classic)
            && SakuraCardModel.ShouldShowMagicChargeExtraEffectDescription(transparent),
            "Expected mutable non-combat cards to keep complete Extra Effect descriptions after projection cleanup.");
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.ShouldShowDescription(classic, isInCombat: true)
            && !SakuraExtraEffectTransaction.ShouldShowDescription(transparent, isInCombat: true),
            "Expected mutable combat cards to hide Extra Effect descriptions after projection cleanup.");
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
    public void ActivationMarkerPreservesTheActualPaymentKind()
    {
        var card = new Gale();
        var play = CardPlayFor(card);

        SakuraExtraEffectTransaction.ExecuteCoreForTests(
                card,
                play,
                new SakuraExtraEffectActivation(true),
                static () => Task.CompletedTask,
                static () => Task.CompletedTask,
                static () => Task.CompletedTask,
                static () => Task.CompletedTask,
                SakuraExtraEffectActivationCost.LockSakura)
            .GetAwaiter()
            .GetResult();

        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.DidActivate(play)
            && !SakuraExtraEffectTransaction.DidSpendMagicCharge(play),
            "Expected a Sakura Lock-funded Extra Effect to remain activated without being marked as a Magic Charge spend.");
    }

    [Fact]
    public void GameplayFailureClearsProjectionWithoutReplacingFailure()
    {
        var card = new Gale();
        var play = CardPlayFor(card);
        var spent = false;
        var postPlayed = false;

        try
        {
            SakuraExtraEffectTransaction.ExecuteCoreForTests(
                    card,
                    play,
                    new SakuraExtraEffectActivation(true),
                    () =>
                    {
                        spent = true;
                        return Task.CompletedTask;
                    },
                    () => Task.CompletedTask,
                    () => throw new TestGameplayException("original gameplay failure"),
                    () =>
                    {
                        postPlayed = true;
                        return Task.CompletedTask;
                    })
                .GetAwaiter()
                .GetResult();
            throw new InvalidOperationException("Expected the fake gameplay to throw.");
        }
        catch (TestGameplayException exception)
        {
            RegressionTestHarness.Require(
                exception.Message == "original gameplay failure",
                "Expected cleanup not to replace the gameplay exception.");
        }

        RegressionTestHarness.Require(
            spent
            && !postPlayed
            && SakuraExtraEffectTransaction.DidActivate(play)
            && !SakuraExtraEffectTransaction.IsActivelyProjected(card),
            "Expected a failed play to keep its completed spend, skip post-effects, and clear its projection.");
    }

    [Fact]
    public void NestedTransactionsRestoreOuterProjectionInLifoOrder()
    {
        var card = new Gale();
        var outerPlay = CardPlayFor(card);
        var innerPlay = CardPlayFor(card);

        RunCore(
            card,
            outerPlay,
            active: true,
            static () => { },
            static () => { },
            () =>
            {
                RunCore(
                    card,
                    innerPlay,
                    active: true,
                    static () => { },
                    static () => { },
                    () => RegressionTestHarness.Require(
                        SakuraExtraEffectTransaction.IsActivelyProjected(card),
                        "Expected the nested play projection to be active."),
                    static () => { });
                RegressionTestHarness.Require(
                    SakuraExtraEffectTransaction.IsActivelyProjected(card),
                    "Expected the outer projection to resume after nested cleanup.");
            },
            static () => { });

        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.DidActivate(outerPlay)
            && SakuraExtraEffectTransaction.DidActivate(innerPlay)
            && !SakuraExtraEffectTransaction.IsActivelyProjected(card),
            "Expected nested plays to retain distinct markers and clear projections in LIFO order.");
    }

    [Fact]
    public void SpendAndRecordFailuresPreserveTransactionBoundaries()
    {
        var spendCard = new Gale();
        var spendPlay = CardPlayFor(spendCard);
        RegressionTestHarness.RequireThrows<TestSpendException>(
            () => SakuraExtraEffectTransaction.ExecuteCoreForTests(
                    spendCard,
                    spendPlay,
                    new SakuraExtraEffectActivation(true),
                    () => throw new TestSpendException(),
                    static () => Task.CompletedTask,
                    static () => Task.CompletedTask,
                    static () => Task.CompletedTask)
                .GetAwaiter()
                .GetResult(),
            "Expected a failed standard spend to propagate.");
        RegressionTestHarness.Require(
            !SakuraExtraEffectTransaction.DidActivate(spendPlay)
            && !SakuraExtraEffectTransaction.IsActivelyProjected(spendCard),
            "Expected spend failure to avoid an activation marker and clear the projection.");

        var recordCard = new Gale();
        var recordPlay = CardPlayFor(recordCard);
        var gameplayRan = false;
        RegressionTestHarness.RequireThrows<TestRecordException>(
            () => SakuraExtraEffectTransaction.ExecuteCoreForTests(
                    recordCard,
                    recordPlay,
                    new SakuraExtraEffectActivation(true),
                    static () => Task.CompletedTask,
                    () => throw new TestRecordException(),
                    () =>
                    {
                        gameplayRan = true;
                        return Task.CompletedTask;
                    },
                    static () => Task.CompletedTask)
                .GetAwaiter()
                .GetResult(),
            "Expected a failed trigger record to propagate.");
        RegressionTestHarness.Require(
            SakuraExtraEffectTransaction.DidActivate(recordPlay)
            && !gameplayRan
            && !SakuraExtraEffectTransaction.IsActivelyProjected(recordCard),
            "Expected record failure after spend to keep the marker, skip gameplay, and clear the projection.");
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

    private static void RunCore(
        CardModel card,
        CardPlay play,
        bool active,
        System.Action spend,
        System.Action record,
        System.Action gameplay,
        System.Action postPlay) =>
        SakuraExtraEffectTransaction.ExecuteCoreForTests(
                card,
                play,
                new SakuraExtraEffectActivation(active),
                AsTask(spend),
                AsTask(record),
                AsTask(gameplay),
                AsTask(postPlay))
            .GetAwaiter()
            .GetResult();

    private static Func<Task> AsTask(System.Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    private static CardPlay CardPlayFor(CardModel card) => new()
    {
        Card = card,
        Target = null,
        ResultPile = PileType.Discard,
        Resources = new ResourceInfo
        {
            EnergySpent = 0,
            EnergyValue = 0,
            StarsSpent = 0,
            StarValue = 0
        },
        IsAutoPlay = false,
        PlayIndex = 0,
        PlayCount = 1
    };

    private sealed class TestGameplayException(string message) : Exception(message);
    private sealed class TestRecordException : Exception;
    private sealed class TestSpendException : Exception;
}
