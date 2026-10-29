using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraElementSlotLayout
{
    /// <summary>
    /// The hitbox floor sits below the standee's visible feet, level with the HP/Block
    /// strip. Anything that has to stand on the ground the player sees stands on this
    /// line instead, which also keeps it above that strip. One shared lift, so every
    /// ground-rooted mark agrees on where the ground is.
    /// </summary>
    internal const float GroundVisualLift = 44f;
    /// <summary>
    /// Gap between the hitbox's right edge and the earth wall's axis. The HP bar is as
    /// wide as the hitbox, so measuring from that edge keeps the wall beside the
    /// standee's legs at every outfit width: outfit hitboxes run from about 230 to 330
    /// pixels wide.
    /// </summary>
    private const float EarthEdgeClearance = 4f;

    /// <summary>
    /// Where the earth wall's slot sits, in the visuals root's local space. Rooted on
    /// the visual ground just past the hitbox's right edge, raised by
    /// <paramref name="earthContactInset"/> so the shader's contact line lands on it.
    /// Read once at mount and never mirrored with the standee: facing is republished
    /// every frame by the idle controllers' SyncFlip, so a mirrored slot would jump.
    /// </summary>
    internal static Vector2 EarthFromBody(
        Vector2 bodySize,
        float bodyCentreX,
        float floorY,
        float earthContactInset)
    {
        // A degenerate hitbox can report a floor level with or above the mount point,
        // which would fling the wall up through the standee; it then degrades to
        // mount height instead.
        var visualGroundY = Math.Max(floorY, 0f) - GroundVisualLift;
        var hitboxRight = bodyCentreX + Math.Max(bodySize.X, 100f) * 0.5f;
        return new(hitboxRight + EarthEdgeClearance, visualGroundY - earthContactInset);
    }
}

/// <summary>
/// One-shot element-state trigger beats on the stage. The state itself is shown only
/// on the Lasin-board HUD (<see cref="SakuraElementStateHud"/>); fire, wind and water
/// fire a beam from their HUD medallion, earth raises a wall in front of the
/// character. Every entry point is presentation-only: it never reads or gates the
/// gameplay amount or command it accompanies.
/// </summary>
internal static class SakuraElementStateVisuals
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/sakura_element_state_visuals.tscn";
    internal const string EarthShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_earthy.gdshader";
    internal const string CommonShaderIncludePath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_common.gdshaderinc";

    private const string RootName = "SakuraElementStateVisuals";
    private const string EarthSlotName = "EarthSlot";
    private const string EarthWallName = "EarthWall";
    private const float BeamDuration = 0.34f;
    private const float BeamWidth = 7f;
    private const float BeamHeadRadius = 8f;
    /// <summary>
    /// Long enough that the wall's dead hold — <c>WALL_HOLD_END - WALL_HOLD_START</c>
    /// of this span in <c>sakura_element_state_earthy.gdshader</c>, i.e. 0.26 of it —
    /// outlasts one <c>CEL_STEP_HZ</c> tick. A freeze shorter than the detail clock is
    /// not a freeze, and the hold is what makes the wall read as enduring.
    /// </summary>
    private const float EarthTriggerDuration = 0.38f;
    /// <summary>
    /// Where the earth shader roots the wall, as a fraction of the region height
    /// measured down from the rect's centre. Must stay equal to <c>CONTACT_Y</c> in
    /// <c>sakura_element_state_earthy.gdshader</c>.
    /// </summary>
    private const float EarthContactSurfaceFraction = 0.36f;
    private const int MaxTriggerTargets = 8;

    // Beam colours follow the HUD medallions: light core, element edge.
    private static readonly Color FireBeam = new(1f, 0.32f, 0.22f, 0.9f);
    private static readonly Color WindBeam = new(0.42f, 0.9f, 0.55f, 0.9f);
    private static readonly Color WaterBeam = new(0.4f, 0.68f, 1f, 0.9f);

    private static readonly ConditionalWeakTable<Creature, State> States = new();

    internal static IEnumerable<string> AssetPaths => [ScenePath, EarthShaderPath, CommonShaderIncludePath];

    internal static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || creatureNode.Entity.Player is not { Character: ClassicSakura } player
            || player.Creature.CombatState is not { } combatState
            || !GodotObject.IsInstanceValid(creatureNode.Visuals.VfxSpawnPosition)
            || States.TryGetValue(creatureNode.Entity, out _))
        {
            return;
        }

        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (scene is null)
        {
            MainFile.Logger.Error($"Could not load Sakura element state VFX scene: {ScenePath}");
            return;
        }

        Node2D? root = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = RootName;
            root.ZAsRelative = true;
            // Stay in the creature's draw order so combat UI and card grids cover the wall.
            root.ZIndex = 0;
            creatureNode.Visuals.VfxSpawnPosition.AddChildSafely(root);
            creatureNode.Visuals.VfxSpawnPosition.MoveChildSafely(root, 0);

            var state = new State(root, creatureNode, player, combatState);
            state.Start();
            States.Add(creatureNode.Entity, state);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not mount Sakura element state VFX: {exception}");
            root?.QueueFreeSafely();
        }
    }

    internal static void NotifyFireTriggered(Creature owner, IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0 || targets.Count > MaxTriggerTargets || !SakuraModConfig.IsCardVfxEnabled())
            return;

        SakuraElementStateHud.NotifyTriggered(owner.Player, SakuraElement.Fire);
        if (NCombatRoom.Instance is not { } room)
            return;

        var origin = ResolveBeamOrigin(room, owner, SakuraElement.Fire);
        foreach (var target in targets)
        {
            if (origin is { } start && room.GetCreatureNode(target) is { } targetNode)
                AddBeam(room, start, targetNode.Visuals.VfxSpawnPosition.GlobalPosition, FireBeam);
        }
    }

    /// <summary>
    /// Presentation-only signal that the wind state just spent its counter on a
    /// draw. Gameplay owns the counter and the draw command.
    /// </summary>
    internal static void NotifyWindTriggered(Creature owner)
    {
        if (!SakuraModConfig.IsCardVfxEnabled())
            return;

        SakuraElementStateHud.NotifyTriggered(owner.Player, SakuraElement.Wind);
        // Hand geometry is presentation-only; when it is missing the medallion flash
        // above still plays and the draw itself is untouched.
        if (NCombatRoom.Instance is { } room
            && ResolveBeamOrigin(room, owner, SakuraElement.Wind) is { } origin
            && NPlayerHand.Instance is { } hand
            && GodotObject.IsInstanceValid(hand.CardHolderContainer))
        {
            AddBeam(room, origin, hand.CardHolderContainer.GetGlobalRect().GetCenter(), WindBeam);
        }
    }

    /// <summary>
    /// Presentation-only signal that the earth state is about to pay out Block.
    /// Gameplay owns the Block and the command.
    /// </summary>
    /// <remarks>
    /// Earth has no counter to reach — every earth card played while the state is up
    /// triggers — so this routinely re-enters a wall that is still playing.
    /// <see cref="State.PlayEarthTrigger"/> owns that restart.
    /// </remarks>
    internal static void NotifyEarthTriggered(Creature owner)
    {
        if (!SakuraModConfig.IsCardVfxEnabled())
            return;

        SakuraElementStateHud.NotifyTriggered(owner.Player, SakuraElement.Earth);
        if (States.TryGetValue(owner, out var state))
            state.PlayEarthTrigger();
    }

    /// <summary>
    /// Presentation-only signal that the water state just spent its counter on
    /// energy. Gameplay owns the counter and the energy command.
    /// </summary>
    internal static void NotifyWaterTriggered(Creature owner)
    {
        if (!SakuraModConfig.IsCardVfxEnabled())
            return;

        SakuraElementStateHud.NotifyTriggered(owner.Player, SakuraElement.Water);
        if (NCombatRoom.Instance is { } room
            && ResolveBeamOrigin(room, owner, SakuraElement.Water) is { } origin
            && ResolveEnergyTarget(room) is { } target)
        {
            AddBeam(room, origin, target, WaterBeam);
        }
    }

    /// <summary>
    /// The local player's beams leave their HUD medallion. A teammate has no HUD on
    /// this machine, so theirs leave the character instead.
    /// </summary>
    private static Vector2? ResolveBeamOrigin(NCombatRoom room, Creature owner, SakuraElement element)
    {
        if (SakuraElementStateHud.TryGetMedallionCenter(owner.Player, element, out var medallion))
            return medallion;

        return room.GetCreatureNode(owner) is { } node && GodotObject.IsInstanceValid(node.Visuals.VfxSpawnPosition)
            ? node.Visuals.VfxSpawnPosition.GlobalPosition
            : null;
    }

    /// <summary>
    /// Locates the live energy counter purely as a presentation anchor. A miss only
    /// costs the beam; the energy gain is never gated on UI geometry.
    /// </summary>
    private static Vector2? ResolveEnergyTarget(NCombatRoom room)
    {
        if (room.Ui is not { } ui
            || ui.EnergyCounterContainer is not { } container
            || !GodotObject.IsInstanceValid(container))
        {
            return null;
        }

        var counter = container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault();
        return counter is not null && GodotObject.IsInstanceValid(counter)
            ? counter.GetGlobalRect().GetCenter()
            : container.GetGlobalRect().GetCenter();
    }

    /// <summary>
    /// The Lasin board's pointing light: a straight beam that shoots from the
    /// medallion to the target with a bright head, then fades from its tail.
    /// </summary>
    private static void AddBeam(NCombatRoom room, Vector2 origin, Vector2 target, Color colour)
    {
        var root = new Node2D
        {
            Name = "SakuraElementBeam",
            GlobalPosition = origin,
            ZIndex = 20,
            ZAsRelative = false
        };
        room.CombatVfxContainer.AddChildSafely(root);
        var delta = target - origin;
        var glow = new Line2D
        {
            Width = BeamWidth * 2.4f,
            DefaultColor = colour with { A = 0.32f },
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Points = [Vector2.Zero, Vector2.Zero]
        };
        var core = new Line2D
        {
            Width = BeamWidth,
            Gradient = new Gradient
            {
                Offsets = [0f, 1f],
                Colors = [colour with { A = 0.2f }, colour.Lerp(Colors.White, 0.55f)]
            },
            Antialiased = true,
            EndCapMode = Line2D.LineCapMode.Round,
            Points = [Vector2.Zero, Vector2.Zero]
        };
        var head = new Polygon2D
        {
            Color = colour.Lerp(Colors.White, 0.7f),
            Polygon = CirclePoints(BeamHeadRadius, 12)
        };
        root.AddChild(glow);
        root.AddChild(core);
        root.AddChild(head);

        var tween = root.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(progress =>
        {
            var tip = delta * progress;
            glow.Points = [Vector2.Zero, tip];
            core.Points = [Vector2.Zero, tip];
            head.Position = tip;
        }),
            0f, 1f, BeamDuration * 0.55f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(head, "scale", Vector2.One * 1.6f, BeamDuration * 0.2f)
            .SetDelay(BeamDuration * 0.55f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(root, "modulate:a", 0f, BeamDuration * 0.45f)
            .SetDelay(BeamDuration * 0.55f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private sealed class State : IDisposable
    {
        private readonly Node2D _root;
        private readonly NCreature _creatureNode;
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly ICombatState _combatState;
        private readonly Node2D _earthSlot;
        private readonly ColorRect _wall;
        private readonly ShaderMaterial _earthMaterial;
        /// <summary>
        /// Held as a field: consecutive earth cards re-enter the trigger while the wall
        /// is still forming, and without a handle to kill, several tweens would drive one
        /// <c>trigger_progress</c> at once and drag the wall back to its start.
        /// </summary>
        private Tween? _earthTriggerTween;
        private bool _disposed;

        internal State(Node2D root, NCreature creatureNode, Player player, ICombatState combatState)
        {
            _root = root;
            _creatureNode = creatureNode;
            _player = player;
            _creature = player.Creature;
            _combatState = combatState;
            _earthSlot = root.GetNode<Node2D>(EarthSlotName);
            _wall = root.GetNode<ColorRect>($"{EarthSlotName}/{EarthWallName}");
            _earthMaterial = _wall.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura earth wall VFX requires a ShaderMaterial.");
            _wall.Material = _earthMaterial;
        }

        internal void Start()
        {
            var geometry = CelVfxGeometry.ResolveCaster(_creatureNode)
                ?? throw new InvalidOperationException("Sakura element state VFX requires caster geometry.");
            // The anchor's floor is global; slot positions are local to this root.
            _earthSlot.Position = SakuraElementSlotLayout.EarthFromBody(
                geometry.BodySize,
                _root.ToLocal(geometry.BodyCenter).X,
                _root.ToLocal(geometry.Floor).Y,
                _wall.Size.Y * EarthContactSurfaceFraction);
            _earthMaterial.SetShaderParameter("region_size", _wall.Size);
            _earthMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            // Written once before any tween drives it: a ShaderMaterial reports a
            // never-set uniform as Nil, and tweening from Nil crashes.
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("facing", ResolveFacingSign());

            _creature.Died += OnDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
        }

        /// <summary>
        /// Which way the standee faces, for the wall to form toward. Read at the start
        /// of a beat, never per frame: SyncFlip republishes the sign continuously, so
        /// reading it live would let the wall mirror itself mid-formation.
        /// </summary>
        private float ResolveFacingSign() =>
            CelVfxGeometry.ResolveCaster(_creatureNode) is { } anchor ? anchor.FacingSign : 1f;

        internal void PlayEarthTrigger()
        {
            if (_disposed || !IsCurrentMount())
                return;

            KillTween(ref _earthTriggerTween);
            _earthMaterial.SetShaderParameter("facing", ResolveFacingSign());
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _wall.Visible = true;
            _earthTriggerTween = _root.CreateTween();
            // Linear: rise, snap, hold and shatter are fixed spans of trigger_progress in
            // the shader, and the dead hold is exactly what an ease would smear.
            _earthTriggerTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("trigger_progress", value)),
                0f, 1f, EarthTriggerDuration);
            // Settling at 0 and hidden is what makes a kill safe at any point: the wall
            // can never be left standing half-formed.
            _earthTriggerTween.TweenCallback(Callable.From(() =>
            {
                _earthMaterial.SetShaderParameter("trigger_progress", 0f);
                _wall.Visible = false;
                _earthTriggerTween = null;
            }));
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creatureNode.Entity.Player, _player)
            && ReferenceEquals(_creature.CombatState, _combatState);

        private void OnDied(Creature creature)
        {
            if (ReferenceEquals(creature, _creature))
                DisposeAndFree();
        }

        private void OnCombatEnded(CombatRoom _) => DisposeAndFree();
        private void OnTreeExiting() => Dispose();

        private void DisposeAndFree()
        {
            Dispose();
            if (GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            States.Remove(_creature);
            _creature.Died -= OnDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _root.TreeExiting -= OnTreeExiting;
            KillTween(ref _earthTriggerTween);
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }
    }

    private static Vector2[] CirclePoints(float radius, int count)
    {
        var points = new Vector2[count];
        for (var index = 0; index < count; index++)
        {
            var angle = Mathf.Tau * index / count;
            points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }
}
