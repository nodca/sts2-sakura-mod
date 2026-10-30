using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.FourthAct.Water;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Models;

public sealed class RainMonster : FourthActMonsterTemplate
{
    public const int BaseHp = 370, ToughHp = 390, DownpourDamage = 22, DeadlyDownpourDamage = 25, FloodDamage = 34, DeadlyFloodDamage = 40;
    public const int CoverBlock = 14, DeadlyCoverBlock = 16;
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => WaterEnemyAssets.Rain;
    protected override IEnumerable<string> MonsterAssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.CreateFourthActEnemy(CustomVisualsPath!, "The Rain");
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var downpour = new MoveState("DOWNPOUR", t => Hit(t, CurrentDownpour), new SingleAttackIntent(() => CurrentDownpour));
        var cover = new MoveState("CLOUD_COVER", Cover, new DefendIntent());
        var flood = new MoveState("FLOOD", t => Hit(t, CurrentFlood), new SingleAttackIntent(() => CurrentFlood));
        downpour.FollowUpState = cover; cover.FollowUpState = flood; flood.FollowUpState = downpour;
        return new MonsterMoveStateMachine([downpour, cover, flood], downpour);
    }
    private int CurrentDownpour => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyDownpourDamage, DownpourDamage);
    private int CurrentFlood => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyFloodDamage, FloodDamage);
    private Task Cover(IReadOnlyList<Creature> targets) => PerformAsync(async () =>
    {
        await CreatureCmd.GainBlock(Creature, AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyCoverBlock, CoverBlock), ValueProp.Move, null, false);
        await PowerCmd.Apply<DrenchedPower>(new ThrowingPlayerChoiceContext(), CombatState.Players.Where(static p => p.Creature.IsAlive).Select(static p => p.Creature), 1, Creature, null, false);
    }, [Creature]);
    private Task Hit(IReadOnlyList<Creature> targets, int damage) => AttackAsync(DamageCmd.Attack(damage).FromMonster(this));
}
