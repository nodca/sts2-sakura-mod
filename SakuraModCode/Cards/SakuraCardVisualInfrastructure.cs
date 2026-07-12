using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVisualInfrastructure
{
    private static readonly StringName PanelStyleName = new("panel");
    private static readonly string[] ReloadOwnedVisibilityFieldNames =
    [
        "_portraitBorder",
        "_portrait",
        "_frame",
        "_ancientPortrait",
        "_ancientBorderGlassOverlay",
        "_ancientBorder",
        "_ancientTextBg",
        "_ancientBanner",
        "_banner",
        "_lock",
    ];

    private static readonly FieldInfo[] ReloadOwnedVisibilityFields =
        ReloadOwnedVisibilityFieldNames
            .Select(static fieldName => typeof(NCard).GetField(
                fieldName,
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .OfType<FieldInfo>()
            .ToArray();
    private static readonly FieldInfo PortraitField =
        PrivateAccess.DeclaredField<NCard>("_portrait");
    private static readonly FieldInfo AncientPortraitField =
        PrivateAccess.DeclaredField<NCard>("_ancientPortrait");

    public static bool IsGodotInstanceUsable(GodotObject? instance)
    {
        try
        {
            return instance is not null
                && GodotObject.IsInstanceValid(instance)
                && (instance is not Node node || !node.IsQueuedForDeletion());
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static bool IsReloadOwnedVisibility(NCard card, CanvasItem item)
    {
        foreach (var field in ReloadOwnedVisibilityFields)
        {
            if (ReferenceEquals(field.GetValue(card), item))
                return true;
        }

        return false;
    }

    public static bool TrySynchronizeCurrentModelPortraits(NCard card)
    {
        try
        {
            var portrait = card.Model?.Portrait;
            var normalPortrait = PortraitField.GetValue(card) as TextureRect;
            var ancientPortrait = AncientPortraitField.GetValue(card) as TextureRect;
            if (!IsGodotInstanceUsable(portrait)
                || !IsGodotInstanceUsable(normalPortrait)
                || !IsGodotInstanceUsable(ancientPortrait))
            {
                return false;
            }

            SetTextureIfDifferent(normalPortrait, portrait);
            SetTextureIfDifferent(ancientPortrait, portrait);
            return HasTexture(normalPortrait, portrait)
                && HasTexture(ancientPortrait, portrait);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static void ApplySize(Control? control, Vector2 size)
    {
        ApplySize(control, size, size * 0.5f);
    }

    public static void ApplySize(Control? control, Vector2 size, Vector2 pivotOffset)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (control.CustomMinimumSize != size)
            control.CustomMinimumSize = size;
        if (control.Size != size)
            control.Size = size;
        if (control.PivotOffset != pivotOffset)
            control.PivotOffset = pivotOffset;
    }

    public static void ApplyBox(Control? control, Rect2 box)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (control.Position != box.Position)
            control.Position = box.Position;
        if (control.CustomMinimumSize != box.Size)
            control.CustomMinimumSize = box.Size;
        if (control.Size != box.Size)
            control.Size = box.Size;
        if (control.Scale != Vector2.One)
            control.Scale = Vector2.One;

        var pivotOffset = box.Size * 0.5f;
        if (control.PivotOffset != pivotOffset)
            control.PivotOffset = pivotOffset;
    }

    public static void ApplyTopLeftAnchors(Control? control)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (control.AnchorLeft != 0f)
            control.AnchorLeft = 0f;
        if (control.AnchorTop != 0f)
            control.AnchorTop = 0f;
        if (control.AnchorRight != 0f)
            control.AnchorRight = 0f;
        if (control.AnchorBottom != 0f)
            control.AnchorBottom = 0f;
    }

    public static void ApplyThemeColorOverride(Control? control, StringName name, Color color)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (!control.HasThemeColorOverride(name) || control.GetThemeColor(name) != color)
            control.AddThemeColorOverride(name, color);
    }

    public static void ApplyThemeConstantOverride(Control? control, StringName name, int value)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (!control.HasThemeConstantOverride(name) || control.GetThemeConstant(name) != value)
            control.AddThemeConstantOverride(name, value);
    }

    public static void ApplyThemeFontSizeOverride(Control? control, StringName name, int value)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (!control.HasThemeFontSizeOverride(name) || control.GetThemeFontSize(name) != value)
            control.AddThemeFontSizeOverride(name, value);
    }

    public static void RemoveThemeColorOverride(Control? control, StringName name)
    {
        if (control is null || !IsGodotInstanceUsable(control) || !control.HasThemeColorOverride(name))
            return;

        control.RemoveThemeColorOverride(name);
    }

    public static void RemoveThemeConstantOverride(Control? control, StringName name)
    {
        if (control is null || !IsGodotInstanceUsable(control) || !control.HasThemeConstantOverride(name))
            return;

        control.RemoveThemeConstantOverride(name);
    }

    public static void RemoveThemeFontSizeOverride(Control? control, StringName name)
    {
        if (control is null || !IsGodotInstanceUsable(control) || !control.HasThemeFontSizeOverride(name))
            return;

        control.RemoveThemeFontSizeOverride(name);
    }

    public static bool HasTexture(TextureRect? textureRect, Texture2D? texture) =>
        TryGetTexture(textureRect, out var currentTexture)
        && SameTexture(currentTexture, texture);

    public static bool HasTexture(NinePatchRect? textureRect, Texture2D? texture) =>
        TryGetTexture(textureRect, out var currentTexture)
        && SameTexture(currentTexture, texture);

    public static void SetTextureIfDifferent(TextureRect? textureRect, Texture2D? texture)
    {
        if (!IsGodotInstanceUsable(textureRect) || (texture is not null && !IsGodotInstanceUsable(texture)))
            return;

        if (HasTexture(textureRect, texture))
            return;

        try
        {
            textureRect!.Texture = texture;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    public static void SetTextureIfDifferent(NinePatchRect? textureRect, Texture2D? texture)
    {
        if (!IsGodotInstanceUsable(textureRect) || (texture is not null && !IsGodotInstanceUsable(texture)))
            return;

        if (HasTexture(textureRect, texture))
            return;

        try
        {
            textureRect!.Texture = texture;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    public static bool TryGetTexture(TextureRect? textureRect, out Texture2D? texture)
    {
        if (!IsGodotInstanceUsable(textureRect))
        {
            texture = null;
            return false;
        }

        try
        {
            texture = textureRect!.Texture;
            return true;
        }
        catch (ObjectDisposedException)
        {
            texture = null;
            return false;
        }
    }

    public static bool TryGetTexture(NinePatchRect? textureRect, out Texture2D? texture)
    {
        if (!IsGodotInstanceUsable(textureRect))
        {
            texture = null;
            return false;
        }

        try
        {
            texture = textureRect!.Texture;
            return true;
        }
        catch (ObjectDisposedException)
        {
            texture = null;
            return false;
        }
    }

    public static Panel CreatePanel(string name, Color color, int cornerRadius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius
        };

        var panel = new Panel
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride(PanelStyleName, style);
        return panel;
    }

    public static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        var cornerRadius = Mathf.Min(radius, Mathf.Min(halfSize.X, halfSize.Y));
        var q = point.Abs() - (halfSize - Vector2.One * cornerRadius);
        var outside = new Vector2(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f));
        return outside.Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0f) - cornerRadius;
    }

    private static bool SameTexture(Texture2D? currentTexture, Texture2D? texture) =>
        (currentTexture is null && texture is null)
        || (IsGodotInstanceUsable(currentTexture)
            && IsGodotInstanceUsable(texture)
            && ReferenceEquals(currentTexture, texture));
}
