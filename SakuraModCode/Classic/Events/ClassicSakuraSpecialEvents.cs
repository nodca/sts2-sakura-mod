using BaseLib.Abstracts;
using BaseLib.Utils;
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
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Events;

public class ClassicXiaoLangsFeelingsEvent() : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Hive>()];
    public override string? CustomInitialPortraitPath => "events/xiaolangs_feelings.png".ImagePath();

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(static player => player.Character is ClassicSakura);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Option(Accept, HoverTipFactory.FromCardWithCardHoverTips<SakuraLove>()),
        Option(Reject)
    ];

    private async Task Accept()
    {
        var love = Owner!.RunState.CreateCard<SakuraLove>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(love, PileType.Deck), 2f);
        SetEventFinished(PageDescription("ACCEPT"));
    }

    private async Task Reject()
    {
        var missingHp = Math.Max(0, Owner!.Creature.MaxHp - Owner.Creature.CurrentHp);
        if (missingHp > 0)
            await CreatureCmd.Heal(Owner.Creature, missingHp);

        SetEventFinished(PageDescription("REJECT"));
    }
}

public class ClassicTheSealedCardEvent() : CustomEventModel
{
    private const int HpLossPercent = 20;
    private const int CardsToRemove = 2;
    private const int GoldMin = 32;
    private const int GoldMax = 47;
    private const string FightWithoutLoveLockedOptionKey = "FIGHT_WITHOUT_LOVE_LOCKED";
    private const string FightWithLoveLockedOptionKey = "FIGHT_WITH_LOVE_LOCKED";

    private bool _removeLoveAfterCombat;

    public override ActModel[] Acts => [ModelDb.Act<Glory>()];
    public override bool IsShared => true;
    public override string? CustomInitialPortraitPath => "monsters/the_nothing.png".ImagePath();

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(static player => player.Character is ClassicSakura);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var hasLove = Owner!.Deck.Cards.Any(static card => card is SakuraLove);
        return
        [
            hasLove
                ? LockedOption(FightWithoutLoveLockedOptionKey, tips: HoverTipFactory.FromCardWithCardHoverTips<ClowNothing>().ToArray())
                : Option(FightWithoutLove, HoverTipFactory.FromCardWithCardHoverTips<ClowNothing>()),
            hasLove
                ? Option(FightWithLove, HoverTipFactory.FromCardWithCardHoverTips<SakuraHope>())
                : LockedOption(FightWithLoveLockedOptionKey, tips: HoverTipFactory.FromCardWithCardHoverTips<SakuraLove>().ToArray()),
            Option(Escape)
        ];
    }

    private Task FightWithoutLove()
    {
        _removeLoveAfterCombat = false;
        EnterSealedCombat();
        return Task.CompletedTask;
    }

    private Task FightWithLove()
    {
        _removeLoveAfterCombat = true;
        EnterSealedCombat();
        return Task.CompletedTask;
    }

    private async Task Escape()
    {
        var hpLoss = Math.Max(0, Owner!.Creature.MaxHp * HpLossPercent / 100);
        if (hpLoss > 0)
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        var cards = Owner.Deck.Cards.ToList();
        var removed = new List<CardModel>();
        for (var i = 0; i < CardsToRemove && cards.Count > 0; i++)
        {
            var card = Owner.PlayerRng.Rewards.NextItem(cards);
            if (card is null)
                break;

            cards.Remove(card);
            removed.Add(card);
        }

        if (removed.Count > 0)
            await CardPileCmd.RemoveFromDeck(removed);

        SetEventFinished(PageDescription("ESCAPE"));
    }

    public override async Task Resume(AbstractRoom exitedRoom)
    {
        if (exitedRoom is not CombatRoom)
            return;

        var shouldRewardHope = _removeLoveAfterCombat;
        if (_removeLoveAfterCombat)
        {
            var love = Owner!.Deck.Cards.OfType<SakuraLove>().FirstOrDefault();
            if (love is not null)
                await CardPileCmd.RemoveFromDeck(love);
            _removeLoveAfterCombat = false;
        }

        await OfferSealedCombatRewards(shouldRewardHope);
        SetEventFinished(PageDescription("DONE"));
    }

    private void EnterSealedCombat()
    {
        EnterCombatWithoutExitingEvent<ClassicTheNothingEncounter>(
            [],
            shouldResumeAfterCombat: true);
    }

    private async Task OfferSealedCombatRewards(bool shouldRewardHope)
    {
        var rewardCard = shouldRewardHope
            ? (CardModel)Owner!.RunState.CreateCard<SakuraHope>(Owner)
            : Owner!.RunState.CreateCard<ClowNothing>(Owner);

        List<Reward> rewards =
        [
            new GoldReward(GoldMin, GoldMax, Owner),
            new SpecialCardReward(rewardCard, Owner)
        ];

        await new RewardsSet(Owner)
            .WithCustomRewards(rewards)
            .WithSkippingDisallowed()
            .Offer();
    }
}

public class ClassicTheNothingEncounter() : CustomEncounterModel(RoomType.Elite)
{
    public override bool ShouldGiveRewards => false;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<ClassicTheNothingMonster>()
    ];

    public override bool IsValidForAct(ActModel act) => false;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<ClassicTheNothingMonster>().ToMutable(), null)
    ];
}

public class ClassicTheNothingMonster : CustomMonsterModel
{
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
    public override string? CustomVisualPath => "monsters/the_nothing.png".ImagePath();
    public override bool HasDeathSfx => false;
    public override string? HurtSfx => null;
    public override IEnumerable<string> AssetPaths => GetIntentAssets().Prepend(CustomVisualPath!);

    private int StartVoid => DeadlyValue(DeadlyStartVoid, BaseStartVoid);
    private int ThumpDamage => DeadlyValue(DeadlyThumpDamage, BaseThumpDamage);
    private int BatterDamage => DeadlyValue(DeadlyBatterDamage, BaseBatterDamage);
    private int BatterHits => DeadlyValue(DeadlyBatterHits, BaseBatterHits);
    private int DestructionDamage => DeadlyValue(DeadlyDestructionDamage, BaseDestructionDamage);
    private int DestructionVoid => DeadlyValue(DeadlyDestructionVoid, BaseDestructionVoid);
    private int DestructionExhaust => DeadlyValue(DeadlyDestructionExhaust, BaseDestructionExhaust);
    private int Strengthen => DeadlyValue(DeadlyStrengthen, BaseStrengthen);
    private int Weaken => DeadlyValue(DeadlyWeaken, BaseWeaken);

    public override NCreatureVisuals? CreateCustomVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualPath!, "The Nothing", CombatVisualScale);

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

    private async Task StartVoidMove(IReadOnlyList<Creature> targets)
    {
        foreach (var target in targets)
        {
            await PowerCmd.Apply<ClassicNothingMonsterPower>(new ThrowingPlayerChoiceContext(), target, 1, Creature, null, false);
            await AddVoids(target, StartVoid);
        }
    }

    private async Task ThumpAttack(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ThumpDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .Execute(null);

    private async Task BatterAttack(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(BatterDamage)
            .FromMonster(this)
            .WithHitCount(BatterHits)
            .WithNoAttackerAnim()
            .Execute(null);

    private async Task Destruction(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DestructionDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .Execute(null);

        foreach (var target in targets)
        {
            await PurgeRandomCombatCards(target, DestructionExhaust);
            await AddVoids(target, DestructionVoid);
        }
    }

    private async Task StrengthenSelf(IReadOnlyList<Creature> targets)
    {
        foreach (var power in Creature.Powers.ToList())
            await PowerCmd.Remove(power);

        await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, false);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, Strengthen, Creature, null, false);
    }

    private async Task WeakenPlayers(IReadOnlyList<Creature> targets)
    {
        foreach (var target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
            await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, Weaken, Creature, null, false);
        }
    }

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

    private static async Task PurgeRandomCombatCards(Creature target, int count)
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

        if (removed.Count > 0)
            await CardPileCmd.RemoveFromCombat(removed, skipVisuals: false);
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
