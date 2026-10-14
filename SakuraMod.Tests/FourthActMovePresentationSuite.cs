using System.Reflection;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

public sealed class FourthActMovePresentationSuite
{
    private static IReadOnlyList<Type> FourthActMonsters { get; } =
        typeof(FourthActCombatRules).Assembly.GetTypes()
            .Where(static type => !type.IsAbstract
                && typeof(MonsterModel).IsAssignableFrom(type)
                && type.Namespace?.Contains(".FourthAct.", StringComparison.Ordinal) == true)
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToList();

    public static TheoryData<Type> FourthActMonsterTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in FourthActMonsters)
            data.Add(type);
        return data;
    }

    [Fact]
    public void EveryFourthActMonsterSharesThePresentationTemplateAndHasAnElement()
    {
        Assert.All(FourthActMonsters, static type =>
            Assert.True(typeof(FourthActMonsterTemplate).IsAssignableFrom(type), type.Name));
        Assert.Equal(
            FourthActMonsters.OrderBy(static type => type.Name),
            FourthActMovePresentationCatalog.MonsterTypes.OrderBy(static type => type.Name));
    }

    [Theory]
    [MemberData(nameof(FourthActMonsterTypes))]
    public void EveryMoveStateHasExactlyOnePresentation(Type monsterType)
    {
        var monster = (MonsterModel)Activator.CreateInstance(monsterType)!;
        var moves = ((MonsterMoveStateMachine)monsterType
                .GetMethod("GenerateMoveStateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(monster, null)!)
            .States.Values.OfType<MoveState>().Select(static move => move.StateId).ToHashSet(StringComparer.Ordinal);
        var entries = FourthActMovePresentationCatalog.MovesOf(monsterType);

        Assert.All(moves, move => Assert.True(entries.ContainsKey(move), $"{monsterType.Name}.{move}"));
        Assert.All(entries.Keys.Where(key => !moves.Contains(key)), key =>
            Assert.True(FourthActMovePresentationCatalog.SubActionIds.Contains(key), $"stale {monsterType.Name}.{key}"));
        var element = FourthActMovePresentationCatalog.ElementOf(monsterType);
        Assert.All(entries.Values, presentation => Assert.Equal(element, presentation.Element));
    }

    [Fact]
    public void EverySubActionIsResolvedByItsMonster()
    {
        var sources = FourthActModelSources();
        foreach (var id in FourthActMovePresentationCatalog.SubActionIds)
        {
            var name = typeof(FourthActMovePresentationCatalog)
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Single(field => field.IsLiteral && (string?)field.GetRawConstantValue() == id).Name;
            Assert.Contains(sources, source =>
                source.Contains($"FourthActMovePresentationCatalog.{name}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void EveryFourthActAttackCarriesItsMovePresentation()
    {
        var sources = FourthActModelSources();
        var attacks = sources.Sum(static source => Count(source, "DamageCmd.Attack("));
        var dressed = sources.Sum(static source =>
            Count(source, "AttackAsync(") + Count(source, "WithMoveFx("));

        Assert.True(attacks > 0);
        Assert.Equal(attacks, dressed);
        Assert.All(sources, static source =>
        {
            Assert.DoesNotContain("FourthActEnemyActionCmd", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAttackerAnim(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CreatureCmd.Damage(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SakuraStandeeVisuals.Create(", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void EveryEffectSceneDeclaresItsTimelineAndShipsWithTheMonster()
    {
        foreach (var id in Enum.GetValues<FourthActFxId>())
        {
            var path = FourthActFxScenes.PathFor(id);
            var scene = File.ReadAllText(RepoFile(path));
            var contact = Metadata(scene, FourthActFxPlayer.ContactTimeMeta);
            var lifetime = Metadata(scene, FourthActFxPlayer.LifetimeMeta);
            Assert.True(contact is >= 0f, $"{id} contact_time");
            if (scene.Contains("metadata/ambient_layer", StringComparison.Ordinal))
            {
                // Ambient layers live as long as their monster and loop instead of expiring.
                Assert.Contains("loop_mode = 1", scene, StringComparison.Ordinal);
                Assert.Matches(new Regex(@"metadata/ambient_layer = ""(behind|room)"""), scene);
            }
            else if (scene.Contains("metadata/persistent = true", StringComparison.Ordinal))
            {
                // Persistent layers (Windy's wall) loop until the power that owns them frees them.
                Assert.Contains("loop_mode = 1", scene, StringComparison.Ordinal);
            }
            else
            {
                Assert.True(lifetime > contact, $"{id} lifetime {lifetime} must outlast contact {contact}");
            }

            if (scene.Contains("metadata/travel_time", StringComparison.Ordinal))
                Assert.Equal(Metadata(scene, FourthActFxPlayer.TravelTimeMeta), contact);
            Assert.Contains("[node name=", scene, StringComparison.Ordinal);
        }

        // Instances can't be asked for AssetPaths here (Windy resolves its summons through ModelDb),
        // so check the catalog half and that the template always appends it.
        var template = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/FourthActMonsterTemplate.cs"));
        Assert.Contains("public sealed override IEnumerable<string> AssetPaths", template, StringComparison.Ordinal);
        Assert.Contains(".Concat(FourthActMovePresentationCatalog.AssetPathsFor(GetType()))", template, StringComparison.Ordinal);
        foreach (var monster in FourthActMonsters)
        {
            var assets = FourthActMovePresentationCatalog.AssetPathsFor(monster).ToHashSet(StringComparer.Ordinal);
            foreach (var fx in FourthActMovePresentationCatalog.FxOf(monster))
                Assert.Contains(FourthActFxScenes.PathFor(fx), assets);
            var shader = FourthActElements.StandeeShaderPath(FourthActMovePresentationCatalog.ElementOf(monster));
            Assert.Contains(shader, assets);
            Assert.True(File.Exists(RepoFile(shader)), shader);
        }
    }

    [Fact]
    public void ImpactVariantsCycleFromTheVariantTheMovePasses()
    {
        var sword = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Fire.Models.SwordMonster), "DOUBLE_CUT");
        Assert.Equal(FourthActFxId.SwordCutDown, sword.ImpactFor(0, 0));
        Assert.Equal(FourthActFxId.SwordCutUp, sword.ImpactFor(0, 1));
        Assert.Equal(FourthActFxId.SwordCutDown, sword.ImpactFor(0, 2));

        var libra = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Fire.Models.LibraPanMonster), "WEIGH");
        Assert.Equal(FourthActFxId.LibraMoonWeight, libra.ImpactFor(0, 0));
        Assert.Equal(FourthActFxId.LibraSunWeight, libra.ImpactFor(1, 0));

        var plain = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Fire.Models.FireyMonster), "FLAME_BREATH");
        Assert.Equal(FourthActFxId.FireImpact, plain.ImpactFor(3, 5));

        var fire = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Fire/Models/FireMonsters.cs"));
        foreach (var call in new[] { "variant: IsLeftPan ? 0 : 1", "FireballTier(burns), player.Creature", "JudgmentTier(hand), player.Creature" })
            Assert.Contains(call, fire, StringComparison.Ordinal);

        Assert.Equal([0, 0, 1, 1, 2], new[] { 0, 3, 4, 7, 8 }.Select(SakuraMod.SakuraModCode.FourthAct.Earth.Models.WoodMonster.SnareTier));
        Assert.Equal([0, 0, 1, 1, 2], new[] { 0, 2, 3, 5, 6 }.Select(SakuraMod.SakuraModCode.FourthAct.Earth.Models.EarthyMonster.FissureTier));
        var tremor = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Earth.Models.EarthyMonster), "TREMOR");
        Assert.Equal(FourthActFxId.FissureLarge, tremor.ImpactFor(2, 0));
        var earth = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Earth/Models/EarthMonsters.cs"));
        foreach (var call in new[] { "SnareTier(count)", "FissureTier(sediment)" })
            Assert.Contains(call, earth, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenShakeUsesTheNativeCameraShakeOnHits()
    {
        var landslide = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Earth.Models.EarthyMonster), "LANDSLIDE");
        Assert.Equal(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong, landslide.Shake);
        Assert.Equal(FourthActFxAnchor.Feet, landslide.ImpactAnchor);

        var moveFx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/Presentation/FourthActMoveFx.cs"));
        Assert.Contains("NGame.Instance?.ScreenShake(", moveFx, StringComparison.Ordinal);
        Assert.Contains("aim, presentation.ImpactAnchor)", moveFx, StringComparison.Ordinal);
    }

    [Fact]
    public void WaterDragonFliesAgainForItsReservoirHit()
    {
        var dragon = FourthActMovePresentationCatalog.Get(typeof(SakuraMod.SakuraModCode.FourthAct.Water.Models.WateryMonster), "WATER_DRAGON");
        Assert.Equal(FourthActFxId.WaterDragon, dragon.TravelFx);

        var watery = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Water/Models/WateryMonster.cs"));
        Assert.True(watery.IndexOf("await TravelAsync(target);", StringComparison.Ordinal)
                    is var travel and > 0 && travel < watery.IndexOf("TargetingFiltered([target])", StringComparison.Ordinal));
        Assert.Contains("GetPower<WaterReservoirPower>()?.Total", watery, StringComparison.Ordinal);

        var moveFx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/Presentation/FourthActMoveFx.cs"));
        Assert.Contains("internal static async Task TravelAsync(", moveFx, StringComparison.Ordinal);
    }

    [Fact]
    public void WindPowerFeedbackPlaysPreloadedScenesInsteadOfDrawnLines()
    {
        var windy = typeof(SakuraMod.SakuraModCode.FourthAct.Wind.Models.WindyMonster);
        foreach (var fx in new[] { FourthActFxId.BindCoil, FourthActFxId.BindReturn, FourthActFxId.WindWallShield, FourthActFxId.WindWallDeflect })
            Assert.Contains(fx, FourthActMovePresentationCatalog.FxOf(windy));
        Assert.Contains(FourthActFxId.SleepPowder,
            FourthActMovePresentationCatalog.FxOf(typeof(SakuraMod.SakuraModCode.FourthAct.Wind.Models.SleepMonster)));

        var feedback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        foreach (var fx in new[] { "FourthActFxId.WindWallShield", "FourthActFxId.WindWallDeflect", "FourthActFxId.BindReturn" })
            Assert.Contains(fx, feedback, StringComparison.Ordinal);
        Assert.DoesNotContain("BarrierPolygon", feedback, StringComparison.Ordinal);

        var attendants = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Wind/Models/WindAttendants.cs"));
        Assert.DoesNotContain("PlayTransferAsync", attendants, StringComparison.Ordinal);
        Assert.Contains("}, [windy]);", attendants, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkVeilIsTheAmbientLayerAndMicroLightPiercesIt()
    {
        var dark = typeof(SakuraMod.SakuraModCode.FourthAct.Dark.Models.DarkMonster);
        Assert.Equal(FourthActFxId.DarkVeil, FourthActMovePresentationCatalog.AmbientOf(dark));
        foreach (var fx in new[] { FourthActFxId.MicroLightMote, FourthActFxId.VeilPierce, FourthActFxId.DarkCorona })
            Assert.Contains(fx, FourthActMovePresentationCatalog.FxOf(dark));

        var feedback = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        Assert.Contains("FourthActFxId.VeilPierce", feedback, StringComparison.Ordinal);
        Assert.Contains("FourthActFxId.DarkCorona", feedback, StringComparison.Ordinal);
        Assert.DoesNotContain("Polygon2D", feedback, StringComparison.Ordinal);

        var powers = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Dark/Powers/DarkCombatPowers.cs"));
        Assert.Contains("FourthActFxId.MicroLightMote", powers, StringComparison.Ordinal);
        var model = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Dark/Models/DarkMonster.cs"));
        Assert.Contains("DarkEnemyRules.DarknessReset), [Creature]);", model, StringComparison.Ordinal);
    }

    [Fact]
    public void AmbientLayersAreOwnedByTheStandeeAndPreloadedWithTheirMonster()
    {
        foreach (var monster in FourthActMonsters)
        {
            if (FourthActMovePresentationCatalog.AmbientOf(monster) is not { } ambient)
                continue;
            Assert.Contains(ambient, FourthActMovePresentationCatalog.FxOf(monster));
            Assert.Contains("metadata/ambient_layer", File.ReadAllText(RepoFile(FourthActFxScenes.PathFor(ambient))), StringComparison.Ordinal);
        }

        var material = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/Presentation/FourthActStandeeMaterial.cs"));
        Assert.Contains("AmbientOf(monster.GetType())", material, StringComparison.Ordinal);
        Assert.Contains("_ambient.QueueFreeSafely()", material, StringComparison.Ordinal);
        Assert.Contains("SetShaderParameter(StateParam", material, StringComparison.Ordinal);
    }

    [Fact]
    public void StandeeShadersShareTheCommonActingUniformsAndNeverReadTime()
    {
        foreach (var element in Enum.GetValues<FourthActElement>())
        {
            var source = ShaderCode(RepoFile(FourthActElements.StandeeShaderPath(element)));
            Assert.Contains("standee_common.gdshaderinc", source, StringComparison.Ordinal);
            Assert.DoesNotMatch(new Regex(@"\bTIME\b"), source);
        }

        var common = ShaderCode(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/fourth_act/standee_common.gdshaderinc"));
        foreach (var uniform in new[] { "u_time", "charge", "hit_flash", "dissolve", "accent_color", "flash_color", "dissolve_edge", "seed", "state_glow", "state_color" })
            Assert.Matches(new Regex($@"uniform\s+\w+\s+{uniform}\b"), common);
        Assert.DoesNotMatch(new Regex(@"\bTIME\b"), common);
    }

    [Fact]
    public void AudioCuesResolveToVanillaSoundsOrShippedResources()
    {
        foreach (var cue in Enum.GetValues<FourthActAudioCue>())
        {
            var path = FourthActEnemyAudio.PathFor(cue);
            if (FourthActEnemyAudio.IsResource(path))
                Assert.True(File.Exists(RepoFile(path)), $"{cue}: {path}");
            else if (!FourthActEnemyAudio.IsEvent(path))
                Assert.Matches(new Regex(@"^[a-z_]+\.mp3$"), path);
        }

        foreach (var monster in FourthActMovePresentationCatalog.MonsterTypes)
        {
            foreach (var (id, presentation) in FourthActMovePresentationCatalog.MovesOf(monster))
            {
                if (presentation.ImpactCue is { } cue)
                    Assert.False(FourthActEnemyAudio.IsResource(FourthActEnemyAudio.PathFor(cue)), $"{monster.Name}.{id}");
            }
        }

        foreach (var element in Enum.GetValues<FourthActElement>())
            Assert.True(FourthActEnemyAudio.IsEvent(FourthActEnemyAudio.PathFor(FourthActElements.Profile(element).Death)));
    }

    private static List<string> FourthActModelSources()
    {
        var root = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/FourthActCombatRules.cs"))!;
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
    }

    private static string RepoFile(string resPath) =>
        RegressionTestHarness.FindRepoFile(resPath.Replace("res://", string.Empty, StringComparison.Ordinal));

    private static float? Metadata(string scene, string key)
    {
        var match = Regex.Match(scene, $@"metadata/{key}\s*=\s*([0-9.]+)");
        return match.Success ? float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var offset = source.IndexOf(value, StringComparison.Ordinal);
             offset >= 0;
             offset = source.IndexOf(value, offset + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string ShaderCode(string file) =>
        Regex.Replace(File.ReadAllText(file), @"(?<![:""])//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
}
