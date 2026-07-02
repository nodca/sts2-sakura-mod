using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Events;

public class AnotherMeEvent : CustomEventModel
{
    private const int DeclineHeal = 6;
    private const int DeclineGold = 25;
    private const int TsubasaChoiceCount = 3;
    private const float CardPreviewDuration = 2f;
    private const string BgmPath = "music/another_me.ogg";
    private static readonly LocString TsubasaSelectionPrompt =
        new("events", "SAKURAMOD-ANOTHER_ME_EVENT.selectionPrompt");

    public override ActModel[] Acts => [ModelDb.Act<Hive>()];
    public override string? CustomInitialPortraitPath => "events/another_me.png".ImagePath();

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsSakuraRun(runState);

    public override void OnRoomEnter()
    {
        base.OnRoomEnter();
        SakuraEventBgm.Play(BgmPath);
    }

    protected override void OnEventFinished()
    {
        SakuraEventBgm.Stop();
        base.OnEventFinished();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Option(AcceptEncounter, HoverTipFactory.FromCardWithCardHoverTips<MemoryFracture>()),
        Option(LeaveTheDream)
    ];

    private async Task AcceptEncounter()
    {
        await AddTsubasaRewards();
        SetEventFinished(PageDescription("ACCEPT_ENCOUNTER"));
    }

    private async Task LeaveTheDream()
    {
        var player = Owner!;
        var missingHp = Math.Max(0, player.Creature.MaxHp - player.Creature.CurrentHp);
        var heal = Math.Min(DeclineHeal, missingHp);
        if (heal > 0)
            await CreatureCmd.Heal(player.Creature, heal);

        await PlayerCmd.GainGold(DeclineGold, player);
        SetEventFinished(PageDescription("LEAVE_THE_DREAM"));
    }

    private async Task AddTsubasaRewards()
    {
        var player = Owner!;
        var choices = SakuraCardCatalog.TsubasaCardTypes
            .Select(type => player.RunState.CreateCard(SakuraCardCatalog.CardTemplate(type), player))
            .ToList();

        IReadOnlyList<CardModel> selected = [];
        try
        {
            selected = (await CardSelectCmd.FromSimpleGrid(
                    EventCardSelectionContext.Instance,
                    choices,
                    player,
                    new CardSelectorPrefs(TsubasaSelectionPrompt, 0, TsubasaChoiceCount)
                    {
                        Cancelable = true
                    }))
                .ToList();

            var cardsToAdd = new List<CardModel> { player.RunState.CreateCard<MemoryFracture>(player) };
            cardsToAdd.AddRange(selected);

            var results = await CardPileCmd.Add(cardsToAdd, PileType.Deck);
            CardCmd.PreviewCardPileAdd(results, CardPreviewDuration);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }

    private sealed class EventCardSelectionContext : PlayerChoiceContext
    {
        public static readonly EventCardSelectionContext Instance = new();

        // Event-room choices are already serialized by the selection command; there is no combat queue to pause.
        public override Task SignalPlayerChoiceBegun(PlayerChoiceOptions options) => Task.CompletedTask;

        public override Task SignalPlayerChoiceEnded() => Task.CompletedTask;
    }
}
