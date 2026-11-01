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

public class DreamingPower : SakuraPowerModel
{
    protected override string IconFileName => "dreaming.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDrawLate(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player.Creature != Owner || Amount <= 0)
            return;

        for (var i = 0; i < Amount; i++)
        {
            var drawPile = CardPile.Get(PileType.Draw, player);
            var memory = SakuraMemoryPile.Get(player);
            var hasDraw = drawPile is { IsEmpty: false };
            var hasMemory = memory is { IsEmpty: false };
            if (!hasDraw && !hasMemory)
                return;

            var fromMemory = hasMemory
                && (!hasDraw || await ChooseMemory(player, choiceContext, combatState, drawPile!, memory!));
            if (fromMemory)
            {
                var recalled = await SakuraActions.SelectFromCardPreviews(player, choiceContext, memory!.Cards, cancelable: false);
                if (recalled is not null)
                    await SakuraMemoryPile.RecallToHand(player, [recalled], freeThisTurn: false);
                continue;
            }

            var card = await SakuraActions.SelectFromCardPreviews(player, choiceContext, drawPile!.Cards, cancelable: false);
            if (card is null)
                return;

            await CardPileCmd.Add(card, PileType.Hand);
            await SakuraForgotten.GrantTemporary(choiceContext, card);
            card.SetToFreeThisTurn();
        }
    }

    // Draw Pile comes first, so index 0 still picks it like the old single-pile prompt.
    private static async Task<bool> ChooseMemory(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardPile drawPile,
        CardPile memory)
    {
        var draw = combatState.CreateCard<ExchangeDrawChoice>(player);
        var recall = combatState.CreateCard<ExchangeMemoryChoice>(player);
        draw.SetDisplayedCount(drawPile.Cards.Count);
        recall.SetDisplayedCount(memory.Cards.Count);
        return await SakuraActions.SelectFromCards(player, choiceContext, [draw, recall], cancelable: false) is ExchangeMemoryChoice;
    }
}
