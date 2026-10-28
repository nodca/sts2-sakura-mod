using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;
using System.Reflection;

public sealed class CardVisualContractSuite
{
    [Fact]
    public void ClassicPrintedTitlesAndTopGlyphsFollowTheirArtwork()
    {
        RegressionTestHarness.Require(
            !ClassicCardTextLayoutPolicy.ShowsRuntimeEnglishName(new ClowShield())
            && !ClassicCardTextLayoutPolicy.ShowsRuntimeEnglishName(new SakuraShield())
            && ClassicCardTextLayoutPolicy.ShowsRuntimeEnglishName(new SakuraLove())
            && !ClassicCardTextLayoutPolicy.ShowsRuntimeEnglishName(new SpellSeal()),
            "Expected runtime English names only for non-Spell Classic art that lacks a printed title.");

        var clowTitleBox = ClassicCardTextLayoutPolicy.TitleBox(new ClowShield());
        var sakuraTitleBox = ClassicCardTextLayoutPolicy.TitleBox(new SakuraShield());
        RegressionTestHarness.Require(
            clowTitleBox.Position == new Vector2(28f, 1f)
            && sakuraTitleBox.Position == new Vector2(28f, 6f)
            && clowTitleBox.Size == sakuraTitleBox.Size,
            "Expected the runtime top glyph to follow the higher Clow sun disc while preserving the Sakura layout.");
    }

    [Fact]
    public void CardVfxRouteCoversEveryConsumerWithoutReplacingVisualAssetOwners()
    {
        var sharedCelPaths = CelVfxSession.SharedAssetPaths;
        var cases = new[]
        {
            new VfxCase(
                new Aqua(),
                [AquaPhoenixVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new Hail(),
                [HailStormVfx.ScenePath, HailStormVfx.StoneScenePath, FreezeShellVisual.ShaderPath, .. sharedCelPaths]),
            new VfxCase(
                new Blaze(),
                [BlazePhoenixVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new Shade(),
                [ShadeGroundDragonVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraMod.SakuraModCode.Cards.Time(),
                [TimeStopVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new ClowCloud(),
                [CloudRainWeatherVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraCloud(),
                [CloudRainWeatherVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new ClowRain(),
                [CloudRainWeatherVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraRain(),
                [CloudRainWeatherVfx.ScenePath, .. sharedCelPaths]),
            new VfxCase(
                new SpellTurn(),
                [
                    SpellTurnTransformationVfx.ScenePath,
                    SpellTurnTransformationVfx.TurnAudioPath,
                    SpellTurnTransformationVfx.OpeningBuffAudioPath,
                    SpellTurnTransformationVfx.SwitchAudioPath,
                    .. SpellTurnTransformationTimeline.CompletionAudioPaths
                ])
        };

        foreach (var testCase in cases)
        {
            var vfxAssets = SakuraCardVfxAssets.RunAssetPaths(testCase.Card)
                .ToHashSet(StringComparer.Ordinal);
            RegressionTestHarness.Require(
                vfxAssets.SetEquals(testCase.VfxAssets),
                $"Expected {testCase.Card.GetType().Name} to declare exactly its combat VFX run assets.");
        }

        // SakuraCardModel composes every family's run assets as family assets, then
        // VFX. Classic face paths need Godot, so the fast host checks a Transparent card.
        var hail = new Hail();
        Assert.Equal(
            SakuraCardFrameVisuals.RunAssetPaths(hail).Concat(SakuraCardVfxAssets.RunAssetPaths(hail)),
            hail.RunAssetPaths);
    }

    [Fact]
    public void CardVfxPlaybackUsesTheNativeRunAssetCache()
    {
        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/Cards/Visuals/Transparent/HailStormVfx.cs",
                     "SakuraModCode/Cards/Visuals/Transparent/BlazePhoenixVfx.cs",
                     "SakuraModCode/Cards/Visuals/Transparent/TimeStopVfx.cs",
                     "SakuraModCode/Cards/Visuals/Transparent/ShadeGroundDragonVfx.cs",
                     "SakuraModCode/Cards/Visuals/Classic/CloudRainWeatherVfx.cs",
                     "SakuraModCode/Cards/Visuals/Classic/ArrowBowProjectileVfx.cs",
                     "SakuraModCode/Cards/Visuals/Classic/SpellTurnTransformationVfx.cs",
                     "SakuraModCode/Cards/Visuals/Classic/ClowTimeHourglassVfx.cs",
                     "SakuraModCode/Cards/Visuals/Classic/TimeStasisSandFx.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                source.Contains("PreloadManager.Cache", StringComparison.Ordinal)
                && !source.Contains("ResourceLoader.Load", StringComparison.Ordinal),
                $"Expected {relativePath} to consume native run assets without synchronous playback-path loads.");
        }

        var spellTurnSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SpellTurnTransformationVfx.cs"));
        RegressionTestHarness.Require(
            spellTurnSource.Contains("LuminTemplate", StringComparison.Ordinal),
            "Expected Spell Turn to reuse the lumin texture already owned by its instantiated scene.");
    }

    [Fact]
    public void SnowCardVfxRouteCoversBothScenesPlusSharedCelAssets()
    {
        var sharedCelPaths = CelVfxSession.SharedAssetPaths;
        var cases = new[]
        {
            new VfxCase(
                new ClowSnow(),
                [SnowBlizzardVfx.ScenePath, SnowBlizzardVfx.TargetScenePath, FreezeShellVisual.ShaderPath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraSnow(),
                [SnowBlizzardVfx.ScenePath, SnowBlizzardVfx.TargetScenePath, FreezeShellVisual.ShaderPath, .. sharedCelPaths])
        };

        foreach (var testCase in cases)
        {
            var vfxAssets = SakuraCardVfxAssets.RunAssetPaths(testCase.Card)
                .ToHashSet(StringComparer.Ordinal);
            RegressionTestHarness.Require(
                vfxAssets.SetEquals(testCase.VfxAssets),
                $"Expected {testCase.Card.GetType().Name} to declare exactly its combat VFX run assets.");
        }
    }

    [Fact]
    public void FreezeCardVfxRouteCoversBothScenesPlusSharedCelAssets()
    {
        var sharedCelPaths = CelVfxSession.SharedAssetPaths;
        var cases = new[]
        {
            new VfxCase(
                new ClowFreeze(),
                [FreezeCageVfx.ScenePath, FreezeCageVfx.TargetScenePath, FreezeShellVisual.ShaderPath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraFreeze(),
                [FreezeCageVfx.ScenePath, FreezeCageVfx.TargetScenePath, FreezeShellVisual.ShaderPath, .. sharedCelPaths])
        };

        foreach (var testCase in cases)
        {
            var vfxAssets = SakuraCardVfxAssets.RunAssetPaths(testCase.Card)
                .ToHashSet(StringComparer.Ordinal);
            RegressionTestHarness.Require(
                vfxAssets.SetEquals(testCase.VfxAssets),
                $"Expected {testCase.Card.GetType().Name} to declare exactly its combat VFX run assets.");
        }
    }

    [Fact]
    public void ArrowCardVfxRouteCoversBothScenesPlusSharedCelAssets()
    {
        var sharedCelPaths = CelVfxSession.SharedAssetPaths;
        var cases = new[]
        {
            new VfxCase(
                new ClowArrow(),
                [ArrowBowProjectileVfx.ScenePath, ArrowBowProjectileVfx.TargetScenePath, .. sharedCelPaths]),
            new VfxCase(
                new SakuraArrow(),
                [ArrowBowProjectileVfx.ScenePath, ArrowBowProjectileVfx.TargetScenePath, .. sharedCelPaths])
        };

        foreach (var testCase in cases)
        {
            var vfxAssets = SakuraCardVfxAssets.RunAssetPaths(testCase.Card)
                .ToHashSet(StringComparer.Ordinal);
            RegressionTestHarness.Require(
                vfxAssets.SetEquals(testCase.VfxAssets),
                $"Expected {testCase.Card.GetType().Name} to declare exactly its combat VFX run assets.");
        }
    }

    [Fact]
    public void ArrowVolleyObservesTargetsResolvedByGameplay()
    {
        var arrowCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Arrow.cs"));
        var vfxSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/ArrowBowProjectileVfx.cs"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/arrow_bow.gdshader"));

        // Arrow strikes random enemies, so the volley may never guess a target:
        // every creature it draws has to be the one the engine just resolved.
        RegressionTestHarness.Require(
            arrowCard.Contains("onHit: volley.Loose", StringComparison.Ordinal)
            && !arrowCard.Contains("HittableEnemies", StringComparison.Ordinal),
            "Expected Arrow to hand each hit's creature to the volley instead of guessing targets up front.");
        RegressionTestHarness.Require(
            vfxSource.Contains("CelVfxSession.PlayOrResolveAsync", StringComparison.Ordinal)
            && vfxSource.Contains("PreloadManager.Cache", StringComparison.Ordinal)
            && !vfxSource.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the Arrow session to inherit the one card-VFX preference from the shared session entry point.");
        RegressionTestHarness.Require(
            !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal),
            "Expected the Arrow field to take its clock from the shared session without shader TIME or screen sampling.");
    }

    /// <summary>
    /// Arrow's added wait is capped, and the cap has to hold at every hit count a
    /// real play can reach — a full hand plus X energy clears twenty.
    /// </summary>
    /// <remarks>
    /// Asserted on the schedule itself rather than on the constants that feed it.
    /// A cap stated as "four full flights and eight rapid ones" is one widened
    /// tier away from a two-second cutscene, and nothing in a source contract
    /// would notice; summing the real per-hit flight cannot drift the same way.
    /// </remarks>
    [Fact]
    public void ArrowVolleyWaitsBoundedTimeWhateverTheHitCount()
    {
        var flightFor = typeof(ArrowBowProjectileVfx)
            .GetMethod("FlightFor", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected Arrow's private flight schedule.");
        float FlightFor(int index) => (float)flightFor.Invoke(null, [index])!;

        foreach (var hitCount in new[] { 1, 4, 10, 20, 40 })
        {
            var waited = 0d;
            for (var index = 0; index < hitCount; index++)
                waited += FlightFor(index);

            RegressionTestHarness.Require(
                waited <= 0.65d,
                $"Expected a {hitCount}-hit volley to add at most 0.65s of waiting, got {waited:0.###}s.");
        }

        // Past the cap the volley stops waiting but must still be visible, which
        // is the difference between a compressed volley and a dropped hit.
        RegressionTestHarness.Require(
            FlightFor(3) > 0f
            && FlightFor(11) > 0f
            && FlightFor(12) == 0f
            && FlightFor(40) == 0f,
            "Expected every hit up to the cap to travel and every hit past it to spend no time in flight.");
    }

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

    private sealed record VfxCase(CardModel Card, IReadOnlyList<string> VfxAssets);

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
    public void SakuraCardsDoNotDependOnTheRetiredPlaceholderPortrait()
    {
        var sourceCard = new ClowSword();
        RegressionTestHarness.Require(
            sourceCard.CustomPortraitPath == CardModel.MissingPortraitPath
            && sourceCard.PortraitPath == CardModel.MissingPortraitPath
            && sourceCard.BetaPortraitPath == CardModel.MissingPortraitPath,
            "Expected Classic Source Cards to use the native stable missing portrait beneath their full-face renderer.");

        var gale = new Gale();
        var manifestChoice = new ChoiceManifestChoice();
        var drawChoice = new TrueOrFalseDrawChoice();
        foreach (var visual in new[]
                 {
                     (Name: nameof(Gale), Type: typeof(Gale), Card: (CardModel)gale, gale.CustomPortraitPath, gale.PortraitPath, gale.BetaPortraitPath),
                     (Name: nameof(ChoiceManifestChoice), Type: typeof(ChoiceManifestChoice), Card: (CardModel)manifestChoice, manifestChoice.CustomPortraitPath, manifestChoice.PortraitPath, manifestChoice.BetaPortraitPath),
                     (Name: nameof(TrueOrFalseDrawChoice), Type: typeof(TrueOrFalseDrawChoice), Card: (CardModel)drawChoice, drawChoice.CustomPortraitPath, drawChoice.PortraitPath, drawChoice.BetaPortraitPath)
                 })
        {
            var expectedArtPath = ClearCardVisualAssets.ArtPath(visual.Type);
            RegressionTestHarness.Require(
                visual.CustomPortraitPath == CardModel.MissingPortraitPath
                && visual.PortraitPath == CardModel.MissingPortraitPath
                && visual.BetaPortraitPath == CardModel.MissingPortraitPath,
                $"Expected {visual.Name} to keep its hidden native portrait on the stable game-owned resource.");
            RegressionTestHarness.Require(
                SakuraCardFrameVisuals.RunAssetPaths(visual.Card).Contains(expectedArtPath),
                $"Expected {visual.Name} to retain its visible Clear art under the Clear renderer.");
        }

        var classicAssetSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs"));
        var recoverySource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardVisualLifecycle.cs"));
        RegressionTestHarness.Require(
            classicAssetSource.Split("card.PortraitPath", StringSplitOptions.None).Length - 1 == 1,
            "Expected only non-Classic cards to preload their visible native portrait.");
        RegressionTestHarness.Require(
            !recoverySource.Contains("source == nameof(NCard._EnterTree)", StringComparison.Ordinal),
            "Expected _EnterTree recovery to validate the active layout instead of reporting success by source name.");
    }

    [Fact]
    public void MixedHandLayoutPreservesVisibleEdgeSpacing()
    {
        var clearThenClassicHandXs = SakuraHandLayout.CalculateTargetXs(
            [0f, 160f],
            [SakuraCardVisualLayout.Clear, SakuraCardVisualLayout.Classic]);
        var classicThenClearHandXs = SakuraHandLayout.CalculateTargetXs(
            [0f, 160f],
            [SakuraCardVisualLayout.Classic, SakuraCardVisualLayout.Clear]);
        RegressionTestHarness.RequireFloatSequence(
            clearThenClassicHandXs,
            [21.448f, 138.552f],
            "Expected a Clear-to-Classic boundary to preserve the two layouts' blended visible-edge clearance instead of expanding the gap.");
        RegressionTestHarness.RequireFloatSequence(
            classicThenClearHandXs,
            [21.448f, 138.552f],
            "Expected a Classic-to-Clear boundary to preserve the two layouts' blended visible-edge clearance instead of expanding the gap.");

        var mixedThreeCardHandXs = SakuraHandLayout.CalculateTargetXs(
            [0f, 160f, 320f],
            [SakuraCardVisualLayout.Clear, SakuraCardVisualLayout.Classic, SakuraCardVisualLayout.Clear]);
        RegressionTestHarness.RequireFloatSequence(
            mixedThreeCardHandXs,
            [42.896f, 160f, 277.104f],
            "Expected two mixed boundaries to use one geometry-derived compression each without accumulating sequential passes.");

        var nonSakuraBoundaryHandXs = SakuraHandLayout.CalculateTargetXs(
            [0f, 160f, 320f, 480f],
            [SakuraCardVisualLayout.None, SakuraCardVisualLayout.Clear, SakuraCardVisualLayout.Classic, SakuraCardVisualLayout.None]);
        RegressionTestHarness.RequireFloatSequence(
            nonSakuraBoundaryHandXs,
            [45.886f, 185.905f, 303.009f, 434.114f],
            "Expected Clear and Classic outer boundaries to blend toward the native card edge clearance.");

        var classicVanillaProjectedHandXs = SakuraHandLayout.CalculateTargetXs(
            [273f, 460f],
            [
                new SakuraHandCardGeometry(SakuraCardVisualLayout.Classic, 6f, new Vector2(0.8f, 0.8f)),
                new SakuraHandCardGeometry(SakuraCardVisualLayout.None, 9f, new Vector2(0.8f, 0.8f))
            ]);
        RegressionTestHarness.RequireFloatSequence(
            classicVanillaProjectedHandXs,
            [286.964f, 446.036f],
            "Expected a rotated Classic-to-vanilla pair to preserve the native hand's visible-edge relation instead of applying a fixed positive offset.");

        RegressionTestHarness.RequireFloatSequence(
            SakuraHandLayout.CalculateTargetXs(
                [0f, 160f, 320f],
                [SakuraCardVisualLayout.Clear, SakuraCardVisualLayout.Clear, SakuraCardVisualLayout.Clear]),
            [31.5f, 160f, 288.5f],
            "Expected all-Clear hand spacing to retain its existing footprint.");
        RegressionTestHarness.RequireFloatSequence(
            SakuraHandLayout.CalculateTargetXs(
                [0f, 160f, 320f],
                [SakuraCardVisualLayout.Classic, SakuraCardVisualLayout.Classic, SakuraCardVisualLayout.Classic]),
            [54f, 160f, 266f],
            "Expected all-Classic hand spacing to retain its existing footprint.");
        RegressionTestHarness.RequireFloatSequence(
            SakuraHandLayout.CalculateTargetXs(
                [10f, 170f],
                [SakuraCardVisualLayout.None, SakuraCardVisualLayout.None]),
            [10f, 170f],
            "Expected non-Sakura hands to keep vanilla target positions.");
    }

    [Fact]
    public void TransparentCardsDoNotUseRetiredNonClearFrames()
    {
        RegressionTestHarness.Require(
            SakuraTransparentCardCatalog.TransparentCardTypes
                .Select(type => (CardModel)Activator.CreateInstance(type)!)
                .All(card => !SakuraCardFrameVisuals.UsesCustomNonClearFrame(card)),
            "Expected active Clear Sakura cards not to use retired custom non-Clear frames.");
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
            SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: false,
                isFocused: false,
                isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: false,
                isFocused: true,
                isCurrentCardPlay: false)
            && !SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: true,
                isFocused: false,
                isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: true,
                isFocused: true,
                isCurrentCardPlay: false)
            && SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: true,
                isFocused: false,
                isCurrentCardPlay: true),
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
    public void ClassicAndClearDescriptionGeometryUsesSharedFixedBounds()
    {
        var classicRegion = SakuraDescriptionRegion.RegionBox(SakuraCardVisualLayout.Classic);
        var clearRegion = SakuraDescriptionRegion.RegionBox(SakuraCardVisualLayout.Clear);
        var classicText = SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Classic);
        var clearText = SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Clear);
        RegressionTestHarness.Require(
            classicRegion.Size == clearRegion.Size
            && classicText.Size == clearText.Size
            && classicRegion.Size == new Vector2(204f, 236f)
            && classicText.Size == new Vector2(190f, 200f),
            "Expected Classic and Clear cards to use identical fixed description region and native text-box sizes.");
        RegressionTestHarness.Require(
            classicText.Position == new Vector2(15.5f, 221f)
            && clearText.Position == new Vector2(13.15f, 206f),
            "Expected the wider shared text box to retain each frame's horizontal alignment and use the upper region space.");
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
        const string sakuraIdentityKeywordText =
            "[center][gold]Sakura Card[/gold] [gold]Fiery[/gold] [gold]Removable[/gold].\nCopy a [gold]Sakura Card[/gold].[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new SakuraSword(), sakuraIdentityKeywordText)
                == "「[gold]Sakura Card[/gold]」「[gold]Fiery[/gold]」 [gold]Removable[/gold]\nCopy a [gold]Sakura Card[/gold].",
            "Expected Sakura Card and element identity labels to be bracketed without decorating unrelated keywords or body references.");
        const string transparentElementOnlyHeader =
            "[center][gold]Watery[/gold]\nChoose 1 enemy.[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new Mirage(), transparentElementOnlyHeader)
                == "「[gold]Watery[/gold]」\nChoose 1 enemy.",
            "Expected transparent element headers to receive identity brackets even when the card also owns native keywords not shown in the header.");
        const string transparentEarthHeader =
            "[center][gold]Earthy[/gold]\nGain [gold]3[/gold] Regen.[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new Repair(), transparentEarthHeader)
                == "「[gold]Earthy[/gold]」\nGain [gold]3[/gold] Regen.",
            "Expected transparent Earth headers to receive identity brackets when only the element label is rendered.");
        const string aquaElementOnlyHeader =
            "[center][gold]Watery[/gold]\nDeal [gold]4[/gold] damage.[/center]";
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.NormalizeText(new Aqua(), aquaElementOnlyHeader)
                == "「[gold]Watery[/gold]」\nDeal [gold]4[/gold] damage.",
            "Expected transparent cards without Exhaust in the header to still bracket their element identity label.");
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
    public void DescriptionProjectionPreservesClowExtraDescriptionFragmentsInCardLibrary()
    {
        var card = new ClowSword();
        const string nativeText = "Deal 15 damage.\n[gold]Extra:[/gold] Also deal 15 HP loss.";

        var projected = SakuraDescriptionRegion.ProjectText(card, nativeText);

        RegressionTestHarness.Require(
            projected.Contains("[gold]Extra:[/gold] Also deal 15 HP loss.", StringComparison.Ordinal),
            "Expected the card-library projection to preserve Clow extra-description fragments.");
    }

    [Fact]
    public void DescriptionProjectionPreservesNativeSynchronizedFragmentPlacement()
    {
        var card = new ClowRain();
        const string nativeText = "Deal damage.\n[gold]Synced:[/gold] ClowShield.";
        var synchronizedLine = SakuraStateText.SynchronizedLine(["ClowShield"]);

        RegressionTestHarness.Require(
            SakuraDescriptionRegion.ProjectText(card, nativeText) == nativeText,
            "Expected the shared projection to preserve a native AfterBase synchronized fragment without moving or re-appending it.");
        RegressionTestHarness.Require(
            !synchronizedLine.StartsWith('\n') && !synchronizedLine.StartsWith('\r'),
            "Expected the native AfterBase synchronized fragment to own its line boundary without a leading manual newline.");
    }

    [Fact]
    public void HandHighlightPolicyGatesExtraEffectsByPlayabilityAndLeavesForgottenToOverlay()
    {
        RegressionTestHarness.Require(
            SakuraHandHighlightPolicy.Select(
                new ClowSword(),
                isPlayable: true,
                extraEffectActive: true) == SakuraHandHighlightKind.ExtraEffect
            && SakuraHandHighlightPolicy.Select(
                new Gale(),
                isPlayable: true,
                extraEffectActive: true) == SakuraHandHighlightKind.ExtraEffect,
            "Expected active Clow and Transparent Extra Effects to select the gold hand highlight.");

        RegressionTestHarness.Require(
            SakuraHandHighlightPolicy.Select(
                new SakuraSword(),
                isPlayable: true,
                extraEffectActive: true) == SakuraHandHighlightKind.None
            && SakuraHandHighlightPolicy.Select(
                new ClowSword(),
                isPlayable: false,
                extraEffectActive: true) == SakuraHandHighlightKind.None
            && SakuraHandHighlightPolicy.Select(
                new Gale(),
                isPlayable: true,
                extraEffectActive: false) == SakuraHandHighlightKind.None,
            "Expected Sakura-form, unplayable, and inactive Extra Effect cards to keep the native highlight color.");

        var source = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraHandHighlightVisual.cs"));
        RegressionTestHarness.Require(
            !source.Contains("IsTemporary", StringComparison.Ordinal),
            "Expected Forgotten to be shown by the card overlay, not by a hand highlight color that hides playability.");
    }

    [Fact]
    public void HandHighlightVisualUsesCardPlayabilityInsteadOfOwnedColorAsGate()
    {
        var source = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraHandHighlightVisual.cs"));
        RegressionTestHarness.Require(
            source.Contains("model.CanPlay(),", StringComparison.Ordinal)
            && !source.Contains("hasNativePlayableHighlight || hasOwnedHighlight", StringComparison.Ordinal),
            "Expected the gold Extra Effect highlight to use CardModel.CanPlay() even after a prior Sakura-owned color was applied.");
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
    public void ForgottenOverlayLayoutMatchesModAfflictionOverlayWrapperWithMoteMargin()
    {
        foreach (var (layout, size, root) in new[]
                 {
                     (SakuraCardVisualLayout.None, SakuraCardGeometry.VanillaLayoutSize, Vector2.Zero),
                     (SakuraCardVisualLayout.Clear, SakuraCardGeometry.ClearLayoutSize, SakuraCardGeometry.ClearLayoutSize * 0.5f),
                     (SakuraCardVisualLayout.Classic, SakuraCardGeometry.ClassicLayoutSize, SakuraCardGeometry.ClassicLayoutSize * 0.5f)
                 })
        {
            var (rootPosition, frame, topMargin) = SakuraForgottenOverlayPolicy.Layout(layout);
            var margin = size.Y * SakuraForgottenOverlayPolicy.MoteMarginFraction;
            RegressionTestHarness.Require(
                rootPosition == root
                && frame.Position.IsEqualApprox(new Vector2(size.X * -0.5f, size.Y * -0.5f - margin))
                && frame.End.IsEqualApprox(size * 0.5f)
                && Mathf.IsEqualApprox(topMargin, margin / (size.Y + margin)),
                $"Expected the {layout} overlay to cover the face exactly and extend above it only by the mote margin.");
        }
    }

    [Fact]
    public void ForgottenOverlayFadesOnlyTheCardArt()
    {
        RegressionTestHarness.Require(
            Mathf.IsEqualApprox(SakuraForgottenOverlayPolicy.ArtAlpha(reveal: 0f, boost: 1f), 1f)
            && Mathf.IsEqualApprox(SakuraForgottenOverlayPolicy.ArtAlpha(reveal: 1f, boost: 0f), SakuraForgottenOverlayPolicy.FadedArtAlpha)
            && Mathf.IsEqualApprox(SakuraForgottenOverlayPolicy.ArtAlpha(reveal: 1f, boost: 1f), SakuraForgottenOverlayPolicy.PulseArtAlpha)
            && SakuraForgottenOverlayPolicy.PulseArtAlpha < SakuraForgottenOverlayPolicy.FadedArtAlpha,
            "Expected Forgotten art to be opaque while hidden, translucent when shown, and fainter during the end-turn pulse.");

        RegressionTestHarness.Require(
            SakuraForgottenOverlayPolicy.ArtNodeName(SakuraCardVisualLayout.Classic) == ClassicCardLayout.FaceNodeName
            && SakuraForgottenOverlayPolicy.ArtNodeName(SakuraCardVisualLayout.Clear) == ClearCardLayout.ArtNodeName
            && SakuraForgottenOverlayPolicy.ArtNodeName(SakuraCardVisualLayout.None) is null,
            "Expected Sakura faces to fade their single art node and vanilla cards to fall back to the native portrait group.");

        var face = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraCardVisualFace.cs"));
        var overlay = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraForgottenOverlay.cs"));
        RegressionTestHarness.Require(
            ((ISakuraCardLayoutAdapter)ClassicCardLayout.Instance).ArtNodeName == ClassicCardLayout.FaceNodeName
            && ((ISakuraCardLayoutAdapter)ClearCardLayout.Instance).ArtNodeName == ClearCardLayout.ArtNodeName
            && face.Contains("Name = RequireAdapter().ArtNodeName,", StringComparison.Ordinal)
            && overlay.Contains("\"_portraitCanvasGroup\"", StringComparison.Ordinal)
            && overlay.Contains("SelfModulate", StringComparison.Ordinal),
            "Expected the renderers to name their art nodes through the shared constants the Forgotten overlay resolves.");
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
    public void MemoryVisualsNeverTakeOverCardNodesAndResetPooledOverlays()
    {
        var memoryPile = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraMemoryPile.cs"));
        RegressionTestHarness.Require(
            memoryPile.Contains("skipVisuals: false", StringComparison.Ordinal)
            && memoryPile.Contains("SakuraMemoryArrivalRipple.EnsureSubscribed(pile, card.Owner);", StringComparison.Ordinal),
            "Expected Forgotten cards to keep the native flight into Memory, with the ripple driven by the pile's own arrival event.");

        var patches = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraCardVisualPatches.cs"));
        RegressionTestHarness.Require(
            !patches.Contains("NCardFlyVfx", StringComparison.Ordinal)
            && patches.Contains("RegisterPatch<SakuraMemoryRecallCardNodePatch>", StringComparison.Ordinal)
            && patches.Contains("RegisterPatch<SakuraCardReturnedFromPoolPatch>", StringComparison.Ordinal)
            && patches.Contains("nameof(NCard.OnReturnedFromPool)", StringComparison.Ordinal),
            "Expected no takeover of native card flights, the recall start patch, and a pool-reuse reset.");

        var overlay = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraForgottenOverlay.cs"));
        var memoryPileSource = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraMemoryPile.cs"));
        RegressionTestHarness.Require(
            overlay.Contains("_transitModel", StringComparison.Ordinal)
            && overlay.Contains("public static void ResetForReuse(NCard card)", StringComparison.Ordinal)
            && memoryPileSource.Contains("SakuraMemoryRecallVisual.Mark(card);", StringComparison.Ordinal)
            && memoryPileSource.Contains("SakuraMemoryRecallVisual.Unmark(card);", StringComparison.Ordinal),
            "Expected the recall develop to be bound to its model and every pooled node to be reset before reuse.");
    }

    [Fact]
    public void ForgottenOverlayStaysOutOfNativeOverlaySlotAndRidesEndTurnFlash()
    {
        var overlay = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraForgottenOverlay.cs"));
        RegressionTestHarness.Require(
            !overlay.Contains("_cardOverlay", StringComparison.Ordinal)
            && !overlay.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && overlay.Contains("OverlayContainer", StringComparison.Ordinal),
            "Expected the Forgotten overlay to be an extra OverlayContainer child, not the native Affliction overlay slot.");

        var shader = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraMod/shaders/card_vfx/forgotten_overlay.gdshader"));
        RegressionTestHarness.Require(
            !shader.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal),
            "Expected the Forgotten overlay shader not to sample the screen; overlapping hand cards would read a stale copy.");

        var patches = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraCardVisualPatches.cs"));
        var character = File.ReadAllText(
            RegressionTestHarness.FindRepoFile("SakuraModCode/Character/ClassicSakura.cs"));
        RegressionTestHarness.Require(
            patches.Contains("RegisterPatch<SakuraPlayerHandFlashPlayableHoldersPatch>", StringComparison.Ordinal)
            && patches.Contains("nameof(NPlayerHand.FlashPlayableHolders)", StringComparison.Ordinal)
            && character.Contains("SakuraForgottenOverlay.AssetPaths", StringComparison.Ordinal),
            "Expected the end-turn pulse patch to be registered and the overlay shader to be preloaded with the character.");
    }

    [Fact]
    public void CustomFramesLeaveNativeUnplayableOverlayUntouched()
    {
        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs",
                     "SakuraModCode/Cards/ClearCardVisualPatch.cs",
                     "SakuraModCode/Cards/SakuraCardVisualFace.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                source.Contains("nodes.EnergyIcon", StringComparison.Ordinal)
                && source.Contains("nodes.EnergyLabel", StringComparison.Ordinal),
                $"Expected {relativePath} to keep owning the custom energy icon and cost label.");
            RegressionTestHarness.Require(
                !source.Contains("UnplayableEnergyIconField", StringComparison.Ordinal)
                && !source.Contains("nodes.UnplayableEnergyIcon", StringComparison.Ordinal)
                && !source.Contains("unplayableIcon", StringComparison.Ordinal),
                $"Expected {relativePath} to leave the native child unplayable overlay geometry, texture, and visibility unmodified.");
        }
    }

    [Fact]
    public void CustomFramesPreserveNativeEnchantmentVisualOwnership()
    {
        foreach (var relativePath in new[]
                 {
                     "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs",
                     "SakuraModCode/Cards/ClearCardVisualPatch.cs",
                     "SakuraModCode/Cards/SakuraCardVisualFace.cs"
                 })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));
            RegressionTestHarness.Require(
                !source.Contains("\"_enchantmentTab\"", StringComparison.Ordinal)
                && !source.Contains("\"_enchantmentVfxOverride\"", StringComparison.Ordinal),
                $"Expected {relativePath} to preserve native enchantment visibility instead of classifying the tab and VFX override as hidden frame nodes.");
        }

        var geometrySource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardGeometryLifecycle.cs"));
        RegressionTestHarness.Require(
            geometrySource.Contains("card.EnchantmentTab", StringComparison.Ordinal)
            && geometrySource.Contains("card.EnchantmentVfxOverride", StringComparison.Ordinal)
            && geometrySource.Contains("TryGetPositionBaseline", StringComparison.Ordinal),
            "Expected the shared geometry lifecycle to adapt both native enchantment controls from their borrowed native position baselines.");
        RegressionTestHarness.Require(
            SakuraCardGeometry.MapNativeCenteredOverlayPosition(new Vector2(-166f, -161f)) == new Vector2(-16f, 50f)
            && SakuraCardGeometry.MapNativeCenteredOverlayPosition(new Vector2(-202f, -142f)) == new Vector2(-52f, 69f),
            "Expected native centered enchantment controls to map into Sakura's top-left local card space without resizing.");

        var patchSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardVisualPatches.cs"));
        RegressionTestHarness.Require(
            patchSource.Contains("NCardOnEnchantmentChangedTarget", StringComparison.Ordinal)
            && patchSource.Contains("SakuraCardEnchantmentChangedGeometryPatch", StringComparison.Ordinal)
            && patchSource.Contains("AfterNativeEnchantmentChanged", StringComparison.Ordinal),
            "Expected live enchantment changes to restore the native tab's Sakura geometry after NCard resets its position.");
    }
}
