using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace SakuraMod.SakuraModCode.Ui;

internal static class HoverTipViewportCompatibility
{
    private const float ViewportMargin = 8f;
    private const string TextContainerName = "textHoverTipContainer";
    private const string CardContainerName = "cardHoverTipContainer";

    public static void ScheduleCorrection(NHoverTipSet? tipSet)
    {
        if (tipSet is null || !GodotObject.IsInstanceValid(tipSet))
            return;

        Callable.From(() => CorrectOverflow(tipSet)).CallDeferred();
    }

    private static void CorrectOverflow(NHoverTipSet tipSet)
    {
        if (!IsUsable(tipSet))
            return;

        var textContainer = tipSet.GetNodeOrNull<Control>(TextContainerName);
        var cardContainer = tipSet.GetNodeOrNull<Control>(CardContainerName);
        if (!IsUsable(textContainer) || !IsUsable(cardContainer))
            return;

        if (!TryGetContentBounds(textContainer, cardContainer, out var contentBounds))
            return;

        var viewport = tipSet.GetViewportRect();
        var viewportEnd = viewport.Position + viewport.Size;
        var contentEnd = contentBounds.Position + contentBounds.Size;
        var offset = HoverTipViewportGeometry.CalculateTranslation(
            viewport.Position.X,
            viewport.Position.Y,
            viewportEnd.X,
            viewportEnd.Y,
            contentBounds.Position.X,
            contentBounds.Position.Y,
            contentEnd.X,
            contentEnd.Y,
            ViewportMargin);
        if (offset.IsZero)
            return;

        var translation = new Vector2(offset.X, offset.Y);
        textContainer.GlobalPosition += translation;
        cardContainer.GlobalPosition += translation;
    }

    private static bool TryGetContentBounds(
        Control textContainer,
        Control cardContainer,
        out Rect2 bounds)
    {
        bounds = default;
        var hasBounds = false;

        AddVisibleChildBounds(textContainer, ref bounds, ref hasBounds);
        AddVisibleChildBounds(cardContainer, ref bounds, ref hasBounds);

        return hasBounds;
    }

    private static void AddVisibleChildBounds(
        Control container,
        ref Rect2 bounds,
        ref bool hasBounds)
    {
        foreach (var child in container.GetChildren())
        {
            if (child is not Control { Visible: true } control
                || !IsUsable(control)
                || control.Size.X <= 0f
                || control.Size.Y <= 0f)
            {
                continue;
            }

            AddBounds(control.GetGlobalRect(), ref bounds, ref hasBounds);
        }
    }

    private static void AddBounds(Rect2 candidate, ref Rect2 bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return;
        }

        var boundsEnd = bounds.Position + bounds.Size;
        var candidateEnd = candidate.Position + candidate.Size;
        var minimum = new Vector2(
            MathF.Min(bounds.Position.X, candidate.Position.X),
            MathF.Min(bounds.Position.Y, candidate.Position.Y));
        var maximum = new Vector2(
            MathF.Max(boundsEnd.X, candidateEnd.X),
            MathF.Max(boundsEnd.Y, candidateEnd.Y));
        bounds = new Rect2(minimum, maximum - minimum);
    }

    private static bool IsUsable(GodotObject? instance) =>
        instance is not null && GodotObject.IsInstanceValid(instance);
}

[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class HoverTipSetCreateAndShowViewportPatch
{
    [HarmonyPostfix]
    private static void Postfix(NHoverTipSet? __result) =>
        HoverTipViewportCompatibility.ScheduleCorrection(__result);
}
