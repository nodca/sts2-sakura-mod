using System.Text.Json;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Scaffolding.Content;

public sealed class FourthActRoutingSuite
{
    [Fact]
    public void ProductionCatalogEmitsTheCompleteWindToDarkRoute()
    {
        var resolution = FourthActRouteCatalog.Resolve();
        var route = Assert.Single(resolution.CompleteRoutes);
        Assert.Equal(SakuraElement.Wind, route.Element);
        Assert.Equal(typeof(DarkEncounter), route.Endpoint.EncounterType);
        Assert.True(resolution.HasCompleteRoutes);
        Assert.True(FourthActEntryRegistration.CanRegister(resolution));
        Assert.Equal(
            [typeof(FlyEncounter), typeof(IllusionEncounter), typeof(WindyEncounter), typeof(DarkEncounter)],
            resolution.CompleteEncounterTypes);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void FourthActEntryRequiresBothTheSettingAndASakuraRun(
        bool fourthActEnabled,
        bool isSakuraRun,
        bool expected) =>
        Assert.Equal(expected, FourthActEntryRegistration.CanEnter(fourthActEnabled, isSakuraRun));

    [Fact]
    public void FourthActIsNonRandomAndEncountersAreScopedToIt()
    {
        var act = new SakuraFourthAct();
        Assert.Equal(3, act.Index);
        Assert.False(act.IsDefault);
        Assert.False(act.AllowInRandomActList);

        Assert.All(new ModEncounterTemplate[]
        {
            new FlyEncounter(), new IllusionEncounter(), new WindyEncounter(), new DarkEncounter()
        }, encounter =>
        {
            Assert.True(encounter.IsValidForAct(act));
            Assert.False(encounter.IsValidForAct(new Glory()));
        });
    }

    [Fact]
    public void MapFactoryInterceptsOnlyTheFourthAct()
    {
        Assert.True(SakuraFourthActMapFactory.TryCreate(new SakuraFourthAct(), out var fourthActMap));
        Assert.IsType<SakuraFourthActMap>(fourthActMap);
        Assert.False(SakuraFourthActMapFactory.TryCreate(new Glory(), out var vanillaMap));
        Assert.Null(vanillaMap);
    }

    [Theory]
    [InlineData(true, true, 2, 3, true)]
    [InlineData(false, true, 2, 3, false)]
    [InlineData(true, false, 2, 3, false)]
    [InlineData(true, true, 1, 3, false)]
    [InlineData(true, true, 2, 4, false)]
    public void RunTransitionOnlyAppendsTheMissingSakuraFourthSlot(
        bool hasCompleteRoute,
        bool canEnterFourthAct,
        int currentActIndex,
        int actCount,
        bool expected) =>
        Assert.Equal(
            expected,
            SakuraFourthActRunTransition.ShouldAppendSlot(
                hasCompleteRoute,
                canEnterFourthAct,
                currentActIndex,
                actCount));

    [Theory]
    [InlineData(true, true, true, 2, 3, true)]
    [InlineData(true, true, true, 2, 4, false)]
    [InlineData(true, true, false, 2, 3, false)]
    [InlineData(true, false, true, 2, 3, false)]
    [InlineData(false, true, true, 2, 3, false)]
    [InlineData(true, true, true, 1, 3, false)]
    public void TerminalDarkOnlyRoutesTheFinalFourthActToTheArchitect(
        bool isSakuraFourthAct,
        bool isDarkEncounter,
        bool isBossRoom,
        int currentActIndex,
        int actCount,
        bool expected)
    {
        ActModel act = isSakuraFourthAct ? new SakuraFourthAct() : new Glory();
        EncounterModel encounter = isDarkEncounter ? new DarkEncounter() : new WindyEncounter();
        var current = new MapCoord { col = 0, row = 3 };
        var boss = new MapCoord { col = 0, row = 3 };

        Assert.Equal(
            expected,
            SakuraFourthActTerminalTransition.ShouldRouteToArchitect(
                act,
                encounter,
                shouldGiveRewards: false,
                isBossRoom ? RoomType.Boss : RoomType.Elite,
                currentActIndex,
                actCount,
                current,
                boss));
    }

    [Fact]
    public void TerminalDarkRejectsRewardGivingOrNonTerminalCoordinates()
    {
        var act = new SakuraFourthAct();
        var encounter = new DarkEncounter();
        var boss = new MapCoord { col = 0, row = 3 };

        Assert.False(SakuraFourthActTerminalTransition.ShouldRouteToArchitect(
            act,
            encounter,
            shouldGiveRewards: true,
            RoomType.Boss,
            currentActIndex: 2,
            actCount: 3,
            currentMapCoord: boss,
            bossMapCoord: boss));
        Assert.False(SakuraFourthActTerminalTransition.ShouldRouteToArchitect(
            act,
            encounter,
            shouldGiveRewards: false,
            RoomType.Boss,
            currentActIndex: 2,
            actCount: 3,
            currentMapCoord: new MapCoord { col = 1, row = 3 },
            bossMapCoord: boss));
    }

    [Fact]
    public void FourthActSettingUsesOneDefaultOffRitsuToggle()
    {
        var page = SakuraModConfig.BuildSettingsPageForTests();
        var section = Assert.Single(
            page.Sections,
            static section => section.Id == SakuraModConfig.GameplaySectionId);
        var toggle = Assert.IsType<ToggleModSettingsEntryDefinition>(Assert.Single(section.Entries));
        var defaultBinding = Assert.IsAssignableFrom<IDefaultModSettingsValueBinding<bool>>(toggle.Binding);

        Assert.False(new SakuraModConfig().EnableFourthAct);
        Assert.Equal(SakuraModConfig.FourthActToggleId, toggle.Id);
        Assert.False(defaultBinding.CreateDefaultValue());

        foreach (var locale in new[] { "eng", "zhs" })
        {
            var relativePath = $"SakuraMod/localization/{locale}/settings_ui.json";
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
                ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
            Assert.False(string.IsNullOrWhiteSpace(settings[SakuraModConfig.FourthActTitleKey]));
            Assert.False(string.IsNullOrWhiteSpace(settings[SakuraModConfig.FourthActDescriptionKey]));
        }
    }

    [Fact]
    public void FourthActTitleIsLocalizedInBothSupportedLocales()
    {
        const string key = "SAKURA_MOD_ACT_SAKURA_FOURTH_ACT.title";
        Assert.Contains(key, File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/acts.json")));
        Assert.Contains(key, File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/acts.json")));
    }

    [Fact]
    public void CompletenessUsesEncounterRoomTypesAndAuthoritativeCardElements()
    {
        var complete = Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy);
        var wrongElement = Route(SakuraElement.Water, SourceCardIdentity.Fly, SourceCardIdentity.Windy);
        var missingEndpoint = new FourthActRouteDefinition(
            SakuraElement.Wind,
            WindEnemyCatalog.EliteEncounters,
            WindEnemyCatalog.BossEncounter,
            new(FourthActEndpoint.Dark, null));

        Assert.True(FourthActRouteResolver.Resolve([complete]).HasCompleteRoutes);
        Assert.False(FourthActRouteResolver.Resolve([wrongElement]).HasCompleteRoutes);
        Assert.False(FourthActRouteResolver.Resolve([missingEndpoint]).HasCompleteRoutes);
    }

    [Fact]
    public void RouteDefinitionDefersResolutionUntilTheRuntimeSeam()
    {
        var route = Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy);
        var unresolved = new FourthActRouteResolutionContext(
            static _ => null,
            static _ => null);
        var resolved = new FourthActRouteResolutionContext(
            static encounterType => encounterType == typeof(FlyEncounter)
                ? RoomType.Elite
                : encounterType == typeof(WindyEncounter)
                    ? RoomType.Boss
                    : null,
            static identity => identity is SourceCardIdentity.Fly or SourceCardIdentity.Windy
                ? SakuraElementSet.Wind
                : null);

        Assert.Empty(FourthActRouteResolver.Resolve([route], unresolved).CompleteRoutes);
        var successfulResolution = FourthActRouteResolver.Resolve([route], resolved);
        Assert.Single(successfulResolution.CompleteRoutes);
        Assert.Empty(successfulResolution.Diagnostics);
    }

    [Fact]
    public void RouteResolutionReportsIncompleteRouteDiagnostics()
    {
        var route = new FourthActRouteDefinition(
            SakuraElement.Water,
            [new(typeof(FlyEncounter), SourceCardIdentity.Fly)],
            null,
            new(FourthActEndpoint.Dark, null));
        var resolution = FourthActRouteResolver.Resolve(
            [route],
            new FourthActRouteResolutionContext(
                static _ => RoomType.Elite,
                static _ => SakuraElementSet.Wind));

        Assert.Empty(resolution.CompleteRoutes);
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Code == "elite-element-mismatch");
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Code == "elemental-boss-missing");
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Code == "endpoint-missing");
    }

    [Fact]
    public void CompleteRoutesHaveStableElementOrder()
    {
        var routes = new[]
        {
            Route(SakuraElement.Earth, SourceCardIdentity.Earthy, SourceCardIdentity.Earthy),
            Route(SakuraElement.Fire, SourceCardIdentity.Firey, SourceCardIdentity.Firey),
            Route(SakuraElement.Water, SourceCardIdentity.Watery, SourceCardIdentity.Watery),
            Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy)
        };

        Assert.Equal(
            [SakuraElement.Wind, SakuraElement.Water, SakuraElement.Fire, SakuraElement.Earth],
            FourthActRouteCatalog.CompleteRoutesFrom(routes).Select(static route => route.Element));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NativeMapForkMatchesCompleteRouteCount(int routeCount)
    {
        var routes = new[]
        {
            Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy),
            Route(SakuraElement.Water, SourceCardIdentity.Watery, SourceCardIdentity.Watery),
            Route(SakuraElement.Fire, SourceCardIdentity.Firey, SourceCardIdentity.Firey),
            Route(SakuraElement.Earth, SourceCardIdentity.Earthy, SourceCardIdentity.Earthy)
        };
        var map = new SakuraFourthActMap(routes.Take(routeCount));

        Assert.Equal(MapPointType.Ancient, map.StartingMapPoint.PointType);
        Assert.Equal(MapPointType.Shop, map.MerchantMapPoint.PointType);
        Assert.Equal(routeCount, map.MerchantMapPoint.Children.Count);
        Assert.Equal(routeCount == 1 ? 1 : routeCount * 2 - 1, map.GetColumnCount());
        Assert.All(map.MerchantMapPoint.Children, elite =>
        {
            Assert.Equal(MapPointType.Elite, elite.PointType);
            var elementalBoss = Assert.Single(elite.Children);
            Assert.Equal(MapPointType.Boss, elementalBoss.PointType);
            var restSite = Assert.Single(elementalBoss.Children);
            Assert.Equal(MapPointType.RestSite, restSite.PointType);
            Assert.Same(map.BossMapPoint, Assert.Single(restSite.Children));
            Assert.NotNull(map.RouteAt(restSite.coord));
        });
    }

    [Fact]
    public void MapRejectsZeroIncompleteOrExcessRoutes()
    {
        var complete = Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy);

        Assert.Throws<ArgumentException>(() => new SakuraFourthActMap([]));
        var incomplete = new[]
        {
            new FourthActRouteDefinition(
                SakuraElement.Wind,
                WindEnemyCatalog.EliteEncounters,
                WindEnemyCatalog.BossEncounter,
                new(FourthActEndpoint.Dark, null))
        };
        Assert.Throws<ArgumentException>(() => new SakuraFourthActMap(incomplete));
        Assert.Throws<ArgumentException>(() => new SakuraFourthActMap(Enumerable.Repeat(complete, 5)));
    }

    [Fact]
    public void NativeSerializationRoundTripPreservesTheFork()
    {
        var map = new SakuraFourthActMap(
        [
            Route(SakuraElement.Wind, SourceCardIdentity.Fly, SourceCardIdentity.Windy),
            Route(SakuraElement.Water, SourceCardIdentity.Watery, SourceCardIdentity.Watery)
        ]);

        var restored = new SavedActMap(SerializableActMap.FromActMap(map));
        var merchant = restored.GetPoint(map.MerchantMapPoint.coord)!;

        Assert.Equal(MapPointType.Shop, merchant.PointType);
        Assert.Equal(2, merchant.Children.Count);
        Assert.All(merchant.Children, elite =>
        {
            var elementalBoss = Assert.Single(elite.Children);
            var restSite = Assert.Single(elementalBoss.Children);
            Assert.Same(restored.BossMapPoint, Assert.Single(restSite.Children));
        });
    }

    [Fact]
    public void SaveCompatibilityNormalizesOmittedFourthActRoomCollections()
    {
        var rooms = new SerializableRoomSet
        {
            EventIds = null!,
            NormalEncounterIds = null!,
            EliteEncounterIds = null!
        };

        SakuraFourthActSaveCompatibility.NormalizeRoomCollections(rooms);

        Assert.Empty(rooms.EventIds);
        Assert.Empty(rooms.NormalEncounterIds);
        Assert.Empty(rooms.EliteEncounterIds);
    }

    private static FourthActRouteDefinition Route(
        SakuraElement element,
        SourceCardIdentity eliteIdentity,
        SourceCardIdentity bossIdentity) =>
        new(
            element,
            [new(typeof(FlyEncounter), eliteIdentity)],
            new(typeof(WindyEncounter), bossIdentity),
            new(FourthActEndpoint.Dark, typeof(WindyEncounter)));
}
