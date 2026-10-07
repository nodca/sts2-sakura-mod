using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public abstract class SakuraElementStatePower : SakuraPowerModel
{
    // Set only from synced flow (Transparent Time's card play, TimeStop, Clow Time's
    // stasis in BeforeSideTurnEnd) and consumed in the synced AfterSideTurnEnd; both
    // machines see the same set/consume sequence, so the flag never gates a
    // one-sided command.
    private bool _preserveForNextTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    internal abstract SakuraElement Element { get; }

    protected abstract Type PermanentPowerType { get; }

    public static void PreserveAllForNextTurn(Creature owner)
    {
        foreach (var power in owner.Powers.OfType<SakuraElementStatePower>())
            power.PreserveForNextTurn();
    }

    public void PreserveForNextTurn() =>
        _preserveForNextTurn = true;

    // A state pays off on element cards played while it is up, not on the play that entered
    // it. Entering is part of resolving a play, and the play is what records the entry, so
    // the decision still reads the After hook's play rather than a Before/After flag pair.
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner?.Creature != Owner)
            return;

        // Consumed even where it suppresses: the record belongs to one play, and the card's
        // next play decides from its own entry.
        var enteredByThisPlay = SakuraElementState.ConsumeEnteredByPlay(play.Card, Element);
        if (enteredByThisPlay || Amount <= 0 || !SakuraActions.HasElement(play.Card, Element))
            return;

        await TriggerElement(choiceContext, play);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        if (_preserveForNextTurn)
        {
            _preserveForNextTurn = false;
            return;
        }

        if (Owner.Powers.Any(power => power.GetType() == PermanentPowerType))
            return;

        await PowerCmd.Decrement(this);
    }

    protected abstract Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play);
}

