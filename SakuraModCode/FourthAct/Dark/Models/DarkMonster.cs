using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Models;

public sealed class DarkMonster : FourthActMonsterTemplate
{
    private MoveState? _confinement;
    private MoveState? _nonConfinement;
    private MoveState? _ultimate;

    public DarkRegularAction NextRegularAction { get; private set; } = DarkRegularAction.Confinement;
    public bool TransitionTriggered { get; private set; }
    public int Darkness => Creature.GetPower<DarknessPower>()?.Amount ?? 1;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, DarkEnemyRules.ToughHp, DarkEnemyRules.BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => DarkEnemyAssets.Standee;
    public override string? HurtSfx => "event:/sfx/enemy/enemy_attacks/magi_knight/magi_knight_hurt";
    // The veil thickens and the rim brightens with each layer of Darkness.
    internal override float AmbientLevel => 0.3f + 0.7f * (DarkEnemyRules.ClampDarkness(Darkness) - 1) / (DarkEnemyRules.DarknessMaximum - 1f);
    internal override float StandeeStateGlow => DarkEnemyRules.ClampDarkness(Darkness) / (float)DarkEnemyRules.DarknessMaximum;
    public bool IsDeadly => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 0) == 1;

    protected override IEnumerable<string> MonsterAssetPaths => DeclaredIntents().SelectMany(static intent => intent.AssetPaths)
        .Prepend(DarkEnemyAssets.Action).Prepend(DarkEnemyAssets.Standee).Distinct();

    protected override NCreatureVisuals? CreateStandee() => SakuraStandeeVisuals.CreateFourthActEnemy(DarkEnemyAssets.Standee, "The Dark");

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<DarknessPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<DarkSovereigntyPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<DarkBattlePower>(context, Creature, 1, Creature, null, true);
    }

    public async Task BeginTransition()
    {
        if (TransitionTriggered || Creature.IsDead)
            return;
        TransitionTriggered = true;
        await CreatureCmd.Stun(Creature, CompleteTransition);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var branch = new ConditionalBranchState("DARK_BRANCH");
        _confinement = new MoveState("CONFINEMENT", DoConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, Darkness, IsDeadly)),
            new CardDebuffIntent());
        _nonConfinement = new MoveState("NON_CONFINEMENT", DoNonConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, Darkness, IsDeadly)),
            new DefendIntent());
        _ultimate = new MoveState("ULTIMATE", DoUltimate,
            new SingleAttackIntent(() => DarkEnemyRules.UltimateDamage(IsDeadly)),
            new StatusIntent(1));
        _confinement.FollowUpState = branch;
        _nonConfinement.FollowUpState = branch;
        _ultimate.FollowUpState = branch;
        branch.AddState(_ultimate, () => DarkEnemyRules.ShouldUseUltimate(Darkness));
        branch.AddState(_confinement, () => NextRegularAction == DarkRegularAction.Confinement);
        branch.AddState(_nonConfinement, () => true);
        return new MonsterMoveStateMachine([_confinement, _nonConfinement, branch, _ultimate], _confinement);
    }

    private async Task DoConfinement(IReadOnlyList<Creature> targets)
    {
        await AttackAndArmConfinement(targets, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, Darkness, IsDeadly));
        NextRegularAction = DarkRegularAction.NonConfinement;
        await IncreaseDarkness();
    }

    private async Task DoNonConfinement(IReadOnlyList<Creature> targets)
    {
        await AttackAsync(
            DamageCmd.Attack(DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, Darkness, IsDeadly)).FromMonster(this));
        await CreatureCmd.GainBlock(Creature, DarkEnemyRules.NightBlock, ValueProp.Move, null, false);
        NextRegularAction = DarkRegularAction.Confinement;
        await IncreaseDarkness();
    }

    private async Task DoUltimate(IReadOnlyList<Creature> targets)
    {
        await AttackAsync(DamageCmd.Attack(DarkEnemyRules.UltimateDamage(IsDeadly)).FromMonster(this));
        var context = new ThrowingPlayerChoiceContext();
        foreach (var player in CombatState.Players)
            await SakuraMagicCharge.AddVoidToDrawPile(context, player);
        await SetDarkness(context, DarkEnemyRules.DarknessReset);
    }

    private async Task AttackAndArmConfinement(IReadOnlyList<Creature> targets, int damage)
    {
        await AttackAsync(DamageCmd.Attack(damage).FromMonster(this));
        var context = new ThrowingPlayerChoiceContext();
        foreach (var target in targets.Where(static target => target.IsAlive && target.Player is not null))
            await PowerCmd.Apply<DarkConfinementSelectionPower>(context, target, 1, Creature, null, false);
    }

    private async Task IncreaseDarkness()
    {
        await SetDarkness(new ThrowingPlayerChoiceContext(), DarkEnemyRules.ChangeDarkness(Darkness, 1));
    }

    private async Task SetDarkness(PlayerChoiceContext choiceContext, int value)
    {
        var darkness = Creature.GetPower<DarknessPower>();
        if (darkness is null)
            await PowerCmd.Apply<DarknessPower>(choiceContext, Creature, DarkEnemyRules.ClampDarkness(value), Creature, null, true);
        else
            await PowerCmd.ModifyAmount(choiceContext, darkness, DarkEnemyRules.ClampDarkness(value) - darkness.Amount, Creature, null, true);
    }

    private async Task CompleteTransition(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
            return;
        await PerformAsync(FourthActMovePresentationCatalog.DarkTransition,
            () => SetDarkness(new ThrowingPlayerChoiceContext(), DarkEnemyRules.DarknessReset), [Creature]);
    }

    private IEnumerable<AbstractIntent> DeclaredIntents() =>
    [
        new SingleAttackIntent(DarkEnemyRules.BaseConfinementDamage),
        new SingleAttackIntent(DarkEnemyRules.BaseNonConfinementDamage),
        new SingleAttackIntent(DarkEnemyRules.BaseUltimateDamage),
        new CardDebuffIntent(),
        new StatusIntent(1),
        new DefendIntent()
    ];
}
