using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Events.Visuals;
using SakuraMod.SakuraModCode.FourthAct;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;
using Godot;

namespace SakuraMod.SakuraModCode.Events;

// Presented through the fourth-act template: she floats inside a sphere (the catalog's front
// ambient), every move leaves the sphere, hits ripple it instead of flashing her, and her
// death lets the sphere go before she fades.
public class ClassicTheNothingMonster : FourthActMonsterTemplate
{
    // Radius of her sphere in creature-visual pixels; the sphere scene draws the same.
    internal const float SphereRadius = 196f;
    private const float CardImpactStrength = 0.6f;

    private const int BaseHp = 187;
    private const int ToughHp = 214;

    private const int BaseStartVoid = 3;
    private const int DeadlyStartVoid = 5;
    private const float CombatVisualScale = 1f;
    private const int BaseThumpDamage = 32;
    private const int DeadlyThumpDamage = 44;
    private const int BaseBatterDamage = 7;
    private const int DeadlyBatterDamage = 9;
    private const int BaseBatterHits = 3;
    private const int DeadlyBatterHits = 4;
    private const int BaseDestructionDamage = 12;
    private const int DeadlyDestructionDamage = 19;
    private const int BaseDestructionVoid = 2;
    private const int DeadlyDestructionVoid = 3;
    private const int BaseDestructionExhaust = 1;
    private const int DeadlyDestructionExhaust = 2;
    private const int BaseStrengthen = 2;
    private const int DeadlyStrengthen = 4;
    private const int BaseWeaken = 3;
    private const int DeadlyWeaken = 4;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ToughHp, BaseHp);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualsPath => "monsters/the_nothing.png".ImagePath();
    public override string? HurtSfx => null;
    internal override float StandeeStateGlow => Creature.GetPower<IntangiblePower>() is null ? 0f : 1f;
    internal override float DissolveDuration => 1.8f;
    internal override FourthActFxId? DeathFx => FourthActFxId.NothingRelease;

    protected override IEnumerable<string> MonsterAssetPaths =>
        GetIntentAssets().Prepend(CustomVisualsPath!).Concat(TheNothingCardTheftVfx.AssetPaths);

    private int StartVoid => DeadlyValue(DeadlyStartVoid, BaseStartVoid);
    private int ThumpDamage => DeadlyValue(DeadlyThumpDamage, BaseThumpDamage);
    private int BatterDamage => DeadlyValue(DeadlyBatterDamage, BaseBatterDamage);
    private int BatterHits => DeadlyValue(DeadlyBatterHits, BaseBatterHits);
    private int DestructionDamage => DeadlyValue(DeadlyDestructionDamage, BaseDestructionDamage);
    private int DestructionVoid => DeadlyValue(DeadlyDestructionVoid, BaseDestructionVoid);
    private int DestructionExhaust => DeadlyValue(DeadlyDestructionExhaust, BaseDestructionExhaust);
    private int Strengthen => DeadlyValue(DeadlyStrengthen, BaseStrengthen);
    private int Weaken => DeadlyValue(DeadlyWeaken, BaseWeaken);

    // She keeps the standee's gentle idle float; the sphere stays still around her.
    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath!, "The Nothing", CombatVisualScale);

    // A hit is stopped by the sphere: its surface ripples like water from the point struck, on
    // the side facing the players (harder while Intangible), and she does not flash.
    internal override bool TryPlayStandeeHit(NCreature node)
    {
        var at = new Vector2((float)GD.RandRange(-0.7, -0.3), (float)GD.RandRange(-0.45, 0.4));
        var strength = Creature.GetPower<IntangiblePower>() is null ? 1f : 1.6f;
        return FourthActStandeeMaterial.TryGet(node)?.Impact(at, strength) == true;
    }

    // A taken card going into the sphere: a smaller ripple where it went in (unit-disc point).
    internal void RippleWhereCardEntered(Vector2 at) =>
        FourthActStandeeMaterial.TryGet(Creature)?.Impact(at, CardImpactStrength);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var start = new MoveState("START_VOID", StartVoidMove, new DebuffIntent(strong: true), new StatusIntent(StartVoid))
        {
            FollowUpStateId = "ATTACK_BRANCH",
            MustPerformOnceBeforeTransitioning = true
        };
        var thump = new MoveState("THUMP", ThumpAttack, new SingleAttackIntent(() => ThumpDamage))
        {
            FollowUpStateId = "NON_ATTACK_BRANCH"
        };
        var batter = new MoveState("BATTER", BatterAttack, new MultiAttackIntent(BatterDamage, () => BatterHits))
        {
            FollowUpStateId = "NON_ATTACK_BRANCH"
        };
        var destruction = new MoveState("DESTRUCTION", Destruction, new SingleAttackIntent(() => DestructionDamage), new CardDebuffIntent(), new StatusIntent(DestructionVoid))
        {
            FollowUpStateId = "ATTACK_BRANCH"
        };
        var strengthen = new MoveState("STRENGTHEN", StrengthenSelf, new BuffIntent())
        {
            FollowUpStateId = "ATTACK_BRANCH"
        };
        var weaken = new MoveState("WEAKEN", WeakenPlayers, new DebuffIntent())
        {
            FollowUpStateId = "ATTACK_BRANCH"
        };

        var attackBranch = new RandomBranchState("ATTACK_BRANCH");
        attackBranch.AddBranch(thump, MoveRepeatType.CanRepeatForever, 50f);
        attackBranch.AddBranch(batter, MoveRepeatType.CanRepeatForever, 50f);

        var nonAttackBranch = new RandomBranchState("NON_ATTACK_BRANCH");
        nonAttackBranch.AddBranch(destruction, MoveRepeatType.CanRepeatForever, 33f);
        nonAttackBranch.AddBranch(strengthen, MoveRepeatType.CanRepeatForever, 33f);
        nonAttackBranch.AddBranch(weaken, MoveRepeatType.CanRepeatForever, 34f);

        return new MonsterMoveStateMachine(
            [start, thump, batter, destruction, strengthen, weaken, attackBranch, nonAttackBranch],
            start);
    }

    private Task StartVoidMove(IReadOnlyList<Creature> targets) =>
        PerformAsync(async () =>
        {
            foreach (var target in targets)
            {
                await PowerCmd.Apply<ClassicNothingMonsterPower>(new ThrowingPlayerChoiceContext(), target, 1, Creature, null, false);
                await AddVoids(target, StartVoid);
            }
        }, targets);

    private Task ThumpAttack(IReadOnlyList<Creature> targets) =>
        AttackAsync(DamageCmd.Attack(ThumpDamage).FromMonster(this));

    private Task BatterAttack(IReadOnlyList<Creature> targets) =>
        AttackAsync(DamageCmd.Attack(BatterDamage).FromMonster(this).WithHitCount(BatterHits));

    private async Task Destruction(IReadOnlyList<Creature> targets)
    {
        await AttackAsync(DamageCmd.Attack(DestructionDamage).FromMonster(this));

        foreach (var target in targets)
        {
            await PurgeRandomCombatCards(target, DestructionExhaust);
            await AddVoids(target, DestructionVoid);
        }
    }

    private Task StrengthenSelf(IReadOnlyList<Creature> targets) =>
        PerformAsync(async () =>
        {
            foreach (var power in Creature.Powers.ToList())
                await PowerCmd.Remove(power);

            await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, false);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, Strengthen, Creature, null, false);
        });

    private Task WeakenPlayers(IReadOnlyList<Creature> targets) =>
        PerformAsync(async () =>
        {
            foreach (var target in targets)
            {
                await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
                await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
            }
        }, targets);

    private static async Task AddVoids(Creature target, int count)
    {
        if (target.Player is not { } player || target.CombatState is not { } combatState)
            return;

        var cards = Enumerable.Range(0, count)
            .Select(_ => (CardModel)combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(player))
            .ToList();
        CardCmd.PreviewCardPileAdd(await SakuraGeneratedCardLifecycle.AddGeneratedCardsToCombatWithResults(
            cards,
            PileType.Discard,
            player,
            CardPilePosition.Bottom));
    }

    // The cards she takes drift into her sphere (TheNothingCardTheftVfx) instead of the native
    // removal smoke; the Voids follow once they are gone.
    private async Task PurgeRandomCombatCards(Creature target, int count)
    {
        if (target.Player is not { } player || count <= 0)
            return;

        var candidates = CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard).ToList();
        var removed = new List<CardModel>();
        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var card = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
            if (card is null)
                break;

            candidates.Remove(card);
            removed.Add(card);
        }

        if (removed.Count == 0)
            return;

        var theft = TheNothingCardTheftVfx.Begin(removed, Creature);
        await CardPileCmd.RemoveFromCombat(removed, skipVisuals: true);
        if (theft is not null)
            await theft.WaitAsync();
    }

    private static int DeadlyValue(int deadly, int normal) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, deadly, normal);

    private static IEnumerable<string> GetIntentAssets()
    {
        AbstractIntent[] intents =
        [
            new DebuffIntent(strong: true),
            new StatusIntent(BaseStartVoid),
            new SingleAttackIntent(BaseThumpDamage),
            new MultiAttackIntent(BaseBatterDamage, BaseBatterHits),
            new CardDebuffIntent(),
            new BuffIntent(),
            new DebuffIntent()
        ];
        return intents.SelectMany(static intent => intent.AssetPaths).Distinct();
    }
}
