using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Events;

public sealed class ClassicMonsterEvent : SakuraModEventTemplate
{
    private const int GoldReward = 100;
    private const int HealAmount = 15;
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "events/monster_event.png".ImagePath());

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsKinomotoSakuraRun(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var relic = ModelDb.Relic<ClassicMonsterRelic>().ToMutable();
        relic.Owner = Owner!;

        return
        [
            new EventOption(
                    this,
                    Accept,
                    InitialOptionKey("ACCEPT"),
                    HoverTipFactory.FromRelic(relic))
                .WithRelic(relic),
            Option(Reject)
        ];
    }

    private async Task Accept()
    {
        var player = Owner ?? throw new InvalidOperationException("Monster event has no owner.");
        var relic = ModelDb.Relic<ClassicMonsterRelic>().ToMutable();
        await RelicCmd.Obtain(relic, player);
        SetEventFinished(PageDescription("ACCEPT"));
    }

    private async Task Reject()
    {
        var player = Owner ?? throw new InvalidOperationException("Monster event has no owner.");
        await PlayerCmd.GainGold(GoldReward, player);
        await CreatureCmd.Heal(player.Creature, HealAmount);
        SetEventFinished(PageDescription("REJECT"));
    }
}
