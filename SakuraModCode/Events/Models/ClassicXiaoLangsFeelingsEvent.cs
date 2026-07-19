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

public class ClassicXiaoLangsFeelingsEvent() : SakuraModEventTemplate
{
    public override string? CustomInitialPortraitPath => "events/xiaolangs_feelings.png".ImagePath();

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsKinomotoSakuraRun(runState);

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

