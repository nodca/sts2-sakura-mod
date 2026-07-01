using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
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
    private const float CardPreviewDuration = 2f;
    private const string BgmPath = "music/another_me.ogg";

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
        Option(AcceptEncounter, HoverTipFactory.FromCardWithCardHoverTips<Kindness>()),
        Option(LeaveTheDream)
    ];

    private async Task AcceptEncounter()
    {
        await AddPlaceholderReward();
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

    private async Task AddPlaceholderReward()
    {
        var card = Owner!.RunState.CreateCard<Kindness>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), CardPreviewDuration);
    }
}
