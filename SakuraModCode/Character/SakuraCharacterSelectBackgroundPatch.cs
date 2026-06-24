using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using SakuraMod.SakuraModCode.Classic.Character;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class SakuraCharacterSelectBackgroundPatch
{
    private static readonly FieldInfo BgContainerField = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer");

    [HarmonyPostfix]
    private static void SelectCharacterPostfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        if (characterModel is not SakuraMod and not ClassicSakura)
            return;

        Apply(__instance, characterModel);
    }

    private static void Apply(NCharacterSelectScreen screen, CharacterModel characterModel)
    {
        if (BgContainerField.GetValue(screen) is not Control bgContainer)
            return;

        var expectedName = characterModel.Id.Entry + "_bg";
        var root = bgContainer.GetNodeOrNull<Control>(expectedName) ?? LastControlChild(bgContainer);
        if (root is null)
            return;

        var hasSizedBackground = false;

        var globalTarget = root.GetViewport().GetVisibleRect();
        var localTarget = ToLocalRect(bgContainer, globalTarget);

        ApplyTopLeftRect(root, localTarget);
        foreach (var background in Backgrounds(root))
        {
            var textureSize = background.Texture?.GetSize() ?? Vector2.Zero;
            if (textureSize.X <= 0f || textureSize.Y <= 0f)
                continue;

            ApplyFullRect(background);
            background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            hasSizedBackground = true;
        }

        if (!hasSizedBackground)
            return;
    }

    private static Rect2 ToLocalRect(Control parent, Rect2 globalRect)
    {
        var inverse = parent.GetGlobalTransformWithCanvas().AffineInverse();
        var localTopLeft = inverse * globalRect.Position;
        var localBottomRight = inverse * (globalRect.Position + globalRect.Size);
        return new Rect2(localTopLeft, localBottomRight - localTopLeft);
    }

    private static void ApplyTopLeftRect(Control control, Rect2 rect)
    {
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 0f;
        control.AnchorBottom = 0f;
        control.OffsetLeft = rect.Position.X;
        control.OffsetTop = rect.Position.Y;
        control.OffsetRight = rect.Position.X + rect.Size.X;
        control.OffsetBottom = rect.Position.Y + rect.Size.Y;
        control.Scale = Vector2.One;
        control.PivotOffset = Vector2.Zero;
    }

    private static void ApplyFullRect(Control control)
    {
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
        control.Scale = Vector2.One;
        control.PivotOffset = Vector2.Zero;
    }

    private static IEnumerable<TextureRect> Backgrounds(Control root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is TextureRect textureRect)
                yield return textureRect;
        }
    }

    private static Control? LastControlChild(Control parent)
    {
        for (var index = parent.GetChildCount() - 1; index >= 0; index--)
        {
            if (parent.GetChild(index) is Control control)
                return control;
        }

        return null;
    }
}
