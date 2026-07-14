using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using STS2RitsuLib.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraCardVisualFamily
{
    Vanilla,
    Kinomoto
}

internal enum SakuraCardContentOwner
{
    Vanilla,
    Sakura
}

internal enum SakuraCardVisualLayout
{
    None,
    Clear,
    Classic
}

internal static class SakuraCardVisualFamilies
{
    public static SakuraCardContentOwner ContentOwner(NCard? card) =>
        ContentOwner(card?.Model);

    public static SakuraCardContentOwner ContentOwner(CardModel? card) =>
        card is SakuraOptionCard
            || card is not null && SakuraCardCatalog.TryGetMetadata(card, out _)
            ? SakuraCardContentOwner.Sakura
            : SakuraCardContentOwner.Vanilla;

    public static SakuraCardVisualFamily Family(NCard? card) =>
        Family(card?.Model);

    public static SakuraCardVisualFamily Family(CardModel? card)
    {
        if (card is null)
            return SakuraCardVisualFamily.Vanilla;

        if (Layout(card) != SakuraCardVisualLayout.None)
            return SakuraCardVisualFamily.Kinomoto;

        return SakuraCardVisualFamily.Vanilla;
    }

    public static SakuraCardVisualLayout Layout(NCard? card) =>
        Layout(card?.Model);

    public static SakuraCardVisualLayout Layout(CardModel? card)
    {
        if (card is SakuraOptionCard)
            return SakuraCardVisualLayout.Clear;

        if (card is null || !SakuraCardCatalog.TryGetMetadata(card, out var metadata))
            return SakuraCardVisualLayout.None;

        return metadata.VisualRoute switch
        {
            SakuraSourceCardVisualRoute.Clear => SakuraCardVisualLayout.Clear,
            SakuraSourceCardVisualRoute.Classic => SakuraCardVisualLayout.Classic,
            SakuraSourceCardVisualRoute.Vanilla => SakuraCardVisualLayout.None,
            _ => throw new ArgumentOutOfRangeException(nameof(metadata.VisualRoute), metadata.VisualRoute, null)
        };
    }

    public static bool IsKinomoto(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Kinomoto;

    public static bool UsesClearLayout(NCard? card) =>
        Layout(card) == SakuraCardVisualLayout.Clear;

    public static bool UsesClassicLayout(NCard? card) =>
        Layout(card) == SakuraCardVisualLayout.Classic;

    public static bool IsVanilla(NCard? card) =>
        Family(card) == SakuraCardVisualFamily.Vanilla;

    public static bool IsKinomoto(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Kinomoto;

    public static bool UsesClearLayout(CardModel? card) =>
        Layout(card) == SakuraCardVisualLayout.Clear;

    public static bool UsesClassicLayout(CardModel? card) =>
        Layout(card) == SakuraCardVisualLayout.Classic;

    public static bool IsVanilla(CardModel? card) =>
        Family(card) == SakuraCardVisualFamily.Vanilla;
}
