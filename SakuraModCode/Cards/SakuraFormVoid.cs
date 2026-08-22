using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraFormVoid
{
    internal static bool ShouldAdd(bool addsVoidOnNormalSakuraPlay, bool hasPinkTransformationCostume) =>
        addsVoidOnNormalSakuraPlay && !hasPinkTransformationCostume;

    internal static bool ShouldAdd(CardModel card) =>
        ShouldAdd(
            card is SakuraSourceCard { AddsVoidOnNormalSakuraPlay: true } && card.IsMutable,
            card.Owner?.GetRelic<ClassicPinkTransformationCostumeRelic>() is not null);

    internal static async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!ShouldAdd(card))
            return;

        await SakuraMagicCharge.AddVoidToDrawPile(choiceContext, card.Owner);
    }
}
