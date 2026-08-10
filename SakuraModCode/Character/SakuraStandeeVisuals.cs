using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Godot;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStandeeVisuals
{
    private const float CombatVisualScale = 0.28f;
    private static readonly Vector2 CombatVisualSize = new(264f, 468f);
    private static readonly Vector2 CombatVisualTopLeft = new(-132f, -468f);
    private static readonly Vector2 CombatVisualCenter = new(0f, -234f);
    private static readonly Vector2 SakuraCombatVisualPosition = CombatVisualCenter + Vector2.Down * 16f;

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

    internal static NCreatureVisuals CreateWithLayeredIdle(string visualPath, string label)
    {
        var layout = StandardLayout(CombatVisualScale) with
        {
            VisualPosition = SakuraCombatVisualPosition
        };
        return Create(
            visualPath,
            label,
            layout,
            animate: true,
            playIdleMotion: false,
            attachLayeredIdle: true);
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

    internal static NCreatureVisuals CreateWithChibiLayeredIdle(
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
            animate: true,
            playIdleMotion: false,
            attachChibiLayeredIdle: true);

    private static NCreatureVisuals Create(
        string visualPath,
        string label,
        StandeeLayout layout,
        bool animate,
        bool playIdleMotion = true,
        bool attachLayeredIdle = false,
        bool attachChibiLayeredIdle = false)
    {
        try
        {
            var visuals = RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(visualPath);
            ApplyCombatVisualLayout(visuals, layout);
            var body = visuals.GetNode<Node2D>("%Visuals");
            var isActiveCombat = NCombatRoom.Instance?.Mode == CombatRoomMode.ActiveCombat;
            if (animate)
                SakuraStandeeActionController.Attach(
                    body,
                    layout.VisualPosition,
                    Vector2.One * layout.Scale,
                    layout.Bounds.Position.Y + layout.Bounds.Size.Y,
                    visualPath,
                    playIdleMotion);
            if (attachLayeredIdle && isActiveCombat)
                SakuraStandeeIdleController.Attach(body);
            if (attachChibiLayeredIdle && isActiveCombat)
                SakuraChibiStandeeIdleController.Attach(body);
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

    private static void MoveMarker(Node root, string nodePath, Vector2 position)
    {
        if (root.HasNode(nodePath))
            root.GetNode<Marker2D>(nodePath).Position = position;
    }
}
