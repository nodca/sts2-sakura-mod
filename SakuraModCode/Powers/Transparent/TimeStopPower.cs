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

public class TimeStopPower : SakuraPowerModel
{
    private bool _preserveCurrentTurnState;
    private bool _extraTurnStarted;

    protected override string IconFileName => "time_stop.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public bool PreservesCurrentTurnState => _preserveCurrentTurnState;

    public void PreserveCurrentTurnState()
    {
        _preserveCurrentTurnState = true;
        PreserveElementStates();
    }

    public void PreserveElementStates() =>
        SakuraElementStatePower.PreserveAllForNextTurn(Owner);

    public override bool ShouldTakeExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        Amount > 0 && player.Creature == Owner;

    public override async Task AfterTakingExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        if (_preserveCurrentTurnState)
            _extraTurnStarted = true;
        else
            await PowerCmd.Remove(this);
    }

    public override bool ShouldFlush(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player != Owner.Player || !_preserveCurrentTurnState;

    public override bool ShouldClearBlock(Creature creature) =>
        !_preserveCurrentTurnState || creature != Owner;

    public override bool ShouldPlayerResetEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player != Owner.Player || !_preserveCurrentTurnState;

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (_extraTurnStarted && player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}


