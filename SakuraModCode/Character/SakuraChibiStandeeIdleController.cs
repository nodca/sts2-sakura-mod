using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SakuraMod.SakuraModCode.Character;

internal sealed partial class SakuraChibiStandeeIdleController : Node2D
{
    internal const string NodeName = "SakuraChibiStandeeIdleController";

    private const string RiggedScenePath =
        MainFile.ResPath + "/scenes/charui/sakura_chibi_combat_idle_rigged.tscn";
    private static readonly string[] RequiredTexturedVisualPaths =
    [
        "CharacterRoot/BodyMesh",
        "CharacterRoot/ChestAttachmentRoot/HeadRoot/Head",
        "CharacterRoot/ChestAttachmentRoot/HeldWandRoot/WandRoot/Wand",
        "CharacterRoot/ChestAttachmentRoot/HeldWandRoot/ScreenRightArmRoot/ScreenRightArm",
        "CharacterRoot/ChestAttachmentRoot/HeldWandRoot/ScreenLeftArmRoot/ScreenLeftArm"
    ];
    private static readonly (string Path, string Animation)[] RequiredAnimationPlayers =
    [
        ("PrimaryAnimationPlayer", "chibi_idle"),
        ("MicroAnimationPlayer", "chibi_micro")
    ];

    private readonly Sprite2D _body;
    private readonly Node2D _layers;
    private readonly Vector2 _alignmentPosition;
    private readonly Color _originalSelfModulate;

    private SakuraChibiStandeeIdleController(Sprite2D body, Node2D layers)
    {
        _body = body;
        _layers = layers;
        _alignmentPosition = GetAlignmentPosition(body, layers);
        _originalSelfModulate = body.SelfModulate;
        Name = NodeName;
        AddChild(layers);
        SyncFlip();
    }

    internal static bool Attach(Node2D body)
    {
        if (body.GetNodeOrNull<SakuraChibiStandeeIdleController>(NodeName) is not null)
            return true;
        if (body is not Sprite2D baseSprite || baseSprite.Texture is null)
        {
            MainFile.Logger.Error("Could not find the Sakura chibi standee Sprite2D for layered idle animation.");
            return false;
        }

        var layers = CreateLayers();
        if (layers is null)
            return false;

        var controller = new SakuraChibiStandeeIdleController(baseSprite, layers);
        body.AddChild(controller);
        if (controller.GetParent() != body)
        {
            controller.Free();
            MainFile.Logger.Error("Could not attach the Sakura chibi layered idle scene before battle tree entry.");
            return false;
        }

        var hidden = baseSprite.SelfModulate;
        hidden.A = 0f;
        baseSprite.SelfModulate = hidden;
        return true;
    }

    internal static void ShowStaticStandeeForDeath(Node2D body)
    {
        if (body.GetNodeOrNull<SakuraChibiStandeeIdleController>(NodeName) is { } controller)
            controller.ShowStaticStandee();
    }

    internal static SakuraChibiStandeeIdleController? TryGet(NCreature node) =>
        node.Visuals.GetNodeOrNull<Node2D>("%Visuals")
            ?.GetNodeOrNull<SakuraChibiStandeeIdleController>(NodeName);

    /// <summary>
    /// Which way the standee faces, as a sign a caster-side effect can multiply its
    /// own horizontal offset by.
    /// </summary>
    /// <remarks>
    /// Exposed as a sign rather than as this node's <see cref="Node2D.Scale"/>:
    /// <see cref="SyncFlip"/> publishes the flip by negating that scale, so an effect
    /// reading the transform would inherit the mirroring instead of deciding for
    /// itself which side to sit on.
    /// </remarks>
    internal float FacingSign =>
        GodotObject.IsInstanceValid(_body) && _body.FlipH ? -1f : 1f;

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
            RiggedScenePath,
            null,
            ResourceLoader.CacheMode.Reuse);
        if (scene is null)
        {
            MainFile.Logger.Error($"Could not load Sakura chibi rigged idle scene {RiggedScenePath}.");
            return null;
        }

        Node2D layers;
        try
        {
            layers = scene.Instantiate<Node2D>();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Could not instantiate Sakura chibi rigged idle scene {RiggedScenePath}: {ex}");
            return null;
        }

        foreach (var path in RequiredTexturedVisualPaths)
        {
            if (HasTexture(layers.GetNodeOrNull<Node>(path)))
                continue;

            layers.Free();
            MainFile.Logger.Error($"Sakura chibi rigged idle scene is missing a textured visual at {path}.");
            return null;
        }

        foreach (var (path, animation) in RequiredAnimationPlayers)
        {
            if (layers.GetNodeOrNull<AnimationPlayer>(path) is { } player
                && player.HasAnimation(animation))
                continue;

            layers.Free();
            MainFile.Logger.Error($"Sakura chibi rigged idle scene has an invalid animation player at {path}.");
            return null;
        }

        if (layers.GetNodeOrNull<Marker2D>("CanvasOrigin") is not null)
            return layers;

        layers.Free();
        MainFile.Logger.Error("Sakura chibi layered idle scene is missing its CanvasOrigin marker.");
        return null;
    }

    private static bool HasTexture(Node? node) => node switch
    {
        Sprite2D { Texture: not null } => true,
        Polygon2D { Texture: not null } => true,
        _ => false
    };

    private static Vector2 GetAlignmentPosition(Sprite2D body, Node2D layers)
    {
        var textureTopLeft = body.Offset;
        if (body.Centered)
            textureTopLeft -= body.Texture!.GetSize() * 0.5f;
        return textureTopLeft - layers.GetNode<Marker2D>("CanvasOrigin").Position;
    }

    internal void ForceSyncFlip() => SyncFlip();

    private void SyncFlip()
    {
        if (GodotObject.IsInstanceValid(_body) && _body.Scale.X < 0f)
        {
            _body.Scale = new Vector2(Mathf.Abs(_body.Scale.X), _body.Scale.Y);
            _body.FlipH = true;
        }
        var flip = new Vector2(_body.FlipH ? -1f : 1f, _body.FlipV ? -1f : 1f);
        Scale = flip;
        Position = _alignmentPosition * flip;
    }
}
