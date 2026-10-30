using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.FourthAct.Fire.Cards;
using SakuraMod.SakuraModCode.FourthAct.Fire.Intents;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using SakuraMod.SakuraModCode.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Models;

public abstract class FireMonsterBase : FourthActMonsterTemplate
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
    protected override IEnumerable<string> MonsterAssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.CreateFourthActEnemy(CustomVisualsPath!, "The Sword");
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); await PowerCmd.Apply<SwordBlockFeedingPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true); }

    // 锋芒: the edge burns brighter with every stored bonus until the next attack spends it.
    internal override float StandeeStateGlow =>
        Creature.GetPower<SwordBlockFeedingPower>()?.LargestBonus is > 0 and var bonus
            ? Math.Min(1f, 0.45f + 0.15f * bonus / FireEnemyRules.SwordBlockBonus)
            : 0f;
    internal override Color StandeeStateColor => new(1f, 0.42f, 0.18f);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var slash = new MoveState("SLASH", Hit, new SingleAttackIntent(FireEnemyRules.SwordSlash));
        var cut = new MoveState("DOUBLE_CUT", HitTwice, new MultiAttackIntent(FireEnemyRules.SwordDoubleCut, 2));
        slash.FollowUpState = cut; cut.FollowUpState = slash;
        return new MonsterMoveStateMachine([slash, cut], slash);
    }
    private async Task Hit(IReadOnlyList<Creature> _)
    {
        try { await AttackAsync(DamageCmd.Attack(FireEnemyRules.SwordSlash).FromMonster(this)); }
        finally { Creature.GetPower<SwordBlockFeedingPower>()?.Reset(); }
    }
    private async Task HitTwice(IReadOnlyList<Creature> _)
    {
        try { await AttackAsync(DamageCmd.Attack(FireEnemyRules.SwordDoubleCut).FromMonster(this).WithHitCount(2)); }
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
    internal override Color StandeeAccent => IsLeftPan ? new Color(0.82f, 0.88f, 1f) : base.StandeeAccent;
    protected override IEnumerable<string> MonsterAssetPaths =>
        [.. LibraEnemyAssets.All, Fire.Visuals.LibraVisualController.VignetteShaderPath, Fire.Visuals.LibraHaloVisual.ShaderPath];

    protected override NCreatureVisuals? CreateStandee()
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
        await AttackAsync(DamageCmd.Attack(FireEnemyRules.LibraAttack).FromMonster(this), variant: IsLeftPan ? 0 : 1);
    }
}

public sealed class FireyMonster : FireMonsterBase
{
    private const int FlameBreathBaseHits = 2, ArsonBurns = 2;
    private bool _isAttacking;
    // Like Test Subject's Multi Claw, every Flame Breath permanently adds a hit.
    private int _breathHits = FlameBreathBaseHits;
    protected override int BaseHp => FireEnemyRules.FireyHp; protected override int ToughHp => FireEnemyRules.FireyToughHp;
    public override string? CustomVisualsPath => FireEnemyAssets.Firey;
    protected override IEnumerable<string> MonsterAssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.CreateFourthActEnemy(CustomVisualsPath!, "Firey");
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); await PowerCmd.Apply<FireSovereigntyPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, true); }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var breath = new MoveState("FLAME_BREATH", Breath, new MultiAttackIntent(FireEnemyRules.FlameBreath, () => _breathHits), new GrowingStatusIntent(() => _breathHits), new BuffIntent());
        var arson = new MoveState("ARSON", Arson, new SingleAttackIntent(FireEnemyRules.ArsonAttack), new StatusIntent(ArsonBurns));
        var ball = new MoveState("FIREBALL", Ball, new SingleAttackIntent(() => FireEnemyRules.FireballDamage(
            FourthActCombatRules.PreviewPlayer(CombatState) is { } player ? BurnDamageTotal(player) : 0)));
        arson.FollowUpState = breath; breath.FollowUpState = ball; ball.FollowUpState = arson;
        return new MonsterMoveStateMachine([arson, breath, ball], arson);
    }
    // Breathe first so the Burns these hits add are kindled too; Blaze then levels every Burn.
    private async Task Breath(IReadOnlyList<Creature> _)
    {
        await WithBurningHits(() => AttackAsync(DamageCmd.Attack(FireEnemyRules.FlameBreath).FromMonster(this).WithHitCount(_breathHits)));
        _breathHits++;
        await PerformAsync(FourthActMovePresentationCatalog.BreathKindle,
            () => PowerCmd.Apply<FireyBlazePower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, false),
            CombatState.Players.Select(static p => p.Creature).Where(static c => c.IsAlive).ToList());
    }
    private async Task Arson(IReadOnlyList<Creature> _)
    {
        await WithBurningHits(() => AttackAsync(DamageCmd.Attack(FireEnemyRules.ArsonAttack).FromMonster(this)));
        foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive)) for (var i = 0; i < ArsonBurns; i++) await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Burn>(player), PileType.Discard, player, CardPilePosition.Random);
    }
    // Unblocked hits from any of Firey's attacks shuffle a Burn into the target's discard pile.
    private async Task WithBurningHits(Func<Task> attack) { _isAttacking = true; try { await attack(); } finally { _isAttacking = false; } }
    private async Task Ball(IReadOnlyList<Creature> _) { foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive)) { var burns = CountBurns(player); var burnDamage = BurnDamageTotal(player); await WithBurningHits(() => AttackAsync(DamageCmd.Attack(FireEnemyRules.FireballDamage(burnDamage)).FromMonster(this).TargetingFiltered([player.Creature]), FireballTier(burns), player.Creature)); } }
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, MegaCrit.Sts2.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_isAttacking && dealer == Creature && target.Player is { } player && result.UnblockedDamage > 0)
            await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Burn>(player), PileType.Discard, player);
    }

    // Presentation only: the blast grows at 3 and 6 Burns.
    private static int FireballTier(int burns) => burns >= 6 ? 2 : burns >= 3 ? 1 : 0;

    internal static int CountBurns(Player player) =>
        CardPile.GetCards(player, PileType.Draw, PileType.Hand, PileType.Discard).Count(static card => card is MegaCrit.Sts2.Core.Models.Cards.Burn);

    // Fireball cashes in both growth axes: how many Burns there are and how hot Blaze has made them.
    internal static int BurnDamageTotal(Player player) =>
        (int)CardPile.GetCards(player, PileType.Draw, PileType.Hand, PileType.Discard)
            .OfType<MegaCrit.Sts2.Core.Models.Cards.Burn>()
            .Sum(static burn => burn.DynamicVars.Damage.BaseValue);
}

public sealed class LightMonster : FireMonsterBase
{
    protected override int BaseHp => FireEnemyRules.LightHp; protected override int ToughHp => FireEnemyRules.LightToughHp;
    public override string? CustomVisualsPath => FireEnemyAssets.Light;
    protected override IEnumerable<string> MonsterAssetPaths => [CustomVisualsPath!];
    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.CreateFourthActEnemy(CustomVisualsPath!, "Light");
    private bool IsEmpowered => FireEnemyRules.IsLightEmpowered(Creature.CurrentHp, Creature.MaxHp);
    internal override float StandeeStateGlow => IsEmpowered ? 0.7f : 0f;
    internal override float AmbientLevel => IsEmpowered ? 1f : 0f;
    public override async Task AfterAddedToRoom() { await base.AfterAddedToRoom(); var context = new ThrowingPlayerChoiceContext(); await PowerCmd.Apply<LightSovereigntyPower>(context, Creature, 1, Creature, null, true); await PowerCmd.Apply<LightBattlePower>(context, Creature, 1, Creature, null, true); }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var radiance = new MoveState("RADIANCE", Hit, new SingleAttackIntent(FireEnemyRules.Radiance), new StatusIntent(FireEnemyRules.GlarePerMove));
        var blessing = new MoveState("BENEDICTION", Bless, new SingleAttackIntent(FireEnemyRules.Benediction), new StatusIntent(FireEnemyRules.GlarePerMove), new HealIntent());
        var judgment = new MoveState("JUDGMENT", Judge, new SingleAttackIntent(() => FireEnemyRules.JudgmentDamage(
            FourthActCombatRules.PreviewPlayer(CombatState) is { } player ? CardPile.GetCards(player, PileType.Hand).Count() : 0,
            IsEmpowered)));
        radiance.FollowUpState = blessing; blessing.FollowUpState = judgment; judgment.FollowUpState = radiance;
        return new MonsterMoveStateMachine([radiance, blessing, judgment], radiance);
    }
    private async Task Hit(IReadOnlyList<Creature> _)
    {
        await AttackAsync(DamageCmd.Attack(FireEnemyRules.Radiance).FromMonster(this));
        await AddGlare();
    }

    // Glare is retained under Light's trial, so every unpaid copy swells the next Judgment.
    private async Task AddGlare()
    {
        foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive))
            for (var i = 0; i < FireEnemyRules.GlarePerMove; i++)
                await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(CombatState.CreateCard<Glare>(player));
    }

    // Presentation only: the beam widens at 4 and 8 cards in hand.
    private static int JudgmentTier(int hand) => hand >= 8 ? 2 : hand >= 4 ? 1 : 0;
    private async Task Bless(IReadOnlyList<Creature> _)
    {
        await AttackAsync(DamageCmd.Attack(FireEnemyRules.Benediction).FromMonster(this));
        await AddGlare();
        await PerformAsync(FourthActMovePresentationCatalog.BenedictionHeal, async () =>
        {
            foreach (var power in Creature.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToList()) await PowerCmd.Remove(power);
            await CreatureCmd.Heal(Creature, IsEmpowered ? FireEnemyRules.EmpoweredBenedictionHeal : FireEnemyRules.BenedictionHeal);
        }, [Creature]);
    }
    private async Task Judge(IReadOnlyList<Creature> _)
    {
        LightEternalDayVisuals.For(Creature)?.BeginJudgment();
        try
        {
            foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive))
            {
                var hand = CardPile.GetCards(player, PileType.Hand).Count();
                await AttackAsync(DamageCmd.Attack(FireEnemyRules.JudgmentDamage(hand, IsEmpowered))
                    .FromMonster(this)
                    .TargetingFiltered([player.Creature])
                    .BeforeDamage(() =>
                    {
                        LightEternalDayVisuals.For(Creature)?.ReleaseJudgment();
                        return Task.CompletedTask;
                    }), JudgmentTier(hand), player.Creature);
            }
        }
        finally
        {
            LightEternalDayVisuals.For(Creature)?.CancelJudgmentCharge();
        }
    }
}
