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
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Events;

public class ClassicTheSealedCardEvent() : SakuraModEventTemplate
{
    private const int HpLossPercent = 20;
    private const int CardsToRemove = 2;
    private const int GoldMin = 32;
    private const int GoldMax = 47;
    private const string FightWithoutLoveLockedOptionKey = "FIGHT_WITHOUT_LOVE_LOCKED";
    private const string FightWithLoveLockedOptionKey = "FIGHT_WITH_LOVE_LOCKED";

    private bool _removeLoveAfterCombat;

    public override bool IsShared => true;
    public override string? CustomInitialPortraitPath => "monsters/the_nothing.png".ImagePath();

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsKinomotoSakuraRun(runState);

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

