using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Powers;

// Upgraded Clow Little: whenever the owner plays a card whose actual cost is 0,
// gain Amount Block.
public sealed class ClassicLittleUpgradedPower : SakuraPowerModel
{
    private readonly HashSet<CardModel> _zeroCostPlays = [];

    protected override string IconFileName => "little_upgraded.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Read before the play resolves, so until-played cost changes still count.
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player && ClassicSizeCostRules.PlayedCost(cardPlay) == 0)
            _zeroCostPlays.Add(cardPlay.Card);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_zeroCostPlays.Remove(cardPlay.Card) || Amount <= 0)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, SakuraPowerValueProps.Block, null, false);
    }
}
