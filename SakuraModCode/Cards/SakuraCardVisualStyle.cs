using Godot;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVisualStyle
{
    public const int NativeTitleFontSize = 26;
    public const int NativeTitleMinFontSize = 8;
    public const int NativeTitleMaxFontSize = 26;
    public const int NativeTitleOutlineSize = 12;
    public const int NativeTitleShadowOffset = 2;
    public const int NativeTitleShadowOutlineSize = 12;

    public static Color UpgradedNameTextColor { get; } = new(0.45f, 1f, 0.36f, 1f);
}
