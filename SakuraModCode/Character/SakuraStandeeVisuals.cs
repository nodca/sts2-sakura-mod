using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using BaseLib.Utils.NodeFactories;

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

    public static NCreatureVisuals Create(string visualPath, string label) =>
        Create(visualPath, label, CombatVisualScale);

    public static NCreatureVisuals Create(string visualPath, string label, float combatVisualScale)
    {
        try
        {
            var visuals = NodeFactory<NCreatureVisuals>.CreateFromResource(visualPath);
            ApplyCombatVisualLayout(visuals, combatVisualScale);
            var body = visuals.GetNode<Node2D>("%Visuals");
            StartCombatStandeeAnimation(body);
            return visuals;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to create {label} combat visuals from {visualPath}: {ex}");
            throw;
        }
    }

    private static void ApplyCombatVisualLayout(NCreatureVisuals visuals, float combatVisualScale)
    {
        var body = visuals.GetNode<Node2D>("%Visuals");
        body.Visible = true;
        body.Modulate = Colors.White;
        body.Position = CombatVisualCenter;
        body.Scale = Vector2.One * combatVisualScale;

        var bounds = visuals.GetNode<Control>("%Bounds");
        bounds.Position = CombatVisualTopLeft;
        bounds.Size = CombatVisualSize;
        bounds.CustomMinimumSize = CombatVisualSize;
        bounds.PivotOffset = CombatVisualSize * 0.5f;

        MoveMarker(visuals, "%CenterPos", CombatVisualCenter);
        MoveMarker(visuals, "%IntentPos", new Vector2(0f, CombatVisualTopLeft.Y - 40f));
        MoveMarker(visuals, "%OrbPos", new Vector2(0f, -190f));
        MoveMarker(visuals, "%TalkPos", new Vector2(0f, -420f));
    }

    private static void StartCombatStandeeAnimation(Node2D body)
    {
        if (TestMode.IsOn)
            return;

        TaskHelper.RunSafely(StartCombatStandeeAnimationWhenReady(body));
    }

    private static async Task StartCombatStandeeAnimationWhenReady(Node2D body)
    {
        if (!GodotObject.IsInstanceValid(body))
            return;

        if (!body.IsInsideTree())
            await body.ToSignal(body, Node.SignalName.TreeEntered);

        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        var restPosition = CombatVisualCenter;

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
