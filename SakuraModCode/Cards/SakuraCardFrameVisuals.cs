using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardFrameVisuals
{
    private const string DefaultPortraitFileName = "card.png";
    private static readonly Lazy<CanvasItemMaterial> _plainFrameMaterial = new(static () => new CanvasItemMaterial());

    public static Material PlainFrameMaterial => _plainFrameMaterial.Value;

    public static string BigPortraitPath(CardModel card) => PortraitFileName(card).BigCardImagePath();

    public static string PortraitPath(CardModel card) => PortraitFileName(card).CardImagePath();

    public static bool UsesCustomNonClearFrame(CardModel card) => false;

    public static IEnumerable<string> RunAssetPaths(CardModel card)
    {
        if (SakuraTransparentCardCatalog.IsTransparentCard(card))
        {
            yield return ClearCardLayout.CardArtPath(card.GetType());
            foreach (var path in SakuraDescriptionRegion.AssetPaths(card))
                yield return path;
            yield break;
        }

        yield return PortraitPath(card);
        yield return BigPortraitPath(card);
    }

    public static Material? CustomFrameMaterial(CardModel card) =>
        null;

    private static string PortraitFileName(CardModel card) => DefaultPortraitFileName;

}
