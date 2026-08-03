using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraFadeCardLifecycle
{
    public static bool IsEligible(bool isStillInHand, bool hasFade, bool shouldEtherealTrigger) =>
        isStillInHand && hasFade && shouldEtherealTrigger;

    public static async Task RemoveEligibleCardsFromHand(Player player, ICombatState combatState)
    {
        foreach (var card in CardPile.GetCards(player, PileType.Hand).ToArray())
        {
            if (!IsEligible(
                    card.Pile?.Type == PileType.Hand,
                    card.Keywords.Contains(SakuraKeywords.Fade),
                    Hook.ShouldEtherealTrigger(combatState, card)))
                continue;

            TemporaryDissolveVfx.PlayFade(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
    }
}
