using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SakuraMod.SakuraModCode.Character;

internal sealed partial class SakuraStandeeIdleController : Node2D
{
    internal const string NodeName = "SakuraStandeeIdleController";

    private const string RiggedScenePath =
        MainFile.ResPath + "/scenes/charui/sakura_standee_idle_rigged.tscn";
    private const string IdleAnimation = "idle_preview";
    private const string HurtAnimation = "hurt_preview";

    private static readonly string[] RequiredTexturedVisualPaths =
    [
        "CharacterRoot/BodyLower",
        "CharacterRoot/LegUnderlay",
        "CharacterRoot/BodyUnderlay",
        "CharacterRoot/SkirtMotionRoot/SkirtWaistRoot/SkirtSeamUnderlay",
        "CharacterRoot/SkirtMotionRoot/SkirtWaistRoot/SkirtAnchor",
        "CharacterRoot/SkirtMotionRoot/SkirtBackTrainRoot/SkirtBackTrainMesh",
        "CharacterRoot/SkirtMotionRoot/SkirtBackTrainRoot/SkirtBackTrainOuterTipMesh",
        "CharacterRoot/SkirtMotionRoot/SkirtBackTrainRoot/SkirtBackTrainInnerTipMesh",
        "CharacterRoot/SkirtMotionRoot/SkirtLeftFrontRoot/SkirtLeftFrontMesh",
        "CharacterRoot/SkirtMotionRoot/SkirtCenterFrontRoot/SkirtCenterFrontMesh",
        "CharacterRoot/SkirtMotionRoot/SkirtRightFrontRoot/SkirtRightFrontMesh",
        "CharacterRoot/UpperBodyMotionRoot/TorsoUnderlay",
        "CharacterRoot/UpperBodyMotionRoot/BreathRoot/TorsoBackRoot/TorsoBack",
        "CharacterRoot/UpperBodyMotionRoot/BreathRoot/TorsoFrontRoot/TorsoFront",
        "CharacterRoot/UpperBodyMotionRoot/BodyUpperRigid",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/HeadCoreUnderlay",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/BehindHairMesh",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/Face",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/FrontHairMesh",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/HeadAccessories",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/EyeHalfClosed",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/EyeClosed",
        "CharacterRoot/UpperBodyMotionRoot/HeadMotionRoot/DaimaoRoot/DaimaoMesh"
    ];
    private static readonly (string Path, string[] Animations)[] RequiredAnimationPlayers =
    [
        ("AnimationPlayer", [IdleAnimation, HurtAnimation]),
        ("BreathAnimationPlayer", ["breath_preview"]),
        ("MicroMotionAnimationPlayer", ["micro_preview"]),
        ("BlinkAnimationPlayer", ["blink_preview"])
    ];

    private readonly Sprite2D _body;
    private readonly AnimationPlayer _primaryAnimationPlayer;
    private readonly Vector2 _alignmentPosition;
    private readonly Color _originalSelfModulate;

    private SakuraStandeeIdleController(Sprite2D body, Node2D layers)
    {
        _body = body;
        _primaryAnimationPlayer = layers.GetNode<AnimationPlayer>("AnimationPlayer");
        _primaryAnimationPlayer.AnimationFinished += OnAnimationFinished;
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

    internal static SakuraStandeeIdleController? TryGet(NCreature node) =>
        node.Visuals.GetNodeOrNull<Node2D>("%Visuals")
            ?.GetNodeOrNull<SakuraStandeeIdleController>(NodeName);

    internal void PlayHurt()
    {
        if (!Visible || !IsInsideTree())
            return;

        _primaryAnimationPlayer.Play(HurtAnimation);
        _primaryAnimationPlayer.Seek(0.0, update: true);
    }

    public override void _Process(double delta) => SyncFlip();

    public override void _ExitTree()
    {
        _primaryAnimationPlayer.AnimationFinished -= OnAnimationFinished;
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
            MainFile.Logger.Error($"Could not load Sakura rigged idle scene {RiggedScenePath}.");
            return null;
        }

        Node2D layers;
        try
        {
            layers = scene.Instantiate<Node2D>();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Could not instantiate Sakura rigged idle scene {RiggedScenePath}: {ex}");
            return null;
        }

        foreach (var path in RequiredTexturedVisualPaths)
        {
            if (HasTexture(layers.GetNodeOrNull<Node>(path)))
                continue;

            layers.Free();
            MainFile.Logger.Error($"Sakura rigged idle scene is missing a textured visual at {path}.");
            return null;
        }

        foreach (var (path, animations) in RequiredAnimationPlayers)
        {
            if (layers.GetNodeOrNull<AnimationPlayer>(path) is not { } player
                || animations.Any(animation => !player.HasAnimation(animation)))
            {
                layers.Free();
                MainFile.Logger.Error($"Sakura rigged idle scene has an invalid animation player at {path}.");
                return null;
            }
        }

        if (layers.GetNodeOrNull<Marker2D>("CanvasOrigin") is null)
        {
            layers.Free();
            MainFile.Logger.Error("Sakura layered idle scene is missing its CanvasOrigin marker.");
            return null;
        }

        return layers;
    }

    private static bool HasTexture(Node? node) => node switch
    {
        Sprite2D { Texture: not null } => true,
        Polygon2D { Texture: not null } => true,
        _ => false
    };

    private void OnAnimationFinished(StringName animation)
    {
        if (animation.ToString() == HurtAnimation && Visible && IsInsideTree())
            _primaryAnimationPlayer.Play(IdleAnimation);
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

[HarmonyLib.HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class SakuraRiggedStandeeHitPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (trigger == "Hit"
            && __instance.Entity.Player is { } player
            && SakuraStarterCompatibility.IsKinomotoSakura(player))
        {
            SakuraStandeeIdleController.TryGet(__instance)?.PlayHurt();
        }
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
