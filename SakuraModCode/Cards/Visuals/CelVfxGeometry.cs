using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

internal static class CelVfxGeometry
{
    internal readonly record struct TargetGeometry(Vector2 Center, Vector2 Size);

    internal readonly record struct GeometryBudget(
        float HorizontalPadding,
        float VerticalPadding,
        float MinWidth,
        float MinHeight,
        float MaxWidth,
        float MaxHeight,
        float FallbackWidth,
        float FallbackHeight,
        float FloorClearance,
        float MaxViewportWidthFraction = 0.34f,
        float MaxViewportHeightFraction = 0.58f)
    {
        internal void Validate()
        {
            if (!IsPositive(MinWidth)
                || !IsPositive(MinHeight)
                || MaxWidth < MinWidth
                || MaxHeight < MinHeight
                || !IsPositive(FallbackWidth)
                || !IsPositive(FallbackHeight)
                || !IsPositive(MaxViewportWidthFraction)
                || !IsPositive(MaxViewportHeightFraction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(GeometryBudget),
                    "Cel VFX geometry budgets require finite positive dimensions and ordered bounds.");
            }
        }
    }

    internal static TargetGeometry Resolve(
        NCombatRoom room,
        Creature creature,
        int fallbackIndex,
        GeometryBudget budget)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(creature);
        budget.Validate();

        var viewportRect = room.CombatVfxContainer.GetViewportRect();
        var viewportSize = viewportRect.Size;
        var node = room.GetCreatureNode(creature);
        if (node is not null
            && GodotObject.IsInstanceValid(node)
            && node.Hitbox is { } hitbox
            && GodotObject.IsInstanceValid(hitbox))
        {
            var rect = hitbox.GetGlobalRect();
            if (IsUsable(rect.Size))
            {
                var maxWidth = ViewportBound(
                    budget.MinWidth,
                    budget.MaxWidth,
                    viewportSize.X,
                    budget.MaxViewportWidthFraction);
                var maxHeight = ViewportBound(
                    budget.MinHeight,
                    budget.MaxHeight,
                    viewportSize.Y,
                    budget.MaxViewportHeightFraction);
                var size = new Vector2(
                    Math.Clamp(
                        rect.Size.X + budget.HorizontalPadding * 2f,
                        budget.MinWidth,
                        maxWidth),
                    Math.Clamp(
                        rect.Size.Y + budget.VerticalPadding * 2f,
                        budget.MinHeight,
                        maxHeight));
                var floor = node.GetBottomOfHitbox();
                var center = new Vector2(
                    rect.Position.X + rect.Size.X * 0.5f,
                    floor.Y - size.Y * 0.5f + budget.FloorClearance);
                return new TargetGeometry(center, size);
            }
        }

        var fallbackCenter = node?.VfxSpawnPosition
            ?? viewportRect.GetCenter() + new Vector2((fallbackIndex - 1) * 96f, 24f);
        return new TargetGeometry(
            fallbackCenter,
            new Vector2(budget.FallbackWidth, budget.FallbackHeight));
    }

    internal static float? ResolveCasterX(NCombatRoom room, Creature? caster)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (caster is null
            || room.GetCreatureNode(caster) is not { } node
            || !GodotObject.IsInstanceValid(node))
        {
            return null;
        }

        if (node.Hitbox is { } hitbox && GodotObject.IsInstanceValid(hitbox))
        {
            var rect = hitbox.GetGlobalRect();
            if (IsUsable(rect.Size))
                return rect.Position.X + rect.Size.X * 0.5f;
        }

        return node.VfxSpawnPosition.X;
    }

    internal static ShaderMaterial DuplicateMaterial(CanvasItem body, string label)
    {
        ArgumentNullException.ThrowIfNull(body);
        var source = body.Material as ShaderMaterial
            ?? throw new InvalidOperationException($"Cel VFX {label} requires a ShaderMaterial.");
        var material = source.Duplicate() as ShaderMaterial
            ?? throw new InvalidOperationException($"Could not duplicate Cel VFX {label} material.");
        body.Material = material;
        return material;
    }

    /// <summary>
    /// Displacement of a body under constant acceleration after <paramref name="time"/>
    /// seconds. Godot's Y axis points down, so a positive gravity pulls downward and
    /// an upward initial velocity produces a rise-then-fall arc without a second
    /// integrator.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="AddBallisticDebris"/> because that method also
    /// creates the node and owns a fade schedule. A consumer whose body already
    /// exists — a shader-driven rect, say — can reuse the trajectory alone instead
    /// of restating the formula, which is what keeps one parabola in the codebase.
    /// </remarks>
    internal static Vector2 BallisticOffset(Vector2 velocity, float gravity, float time) =>
        velocity * time + new Vector2(0f, 0.5f * gravity * time * time);

    internal static Polygon2D AddBallisticDebris(
        Tween tween,
        Node2D parent,
        IReadOnlyList<Vector2> points,
        Color color,
        Vector2 origin,
        Vector2 velocity,
        float duration,
        float delay = 0f,
        float gravity = 980f,
        float rotationRate = 2.1f,
        string name = "CelDebris",
        int zIndex = 3001)
    {
        ArgumentNullException.ThrowIfNull(tween);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
            throw new ArgumentException("Cel VFX debris requires at least three points.", nameof(points));
        if (!IsPositive(duration) || !float.IsFinite(delay) || delay < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration), "Debris timing must be finite and non-negative.");

        var piece = new Polygon2D
        {
            Name = name,
            Color = color,
            Polygon = points.ToArray(),
            Modulate = new Color(1f, 1f, 1f, 0f),
            ZAsRelative = false,
            ZIndex = zIndex
        };
        parent.AddChildSafely(piece);
        piece.GlobalPosition = origin;

        tween.TweenMethod(
                Callable.From<float>(time =>
                {
                    if (!GodotObject.IsInstanceValid(piece))
                        return;
                    piece.GlobalPosition = origin + BallisticOffset(velocity, gravity, time);
                    piece.Rotation = time * rotationRate;
                }),
                0f,
                duration,
                duration)
            .SetDelay(delay);
        tween.TweenProperty(piece, "modulate:a", 0.9f, duration * 0.14f)
            .SetDelay(delay);
        tween.TweenProperty(piece, "modulate:a", 0f, duration * 0.38f)
            .SetDelay(delay + duration * 0.62f);
        return piece;
    }

    private static float ViewportBound(
        float configuredMinimum,
        float configuredMaximum,
        float viewportAxis,
        float fraction)
    {
        if (!float.IsFinite(viewportAxis) || viewportAxis <= 0f)
            return configuredMaximum;
        return Math.Max(configuredMinimum, Math.Min(configuredMaximum, viewportAxis * fraction));
    }

    private static bool IsUsable(Vector2 size) =>
        IsPositive(size.X) && IsPositive(size.Y) && size.X > 1f && size.Y > 1f;

    private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
}
