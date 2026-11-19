using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

// False: a Forgotten card showing up in hand hits a random enemy, like Juggernaut.
// True: a card coming back from Memory grants Block.
public class TrueOrFalsePower : SakuraPowerModel
{
    internal const string BlockVar = "Block";

    protected override string IconFileName => "true_or_false.png";
    public override PowerType Type => PowerType.Buff;
    // Amount is the damage; each copy also adds its Block.
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(BlockVar, 0)];

    public void AddBlock(int block)
    {
        if (block <= 0)
            return;

        DynamicVars[BlockVar].BaseValue += block;
        InvokeDisplayAmountChanged();
    }

    // Generated Forgotten cards entering hand and hand cards gaining Forgotten report here.
    internal static async Task AfterForgottenCardInHand(CardModel card)
    {
        if (card.Owner?.Creature.GetPower<TrueOrFalsePower>() is { } power)
            await power.HitRandomEnemy(new ThrowingPlayerChoiceContext());
    }

    // Drawn Forgotten cards (such as Spiral's copies on top of the Draw Pile) appear in hand too.
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner?.Creature == Owner && card.IsTemporary())
            await HitRandomEnemy(choiceContext);
    }

    private async Task HitRandomEnemy(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0 || CombatState is not { } combatState)
            return;

        var enemies = combatState.HittableEnemies;
        if (enemies.Count == 0)
            return;

        var target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(enemies);
        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, SakuraPowerValueProps.Damage, Owner);
    }

    // Each recalled card grants Block, so a 2X Remind pays out per card.
    internal static async Task AfterRecalledFromMemory(Player player, int count)
    {
        if (count <= 0 || player.Creature.GetPower<TrueOrFalsePower>() is not { } power)
            return;

        var block = power.DynamicVars[BlockVar].IntValue;
        if (block <= 0)
            return;

        for (var i = 0; i < count; i++)
        {
            power.Flash();
            await CreatureCmd.GainBlock(power.Owner, block, SakuraPowerValueProps.Block, null, false);
        }
    }
}
