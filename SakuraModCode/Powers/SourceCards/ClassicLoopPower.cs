using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace SakuraMod.SakuraModCode.Powers;

// Loop draws on every shuffle and on the first pile swap each turn; swaps that
// move no cards do not count.
public class ClassicLoopPower : SakuraPowerModel
{
    private bool _exchangedThisTurn;

    protected override string IconFileName => "loop_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (shuffler == Owner.Player)
            await DrawCards(choiceContext);
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            _exchangedThisTurn = false;
        return Task.CompletedTask;
    }

    internal static async Task AfterPileExchange(PlayerChoiceContext choiceContext, Player player, int movedCards)
    {
        if (movedCards <= 0)
            return;

        foreach (var power in player.Creature.Powers.OfType<ClassicLoopPower>().ToArray())
            await power.OnExchange(choiceContext);
    }

    private async Task OnExchange(PlayerChoiceContext choiceContext)
    {
        if (_exchangedThisTurn)
            return;

        _exchangedThisTurn = true;
        Flash();
        await DrawCards(choiceContext);
    }

    private async Task DrawCards(PlayerChoiceContext choiceContext)
    {
        if (Amount > 0 && Owner.Player is { } player)
            await CardPileCmd.Draw(choiceContext, Amount, player, false);
    }
}
