using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardFrameVisuals
{
    private static readonly Lazy<CanvasItemMaterial> _plainFrameMaterial = new(static () => new CanvasItemMaterial());

    public static Material PlainFrameMaterial => _plainFrameMaterial.Value;

    public static bool UsesCustomNonClearFrame(CardModel card) => false;

    public static IEnumerable<string> RunAssetPaths(CardModel card)
    {
        if (!SakuraCardVisualFamilies.UsesClearLayout(card))
            throw new InvalidOperationException($"Clear card assets are not defined for {card.GetType().Name}.");

        yield return ClearCardVisualAssets.ArtPath(card.GetType());
        foreach (var path in SakuraDescriptionRegion.AssetPaths(card))
            yield return path;
    }

    public static Material? CustomFrameMaterial(CardModel card) =>
        null;
}
