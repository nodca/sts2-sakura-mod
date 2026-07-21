using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Scaffolding.Godot;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStandeeVisuals
{
    private const float CombatVisualScale = 0.28f;
    private const float StandeeEntryOffsetY = 16f;
    private const float StandeeEntryDuration = 0.42f;
    private const float StandeeIdleLift = 3.4f;
    private const float StandeeIdleHalfDuration = 1.85f;
    private const float StandeeIdleTilt = 0.005f;
    private static readonly Vector2 CombatVisualSize = new(264f, 468f);
    private static readonly Vector2 CombatVisualTopLeft = new(-132f, -468f);
    private static readonly Vector2 CombatVisualCenter = new(0f, -234f);

    private readonly record struct StandeeLayout(
        float Scale,
        Vector2 VisualPosition,
        Rect2 Bounds,
        Vector2 CenterPosition,
        Vector2 IntentPosition,
        Vector2 OrbPosition,
        Vector2 TalkPosition);

    public static NCreatureVisuals Create(string visualPath, string label) =>
        Create(visualPath, label, CombatVisualScale);

    public static NCreatureVisuals Create(string visualPath, string label, float combatVisualScale)
    {
        var layout = StandardLayout(combatVisualScale);
        return Create(visualPath, label, layout, animate: true);
    }

    internal static NCreatureVisuals CreateStatic(
        string visualPath,
        string label,
        float scale,
        Vector2 visualPosition,
        Rect2 bounds,
        Vector2 centerPosition,
        Vector2 intentPosition,
        Vector2 orbPosition,
        Vector2 talkPosition) =>
        Create(
            visualPath,
            label,
            new StandeeLayout(
                scale,
                visualPosition,
                bounds,
                centerPosition,
                intentPosition,
                orbPosition,
                talkPosition),
            animate: false);

    private static NCreatureVisuals Create(
        string visualPath,
        string label,
        StandeeLayout layout,
        bool animate)
    {
        try
        {
            var visuals = RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(visualPath);
            ApplyCombatVisualLayout(visuals, layout);
            var body = visuals.GetNode<Node2D>("%Visuals");
            if (animate)
                StartCombatStandeeAnimation(body, layout.VisualPosition);
            return visuals;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to create {label} combat visuals from {visualPath}: {ex}");
            throw;
        }
    }

    private static StandeeLayout StandardLayout(float scale) =>
        new(
            scale,
            CombatVisualCenter,
            new Rect2(CombatVisualTopLeft, CombatVisualSize),
            CombatVisualCenter,
            new Vector2(0f, CombatVisualTopLeft.Y - 40f),
            new Vector2(0f, -190f),
            new Vector2(0f, -420f));

    private static void ApplyCombatVisualLayout(NCreatureVisuals visuals, StandeeLayout layout)
    {
        var body = visuals.GetNode<Node2D>("%Visuals");
        body.Visible = true;
        body.Modulate = Colors.White;
        body.Position = layout.VisualPosition;
        body.Scale = Vector2.One * layout.Scale;
        body.Rotation = 0f;

        var bounds = visuals.GetNode<Control>("%Bounds");
        bounds.Position = layout.Bounds.Position;
        bounds.Size = layout.Bounds.Size;
        bounds.CustomMinimumSize = layout.Bounds.Size;
        bounds.PivotOffset = layout.Bounds.Size * 0.5f;

        MoveMarker(visuals, "%CenterPos", layout.CenterPosition);
        MoveMarker(visuals, "%IntentPos", layout.IntentPosition);
        MoveMarker(visuals, "%OrbPos", layout.OrbPosition);
        MoveMarker(visuals, "%TalkPos", layout.TalkPosition);
    }

    private static void StartCombatStandeeAnimation(Node2D body, Vector2 restPosition)
    {
        if (TestMode.IsOn)
            return;

        TaskHelper.RunSafely(StartCombatStandeeAnimationWhenReady(body, restPosition));
    }

    private static async Task StartCombatStandeeAnimationWhenReady(Node2D body, Vector2 restPosition)
    {
        if (!GodotObject.IsInstanceValid(body))
            return;

        if (!body.IsInsideTree())
            await body.ToSignal(body, Node.SignalName.TreeEntered);

        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        body.Position = restPosition + Vector2.Down * StandeeEntryOffsetY;
        body.Rotation = 0f;
        body.Modulate = new Color(1f, 1f, 1f, 0.86f);

        var entry = body.CreateTween().SetParallel();
        entry.TweenProperty(body, "position", restPosition, StandeeEntryDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        entry.TweenProperty(body, "modulate", Colors.White, StandeeEntryDuration * 0.8f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        entry.Chain().TweenCallback(Callable.From(() => StartCombatStandeeIdle(body, restPosition)));
    }

    private static void StartCombatStandeeIdle(Node2D body, Vector2 restPosition)
    {
        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        body.Position = restPosition;
        body.Rotation = 0f;
        body.Modulate = Colors.White;

        var liftedPosition = restPosition + Vector2.Up * StandeeIdleLift;
        var settledPosition = restPosition + Vector2.Down * (StandeeIdleLift * 0.35f);

        var idle = body.CreateTween().SetLoops();
        idle.TweenProperty(body, "position", liftedPosition, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "rotation", -StandeeIdleTilt, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);

        idle.TweenProperty(body, "position", settledPosition, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "rotation", StandeeIdleTilt * 0.55f, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private static void MoveMarker(Node root, string nodePath, Vector2 position)
    {
        if (root.HasNode(nodePath))
            root.GetNode<Marker2D>(nodePath).Position = position;
    }
}
