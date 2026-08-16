using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;

namespace SakuraMod.RuntimeTests;

internal sealed class RuntimeFixtureAction(
    Player owner,
    Func<PlayerChoiceContext, Task> execute) : GameAction
{
    public override ulong OwnerId => owner.NetId;
    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;
    public override bool RecordableToReplay => false;
    public PlayerChoiceContext? ChoiceContext { get; private set; }

    protected override async Task ExecuteAction()
    {
        ChoiceContext = new GameActionPlayerChoiceContext(this);
        await execute(ChoiceContext);
    }

    public override INetAction ToNetAction() =>
        throw new NotSupportedException(
            "Runtime fixture actions are singleplayer-only setup operations.");
}
