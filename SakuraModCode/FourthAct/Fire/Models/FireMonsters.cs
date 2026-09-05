using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Models;

public abstract class FireMonsterBase : ModMonsterTemplate
{
    protected abstract int BaseHp { get; }
    protected abstract int ToughHp { get; }
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
}

public sealed class SwordMonster : FireMonsterBase
{
    protected override int BaseHp => FireEnemyRules.SwordHp; protected override int ToughHp => FireEnemyRules.SwordToughHp;
    public override string? CustomVisualsPath => FireEnemyAssets.Sword;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "The Sword");
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); await PowerCmd.Apply<SwordBlockFeedingPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true); }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var slash = new MoveState("SLASH", Hit, new SingleAttackIntent(FireEnemyRules.SwordSlash));
        var cut = new MoveState("DOUBLE_CUT", HitTwice, new MultiAttackIntent(FireEnemyRules.SwordDoubleCut, 2));
        slash.FollowUpState = cut; cut.FollowUpState = slash;
        return new MonsterMoveStateMachine([slash, cut], slash);
    }
    private async Task Hit(IReadOnlyList<Creature> _)
    {
        try { await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.SwordSlash).FromMonster(this)); }
        finally { Creature.GetPower<SwordBlockFeedingPower>()?.Reset(); }
    }
    private async Task HitTwice(IReadOnlyList<Creature> _)
    {
        try { await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.SwordDoubleCut).FromMonster(this).WithHitCount(2)); }
        finally { Creature.GetPower<SwordBlockFeedingPower>()?.Reset(); }
    }
}

public sealed class LibraPanMonster : FireMonsterBase
{
    private const float ClickMargin = 12f;
    private bool IsLeftPan => Creature.SlotName == "LEFT";
    private string StandeePath => IsLeftPan ? LibraEnemyAssets.Moon : LibraEnemyAssets.Sun;
    private float StandeeScale => IsLeftPan ? LibraEnemyAssets.MoonScale : LibraEnemyAssets.SunScale;
    private Vector2 VisibleSize => IsLeftPan
        ? new Vector2(LibraEnemyAssets.MoonWidth, LibraEnemyAssets.MoonHeight)
        : new Vector2(LibraEnemyAssets.SunWidth, LibraEnemyAssets.SunHeight);

    protected override int BaseHp => FireEnemyRules.LibraPanHp; protected override int ToughHp => FireEnemyRules.LibraToughPanHp;
    public override string? CustomVisualsPath => LibraEnemyAssets.Moon;
    public override IEnumerable<string> AssetPaths => LibraEnemyAssets.All;

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var scaledSize = VisibleSize * StandeeScale;
        var boundsSize = scaledSize + Vector2.One * ClickMargin * 2f;
        var center = new Vector2(0f, LibraVisualLayout.PanVisualCenterOffsetY);
        var bounds = new Rect2(center - boundsSize * 0.5f, boundsSize);
        return SakuraStandeeVisuals.CreateStatic(
            StandeePath,
            IsLeftPan ? "Libra Moon Pan" : "Libra Sun Pan",
            StandeeScale,
            center,
            bounds,
            center,
            new Vector2(0f, bounds.Position.Y - 42f),
            center,
            new Vector2(0f, bounds.Position.Y + 30f));
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<LibraPendulumPower>(new ThrowingPlayerChoiceContext(), Creature, 5, Creature, null, true);
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var strike = new MoveState("WEIGH", Weigh, new SingleAttackIntent(FireEnemyRules.LibraAttack));
        strike.FollowUpState = strike;
        return new MonsterMoveStateMachine([strike], strike);
    }
    private async Task Weigh(IReadOnlyList<Creature> _)
    {
        await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.LibraAttack).FromMonster(this));
    }
}

public sealed class FireyMonster : FireMonsterBase
{
    private bool _isAttacking;
    protected override int BaseHp => FireEnemyRules.FireyHp; protected override int ToughHp => FireEnemyRules.FireyToughHp;
    public override string? CustomVisualsPath => FireEnemyAssets.Firey;
    public override IEnumerable<string> AssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "Firey");
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); await PowerCmd.Apply<FireSovereigntyPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true); }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var breath = new MoveState("FLAME_BREATH", Breath, new MultiAttackIntent(FireEnemyRules.FlameBreath, 2));
        var arson = new MoveState("ARSON", Arson, new DebuffIntent());
        var kindle = new MoveState("KINDLE", Kindle, new BuffIntent());
        var ball = new MoveState("FIREBALL", Ball, new SingleAttackIntent(FireEnemyRules.Fireball));
        breath.FollowUpState = arson; arson.FollowUpState = kindle; kindle.FollowUpState = ball; ball.FollowUpState = breath;
        return new MonsterMoveStateMachine([breath, arson, kindle, ball], breath);
    }
    private async Task Breath(IReadOnlyList<Creature> _) { _isAttacking = true; try { await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.FlameBreath).FromMonster(this).WithHitCount(2)); } finally { _isAttacking = false; } }
    private async Task Arson(IReadOnlyList<Creature> _) { foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive)) for (var i = 0; i < 2; i++) await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Burn>(player), PileType.Discard, player, CardPilePosition.Random); }
    private Task Kindle(IReadOnlyList<Creature> _) { foreach (var player in CombatState.Players) foreach (var burn in CardPile.GetCards(player, PileType.Draw, PileType.Hand, PileType.Discard).OfType<MegaCrit.Sts2.Core.Models.Cards.Burn>()) burn.DynamicVars.Damage.BaseValue++; return Task.CompletedTask; }
    private async Task Ball(IReadOnlyList<Creature> _) { _isAttacking = true; try { foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive)) { var burns = CardPile.GetCards(player, PileType.Draw, PileType.Hand, PileType.Discard).Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Burn); await DamageCmd.Attack(FireEnemyRules.Fireball + FireEnemyRules.FireballPerBurn * burns).FromMonster(this).TargetingFiltered([player.Creature]).Execute(null); } } finally { _isAttacking = false; } }
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, MegaCrit.Sts2.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_isAttacking && dealer == Creature && target.Player is { } player && result.UnblockedDamage > 0)
            await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Burn>(player), PileType.Discard, player);
    }
}

public sealed class LightMonster : FireMonsterBase
{
    protected override int BaseHp => FireEnemyRules.LightHp; protected override int ToughHp => FireEnemyRules.LightToughHp;
    private bool IsEmpowered => Creature.CurrentHp <= Creature.MaxHp * 0.6m;
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); await PowerCmd.Apply<LightBattlePower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true); }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var radiance = new MoveState("RADIANCE", Hit, new SingleAttackIntent(FireEnemyRules.Radiance));
        var blessing = new MoveState("BENEDICTION", Bless, new SingleAttackIntent(FireEnemyRules.Benediction));
        var judgment = new MoveState("JUDGMENT", Judge, new SingleAttackIntent(FireEnemyRules.JudgmentBase));
        radiance.FollowUpState = blessing; blessing.FollowUpState = judgment; judgment.FollowUpState = radiance;
        return new MonsterMoveStateMachine([radiance, blessing, judgment], radiance);
    }
    private Task Hit(IReadOnlyList<Creature> _) => FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.Radiance).FromMonster(this));
    private async Task Bless(IReadOnlyList<Creature> _) { await FourthActEnemyActionCmd.AttackAsync(Creature, DamageCmd.Attack(FireEnemyRules.Benediction).FromMonster(this)); foreach (var power in Creature.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToList()) await PowerCmd.Remove(power); await CreatureCmd.Heal(Creature, Creature.CurrentHp <= Creature.MaxHp * 0.6m ? 25 : 15); }
    private async Task Judge(IReadOnlyList<Creature> _) { foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive)) { var hand = CardPile.GetCards(player, PileType.Hand).Count(); await DamageCmd.Attack(FireEnemyRules.JudgmentDamage(hand, IsEmpowered)).FromMonster(this).TargetingFiltered([player.Creature]).Execute(null); } }
}
