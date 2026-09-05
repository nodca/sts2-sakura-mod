using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Earth.Models;

public abstract class EarthMonsterBase : ModMonsterTemplate
{
    protected abstract int BaseHp { get; }
    protected abstract int ToughHp { get; }
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    protected bool IsDeadly => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 0) == 1;
    protected bool IsTough => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) == 1;
}

public sealed class ShadowMonster : EarthMonsterBase
{
    protected override int BaseHp => EarthEnemyRules.ShadowHp;
    protected override int ToughHp => EarthEnemyRules.ShadowToughHp;
    public override string? CustomVisualsPath => EarthEnemyAssets.Shadow;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "Shadow");

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ShadowEchoPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var echo = new MoveState("SHADOW_ECHO", EchoAct, new UnknownIntent());
        echo.FollowUpState = echo;
        return new MonsterMoveStateMachine([echo], echo);
    }

    private async Task EchoAct(IReadOnlyList<Creature> _)
    {
        var echo = Creature.GetPower<ShadowEchoPower>();
        var players = CombatState.Players.Where(p => p.Creature.IsAlive).ToList();
        var triggeredSkill = false;
        var triggeredPower = false;

        foreach (var player in players)
        {
            var lastCard = echo?.GetLastCardType(player.Creature);
            switch (lastCard)
            {
                case CardType.Attack:
                    var clawsDmg = IsDeadly ? EarthEnemyRules.ShadowClawsA9Damage : EarthEnemyRules.ShadowClawsDamage;
                    await FourthActEnemyActionCmd.AttackAsync(
                        Creature,
                        DamageCmd.Attack(clawsDmg)
                            .FromMonster(this)
                            .WithHitCount(EarthEnemyRules.ShadowClawsHits)
                            .TargetingFiltered([player.Creature]));
                    break;

                case CardType.Skill:
                    triggeredSkill = true;
                    break;

                case CardType.Power:
                    triggeredPower = true;
                    break;

                default:
                    // Status, Curse, pass, or other non-3 types -> Shadow Bite
                    var biteDmg = IsDeadly ? EarthEnemyRules.ShadowBiteA9Damage : EarthEnemyRules.ShadowBiteDamage;
                    await FourthActEnemyActionCmd.AttackAsync(
                        Creature,
                        DamageCmd.Attack(biteDmg)
                            .FromMonster(this)
                            .TargetingFiltered([player.Creature]));
                    break;
            }
        }

        if (triggeredSkill)
        {
            var block = IsTough ? EarthEnemyRules.ShadowVeilA8Block : EarthEnemyRules.ShadowVeilBlock;
            var heal = IsDeadly ? EarthEnemyRules.ShadowVeilA9Heal : EarthEnemyRules.ShadowVeilHeal;
            await CreatureCmd.GainBlock(Creature, block, ValueProp.Move, null, false);
            await CreatureCmd.Heal(Creature, heal);
        }

        if (triggeredPower)
        {
            var str = IsDeadly ? EarthEnemyRules.ShadowSurgeA9Strength : EarthEnemyRules.ShadowSurgeStrength;
            var block = IsTough ? EarthEnemyRules.ShadowSurgeA8Block : EarthEnemyRules.ShadowSurgeBlock;
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, str, Creature, null, false);
            await CreatureCmd.GainBlock(Creature, block, ValueProp.Move, null, false);
        }
    }
}

public sealed class WoodMonster : EarthMonsterBase
{
    protected override int BaseHp => EarthEnemyRules.WoodHp;
    protected override int ToughHp => EarthEnemyRules.WoodToughHp;
    public override string? CustomVisualsPath => EarthEnemyAssets.Wood;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];

    private int RootedCount
    {
        get
        {
            var players = CombatState?.Players.Where(p => p.Creature.IsAlive).ToList();
            return players is { Count: > 0 } ? players.Max(WoodRootedPower.GetRootedCount) : 0;
        }
    }

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "Wood");

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<WoodRootedPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var strike = new MoveState("TWINING_STRIKE", Strike, new SingleAttackIntent(() => EarthEnemyRules.WoodStrikeDamage(RootedCount, IsDeadly)));
        var sprout = new MoveState("VITAL_SPROUT", Sprout, new DefendIntent(), new BuffIntent());
        strike.FollowUpState = sprout;
        sprout.FollowUpState = strike;
        return new MonsterMoveStateMachine([strike, sprout], strike);
    }

    private async Task Strike(IReadOnlyList<Creature> _)
    {
        var players = CombatState.Players.Where(p => p.Creature.IsAlive).ToList();
        foreach (var player in players)
        {
            var count = WoodRootedPower.GetRootedCount(player);
            var damage = EarthEnemyRules.WoodStrikeDamage(count, IsDeadly);
            await FourthActEnemyActionCmd.AttackAsync(
                Creature,
                DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .TargetingFiltered([player.Creature]));
        }
    }

    private async Task Sprout(IReadOnlyList<Creature> _)
    {
        var block = EarthEnemyRules.WoodSproutBlock(RootedCount, IsTough);
        var str = IsDeadly ? EarthEnemyRules.WoodSproutA9Strength : EarthEnemyRules.WoodSproutStrength;
        await CreatureCmd.GainBlock(Creature, block, ValueProp.Move, null, false);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, str, Creature, null, false);
    }
}

public sealed class EarthyMonster : EarthMonsterBase
{
    protected override int BaseHp => EarthEnemyRules.EarthyHp;
    protected override int ToughHp => EarthEnemyRules.EarthyToughHp;
    public override string? CustomVisualsPath => EarthEnemyAssets.Earthy;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];

    private int SedimentCount
    {
        get
        {
            var sedimentPower = Creature.GetPower<EarthySedimentPower>();
            var players = CombatState?.Players.Where(p => p.Creature.IsAlive).ToList();
            return players is { Count: > 0 } ? players.Max(p => sedimentPower?.GetSediment(p.Creature) ?? 0) : 0;
        }
    }

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "Earthy");

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<EarthSovereigntyPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
        await PowerCmd.Apply<EarthySedimentPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }


    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var tremor = new MoveState("TREMOR", Tremor, new SingleAttackIntent(() => EarthEnemyRules.EarthyTremorDamage(SedimentCount, IsDeadly)));
        var rockfall = new MoveState("ROCKFALL", Rockfall, new MultiAttackIntent(IsDeadly ? EarthEnemyRules.EarthyRockfallA9Damage : EarthEnemyRules.EarthyRockfallDamage, EarthEnemyRules.EarthyRockfallHits));
        var charge = new MoveState("QUAKE_CHARGE", Charge, new BuffIntent(), new DefendIntent());
        var landslide = new MoveState("LANDSLIDE", Landslide, new SingleAttackIntent(IsDeadly ? EarthEnemyRules.EarthyLandslideA9Damage : EarthEnemyRules.EarthyLandslideDamage));

        tremor.FollowUpState = rockfall;
        rockfall.FollowUpState = charge;
        charge.FollowUpState = landslide;
        landslide.FollowUpState = tremor;

        return new MonsterMoveStateMachine([tremor, rockfall, charge, landslide], tremor);
    }

    private async Task Tremor(IReadOnlyList<Creature> _)
    {
        var sedimentPower = Creature.GetPower<EarthySedimentPower>();
        var players = CombatState.Players.Where(p => p.Creature.IsAlive).ToList();
        foreach (var player in players)
        {
            var sediment = sedimentPower?.GetSediment(player.Creature) ?? 0;
            var damage = EarthEnemyRules.EarthyTremorDamage(sediment, IsDeadly);
            await FourthActEnemyActionCmd.AttackAsync(
                Creature,
                DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .TargetingFiltered([player.Creature]));
        }
    }

    private async Task Rockfall(IReadOnlyList<Creature> _)
    {
        var damage = IsDeadly ? EarthEnemyRules.EarthyRockfallA9Damage : EarthEnemyRules.EarthyRockfallDamage;
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(damage).FromMonster(this).WithHitCount(EarthEnemyRules.EarthyRockfallHits));
    }

    private async Task Charge(IReadOnlyList<Creature> _)
    {
        var str = IsDeadly ? EarthEnemyRules.EarthyChargeA9Strength : EarthEnemyRules.EarthyChargeStrength;
        var block = IsTough ? EarthEnemyRules.EarthyChargeA8Block : EarthEnemyRules.EarthyChargeBlock;
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, str, Creature, null, false);
        await CreatureCmd.GainBlock(Creature, block, ValueProp.Move, null, false);
    }

    private async Task Landslide(IReadOnlyList<Creature> _)
    {
        var damage = IsDeadly ? EarthEnemyRules.EarthyLandslideA9Damage : EarthEnemyRules.EarthyLandslideDamage;
        await FourthActEnemyActionCmd.AttackAsync(
            Creature,
            DamageCmd.Attack(damage).FromMonster(this));

        var players = CombatState.Players.Where(p => p.Creature.IsAlive).ToList();
        foreach (var player in players)
        {
            var discard = CardPile.Get(PileType.Discard, player);
            if (discard?.Cards.LastOrDefault() is { } topCard)
            {
                EarthCombatRules.MarkBuried(topCard);
            }
        }
    }
}
