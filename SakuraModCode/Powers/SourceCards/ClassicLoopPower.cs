using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace SakuraMod.SakuraModCode.Powers;

// A shuffle is one full cycle of the deck; swapping piles takes two swaps to
// come back around, so Loop draws on every shuffle and on every second swap.
public class ClassicLoopPower : SakuraPowerModel
{
    internal const int ExchangesPerDraw = 2;

    private int _exchanges;

    protected override string IconFileName => "loop_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // The label keeps the draw amount; swap progress lives in the hover text.
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Exchanges", 0), new DynamicVar("ExchangesPerDraw", ExchangesPerDraw)];

    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (shuffler == Owner.Player)
            await DrawCards(choiceContext);
    }

    internal static async Task AfterPileExchange(PlayerChoiceContext choiceContext, Player player, int movedCards)
    {
        if (movedCards <= 0)
            return;

        foreach (var power in player.Creature.Powers.OfType<ClassicLoopPower>().ToArray())
            await power.CountExchange(choiceContext);
    }

    private async Task CountExchange(PlayerChoiceContext choiceContext)
    {
        _exchanges = (_exchanges + 1) % ExchangesPerDraw;
        DynamicVars["Exchanges"].BaseValue = _exchanges;
        Flash();
        if (_exchanges == 0)
            await DrawCards(choiceContext);
    }

    private async Task DrawCards(PlayerChoiceContext choiceContext)
    {
        if (Amount > 0 && Owner.Player is { } player)
            await CardPileCmd.Draw(choiceContext, Amount, player, false);
    }
}
