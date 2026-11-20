using SakuraMod.SakuraModCode;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using System.Reflection;

public sealed class CardVisualContractSuite
{
    [Fact]
    public void CardLibrarySortingPreservesCatalogAndNativePriority()
    {
        var defaultLibrarySort = new List<SortingOrders>
        {
            SortingOrders.RarityAscending,
            SortingOrders.TypeAscending,
            SortingOrders.CostAscending,
            SortingOrders.AlphabetAscending
        };
        RegressionTestHarness.Require(
            SakuraCardLibrarySortPatch.IsDefaultLibrarySort(defaultLibrarySort),
            "Expected the Sakura card-library sort patch to recognize the vanilla default library sort.");
        var sortedLibraryCards = SakuraCardLibrarySortPatch.SortCardsForLibrary(
            [
                new SpellSeal(),
                new Gale(),
                new SakuraSword(),
                new ClowRain(),
                new ClowSword()
            ],
            defaultLibrarySort);
        RegressionTestHarness.Require(
            sortedLibraryCards.Select(static card => card.GetType()).SequenceEqual([
                typeof(ClowSword),
                typeof(ClowRain),
                typeof(SakuraSword),
                typeof(Gale),
                typeof(SpellSeal)
            ]),
            "Expected Sakura card-library default ordering to use Kinomoto Clow, Sakura, Clear, then Spell catalog order.");
        var costSortedClearCards = SakuraCardLibrarySortPatch.SortCardsForLibrary(
            [
                new Blaze(),
                new Gale(),
                new Lucid()
            ],
            [SortingOrders.CostAscending]);
        RegressionTestHarness.Require(
            costSortedClearCards.Select(static card => card.GetType()).SequenceEqual([
                typeof(Gale),
                typeof(Lucid),
                typeof(Blaze)
            ]),
            "Expected Sakura card-library custom sorting to preserve native sort priority inside the Kinomoto card class.");
    }

    [Fact]
    public void VisualFamiliesSeparateOwnershipFromLayout()
    {
        RegressionTestHarness.Require(
            SakuraCardVisualFamilies.IsKinomoto(new ChoiceManifestChoice())
            && SakuraCardVisualFamilies.IsKinomoto(new ClowSword())
            && SakuraCardVisualFamilies.IsKinomoto(new SakuraSword())
            && SakuraCardVisualFamilies.IsKinomoto(new SpellSeal())
            && SakuraCardVisualFamilies.IsKinomoto(new Gale()),
            "Expected option, Clow, Sakura, Spell, and Transparent Cards to share the Kinomoto visual route.");
        RegressionTestHarness.Require(
            SakuraCardVisualFamilies.UsesClassicLayout(new ClowSword())
            && SakuraCardVisualFamilies.UsesClassicLayout(new SakuraSword())
            && SakuraCardVisualFamilies.UsesClassicLayout(new SpellSeal()),
            "Expected Clow, Sakura, and Spell cards to keep the Classic full-card layout under the Kinomoto route.");
        RegressionTestHarness.Require(
            SakuraCardVisualFamilies.UsesClearLayout(new Gale())
            && SakuraOptionCardCatalog.CardTypes
                .Select(static type => (CardModel)Activator.CreateInstance(type)!)
                .All(SakuraCardVisualFamilies.UsesClearLayout),
            "Expected Transparent Cards and option cards to use the Clear full-card layout under the Kinomoto route.");
        RegressionTestHarness.Require(
            SakuraCardVisualFamilies.IsVanilla(new AnotherMe())
            && SakuraCardVisualFamilies.IsVanilla(new GrowingMagic()),
            "Expected Sakura Ancient cards to use native Ancient visuals instead of a Clow/Sakura/Spell full-face art route.");
        RegressionTestHarness.Require(
            SakuraCardVisualFamilies.ContentOwner(new ClowSword()) == SakuraCardContentOwner.Sakura
            && SakuraCardVisualFamilies.ContentOwner(new Gale()) == SakuraCardContentOwner.Sakura
            && SakuraCardVisualFamilies.ContentOwner(new AnotherMe()) == SakuraCardContentOwner.Sakura
            && SakuraCardVisualFamilies.ContentOwner(new GrowingMagic()) == SakuraCardContentOwner.Sakura
            && SakuraCardVisualFamilies.ContentOwner(new ChoiceRemindChoice()) == SakuraCardContentOwner.Sakura
            && SakuraCardVisualFamilies.ContentOwner(new MegaCrit.Sts2.Core.Models.Cards.Bash()) == SakuraCardContentOwner.Vanilla,
            "Expected Sakura content ownership to remain independent from the selected full-card layout.");
    }

    [Fact]
    public void DescriptionRegionScopeShapeAndPatchPriorityRemainStable()
    {
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.AppliesTo(new ClowSword())
            && SakuraDescriptionRegion.AppliesTo(new SakuraSword())
            && SakuraDescriptionRegion.AppliesTo(new Gale())
            && SakuraDescriptionRegion.AppliesTo(new SpellSeal())
            && SakuraDescriptionRegion.AppliesTo(new SpellRelease())
            && SakuraOptionCardCatalog.CardTypes
                .Select(static type => (CardModel)Activator.CreateInstance(type)!)
                .All(SakuraDescriptionRegion.AppliesTo),
            "Expected option, Clow, Sakura, Transparent, and Spell cards to use the shared description region.");
        RegressionTestHarness.Require(
            !SakuraDescriptionRegion.AppliesTo(new AnotherMe())
            && !SakuraDescriptionRegion.AppliesTo(new GrowingMagic()),
            "Expected Sakura Ancient cards to stay outside the shared Sakura description region.");
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.ShouldShow(isInCombatHand: false, isFocused: false, isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(isInCombatHand: false, isFocused: true, isCurrentCardPlay: false)
            && !SakuraDescriptionRegion.ShouldShow(isInCombatHand: true, isFocused: false, isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(isInCombatHand: true, isFocused: true, isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(isInCombatHand: true, isFocused: false, isCurrentCardPlay: true),
            "Expected descriptions to stay visible outside combat hands and follow native focus/current play inside combat hands.");
        var cardVisualPostfix = typeof(SakuraCardUpdateVisualsPatch).GetMethod(
            nameof(SakuraCardUpdateVisualsPatch.Postfix),
            BindingFlags.Public | BindingFlags.Static)!;
        RegressionTestHarness.Require(
            HarmonyMethodExtensions.GetMergedFromMethod(cardVisualPostfix).priority == -1,
            "Expected normal Sakura card visuals to apply at default postfix priority before later-loaded portrait animation patches.");
        var generatedTransparentPostfix = typeof(SakuraGeneratedTransparentCardUpdateVisualsPatch).GetMethod(
            nameof(SakuraGeneratedTransparentCardUpdateVisualsPatch.Postfix),
            BindingFlags.Public | BindingFlags.Static)!;
        RegressionTestHarness.Require(
            HarmonyMethodExtensions.GetMergedFromMethod(generatedTransparentPostfix).priority == Priority.Last,
            "Expected only generated Transparent Card repainting to retain the last-priority UpdateVisuals postfix.");
        RegressionTestHarness.Require(
            SakuraCardCatalog.Entries.All(entry =>
            {
                var card = (CardModel)Activator.CreateInstance(entry.CardType)!;
                var usesSupportedFamily =
                    entry.Era is SourceEraClass.Clow or SourceEraClass.Sakura or SourceEraClass.Clear
                    || card is SpellCard;
                var usesSakuraRenderer =
                    entry.VisualRoute is SakuraSourceCardVisualRoute.Classic or SakuraSourceCardVisualRoute.Clear;
                var usesSupportedType =
                    card.Type is CardType.Attack or CardType.Skill or CardType.Power;
                var expected = usesSupportedFamily && usesSakuraRenderer && usesSupportedType;
                return SakuraDescriptionRegion.AppliesTo(card) == expected;
            }),
            "Expected the description region scope to match source era or Spell family, visual route, and supported card type for the whole catalog.");
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.ShapeFor(new ClowSword()) == SakuraDescriptionShape.Attack
            && SakuraDescriptionRegion.ShapeFor(new ClowShield()) == SakuraDescriptionShape.Skill
            && SakuraDescriptionRegion.ShapeFor(new ClowNothing()) == SakuraDescriptionShape.Power
            && SakuraDescriptionRegion.ShapeFor(new SpellSeal()) == SakuraDescriptionShape.Attack
            && SakuraDescriptionRegion.ShapeFor(new SpellRelease()) == SakuraDescriptionShape.Skill,
            "Expected card type to select the pointed, flat, and arched description silhouettes.");
    }

    [Fact]
    public void DescriptionNormalizationBracketsOnlySakuraIdentityKeywords()
    {
        const string nativeAndModKeywordText =
            "[center][gold]Ethereal[/gold].\n[gold]Watery[/gold]\nCopy a card.\n[gold]Exhaust[/gold].[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new ClowRain(), nativeAndModKeywordText)
                == "[gold]Ethereal[/gold] 「[gold]Watery[/gold]」\nCopy a card.\n[gold]Exhaust[/gold].",
            "Expected adjacent native and Sakura keyword lines to share one naturally wrapping paragraph without reordering the effect or suffix.");
        const string nativeExhaustOnlyText =
            "[center][gold]Exhaust[/gold].\nDeal [gold]3[/gold] damage.[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new ClowRain(), nativeExhaustOnlyText)
                == "[gold]Exhaust[/gold].\nDeal [gold]3[/gold] damage.",
            "Expected native Exhaust text not to receive Sakura identity brackets.");
        const string multiPartModKeywordText =
            "[center][gold]Retain[/gold].\n[gold]Spell[/gold] [gold]Fire[/gold]\nDeal [gold]5[/gold] damage.[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new SpellHuoShen(), multiPartModKeywordText)
                == "[gold]Retain[/gold] 「[gold]Spell[/gold]」「[gold]Fire[/gold]」\nDeal [gold]5[/gold] damage.",
            "Expected a native prefix and multi-part Mod keyword line to share one line while styled effect values stay in the body.");
        var normalizedSpellKeywords = SakuraDescriptionRegion.NormalizeText(new SpellHuoShen(), multiPartModKeywordText);
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new SpellHuoShen(), normalizedSpellKeywords) == normalizedSpellKeywords,
            "Expected repeated visual application to keep identity brackets idempotent.");
    }

    [Fact]
    public void DescriptionProjectionIsSharedAndIdempotentAcrossRendererApplications()
    {
        var card = new ClowRain();
        const string nativeText =
            "[center][gold]Exhaust[/gold].\n[gold]Watery[/gold]\nCopy a card.\n[gold]Exhaust[/gold].[/center]";
        var projected = SakuraDescriptionRegion.ProjectText(card, nativeText);

        RegressionTestHarness.Require(
            projected == "[gold]Exhaust[/gold] 「[gold]Watery[/gold]」\nCopy a card.\n[gold]Exhaust[/gold].",
            "Expected the shared description projection to preserve native suffixes while merging the element identity header.");
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.ProjectText(card, SakuraDescriptionRegion.Centered(projected)) == projected,
            "Expected repeated Classic/Clear renderer applications to keep the projected description idempotent.");
    }

    [Fact]
    public void HandHighlightPolicyGatesExtraEffectsByPlayability()
    {
        RegressionTestHarness.Require(
            SakuraHandHighlightPolicy.Select(new ClowSword(), isPlayable: true, extraEffectActive: true) == SakuraHandHighlightKind.ExtraEffect
            && SakuraHandHighlightPolicy.Select(new Gale(), isPlayable: true, extraEffectActive: true) == SakuraHandHighlightKind.ExtraEffect
            && SakuraHandHighlightPolicy.Select(new SakuraSword(), isPlayable: true, extraEffectActive: true) == SakuraHandHighlightKind.None
            && SakuraHandHighlightPolicy.Select(new ClowSword(), isPlayable: false, extraEffectActive: true) == SakuraHandHighlightKind.None
            && SakuraHandHighlightPolicy.Select(new Gale(), isPlayable: true, extraEffectActive: false) == SakuraHandHighlightKind.None,
            "Expected active Clow and Transparent Extra Effects to highlight only when playable.");
    }

    [Fact]
    public void ForgottenOverlayShowsOnlyForForgottenHandCards()
    {
        RegressionTestHarness.Require(
            SakuraForgottenOverlayPolicy.ShouldShow(isTemporary: true, isInHand: true)
            && !SakuraForgottenOverlayPolicy.ShouldShow(isTemporary: false, isInHand: true)
            && !SakuraForgottenOverlayPolicy.ShouldShow(isTemporary: true, isInHand: false),
            "Expected the Forgotten overlay only on Forgotten cards that are in the hand.");
    }

    [Fact]
    public void MemoryVisualTimingHonoursFastModeAndMergesRipples()
    {
        RegressionTestHarness.Require(
            SakuraMemoryVisualTiming.TimeScale(MegaCrit.Sts2.Core.Settings.FastModeType.Normal) == 1f
            && SakuraMemoryVisualTiming.TimeScale(MegaCrit.Sts2.Core.Settings.FastModeType.Fast) is > 0f and < 1f
            && SakuraMemoryVisualTiming.TimeScale(MegaCrit.Sts2.Core.Settings.FastModeType.Instant) == 0f,
            "Expected Memory presentation to shorten in Fast mode and be skipped in Instant mode.");

        RegressionTestHarness.Require(
            SakuraMemoryVisualTiming.ShouldMergeRipple(now: 10d, lastRippleAt: 9.9d)
            && !SakuraMemoryVisualTiming.ShouldMergeRipple(now: 10d, lastRippleAt: 9.5d)
            && !SakuraMemoryVisualTiming.ShouldMergeRipple(now: 10d, lastRippleAt: double.NegativeInfinity),
            "Expected several cards landing in Memory at once to share one ripple.");
    }

    [Fact]
    public void MemoryRecallPatchResolvesAgainstThePinnedGameApi()
    {
        var target = Assert.Single(SakuraMemoryRecallCardNodePatch.GetTargets());
        Assert.NotNull(AccessTools.Method(target.TargetType, target.MethodName, target.ParameterTypes));
    }
}
