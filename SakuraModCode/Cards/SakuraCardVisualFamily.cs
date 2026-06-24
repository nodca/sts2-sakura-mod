using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraCardVisualFamily
{
    Vanilla,
    Clear,
    Kinomoto,
    Classic
}

internal static class SakuraCardVisualFamilies
{
    public static SakuraCardVisualFamily Family(NCard? card) =>
        Family(card?.Model);

    public static SakuraCardVisualFamily Family(CardModel? card)
    {
        if (card is null)
            return SakuraCardVisualFamily.Vanilla;

        if (IsClassicCard(card))
            return SakuraCardVisualFamily.Classic;

        if (IsKinomotoCard(card))
            return SakuraCardCatalog.IsTransparentCard(card)
                ? SakuraCardVisualFamily.Clear
                : SakuraCardVisualFamily.Kinomoto;

        return SakuraCardVisualFamily.Vanilla;
    }

    public static bool IsClear(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Clear;

    public static bool IsKinomoto(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Kinomoto;

    public static bool IsClassic(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Classic;

    public static bool IsVanilla(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Vanilla;

    public static bool IsClear(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Clear;

    public static bool IsKinomoto(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Kinomoto;

    public static bool IsClassic(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Classic;

    public static bool IsVanilla(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Vanilla;

    private static bool IsKinomotoCard(CardModel card) =>
        card is SakuraModCard or SakuraOptionCard;

    private static bool IsClassicCard(CardModel card) =>
        card is ClassicSakuraCard;
}
