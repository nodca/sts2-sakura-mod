using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public sealed class RecordPower : SakuraPowerModel
{
    private const int MaxRecordedCards = 3;

    private sealed class Data
    {
        public readonly List<CardModel> Cards = [];
    }

    protected override string IconFileName => "record.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => GetInternalData<Data>().Cards.Count;

    protected override object InitInternalData() => new Data();

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner.Player)
            return;

        foreach (var card in GetInternalData<Data>().Cards)
        {
            await SakuraGeneratedCardLifecycle.AddTemporaryGeneratedCardToHand(
                card,
                freeThisTurn: true,
                choiceContext);
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!play.IsLastInSeries
            || play.Card.Owner?.Creature != Owner
            || play.Card is Record
            || play.Card.CombatState is null)
            return Task.CompletedTask;

        var cards = GetInternalData<Data>().Cards;
        if (cards.Count >= MaxRecordedCards)
            return Task.CompletedTask;

        cards.Add(play.Card.CreateClone());
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
