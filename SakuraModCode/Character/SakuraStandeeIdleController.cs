using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SakuraMod.SakuraModCode.Character;

internal sealed partial class SakuraStandeeIdleController : Node2D
{
    internal const string NodeName = "SakuraStandeeIdleController";

    private const string LayerScenePath =
        MainFile.ResPath + "/scenes/charui/sakura_standee_idle_layers.tscn";
    private static readonly string[] RequiredSpritePaths =
    [
        "CharacterRoot/BodyLower",
        "CharacterRoot/LegUnderlay",
        "CharacterRoot/BodyUnderlay",
        "CharacterRoot/SkirtMotionRoot/SkirtWaistRoot/SkirtSeamUnderlay",
        "CharacterRoot/SkirtMotionRoot/SkirtBackTrainRoot/SkirtBackTrain",
        "CharacterRoot/SkirtMotionRoot/SkirtLeftFrontRoot/SkirtLeftFront",
        "CharacterRoot/SkirtMotionRoot/SkirtCenterFrontRoot/SkirtCenterFront",
        "CharacterRoot/SkirtMotionRoot/SkirtRightFrontRoot/SkirtRightFront",
        "CharacterRoot/SkirtMotionRoot/SkirtWaistRoot/SkirtAnchor",
        "CharacterRoot/TorsoUnderlay",
        "CharacterRoot/BreathRoot/TorsoBackRoot/TorsoBack",
        "CharacterRoot/BreathRoot/TorsoFrontRoot/TorsoFront",
        "CharacterRoot/BodyUpperRigid",
        "CharacterRoot/EyeHalfClosed",
        "CharacterRoot/EyeClosed",
        "CharacterRoot/DaimaoRoot/Daimao"
    ];

    private readonly Sprite2D _body;
    private readonly Vector2 _alignmentPosition;
    private readonly Color _originalSelfModulate;

    private SakuraStandeeIdleController(Sprite2D body, Node2D layers)
    {
        _body = body;
        _alignmentPosition = GetAlignmentPosition(body, layers);
        _originalSelfModulate = body.SelfModulate;
        Name = NodeName;
        AddChild(layers);
        SyncFlip();
    }

    internal static bool Attach(Node2D body)
    {
        if (body.GetNodeOrNull<SakuraStandeeIdleController>(NodeName) is not null)
            return true;
        if (body is not Sprite2D baseSprite || baseSprite.Texture is null)
        {
            MainFile.Logger.Error("Could not find the Sakura battle standee Sprite2D for layered idle animation.");
            return false;
        }

        var layers = CreateLayers();
        if (layers is null)
            return false;

        var controller = new SakuraStandeeIdleController(baseSprite, layers);
        body.AddChild(controller);
        if (controller.GetParent() != body)
        {
            controller.Free();
            MainFile.Logger.Error("Could not attach the Sakura layered idle scene before battle tree entry.");
            return false;
        }

        var hidden = baseSprite.SelfModulate;
        hidden.A = 0f;
        baseSprite.SelfModulate = hidden;
        return true;
    }

    internal static void ShowStaticStandeeForDeath(Node2D body)
    {
        if (body.GetNodeOrNull<SakuraStandeeIdleController>(NodeName) is { } controller)
            controller.ShowStaticStandee();
    }

    public override void _Process(double delta) => SyncFlip();

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_body) && _body.IsInsideTree())
            _body.SelfModulate = _originalSelfModulate;
    }

    private void ShowStaticStandee()
    {
        Visible = false;
        if (GodotObject.IsInstanceValid(_body))
            _body.SelfModulate = _originalSelfModulate;
    }

    private static Node2D? CreateLayers()
    {
        var scene = ResourceLoader.Load<PackedScene>(
            LayerScenePath,
            null,
            ResourceLoader.CacheMode.Reuse);
        if (scene is null)
        {
            MainFile.Logger.Error($"Could not load Sakura layered idle scene {LayerScenePath}.");
            return null;
        }

        Node2D layers;
        try
        {
            layers = scene.Instantiate<Node2D>();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Could not instantiate Sakura layered idle scene {LayerScenePath}: {ex}");
            return null;
        }

        foreach (var path in RequiredSpritePaths)
        {
            if (layers.GetNodeOrNull<Sprite2D>(path) is { Texture: not null })
                continue;

            layers.Free();
            MainFile.Logger.Error($"Sakura layered idle scene is missing a textured Sprite2D at {path}.");
            return null;
        }

        if (layers.GetNodeOrNull<Marker2D>("CanvasOrigin") is null)
        {
            layers.Free();
            MainFile.Logger.Error("Sakura layered idle scene is missing its CanvasOrigin marker.");
            return null;
        }

        return layers;
    }

    private static Vector2 GetAlignmentPosition(Sprite2D body, Node2D layers)
    {
        var textureTopLeft = body.Offset;
        if (body.Centered)
            textureTopLeft -= body.Texture!.GetSize() * 0.5f;
        return textureTopLeft - layers.GetNode<Marker2D>("CanvasOrigin").Position;
    }

    private void SyncFlip()
    {
        var flip = new Vector2(_body.FlipH ? -1f : 1f, _body.FlipV ? -1f : 1f);
        Scale = flip;
        Position = _alignmentPosition * flip;
    }
}

[HarmonyLib.HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class SakuraStaticDeathStandeePatch
{
    private static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.Player is not { } player
            || !SakuraStarterCompatibility.IsKinomotoSakura(player)
            || __instance.Visuals.GetNodeOrNull<Node2D>("%Visuals") is not { } body)
            return;

        SakuraStandeeIdleController.ShowStaticStandeeForDeath(body);
    }
}
