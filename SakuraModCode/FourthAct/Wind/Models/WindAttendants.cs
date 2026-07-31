using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public abstract class WindAttendantMonster : WindMonsterTemplate
{
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }
}

public sealed class DashMonster : WindAttendantMonster
{
    public const int BaseHp = 65;
    public const int ToughHp = 70;
    public const int BaseDamage = 8;
    public const int DeadlyDamage = 10;
    public const int BaseGrowth = 3;
    public const int DeadlyGrowth = 4;
    private int _growth;

    protected override string StandeePath => WindEnemyAssets.Dash;
    protected override string StandeeLabel => "Dash";
    protected override IEnumerable<AbstractIntent> DeclaredIntents => [new SingleAttackIntent(BaseDamage)];
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public int Damage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyDamage, BaseDamage) + _growth;
    public int Growth => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyGrowth, BaseGrowth);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var dash = new MoveState("DASH", Attack, new SingleAttackIntent(() => Damage));
        dash.FollowUpState = dash;
        return new MonsterMoveStateMachine([dash], dash);
    }

    private async Task Attack(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Damage).FromMonster(this).WithNoAttackerAnim().Execute(null);
        _growth += Growth;
    }
}

public sealed class FloatMonster : WindAttendantMonster
{
    public const int BaseHp = 60;
    public const int ToughHp = 65;
    public const int BlockPerDraw = 2;

    protected override string StandeePath => WindEnemyAssets.Float;
    protected override string StandeeLabel => "Float";
    protected override IEnumerable<AbstractIntent> DeclaredIntents => [new DefendIntent()];
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<FloatDrawCounterPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var floatMove = new MoveState("FLOAT", GrantBlock, new DefendIntent());
        floatMove.FollowUpState = floatMove;
        return new MonsterMoveStateMachine([floatMove], floatMove);
    }

    private async Task GrantBlock(IReadOnlyList<Creature> targets)
    {
        var counter = Creature.GetPower<FloatDrawCounterPower>();
        var windy = CombatState.Enemies.FirstOrDefault(static enemy => enemy.IsAlive && enemy.Monster is WindyMonster);
        if (counter is null || windy is null)
            return;

        await CreatureCmd.GainBlock(windy, counter.DrawCount * BlockPerDraw, ValueProp.Move, null, false);
        counter.Reset();
    }
}

public sealed class SleepMonster : WindAttendantMonster
{
    public const int BaseHp = 55;
    public const int ToughHp = 60;

    protected override string StandeePath => WindEnemyAssets.Sleep;
    protected override string StandeeLabel => "Sleep";
    protected override IEnumerable<AbstractIntent> DeclaredIntents => [new DebuffIntent(strong: true)];
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var sleep = new MoveState("SLEEP", ArmSleep, new DebuffIntent(strong: true));
        sleep.FollowUpState = sleep;
        return new MonsterMoveStateMachine([sleep], sleep);
    }

    private async Task ArmSleep(IReadOnlyList<Creature> targets)
    {
        foreach (var target in targets.Where(static target => target.IsAlive))
            await PowerCmd.Apply<WindSleepSelectionPower>(new ThrowingPlayerChoiceContext(), target, 1, Creature, null, true);
    }
}
