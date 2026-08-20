using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Audio;
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
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Models;

public sealed class DarkMonster : ModMonsterTemplate
{
    public const string PhaseTwoNonConfinementId = "P2_NON_CONFINEMENT";
    private MoveState? _phaseTwoConfinement;
    private MoveState? _phaseTwoNonConfinement;
    private MoveState? _phaseTwoUltimate;

    public DarkPhase Phase { get; private set; } = DarkPhase.Veiled;
    public DarkRegularAction NextRegularAction { get; private set; } = DarkRegularAction.Confinement;
    public int VeilBreakSidesRemaining { get; private set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, DarkEnemyRules.ToughHp, DarkEnemyRules.BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => DarkEnemyAssets.Standee;
    public override bool HasDeathSfx => true;
    public override string DeathSfx => "event:/sfx/enemy/enemy_attacks/obscura/obscura_die";
    public override string? HurtSfx => "event:/sfx/enemy/enemy_attacks/magi_knight/magi_knight_hurt";
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;
    public override float DeathAnimLengthOverride => SakuraStandeeActionController.DeathDuration;
    public bool IsDeadly => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 0) == 1;
    public int Night => Creature.GetPower<DarkNightPower>()?.Amount ?? 1;
    public bool IsVeilActive => Phase == DarkPhase.Veiled && VeilBreakSidesRemaining == 0;

    public override IEnumerable<string> AssetPaths =>
        DeclaredIntents()
            .SelectMany(static intent => intent.AssetPaths)
            .Prepend(DarkEnemyAssets.Action)
            .Prepend(DarkEnemyAssets.Standee)
            .Distinct();

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(DarkEnemyAssets.Standee, "The Dark");

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<DarkSovereigntyPower>(context, Creature, 1, Creature, null, true);
        await PowerCmd.Apply<DarkVeilPower>(context, Creature, DarkEnemyRules.InitialVeilLayers, Creature, null, true);
        await PowerCmd.Apply<DarkBattlePower>(context, Creature, 1, Creature, null, true);
    }

    public async Task BeginTransition()
    {
        if (Phase != DarkPhase.Veiled || Creature.IsDead)
            return;

        Phase = DarkPhase.TransitionPending;
        VeilBreakSidesRemaining = 0;
        await CreatureCmd.Stun(Creature, CompleteTransition, PhaseTwoNonConfinementId);
    }

    public async Task BreakVeil(PlayerChoiceContext choiceContext)
    {
        if (!IsVeilActive)
            return;

        VeilBreakSidesRemaining = DarkEnemyRules.VeilBreakPlayerSides;
        await FourthActCombatFeedbackVisuals.PlayDarkVeilBreakAsync(Creature);
        await CreatureCmd.LoseBlock(Creature, Creature.Block);
        await PowerCmd.Remove(Creature.GetPower<DarkVeilPower>());
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Creature, 1, Creature, null, false);
        FourthActCombatFeedbackVisuals.RefreshDarkVeilWindow(Creature);
    }

    public async Task ReduceVeil(PlayerChoiceContext choiceContext, int amount, Creature applier)
    {
        if (!IsVeilActive || amount <= 0 || Creature.GetPower<DarkVeilPower>() is not { } veil)
            return;

        var remaining = await PowerCmd.ModifyAmount(
            choiceContext,
            veil,
            -Math.Min(amount, veil.Amount),
            applier,
            null,
            false);
        if (remaining <= 0)
            await BreakVeil(choiceContext);
    }

    public async Task AdvanceVeilWindow(PlayerChoiceContext choiceContext)
    {
        if (Phase != DarkPhase.Veiled || VeilBreakSidesRemaining <= 0)
            return;

        VeilBreakSidesRemaining--;
        FourthActCombatFeedbackVisuals.RefreshDarkVeilWindow(Creature);
        if (VeilBreakSidesRemaining == 0)
            await PowerCmd.Apply<DarkVeilPower>(
                choiceContext,
                Creature,
                DarkEnemyRules.InitialVeilLayers,
                Creature,
                null,
                true);
    }

    public void RefreshPhaseTwoIntent()
    {
        if (Phase != DarkPhase.EternalNight || _phaseTwoConfinement is null || _phaseTwoNonConfinement is null)
            return;

        var move = NextRegularAction == DarkRegularAction.Confinement
            ? _phaseTwoConfinement
            : _phaseTwoNonConfinement;
        if (Night == DarkEnemyRules.MaximumNight && _phaseTwoUltimate is not null)
            move = _phaseTwoUltimate;
        SetMoveImmediate(move, forceTransition: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var p1Confinement = new MoveState("P1_CONFINEMENT", PhaseOneConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 1, IsDeadly)),
            new DebuffIntent(strong: true));
        var p1NonConfinement = new MoveState("P1_NON_CONFINEMENT", PhaseOneNonConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, 1, IsDeadly)));
        p1Confinement.FollowUpState = p1NonConfinement;
        p1NonConfinement.FollowUpState = p1Confinement;

        var phaseTwoBranch = new ConditionalBranchState("P2_BRANCH");
        _phaseTwoConfinement = new MoveState("P2_CONFINEMENT", PhaseTwoConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, Night, IsDeadly)),
            new DebuffIntent(strong: true));
        _phaseTwoNonConfinement = new MoveState(PhaseTwoNonConfinementId, PhaseTwoNonConfinement,
            new SingleAttackIntent(() => DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, Night, IsDeadly)),
            new DefendIntent());
        _phaseTwoUltimate = new MoveState("P2_ULTIMATE", PhaseTwoUltimate,
            new SingleAttackIntent(() => IsDeadly ? DarkEnemyRules.DeadlyUltimateDamage : DarkEnemyRules.BaseUltimateDamage),
            new DebuffIntent(strong: true));
        _phaseTwoConfinement.FollowUpState = phaseTwoBranch;
        _phaseTwoNonConfinement.FollowUpState = phaseTwoBranch;
        _phaseTwoUltimate.FollowUpState = phaseTwoBranch;
        phaseTwoBranch.AddState(_phaseTwoUltimate, () => Night >= DarkEnemyRules.MaximumNight);
        phaseTwoBranch.AddState(_phaseTwoConfinement, () => NextRegularAction == DarkRegularAction.Confinement);
        phaseTwoBranch.AddState(_phaseTwoNonConfinement, () => true);

        return new MonsterMoveStateMachine(
            [p1Confinement, p1NonConfinement, phaseTwoBranch, _phaseTwoConfinement, _phaseTwoNonConfinement, _phaseTwoUltimate],
            p1Confinement);
    }

    private Task PhaseOneConfinement(IReadOnlyList<Creature> targets) =>
        AttackAndArmConfinement(targets, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 1, IsDeadly));

    private Task PhaseOneNonConfinement(IReadOnlyList<Creature> targets) =>
        FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, 1, IsDeadly))
                .FromMonster(this),
            FourthActAttackStyle.Dark);

    private async Task PhaseTwoConfinement(IReadOnlyList<Creature> targets)
    {
        var night = Night;
        await AttackAndArmConfinement(targets, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, night, IsDeadly));
        var debuffs = DarkEnemyRules.ConfinementDebuffs(night);
        var context = new ThrowingPlayerChoiceContext();
        var livingTargets = targets.Where(static target => target.IsAlive).ToList();
        if (debuffs.Weak > 0)
            await PowerCmd.Apply<WeakPower>(context, livingTargets, debuffs.Weak, Creature, null, false);
        if (debuffs.Frail > 0)
            await PowerCmd.Apply<FrailPower>(context, livingTargets, debuffs.Frail, Creature, null, false);
        NextRegularAction = DarkRegularAction.NonConfinement;
        await IncreaseNight(context);
    }

    private async Task PhaseTwoNonConfinement(IReadOnlyList<Creature> targets)
    {
        var night = Night;
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, night, IsDeadly))
                .FromMonster(this),
            FourthActAttackStyle.Dark);
        await CreatureCmd.GainBlock(Creature, DarkEnemyRules.Block(night), ValueProp.Move, null, false);
        NextRegularAction = DarkRegularAction.Confinement;
        await IncreaseNight(new ThrowingPlayerChoiceContext());
    }

    private async Task PhaseTwoUltimate(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(IsDeadly ? DarkEnemyRules.DeadlyUltimateDamage : DarkEnemyRules.BaseUltimateDamage)
                .FromMonster(this),
            FourthActAttackStyle.Dark);
        var context = new ThrowingPlayerChoiceContext();
        foreach (var player in CombatState.Players)
            await SakuraMagicCharge.AddVoidToDrawPile(context, player);
        await SetNight(context, 3);
    }

    private async Task AttackAndArmConfinement(IReadOnlyList<Creature> targets, int damage)
    {
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(damage).FromMonster(this),
            FourthActAttackStyle.Dark);
        var context = new ThrowingPlayerChoiceContext();
        foreach (var target in targets.Where(static target => target.IsAlive && target.Player is not null))
            await PowerCmd.Apply<DarkConfinementSelectionPower>(context, target, 1, Creature, null, false);
    }

    private async Task CompleteTransition(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
            return;

        FourthActEnemyAudio.Play(FourthActAudioCue.DarkTransition);
        await FourthActEnemyActionCmd.PerformAsync(Creature, SakuraStandeeClip.Cast, async () =>
        {
            await PowerCmd.Remove(Creature.GetPower<DarkVeilPower>());
            Phase = DarkPhase.EternalNight;
            NextRegularAction = DarkRegularAction.NonConfinement;
            var context = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<DarkNightPower>(context, Creature, 1, Creature, null, true);
            var microLight = await PowerCmd.Apply<DarkLightPower>(context, Creature, 1, Creature, null, true);
            microLight?.SetMicroLight(0);
        });
    }

    private async Task IncreaseNight(PlayerChoiceContext choiceContext)
    {
        await SetNight(choiceContext, Math.Min(DarkEnemyRules.MaximumNight, Night + 1));
        await DarkMicroLightCoordinator.ResolveEternalNightThresholds(choiceContext, this);
    }

    private async Task SetNight(PlayerChoiceContext choiceContext, int value)
    {
        var night = Creature.GetPower<DarkNightPower>();
        if (night is null)
            await PowerCmd.Apply<DarkNightPower>(choiceContext, Creature, value, Creature, null, true);
        else
            await PowerCmd.ModifyAmount(choiceContext, night, value - night.Amount, Creature, null, true);
    }

    private IEnumerable<AbstractIntent> DeclaredIntents() =>
    [
        new SingleAttackIntent(DarkEnemyRules.BaseConfinementDamage),
        new SingleAttackIntent(DarkEnemyRules.BaseNonConfinementDamage),
        new SingleAttackIntent(DarkEnemyRules.BaseUltimateDamage),
        new DebuffIntent(strong: true),
        new DefendIntent()
    ];
}
