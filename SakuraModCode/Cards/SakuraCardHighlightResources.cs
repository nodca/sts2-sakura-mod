using Godot;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardHighlightResources
{
    public const string ClearPath =
        "res://SakuraMod/images/cards/highlights/clear_card_frame_sdf.exr";
    public const string ClassicPath =
        "res://SakuraMod/images/cards/highlights/classic_card_frame_sdf.exr";

    private static readonly SakuraCardTextureResource ClearResource =
        SakuraCardTextureResource.FromPath(ClearPath);
    private static readonly SakuraCardTextureResource ClassicResource =
        SakuraCardTextureResource.FromPath(ClassicPath);

    public static Texture2D ResolveClear() =>
        ClearResource.ResolveRequired("Clear Card highlight");

    public static Texture2D ResolveClassic() =>
        ClassicResource.ResolveRequired("Classic Sakura highlight");

    public static bool IsSakuraHighlight(Texture2D? texture)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(texture))
            return true;

        return string.Equals(texture!.ResourcePath, ClearPath, StringComparison.Ordinal)
            || string.Equals(texture.ResourcePath, ClassicPath, StringComparison.Ordinal);
    }
}
