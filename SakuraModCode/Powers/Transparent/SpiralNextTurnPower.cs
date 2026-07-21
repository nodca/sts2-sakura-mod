using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class SpiralNextTurnPower : SakuraPowerModel
{
    private sealed class Data
    {
        public CardModel? Source;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public void SetSourceCard(CardModel source) =>
        GetInternalData<Data>().Source = source.CreateClone();

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player.Creature != Owner)
            return;

        var source = GetInternalData<Data>().Source;
        if (source is not null)
        {
            for (var i = 0; i < Amount; i++)
                await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(source, false, choiceContext);
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        GetInternalData<Data>().Source = null;
        return Task.CompletedTask;
    }
}
