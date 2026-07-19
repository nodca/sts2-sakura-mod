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
    private bool _wasActiveForCardPlayed;
    private bool _preserveForNextTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    protected abstract SakuraElement Element { get; }
    protected abstract Type PermanentPowerType { get; }

    public static void PreserveAllForNextTurn(Creature owner)
    {
        foreach (var power in owner.Powers.OfType<SakuraElementStatePower>())
            power.PreserveForNextTurn();
    }

    public void PreserveForNextTurn() =>
        _preserveForNextTurn = true;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        _wasActiveForCardPlayed = Amount > 0
            && play.Card?.Owner?.Creature == Owner;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!_wasActiveForCardPlayed || !SakuraActions.HasElement(play.Card, Element))
        {
            _wasActiveForCardPlayed = false;
            return;
        }

        _wasActiveForCardPlayed = false;
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

