using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public sealed class IllusionMonster : WindMonsterTemplate
{
    public const int BaseHp = 210;
    public const int ToughHp = 225;
    public const int BaseBeguilingDamage = 18;
    public const int DeadlyBeguilingDamage = 21;
    public const int VulnerableAmount = 2;
    public const int BaseLuredFallDamage = 30;
    public const int DeadlyLuredFallDamage = 36;

    protected override string StandeePath => WindEnemyAssets.Illusion;
    protected override string StandeeLabel => "Illusion";
    protected override IEnumerable<AbstractIntent> DeclaredIntents =>
    [
        new SingleAttackIntent(BaseBeguilingDamage),
        new DebuffIntent(),
        new SingleAttackIntent(BaseLuredFallDamage),
        new BuffIntent()
    ];

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public int BeguilingDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyBeguilingDamage, BaseBeguilingDamage);
    public int LuredFallDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyLuredFallDamage, BaseLuredFallDamage);

    internal Task ReshufflePresentationAsync() =>
        Visuals.IllusionVisualController.ReshuffleWithOcclusionAsync(Creature);

    private bool HasProjection =>
        CombatState.Enemies.Any(static creature => creature.IsAlive && creature.Monster is IllusionProjectionMonster);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<IllusionIdentityPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var beguiling = new MoveState(
            "BEGUILING_STRIKE",
            BeguilingStrike,
            new SingleAttackIntent(() => BeguilingDamage),
            new DebuffIntent());
        var luredFall = new MoveState("LURED_FALL", LuredFall, new SingleAttackIntent(() => LuredFallDamage));
        var reweaveBeforeLured = new MoveState("REWEAVE_BEFORE_LURED", Reweave, new BuffIntent());
        var reweaveBeforeBeguiling = new MoveState("REWEAVE_BEFORE_BEGUILING", Reweave, new BuffIntent());
        var afterBeguiling = new ConditionalBranchState("AFTER_BEGUILING");
        var afterLured = new ConditionalBranchState("AFTER_LURED");

        beguiling.FollowUpState = afterBeguiling;
        luredFall.FollowUpState = afterLured;
        reweaveBeforeLured.FollowUpState = luredFall;
        reweaveBeforeBeguiling.FollowUpState = beguiling;
        afterBeguiling.AddState(luredFall, () => HasProjection);
        afterBeguiling.AddState(reweaveBeforeLured, () => !HasProjection);
        afterLured.AddState(beguiling, () => HasProjection);
        afterLured.AddState(reweaveBeforeBeguiling, () => !HasProjection);

        return new MonsterMoveStateMachine(
            [beguiling, luredFall, reweaveBeforeLured, reweaveBeforeBeguiling, afterBeguiling, afterLured],
            beguiling);
    }

    private async Task BeguilingStrike(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(BeguilingDamage).FromMonster(this),
            FourthActAttackStyle.Illusion);
        foreach (var target in targets.Where(static target => target.IsAlive))
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, VulnerableAmount, Creature, null);
    }

    private Task LuredFall(IReadOnlyList<Creature> targets) =>
        FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(LuredFallDamage).FromMonster(this),
            FourthActAttackStyle.Illusion);

    private async Task Reweave(IReadOnlyList<Creature> targets)
    {
        FourthActEnemyAudio.Play(FourthActAudioCue.IllusionReweave);
        await FourthActEnemyActionCmd.PerformAsync(Creature, SakuraStandeeClip.Summon, async () =>
        {
            await Visuals.IllusionVisualController.WithGroupOcclusionAsync(Creature, async () =>
            {
                Visuals.IllusionVisualController.ResetDeclaredPositions(Creature);
                var occupiedSlots = CombatState.Enemies
                    .Where(static creature => creature.IsAlive && creature.Monster is IllusionProjectionMonster)
                    .Select(static creature => creature.SlotName)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var slot in new[] { "LEFT", "RIGHT" })
                {
                    if (!occupiedSlots.Contains(slot))
                        await CreatureCmd.Add<IllusionProjectionMonster>(CombatState, slot);
                }

                var projection = Rng.NextItem(CombatState.Enemies.Where(
                    static creature => creature.IsAlive && creature.Monster is IllusionProjectionMonster).ToList());
                if (projection is not null)
                    Visuals.IllusionVisualController.ExchangePositions(Creature, projection);
                Visuals.IllusionVisualController.SetRealBodyRevealed(Creature, revealed: false);
            });
        });
    }
}

public sealed class IllusionProjectionMonster : WindMonsterTemplate
{
    protected override string StandeePath => WindEnemyAssets.Illusion;
    protected override string StandeeLabel => "Illusion Projection";
    protected override IEnumerable<AbstractIntent> DeclaredIntents => [new StunIntent()];
    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;
    public override bool IsHealthBarVisible => true;
    public override bool HasDeathSfx => false;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<IllusionProjectionPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<MinionPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<IllusionIdentityPower>(context, Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var projection = new MoveState("PROJECTION", static _ => Task.CompletedTask, new StunIntent());
        projection.FollowUpState = projection;
        return new MonsterMoveStateMachine([projection], projection);
    }
}
