using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Content;
using SakuraMod.SakuraModCode.FourthAct.Water;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Models;

public sealed class FreezeMonster : ModMonsterTemplate
{
    private bool _isHeavyStriking;

    public const int BaseHp = 250, ToughHp = 265, HeavyDamage = 7, DeadlyHeavyDamage = 8, HeavyHits = 4;
    public const int ColdDamage = 14, DeadlyColdDamage = 16, ColdBlock = 8, IceBlock = 18, DeadlyIceBlock = 20;
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => WaterEnemyAssets.Freeze;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "The Freeze");
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var heavy = new MoveState("HEAVY_STRIKE", Heavy, new MultiAttackIntent(CurrentHeavyDamage, HeavyHits));
        var cold = new MoveState("COLD_BLOW", Cold, new SingleAttackIntent(() => CurrentColdDamage), new DefendIntent());
        var secondHeavy = new MoveState("HEAVY_STRIKE_SECOND", Heavy, new MultiAttackIntent(CurrentHeavyDamage, HeavyHits));
        var ice = new MoveState("ICE_FORMATION", Ice, new DefendIntent());
        heavy.FollowUpState = cold; cold.FollowUpState = secondHeavy; secondHeavy.FollowUpState = ice; ice.FollowUpState = heavy;
        return new MonsterMoveStateMachine([heavy, cold, secondHeavy, ice], heavy);
    }
    private int CurrentHeavyDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyHeavyDamage, HeavyDamage);
    private int CurrentColdDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyColdDamage, ColdDamage);
    private async Task Heavy(IReadOnlyList<Creature> targets)
    {
        _isHeavyStriking = true;
        try
        {
            await FourthActEnemyActionCmd.AttackAsync(
                Creature,
                DamageCmd.Attack(CurrentHeavyDamage).FromMonster(this).WithHitCount(HeavyHits));
        }
        finally
        {
            _isHeavyStriking = false;
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_isHeavyStriking && dealer == Creature && target.IsPlayer && result.UnblockedDamage > 0)
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, target, 1, Creature, null, false);
    }
    private async Task Cold(IReadOnlyList<Creature> targets) { await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(CurrentColdDamage).FromMonster(this)); await CreatureCmd.GainBlock(Creature, ColdBlock, ValueProp.Move, null, false); }
    private async Task Ice(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature, AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyIceBlock, IceBlock), ValueProp.Move, null, false);
        await PowerCmd.Apply<SakuraFrostbitePower>(
            new ThrowingPlayerChoiceContext(),
            CombatState.Players.Where(static player => player.Creature.IsAlive).Select(static player => player.Creature),
            1,
            Creature,
            null,
            false);
    }
}
