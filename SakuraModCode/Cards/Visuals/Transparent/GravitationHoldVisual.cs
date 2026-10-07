using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Gravitation hold well: the anime's grid sinkhole, drawn as the floor under
/// Sakura denting into a recessed funnel. It stays for the Power's lifetime and
/// fires a tether that holds onto each returning card until it lands in hand.
/// Presentation only; return counts and energy stay on <c>GravitationHoldPower</c>.
/// </summary>
internal static partial class GravitationHoldVisual
{
    internal const string WellShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/gravitation_well.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [WellShaderPath];

    private const string RootName = "SakuraGravitationWell";
    private const string OverlayName = "SakuraGravitationTethers";
    private const int OverlayZIndex = 3;
    private const int MaxConcurrentTethers = 4;

    // Keep in step with scripts/render_gravitation_vfx.gd.
    internal const float WellAspect = 0.34f;
    internal const float RimRadiusFraction = 0.50f;
    internal const float MinRimRadius = 80f;
    internal const float MaxRimRadius = 135f;
    // The region leaves room outside the rim for the halo and the floor-drag band.
    internal const float RegionRimScale = 1.5f;
    internal const float RegionPadding = 32f;
    private const float HandwardX = 8f;

    private const float StepFrequency = 12f;
    private const float BloomOpenDuration = 5f / StepFrequency;
    private const float BloomHoldDuration = 0.42f;
    private const float BloomSettleDuration = 0.36f;
    private const float PersistIntensity = 0.55f;
    private const float PersistOpacity = 0.92f;
    private const float PulseDuration = 6f / StepFrequency;
    private const float FadeDuration = 0.18f;
    // One meridian slot every three seconds, sampled on the stepped clock.
    private const float SpinSlotsPerSecond = 0.33f;

    private const float TetherShootDuration = 2f / StepFrequency;
    private const float TetherMinLife = 0.30f;
    private const float TetherMaxLife = 1.10f;
    private const float TetherRestSpeed = 18f;
    private const float TetherRestDuration = 0.10f;
    private const float TetherFadeDuration = 0.18f;
    private const int TetherPointCount = 20;
    private const int TetherBeadCount = 3;
    private const float TetherBeadSpeed = 1.6f;

    private static readonly Color LineColor = new("dcffff");
    private static readonly Color GlowColor = new("3cefff") { A = 0.34f };
    private static readonly Color BeadColor = new("f5fffe");
    private static readonly ConditionalWeakTable<Creature, WellState> States = [];

    internal readonly record struct Layout(Vector2 Center, float RimRadius, Vector2 RegionSize)
    {
        internal static Layout From(CelVfxGeometry.CasterAnchor anchor)
        {
            var rim = Mathf.Clamp(anchor.BodySize.X * RimRadiusFraction, MinRimRadius, MaxRimRadius);
            var region = new Vector2(
                rim * 2f * RegionRimScale + RegionPadding,
                rim * WellAspect * 2f * RegionRimScale + RegionPadding);
            // The hitbox floor is the HP strip; the well opens on the ground the
            // player sees, under the standee's visible feet.
            var feet = new Vector2(
                anchor.BodyCenter.X + anchor.FacingSign * HandwardX,
                anchor.Floor.Y - SakuraElementSlotLayout.GroundVisualLift);
            return new Layout(feet, rim, region);
        }
    }

    internal static void Mount(Creature creature)
    {
        if (!TryBeginPresentation(creature, out var room, out var creatureNode, out var anchor))
            return;

        if (States.TryGetValue(creature, out var existing))
        {
            existing.EnsureBloom();
            return;
        }

        WellRoot? root = null;
        Node2D? overlay = null;
        try
        {
            var shader = PreloadManager.Cache.GetAsset<Shader>(WellShaderPath);
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("aspect", WellAspect);
            material.SetShaderParameter("open", 0f);
            material.SetShaderParameter("intensity", 1f);
            material.SetShaderParameter("pulse", 0f);
            material.SetShaderParameter("pulse_phase", 0f);
            material.SetShaderParameter("spin", 0f);
            material.SetShaderParameter("opacity", 1f);
            var rect = new ColorRect
            {
                Name = "Well",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root = new WellRoot
            {
                Name = RootName,
                ZAsRelative = true,
                ZIndex = 0
            };
            root.AddChild(rect);
            overlay = new Node2D
            {
                Name = OverlayName,
                ZAsRelative = false,
                ZIndex = OverlayZIndex
            };
            // Directly before the body, like the Freeze shell's slot but one step
            // earlier: Sakura stands on the rim, and her own health bar, Block and
            // Power icons draw over the well. The combat VFX containers sit above
            // or below every creature's UI, so neither can place it there. The
            // tethers go in front, where the cards they hold onto are.
            creatureNode.AddChildSafely(root);
            creatureNode.MoveChildSafely(root, creatureNode.Visuals.GetIndex());
            room.CombatVfxContainer.AddChildSafely(overlay);
            var state = new WellState(root, overlay, rect, material, creature, creatureNode);
            States.Add(creature, state);
            state.Start(anchor);
            state.EnsureBloom();
        }
        catch (Exception exception)
        {
            States.Remove(creature);
            root?.QueueFreeSafely();
            overlay?.QueueFreeSafely();
            MainFile.Logger.Error($"Could not mount Gravitation well: {exception}");
        }
    }

    internal static void Open(Creature creature) => Mount(creature);

    internal static void NotifyReturned(Creature creature, CardModel card)
    {
        if (card is null || !TryGetState(creature, out var state))
            return;

        state.Pulse();
        state.FireTether(card, null);
    }

    internal static void NotifyRemoved(Creature creature)
    {
        if (TestMode.IsOn)
            return;
        if (States.TryGetValue(creature, out var state))
            state.FadeAndDispose();
    }

    internal static void PullFromPile(Creature creature, PileType pileType, CardModel card)
    {
        if (card is null || !TryGetState(creature, out var state))
            return;

        Vector2? pileCenter = NCombatRoom.Instance is { } room
            && PileExchangeVfx.TryGetPileCenter(room, pileType, out var center)
                ? center
                : null;
        state.Pulse();
        state.FireTether(card, pileCenter);
    }

    private static bool TryBeginPresentation(
        Creature creature,
        out NCombatRoom room,
        out NCreature creatureNode,
        out CelVfxGeometry.CasterAnchor anchor)
    {
        room = null!;
        creatureNode = null!;
        anchor = default;
        if (TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || NCombatRoom.Instance is not { } currentRoom
            || currentRoom.CombatVfxContainer is not { } front
            || !GodotObject.IsInstanceValid(front)
            || currentRoom.GetCreatureNode(creature) is not { } node
            || !GodotObject.IsInstanceValid(node.Visuals)
            || node.GetNodeOrNull<WellRoot>(RootName) is not null
            || CelVfxGeometry.ResolveCaster(node) is not { } resolved)
        {
            return false;
        }

        room = currentRoom;
        creatureNode = node;
        anchor = resolved;
        return true;
    }

    private static bool TryGetState(Creature creature, out WellState state)
    {
        state = null!;
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled())
            return false;
        return States.TryGetValue(creature, out state!);
    }

    private sealed partial class WellRoot : Node2D
    {
        internal Action<float>? Tick;

        public override void _Process(double delta) => Tick?.Invoke((float)delta);
    }

    private sealed class WellState : IDisposable
    {
        private readonly WellRoot _root;
        private readonly Node2D _overlay;
        private readonly ColorRect _rect;
        private readonly ShaderMaterial _material;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState? _combatState;
        private readonly List<Tether> _tethers = [];
        private Tween? _bloomTween;
        private Tween? _pulseTween;
        private Tween? _fadeTween;
        private float _rimRadius = -1f;
        private float _elapsed;
        private float _lastSpin = -1f;
        private bool _bloomStarted;
        private bool _disposed;

        internal WellState(
            WellRoot root,
            Node2D overlay,
            ColorRect rect,
            ShaderMaterial material,
            Creature creature,
            NCreature creatureNode)
        {
            _root = root;
            _overlay = overlay;
            _rect = rect;
            _material = material;
            _creature = creature;
            _creatureNode = creatureNode;
            _combatState = creature.CombatState;
        }

        internal void Start(CelVfxGeometry.CasterAnchor anchor)
        {
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            _root.Tick = Tick;
            ApplyLayout(Layout.From(anchor));
        }

        internal void EnsureBloom()
        {
            if (_disposed || _bloomStarted || !GodotObject.IsInstanceValid(_root))
                return;

            _bloomStarted = true;
            KillTween(ref _fadeTween);
            var bloom = _root.CreateTween();
            _bloomTween = bloom;
            TweenFloat(bloom, "open", 0f, 1f, BloomOpenDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            bloom.TweenInterval(BloomHoldDuration);
            bloom.SetParallel();
            TweenFloat(bloom, "intensity", 1f, PersistIntensity, BloomSettleDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            TweenFloat(bloom, "opacity", 1f, PersistOpacity, BloomSettleDuration)
                .SetEase(Tween.EaseType.In);
        }

        internal void Pulse()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root))
                return;

            KillTween(ref _pulseTween);
            var pulse = _root.CreateTween().SetParallel();
            _pulseTween = pulse;
            // An inward ripple: the ring sinks from the lip to the throat while
            // the lift fades, stepped so it lands on drawn frames.
            TweenFloat(pulse, "pulse_phase", 0f, 1f, PulseDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            TweenFloat(pulse, "pulse", 1f, 0f, PulseDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        internal void FireTether(CardModel card, Vector2? fallbackStart)
        {
            if (_disposed || !GodotObject.IsInstanceValid(_overlay) || !_overlay.IsInsideTree())
                return;
            if (_tethers.Count >= MaxConcurrentTethers)
                return;

            var cardEnd = TryCardCenter(card) ?? fallbackStart;
            if (cardEnd is null)
                return;

            try
            {
                _tethers.Add(Tether.Create(_overlay, card, cardEnd.Value));
            }
            catch (Exception exception)
            {
                MainFile.Logger.Warn($"Could not fire Gravitation tether: {exception.Message}");
            }
        }

        private void Tick(float delta)
        {
            if (_disposed)
                return;
            if (!SakuraModConfig.IsCardVfxEnabled() || !IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            if (CelVfxGeometry.ResolveCaster(_creatureNode) is { } anchor)
                ApplyLayout(Layout.From(anchor));

            _elapsed += delta;
            var stepped = Mathf.Floor(_elapsed * StepFrequency) / StepFrequency;
            var spin = stepped * SpinSlotsPerSecond;
            if (!Mathf.IsEqualApprox(spin, _lastSpin))
            {
                _lastSpin = spin;
                _material.SetShaderParameter("spin", spin);
            }

            UpdateTethers(delta);
        }

        private void ApplyLayout(Layout layout)
        {
            // Global, as the Freeze shell does: the hitbox rect is global, and the
            // root follows it wherever the creature node is drawn.
            _root.GlobalPosition = layout.Center;
            if (Mathf.Abs(layout.RimRadius - _rimRadius) < 0.5f)
                return;

            _rimRadius = layout.RimRadius;
            _rect.Size = layout.RegionSize;
            _rect.Position = -layout.RegionSize * 0.5f;
            _material.SetShaderParameter("region_size", layout.RegionSize);
            _material.SetShaderParameter("rim_radius", layout.RimRadius);
        }

        private void UpdateTethers(float delta)
        {
            if (_tethers.Count == 0)
                return;

            var well = WellMouth();
            for (var index = _tethers.Count - 1; index >= 0; index--)
            {
                var tether = _tethers[index];
                if (!tether.Advance(delta, well, TryCardCenter(tether.Card)))
                {
                    tether.Free();
                    _tethers.RemoveAt(index);
                }
            }
        }

        // The tether leaves from the far half of the throat so it reads as coming
        // out of the hole, not off the rim.
        private Vector2 WellMouth()
        {
            var mouth = _root.ToGlobal(new Vector2(0f, -_rimRadius * WellAspect * 0.15f));
            return _overlay.ToLocal(mouth);
        }

        private Vector2? TryCardCenter(CardModel card)
        {
            if (NCard.FindOnTable(card) is not { } node
                || !GodotObject.IsInstanceValid(node)
                || !node.IsInsideTree())
            {
                return null;
            }

            var size = node.GetCurrentSize();
            var scaled = new Vector2(size.X * node.Scale.X, size.Y * node.Scale.Y);
            return node.GlobalPosition + scaled * 0.5f;
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_root.GetParent())
            && GodotObject.IsInstanceValid(_overlay)
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creature.CombatState, _combatState);

        internal void FadeAndDispose()
        {
            if (_disposed)
                return;
            if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree())
            {
                DisposeAndFree();
                return;
            }

            KillTween(ref _bloomTween);
            KillTween(ref _fadeTween);
            var fade = _root.CreateTween().SetParallel();
            _fadeTween = fade;
            var opacity = _material.GetShaderParameter("opacity").AsSingle();
            TweenFloat(fade, "opacity", opacity, 0f, FadeDuration)
                .SetEase(Tween.EaseType.In);
            TweenFloat(fade, "open", 1f, 0.6f, FadeDuration)
                .SetEase(Tween.EaseType.In);
            fade.TweenProperty(_overlay, "modulate:a", 0f, FadeDuration);
            fade.Chain().TweenCallback(Callable.From(DisposeAndFree));
        }

        /// <summary>
        /// Drives one shader uniform through a method tweener with explicit ends,
        /// so the tween never reads the material's current value.
        /// </summary>
        private MethodTweener TweenFloat(Tween tween, string name, float from, float to, float duration)
        {
            var material = _material;
            return tween.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter(name, value)),
                from,
                to,
                duration);
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }

        private void OnCreatureDied(Creature creature)
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
            if (GodotObject.IsInstanceValid(_overlay) && !_overlay.IsQueuedForDeletion())
                _overlay.QueueFreeSafely();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            States.Remove(_creature);
            _creature.Died -= OnCreatureDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _root.TreeExiting -= OnTreeExiting;
            _root.Tick = null;
            KillTween(ref _bloomTween);
            KillTween(ref _pulseTween);
            KillTween(ref _fadeTween);
            foreach (var tether in _tethers)
                tether.Free();
            _tethers.Clear();
            // The overlay lives in the front container and is not a child of the
            // root, so a tree exit on the root alone must still release it.
            if (GodotObject.IsInstanceValid(_overlay) && !_overlay.IsQueuedForDeletion())
                _overlay.QueueFreeSafely();
        }
    }

    /// <summary>
    /// One pull: a luminous line with grid-node beads, re-aimed every frame from
    /// the well's throat to the card it holds, until the card comes to rest.
    /// </summary>
    private sealed class Tether
    {
        private readonly Line2D _core;
        private readonly Line2D _glow;
        private readonly Polygon2D[] _beads;
        private readonly Vector2[] _points = new Vector2[TetherPointCount];
        private Vector2 _end;
        private float _age;
        private float _restTime;
        private float _fadeAge = -1f;

        internal CardModel Card { get; }

        private Tether(CardModel card, Line2D core, Line2D glow, Polygon2D[] beads, Vector2 end)
        {
            Card = card;
            _core = core;
            _glow = glow;
            _beads = beads;
            _end = end;
        }

        internal static Tether Create(Node2D overlay, CardModel card, Vector2 globalEnd)
        {
            var glow = new Line2D
            {
                Name = "TetherGlow",
                Width = 6.5f,
                DefaultColor = GlowColor,
                Antialiased = true,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
                JointMode = Line2D.LineJointMode.Round
            };
            var core = new Line2D
            {
                Name = "TetherCore",
                Width = 2.2f,
                DefaultColor = LineColor,
                Antialiased = true,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
                JointMode = Line2D.LineJointMode.Round
            };
            overlay.AddChild(glow);
            overlay.AddChild(core);
            var beads = new Polygon2D[TetherBeadCount];
            for (var index = 0; index < beads.Length; index++)
            {
                beads[index] = new Polygon2D
                {
                    Name = $"TetherBead{index}",
                    Color = BeadColor,
                    Polygon =
                    [
                        new Vector2(0f, -4.2f),
                        new Vector2(2.6f, 0f),
                        new Vector2(0f, 4.2f),
                        new Vector2(-2.6f, 0f)
                    ]
                };
                overlay.AddChild(beads[index]);
            }

            return new Tether(card, core, glow, beads, overlay.ToLocal(globalEnd));
        }

        /// <summary>Returns false once the tether has fully faded.</summary>
        internal bool Advance(float delta, Vector2 localStart, Vector2? globalCardCenter)
        {
            if (!GodotObject.IsInstanceValid(_core))
                return false;

            _age += delta;
            if (globalCardCenter is { } center)
            {
                var end = _core.GetParent<Node2D>().ToLocal(center);
                var speed = delta > 0f ? end.DistanceTo(_end) / delta : 0f;
                _restTime = speed < TetherRestSpeed ? _restTime + delta : 0f;
                _end = end;
            }
            else
            {
                _restTime += delta;
            }

            var landed = _age >= TetherMinLife && _restTime >= TetherRestDuration;
            if (_fadeAge < 0f && (landed || _age >= TetherMaxLife))
                _fadeAge = 0f;

            var alpha = 1f;
            if (_fadeAge >= 0f)
            {
                _fadeAge += delta;
                alpha = 1f - Mathf.Clamp(_fadeAge / TetherFadeDuration, 0f, 1f);
                if (alpha <= 0f)
                    return false;
            }

            // Shoots out over two drawn frames: half-way, then onto the card.
            var reach = _age < TetherShootDuration * 0.5f ? 0.5f : 1f;
            BuildPoints(localStart, _end, reach);
            _core.Points = _points;
            _glow.Points = _points;
            _core.Modulate = new Color(1f, 1f, 1f, alpha);
            _glow.Modulate = new Color(1f, 1f, 1f, alpha);

            for (var index = 0; index < _beads.Length; index++)
            {
                // Beads run from the card toward the throat: the pull, made visible.
                var phase = Mathf.PosMod(_age * TetherBeadSpeed + index / (float)_beads.Length, 1f);
                var t = (1f - phase) * reach;
                _beads[index].Position = Sample(localStart, _end, t);
                _beads[index].Modulate = new Color(1f, 1f, 1f, alpha * Mathf.Sin(phase * Mathf.Pi));
            }

            return true;
        }

        private void BuildPoints(Vector2 start, Vector2 end, float reach)
        {
            for (var index = 0; index < _points.Length; index++)
                _points[index] = Sample(start, end, reach * index / (float)(_points.Length - 1));
        }

        // A taut arc that sags toward the floor, like a line under load.
        private static Vector2 Sample(Vector2 start, Vector2 end, float t)
        {
            var midpoint = (start + end) * 0.5f;
            var sag = Mathf.Clamp(start.DistanceTo(end) * 0.12f, 10f, 46f);
            var control = midpoint + Vector2.Down * sag;
            var inverse = 1f - t;
            return start * inverse * inverse + control * 2f * inverse * t + end * t * t;
        }

        internal void Free()
        {
            _core.QueueFreeSafely();
            _glow.QueueFreeSafely();
            foreach (var bead in _beads)
                bead.QueueFreeSafely();
        }
    }
}
