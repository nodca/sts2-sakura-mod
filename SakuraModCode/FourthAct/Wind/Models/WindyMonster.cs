using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Wind.Intents;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public sealed class WindyMonster : WindMonsterTemplate
{
    public const int BaseHp = 420;
    public const int ToughHp = 440;
    public const int BaseMultiDamage = 5;
    public const int DeadlyMultiDamage = 6;
    public const int MultiHits = 5;
    public const int BaseSingleDamage = 20;
    public const int DeadlySingleDamage = 24;
    public const int BaseHeavyDamage = 30;
    public const int DeadlyHeavyDamage = 36;

    private readonly List<Type> _attendantBag = [];
    private Type? _preparedAttendant;

    protected override string StandeePath => WindEnemyAssets.Windy;
    protected override string StandeeLabel => "Windy";
    protected override IEnumerable<string> AdditionalAssetPaths =>
        ModelDb.Monster<DashMonster>().AssetPaths
            .Concat(ModelDb.Monster<FloatMonster>().AssetPaths)
            .Concat(ModelDb.Monster<SleepMonster>().AssetPaths);
    protected override IEnumerable<AbstractIntent> DeclaredIntents =>
    [
        new MultiAttackIntent(BaseMultiDamage, MultiHits),
        new SingleAttackIntent(BaseSingleDamage),
        new SingleAttackIntent(BaseHeavyDamage),
        new SummonIntent()
    ];

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public int MultiDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyMultiDamage, BaseMultiDamage);
    public int SingleDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlySingleDamage, BaseSingleDamage);
    public int HeavyDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyHeavyDamage, BaseHeavyDamage);

    private bool HasAttendant => CombatState.Enemies.Any(
        static creature => creature.IsAlive && creature.Monster is DashMonster or FloatMonster or SleepMonster);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WindSovereigntyPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<WindyBattlePower>(context, Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var firstMulti = new MoveState("FIRST_GUSTS", MultiAttack, new MultiAttackIntent(MultiDamage, MultiHits));
        var firstSingle = new MoveState("FIRST_GUST", SingleAttack, new SingleAttackIntent(() => SingleDamage));
        var secondMulti = new MoveState("SECOND_GUSTS", MultiAttack, new MultiAttackIntent(MultiDamage, MultiHits));
        var secondSingle = new MoveState("SECOND_GUST", SingleAttack, new SingleAttackIntent(() => SingleDamage));
        var summon = new MoveState(
            "SUMMON_ATTENDANT",
            SummonAttendant,
            new WindAttendantSummonIntent(() => _preparedAttendant));
        var heavy = new MoveState("HEAVY_GALE", HeavyAttack, new SingleAttackIntent(() => HeavyDamage));
        var firstLight = new RandomBranchState("FIRST_LIGHT_BRANCH");
        var attendantCheck = new ConditionalBranchState("ATTENDANT_CHECK");
        var secondLight = new RandomBranchState("SECOND_LIGHT_BRANCH");

        firstLight.AddBranch(firstMulti, MoveRepeatType.CanRepeatForever, 70f);
        firstLight.AddBranch(firstSingle, MoveRepeatType.CanRepeatForever, 30f);
        secondLight.AddBranch(secondMulti, MoveRepeatType.CanRepeatForever, 70f);
        secondLight.AddBranch(secondSingle, MoveRepeatType.CanRepeatForever, 30f);
        firstMulti.FollowUpState = attendantCheck;
        firstSingle.FollowUpState = attendantCheck;
        attendantCheck.AddState(secondLight, () => HasAttendant);
        attendantCheck.AddState(summon, PrepareAttendant);
        secondMulti.FollowUpState = heavy;
        secondSingle.FollowUpState = heavy;
        summon.FollowUpState = heavy;
        heavy.FollowUpState = firstLight;

        return new MonsterMoveStateMachine(
            [firstMulti, firstSingle, secondMulti, secondSingle, summon, heavy, firstLight, attendantCheck, secondLight],
            firstLight);
    }

    private Task MultiAttack(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(MultiDamage).FromMonster(this).WithHitCount(MultiHits).WithNoAttackerAnim().Execute(null);

    private Task SingleAttack(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(SingleDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);

    private Task HeavyAttack(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(HeavyDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);

    private async Task SummonAttendant(IReadOnlyList<Creature> targets)
    {
        if (HasAttendant)
            return;

        var selected = _preparedAttendant ?? SelectAttendant();
        _preparedAttendant = null;
        _attendantBag.Remove(selected);
        if (selected == typeof(DashMonster))
            await CreatureCmd.Add<DashMonster>(CombatState, "ATTENDANT");
        else if (selected == typeof(FloatMonster))
            await CreatureCmd.Add<FloatMonster>(CombatState, "ATTENDANT");
        else
            await CreatureCmd.Add<SleepMonster>(CombatState, "ATTENDANT");
    }

    private bool PrepareAttendant()
    {
        if (HasAttendant)
            return false;

        _preparedAttendant ??= SelectAttendant();
        return true;
    }

    private Type SelectAttendant()
    {
        if (_attendantBag.Count == 0)
            _attendantBag.AddRange([typeof(DashMonster), typeof(FloatMonster), typeof(SleepMonster)]);

        return Rng.NextItem(_attendantBag)
            ?? throw new InvalidOperationException("Windy attendant bag unexpectedly returned no model type.");
    }
}
