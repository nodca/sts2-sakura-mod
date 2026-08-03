using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public sealed class FlyMonster : WindMonsterTemplate
{
    public const int BaseHp = 220;
    public const int ToughHp = 235;
    public const int BaseHighAttackDamage = 8;
    public const int DeadlyHighAttackDamage = 9;
    public const int HighAttackHits = 3;
    public const int BaseDiveDamage = 36;
    public const int DeadlyDiveDamage = 42;

    protected override string StandeePath => WindEnemyAssets.FlyAirborne;
    protected override string StandeeLabel => "Fly";
    protected override IEnumerable<string> AdditionalAssetPaths =>
        WindEnemyAssets.FlyTransitionFrames.Append(WindEnemyAssets.FlyGrounded);
    protected override IEnumerable<AbstractIntent> DeclaredIntents =>
    [
        new MultiAttackIntent(BaseHighAttackDamage, HighAttackHits),
        new SingleAttackIntent(BaseDiveDamage),
        new BuffIntent()
    ];

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;

    public int HighAttackDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyHighAttackDamage, BaseHighAttackDamage);
    public int DiveDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyDiveDamage, BaseDiveDamage);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SoarPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var highAttack = new MoveState(
            "HIGH_ATTACK",
            HighAttack,
            new MultiAttackIntent(HighAttackDamage, HighAttackHits));
        var dive = new MoveState("DIVE", Dive, new SingleAttackIntent(() => DiveDamage));
        var takeoff = new MoveState("TAKEOFF", Takeoff, new BuffIntent());
        highAttack.FollowUpState = dive;
        dive.FollowUpState = takeoff;
        takeoff.FollowUpState = highAttack;
        return new MonsterMoveStateMachine([highAttack, dive, takeoff], highAttack);
    }

    private Task HighAttack(IReadOnlyList<Creature> targets) =>
        FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(HighAttackDamage)
                .FromMonster(this)
                .WithHitCount(HighAttackHits));

    private async Task Dive(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(DiveDamage).FromMonster(this),
            FourthActAttackStyle.HeavyWind);
        await FlyVisualController.PlayLandingAsync(Creature);
        await PowerCmd.Remove<SoarPower>(Creature);
    }

    private async Task Takeoff(IReadOnlyList<Creature> targets)
    {
        await FlyVisualController.PlayTakeoffAsync(Creature);
        await PowerCmd.Apply<SoarPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, false);
    }
}
