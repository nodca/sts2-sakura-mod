using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Earth.Models;
using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Water.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public sealed class FourthActIntentAccuracySuite
{
    public static TheoryData<MonsterModel, string, Type[]> MoveIntents => new()
    {
        { new FireyMonster(), "FLAME_BREATH", [typeof(MultiAttackIntent), typeof(StatusIntent)] },
        { new FireyMonster(), "ARSON", [typeof(SingleAttackIntent), typeof(StatusIntent)] },
        { new FireyMonster(), "KINDLE", [typeof(SingleAttackIntent), typeof(BuffIntent)] },
        { new FireyMonster(), "FIREBALL", [typeof(SingleAttackIntent)] },
        { new LightMonster(), "RADIANCE", [typeof(SingleAttackIntent), typeof(StatusIntent)] },
        { new LightMonster(), "BENEDICTION", [typeof(SingleAttackIntent), typeof(StatusIntent), typeof(HealIntent)] },
        { new LightMonster(), "JUDGMENT", [typeof(SingleAttackIntent)] },
        { new FreezeMonster(), "HEAVY_STRIKE", [typeof(MultiAttackIntent), typeof(DebuffIntent)] },
        { new FreezeMonster(), "HEAVY_STRIKE_SECOND", [typeof(MultiAttackIntent), typeof(DebuffIntent)] },
        { new FreezeMonster(), "ICE_FORMATION", [typeof(DefendIntent), typeof(DebuffIntent)] },
        { new EarthyMonster(), "LANDSLIDE", [typeof(SingleAttackIntent), typeof(CardDebuffIntent)] },
        { new SleepMonster(), "SLEEP", [typeof(CardDebuffIntent)] },
        { new DarkMonster(), "CONFINEMENT", [typeof(SingleAttackIntent), typeof(CardDebuffIntent)] },
        { new DarkMonster(), "ULTIMATE", [typeof(SingleAttackIntent), typeof(StatusIntent)] },
    };

    [Theory]
    [MemberData(nameof(MoveIntents))]
    public void MoveIntentsMatchWhatTheMoveResolves(MonsterModel monster, string moveId, Type[] expected)
    {
        var move = Assert.IsType<MoveState>(GenerateMoves(monster).States[moveId]);
        Assert.Equal(expected, move.Intents.Select(static intent => intent.GetType()));
    }

    [Theory]
    [InlineData("FLAME_BREATH", 2)]
    [InlineData("ARSON", 2)]
    public void FireyStatusIntentsCountTheBurnsTheMoveCanAdd(string moveId, int expected)
    {
        var move = Assert.IsType<MoveState>(GenerateMoves(new FireyMonster()).States[moveId]);
        Assert.Equal(expected, Assert.Single(move.Intents.OfType<StatusIntent>()).CardCount);
    }

    [Fact]
    public void DarkUltimateStatusIntentCountsTheSingleVoid()
    {
        var move = Assert.IsType<MoveState>(GenerateMoves(new DarkMonster()).States["ULTIMATE"]);
        Assert.Equal(1, Assert.Single(move.Intents.OfType<StatusIntent>()).CardCount);
    }

    [Theory]
    [InlineData(0, 18)]
    [InlineData(3, 30)]
    [InlineData(-1, 18)]
    public void FireballDamageGrowsPerBurn(int burns, int expected) =>
        Assert.Equal(expected, FireEnemyRules.FireballDamage(burns));

    [Fact]
    public void DynamicPreviewsShareTheResolutionFormula()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Fire/Models/FireMonsters.cs"));

        Assert.Equal(2, Count(source, "FireEnemyRules.FireballDamage("));
        Assert.Equal(2, Count(source, "CountBurns(player)"));
        Assert.Equal(2, Count(source, "FireEnemyRules.JudgmentDamage("));
        Assert.DoesNotContain("new SingleAttackIntent(FireEnemyRules.Fireball)", source);
        Assert.DoesNotContain("new SingleAttackIntent(FireEnemyRules.JudgmentBase)", source);
        Assert.Contains("FourthActCombatRules.PreviewPlayer(CombatState)", source);
    }

    private static int Count(string source, string value) => source.Split(value).Length - 1;

    private static MonsterMoveStateMachine GenerateMoves(MonsterModel monster) =>
        (MonsterMoveStateMachine)monster.GetType()
            .GetMethod("GenerateMoveStateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(monster, null)!;
}

public sealed class FourthActMoveTitleSuite
{
    public static TheoryData<Type> FourthActMonsterTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(SakuraMod.SakuraModCode.FourthAct.FourthActCombatRules).Assembly.GetTypes()
                     .Where(static type => !type.IsAbstract
                         && typeof(MonsterModel).IsAssignableFrom(type)
                         && type.Namespace?.Contains(".FourthAct.", StringComparison.Ordinal) == true)
                     .OrderBy(static type => type.Name, StringComparer.Ordinal))
            data.Add(type);
        return data;
    }

    [Theory]
    [MemberData(nameof(FourthActMonsterTypes))]
    public void EveryMoveStateHasALocalizedTitle(Type monsterType)
    {
        var monster = (MonsterModel)Activator.CreateInstance(monsterType)!;
        var entry = monster.Id.Entry.StartsWith("SAKURA_MOD_MONSTER_", StringComparison.Ordinal)
            ? monster.Id.Entry
            : $"SAKURA_MOD_MONSTER_{monster.Id.Entry}";
        var moves = ((MonsterMoveStateMachine)monsterType
                .GetMethod("GenerateMoveStateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(monster, null)!)
            .States.Values.OfType<MoveState>().Select(static move => move.StateId).ToArray();
        Assert.NotEmpty(moves);
        foreach (var locale in new[] { "zhs", "eng" })
        {
            var titles = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/monsters.json")))!;
            foreach (var move in moves)
                Assert.True(titles.ContainsKey($"{entry}.moves.{move}.title"), $"{locale}: {entry}.moves.{move}.title");
            Assert.DoesNotContain(titles.Keys, key => key.StartsWith($"{entry}.moves.", StringComparison.Ordinal)
                && !moves.Any(move => key == $"{entry}.moves.{move}.title"));
        }
    }
}
