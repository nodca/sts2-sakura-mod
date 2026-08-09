using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

public class SakuraFightPower : SakuraPowerModel
{
    protected override string IconFileName => "fight_power_sakuracard.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<ClowFight>()];

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player.Creature != Owner || Amount <= 0)
            return;

        Flash();
        for (var i = 0; i < Amount; i++)
            await AddTemporaryUpgradedFight(choiceContext, player);
    }

    internal static async Task AddTemporaryUpgradedFight(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Fight generated cards require an active combat.");
        var fight = combatState.CreateCard<ClowFight>(player);
        fight.UpgradeInternal();
        await SakuraGeneratedCardLifecycle.AddTemporaryGeneratedCardToHand(
            fight,
            freeThisTurn: false,
            choiceContext);
    }
}
