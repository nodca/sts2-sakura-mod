using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Content;
using SakuraMod.SakuraModCode.FourthAct.Water;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Extensions;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.FourthAct.Water.Intents;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Models;

public sealed class WateryMonster : ModMonsterTemplate
{
    public const int BaseHp = 440, ToughHp = 465;
    public const int TidalMinimum = 14, TidalMaximum = 18, DeadlyTidalMinimum = 16, DeadlyTidalMaximum = 20;
    public const int DragonDamage = 27, DeadlyDragonDamage = 30, FloodDamage = 14, DeadlyFloodDamage = 17;
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => WaterEnemyAssets.Watery;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "The Watery");
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<WaterSovereigntyPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
        await PowerCmd.Apply<WaterReservoirPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true);
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var draw = new MoveState("TIDAL_DRAW", TidalDraw, new TidalDrawIntent(() => CurrentTidal, () => TidalRange), new WaterBlockStealIntent());
        var dragon = new MoveState("WATER_DRAGON", WaterDragon, new SingleAttackIntent(() => CurrentDragon));
        var secondDraw = new MoveState("TIDAL_DRAW_SECOND", TidalDraw, new TidalDrawIntent(() => CurrentTidal, () => TidalRange), new WaterBlockStealIntent());
        var secondDragon = new MoveState("WATER_DRAGON_SECOND", WaterDragon, new SingleAttackIntent(() => CurrentDragon));
        var flood = new MoveState("FLOOD", Flood, new SingleAttackIntent(() => CurrentFlood), new DefendIntent());
        draw.FollowUpState = dragon; dragon.FollowUpState = secondDraw; secondDraw.FollowUpState = secondDragon; secondDragon.FollowUpState = flood; flood.FollowUpState = draw;
        return new MonsterMoveStateMachine([draw, dragon, secondDraw, secondDragon, flood], draw);
    }
    private (int Minimum, int Maximum) TidalRange =>
        (CurrentTidalMinimum, CurrentTidalMaximum);
    private int CurrentTidalMinimum =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyTidalMinimum, TidalMinimum);
    private int CurrentTidalMaximum =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyTidalMaximum, TidalMaximum);
    private int CurrentTidal => WaterEnemyRules.RollTidalDamage(
        CombatState.RunState.Rng.Seed,
        Creature.CombatId ?? 0,
        CombatState.RoundNumber,
        TidalRange.Minimum,
        TidalRange.Maximum);
    private int CurrentDragon => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyDragonDamage, DragonDamage);
    private int CurrentFlood => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, DeadlyFloodDamage, FloodDamage);
    private WaterReservoirPower Reservoir => Creature.GetPower<WaterReservoirPower>()
        ?? throw new InvalidOperationException("Watery requires its Reservoir power.");
    private async Task TidalDraw(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(CurrentTidal).FromMonster(this));
        foreach (var target in CombatState.Players.Select(static p => p.Creature).Where(static c => c.IsAlive))
        {
            var removed = target.Block;
            await CreatureCmd.LoseBlock(target, removed);
            Reservoir.Add(target, removed);
        }
    }

    private async Task WaterDragon(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.PerformAsync(Creature, SakuraStandeeClip.Attack, async () =>
        {
            await DamageCmd.Attack(CurrentDragon).FromMonster(this).Execute(null);
            foreach (var target in CombatState.Players.Select(static p => p.Creature).Where(static c => c.IsAlive).ToList())
            {
                var water = Reservoir.For(target);
                if (water <= 0) continue;
                var command = DamageCmd.Attack(water).FromMonster(this).TargetingFiltered([target]);
                await command.Execute(null);
                var unblocked = command.Results.SelectMany(static hit => hit).Sum(static result => result.UnblockedDamage);
                Reservoir.Consume(target, unblocked);
            }
        });
    }

    private async Task Flood(IReadOnlyList<Creature> targets)
    {
        await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(CurrentFlood).FromMonster(this));
        foreach (var target in CombatState.Players.Select(static p => p.Creature).Where(static c => c.IsAlive).ToList())
        {
            var water = Reservoir.For(target);
            if (water <= 0) continue;
            await CreatureCmd.GainBlock(Creature, water, SakuraPowerValueProps.Block, null, false);
            Reservoir.Consume(target, water);
        }
    }
}
