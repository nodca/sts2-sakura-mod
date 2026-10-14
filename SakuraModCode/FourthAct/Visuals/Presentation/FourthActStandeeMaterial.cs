using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

// A fourth-act standee's acting: the element shader on its sprite, driven by this node.
// Charge lights the rim while a move winds up, a hit flashes the body, a lasting buff keeps
// a state rim lit, and death dissolves it. Uniforms are written every frame from
// session-owned values (the shader never reads TIME), so no uniform is ever tweened.
//
// It also owns the monster's ambient layer from the catalog: either drawn behind the
// standee (`ambient_layer = "behind"`, at its centre or `ambient_anchor = "feet"`) or across the bottom of the room (`"room"`), faded
// by the monster's AmbientLevel and gone with the body.
internal sealed partial class FourthActStandeeMaterial : Node
{
    internal const string NodeName = "FourthActStandeeMaterial";
    internal const float DissolveDuration = 0.7f;

    private const float ChargeRate = 7f;
    private const float FlashDecay = 6.5f;

    private static readonly StringName TimeParam = "u_time";
    private static readonly StringName ChargeParam = "charge";
    private static readonly StringName FlashParam = "hit_flash";
    private static readonly StringName DissolveParam = "dissolve";
    private static readonly StringName AccentParam = "accent_color";
    private static readonly StringName FlashColorParam = "flash_color";
    private static readonly StringName EdgeParam = "dissolve_edge";
    private static readonly StringName SeedParam = "seed";
    private static readonly StringName StateParam = "state_glow";
    private static readonly StringName StateColorParam = "state_color";
    private const string AmbientLayerMeta = "ambient_layer";
    private const string AmbientAnchorMeta = "ambient_anchor";
    private const float StateRate = 3f;
    private const float AmbientRate = 1.5f;
    private const int AmbientAttempts = 120;

    private readonly List<ShaderMaterial> _materials = [];
    private float _time;
    private float _charge;
    private float _chargeTarget;
    private float _flash;
    private float _dissolve = -1f;
    private float _state;
    private FourthActMonsterTemplate? _monster;
    private NCreatureVisuals? _visuals;
    private FourthActFxId? _ambientId;
    private Node2D? _ambient;
    private readonly List<ShaderMaterial> _ambientMaterials = [];
    private int _ambientAttempts;

    private FourthActStandeeMaterial() => Name = NodeName;

    internal static void Attach(NCreatureVisuals visuals, FourthActMonsterTemplate monster)
    {
        var element = monster.Element;
        if (TestMode.IsOn)
            return;

        try
        {
            var body = visuals.GetNode<Node2D>("%Visuals");
            if (body.GetNodeOrNull<FourthActStandeeMaterial>(NodeName) is not null)
                return;

            var sprites = Sprites(body).ToList();
            if (sprites.Count == 0)
                return;

            var shader = PreloadManager.Cache.GetAsset<Shader>(FourthActElements.StandeeShaderPath(element));
            var profile = FourthActElements.Profile(element);
            var driver = new FourthActStandeeMaterial
            {
                _monster = monster,
                _visuals = visuals,
                _ambientId = FourthActMovePresentationCatalog.AmbientOf(monster.GetType())
            };
            var seed = (float)GD.RandRange(0.0, 100.0);
            foreach (var sprite in sprites)
            {
                var material = new ShaderMaterial { Shader = shader };
                material.SetShaderParameter(AccentParam, monster.StandeeAccent);
                material.SetShaderParameter(FlashColorParam, profile.Flash);
                material.SetShaderParameter(EdgeParam, profile.DissolveEdge);
                material.SetShaderParameter(SeedParam, seed);
                driver.Write(material);
                sprite.Material = material;
                driver._materials.Add(material);
            }

            body.AddChild(driver);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act standee material ({element}) failed open: {exception.Message}");
        }
    }

    internal static FourthActStandeeMaterial? TryGet(Creature creature) =>
        creature.GetCreatureNode() is { } node ? TryGet(node) : null;

    internal static FourthActStandeeMaterial? TryGet(NCreature node) =>
        node.Visuals.GetNodeOrNull<Node2D>("%Visuals")?.GetNodeOrNull<FourthActStandeeMaterial>(NodeName);

    internal void SetCharge(bool charging) => _chargeTarget = charging ? 1f : 0f;

    internal void Flash()
    {
        if (_dissolve < 0f)
            _flash = 1f;
    }

    internal void Dissolve()
    {
        _chargeTarget = 0f;
        _dissolve = Mathf.Max(_dissolve, 0f);
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        _time += step;
        _charge = Mathf.MoveToward(_charge, _chargeTarget, ChargeRate * step);
        _flash = Mathf.Max(0f, _flash - FlashDecay * step);
        if (_dissolve >= 0f)
            _dissolve = Mathf.Min(1f, _dissolve + step / DissolveDuration);
        _state = Mathf.MoveToward(_state, _dissolve >= 0f ? 0f : StateTarget(), StateRate * step);
        foreach (var material in _materials)
            Write(material);
        UpdateAmbient(step);
    }

    public override void _ExitTree()
    {
        if (_ambient is not null && IsInstanceValid(_ambient))
            _ambient.QueueFreeSafely();
        _ambient = null;
    }

    private float StateTarget()
    {
        try
        {
            return Mathf.Clamp(_monster?.StandeeStateGlow ?? 0f, 0f, 1f);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act standee state failed open: {exception.Message}");
            _monster = null;
            return 0f;
        }
    }

    private void Write(ShaderMaterial material)
    {
        material.SetShaderParameter(TimeParam, _time);
        material.SetShaderParameter(ChargeParam, _charge);
        material.SetShaderParameter(FlashParam, _flash);
        material.SetShaderParameter(DissolveParam, Mathf.Max(_dissolve, 0f));
        material.SetShaderParameter(StateParam, _state);
        if (_monster is not null)
            material.SetShaderParameter(StateColorParam, _monster.StandeeStateColor);
    }

    private void UpdateAmbient(float step)
    {
        if (_ambientId is not { } id)
            return;
        if (_ambient is null)
        {
            if (_dissolve < 0f && _ambientAttempts++ < AmbientAttempts)
                TryAttachAmbient(id);
            return;
        }
        if (!IsInstanceValid(_ambient))
        {
            _ambient = null;
            _ambientId = null;
            return;
        }

        var target = 0f;
        if (_dissolve < 0f)
        {
            try
            {
                target = Mathf.Clamp(_monster?.AmbientLevel ?? 1f, 0f, 1f);
            }
            catch (Exception exception)
            {
                MainFile.Logger.Warn($"Fourth-act ambient level failed open: {exception.Message}");
            }
        }

        var alpha = Mathf.MoveToward(_ambient.Modulate.A, target, AmbientRate * step);
        _ambient.Modulate = new Color(1f, 1f, 1f, alpha);
        _ambient.Visible = alpha > 0.001f;
        foreach (var material in _ambientMaterials)
            material.SetShaderParameter(TimeParam, _time);
        if (_dissolve >= 1f && alpha <= 0f)
        {
            _ambient.QueueFreeSafely();
            _ambient = null;
            _ambientId = null;
        }
    }

    private void TryAttachAmbient(FourthActFxId id)
    {
        if (_visuals is null || !_visuals.IsInsideTree() || NCombatRoom.Instance is not { } room)
            return;

        try
        {
            var root = FourthActFxPlayer.Instantiate(id);
            if (root is null)
            {
                _ambientId = null;
                return;
            }

            root.Modulate = new Color(1f, 1f, 1f, 0f);
            var layer = root.HasMeta(AmbientLayerMeta) ? root.GetMeta(AmbientLayerMeta).AsString() : "behind";
            if (layer == "room")
            {
                room.CombatVfxContainer.AddChildSafely(root);
                var size = room.GetViewportRect().Size;
                root.GlobalPosition = new Vector2(size.X * 0.5f, size.Y);
            }
            else
            {
                // `ambient_anchor = "feet"` sits the layer on the hitbox bottom (a pool, roots).
                var position = root.HasMeta(AmbientAnchorMeta) && root.GetMeta(AmbientAnchorMeta).AsString() == "feet"
                               && _monster is not null && room.GetCreatureNode(_monster.Creature) is { } creatureNode
                    ? creatureNode.GetBottomOfHitbox()
                    : _visuals.VfxSpawnPosition.GlobalPosition;
                _visuals.AddChild(root);
                _visuals.MoveChild(root, 0);
                root.GlobalPosition = position;
            }

            CollectMaterials(root);
            _ambient = root;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act ambient {id} failed open: {exception.Message}");
            _ambientId = null;
        }
    }

    private void CollectMaterials(Node node)
    {
        if (node is CanvasItem { Material: ShaderMaterial material })
            _ambientMaterials.Add(material);
        foreach (var child in node.GetChildren())
            CollectMaterials(child);
    }

    private static IEnumerable<Sprite2D> Sprites(Node root)
    {
        if (root is Sprite2D sprite)
            yield return sprite;
        foreach (var child in root.GetChildren())
        {
            foreach (var nested in Sprites(child))
                yield return nested;
        }
    }

    // Illusion projections dissolve through their own reveal; the hit shell and death of a
    // real fourth-act body belong here.
    internal static bool Owns(NCreature node) =>
        node.Entity.Monster is FourthActMonsterTemplate and not IllusionProjectionMonster;
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class FourthActStandeeHitPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (trigger == "Hit" && FourthActStandeeMaterial.Owns(__instance))
            FourthActStandeeMaterial.TryGet(__instance)?.Flash();
    }
}

// Native StartDeathAnim only plays a death sound and animation for Spine bodies; a static
// standee gets both here, inside the DeathAnimLengthOverride window the template declares.
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class FourthActStandeeDeathPatch
{
    private static void Prefix(NCreature __instance)
    {
        if (!FourthActStandeeMaterial.Owns(__instance) || __instance.DeathAnimationTask is { IsCompleted: false })
            return;

        FourthActStandeeMaterial.TryGet(__instance)?.Dissolve();
        if (__instance.Entity.Monster is { HasDeathSfx: true } monster)
            SfxCmd.PlayDeath(monster);
    }
}
