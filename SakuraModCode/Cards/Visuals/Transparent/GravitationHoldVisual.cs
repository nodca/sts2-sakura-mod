using System.Runtime.CompilerServices;
using Godot;
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

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Gravitation hold well: a cyan wireframe funnel at Sakura's feet that
/// stays for the Power's lifetime and overlays return / pile-pull tendrils.
/// Presentation only; return counts and energy stay on <c>GravitationHoldPower</c>.
/// </summary>
internal static partial class GravitationHoldVisual
{
    private const string RootName = "SakuraGravitationWell";
    private const int WellZIndex = 1;
    private const int OverlayZIndex = 3;
    private const int MaxConcurrentOverlays = 4;
    private const float FloorBias = 0.12f;
    private const float HandwardX = 16f;
    private const float HandwardY = 14f;
    private const float BloomDuration = 1.02f;
    private const float PersistAlpha = 0.22f;
    private const float PulseAlpha = 0.72f;
    private const float FadeDuration = 0.18f;
    private const float OverlayDuration = 0.34f;
    private const int RingCount = 5;
    private const int MeridianCount = 10;
    private const int EllipsePointCount = 48;
    private static readonly Color LineColor = new("3cefff");
    private static readonly Color GlowColor = new("3cefff") { A = 0.28f };
    private static readonly Color NodeColor = new("f5fffe");
    private static readonly Color SpeckColor = new("f4d56a");
    private static readonly ConditionalWeakTable<Creature, WellState> States = [];

    internal static void Mount(Creature creature)
    {
        if (!TryBeginPresentation(creature, out var container, out var creatureNode, out var anchor))
            return;

        if (States.TryGetValue(creature, out var existing))
        {
            existing.EnsureBloom();
            return;
        }

        WellRoot? root = null;
        try
        {
            var width = Mathf.Clamp(anchor.BodySize.X * 0.70f, 70f, 120f);
            root = BuildRoot(width);
            container.AddChildSafely(root);
            var state = new WellState(root, creature, creatureNode);
            States.Add(creature, state);
            state.Start();
            state.EnsureBloom();
        }
        catch (Exception exception)
        {
            States.Remove(creature);
            root?.QueueFreeSafely();
            MainFile.Logger.Error($"Could not mount Gravitation well: {exception}");
        }
    }

    internal static void Open(Creature creature) => Mount(creature);

    internal static void NotifyReturned(Creature creature, CardModel card)
    {
        if (card is null || !TryGetState(creature, out var state))
            return;

        state.Pulse();
        state.PlayReturnOverlay(card);
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

        state.Pulse();
        state.PlayPilePull(pileType, card);
    }

    private static bool TryBeginPresentation(
        Creature creature,
        out Control container,
        out NCreature creatureNode,
        out CelVfxGeometry.CasterAnchor anchor)
    {
        container = null!;
        creatureNode = null!;
        anchor = default;
        if (TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || NCombatRoom.Instance is not { CombatVfxContainer: { } currentContainer } room
            || !GodotObject.IsInstanceValid(currentContainer)
            || currentContainer.GetNodeOrNull<WellRoot>(RootName) is not null
            || room.GetCreatureNode(creature) is not { } node
            || CelVfxGeometry.ResolveCaster(node) is not { } resolved)
        {
            return false;
        }

        container = currentContainer;
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

    private static WellRoot BuildRoot(float width)
    {
        var root = new WellRoot
        {
            Name = RootName,
            ZAsRelative = false,
            ZIndex = WellZIndex,
            Modulate = Colors.Transparent,
            Scale = Vector2.One * 0.28f
        };
        WellBuilder.Build(root, width);
        return root;
    }

    private sealed partial class WellRoot : Node2D
    {
        internal Action<float>? Tick;

        public override void _Process(double delta) => Tick?.Invoke((float)delta);
    }

    private sealed class WellState : IDisposable
    {
        private readonly WellRoot _root;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState? _combatState;
        private Tween? _fadeTween;
        private Tween? _pulseTween;
        private bool _bloomStarted;
        private bool _disposed;
        private int _activeOverlays;

        internal WellState(WellRoot root, Creature creature, NCreature creatureNode)
        {
            _root = root;
            _creature = creature;
            _creatureNode = creatureNode;
            _combatState = creature.CombatState;
        }

        internal void Start()
        {
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            _root.Tick = Tick;
            FollowAnchor();
        }

        internal void EnsureBloom()
        {
            if (_disposed || _bloomStarted || !GodotObject.IsInstanceValid(_root))
                return;

            _bloomStarted = true;
            if (_fadeTween is { } current && current.IsValid())
                current.Kill();

            var bloom = _root.CreateTween().SetParallel();
            _fadeTween = bloom;
            bloom.TweenProperty(_root, "scale", Vector2.One, BloomDuration * 0.62f)
                .From(Vector2.One * 0.28f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            bloom.TweenProperty(_root, "modulate:a", 0.92f, BloomDuration * 0.38f)
                .From(0f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            bloom.TweenProperty(_root, "modulate:a", PersistAlpha, BloomDuration * 0.32f)
                .SetDelay(BloomDuration * 0.68f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        internal void Pulse()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root))
                return;

            if (_pulseTween is { } current && current.IsValid())
                current.Kill();

            var pulse = _root.CreateTween().SetParallel();
            _pulseTween = pulse;
            pulse.TweenProperty(_root, "modulate:a", PulseAlpha, 0.08f)
                .SetEase(Tween.EaseType.Out);
            pulse.TweenProperty(_root, "scale", Vector2.One * 1.06f, 0.10f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            pulse.TweenProperty(_root, "modulate:a", PersistAlpha, OverlayDuration * 0.72f)
                .SetDelay(0.10f)
                .SetEase(Tween.EaseType.In);
            pulse.TweenProperty(_root, "scale", Vector2.One, OverlayDuration * 0.70f)
                .SetDelay(0.10f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        internal void PlayReturnOverlay(CardModel card)
        {
            if (!TryBeginOverlay())
                return;

            var start = TryCardCenter(card) ?? OverlayOrigin() + Vector2.Down * 48f;
            PlayTendril(start, OverlayOrigin(), "ReturnTendril");
        }

        internal void PlayPilePull(PileType pileType, CardModel card)
        {
            if (!TryBeginOverlay())
                return;

            var end = OverlayOrigin();
            var start = NCombatRoom.Instance is { } room
                && PileExchangeVfx.TryGetPileCenter(room, pileType, out var pileCenter)
                ? pileCenter
                : TryCardCenter(card) ?? end + Vector2.Down * 48f;
            PlayTendril(start, end, "PileTendril");
        }

        private void PlayTendril(Vector2 globalStart, Vector2 globalEnd, string name)
        {
            if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree())
            {
                EndOverlay();
                return;
            }

            var start = _root.ToLocal(globalStart);
            var end = _root.ToLocal(globalEnd);
            var tendril = new Line2D
            {
                Name = name,
                Width = 1.7f,
                DefaultColor = LineColor,
                Antialiased = true,
                ZAsRelative = false,
                ZIndex = OverlayZIndex,
                Points = ArcPoints(start, end),
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            };
            var glow = new Line2D
            {
                Name = name + "Glow",
                Width = 4.4f,
                DefaultColor = GlowColor,
                Antialiased = true,
                ZAsRelative = false,
                ZIndex = OverlayZIndex,
                Points = tendril.Points,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            };
            try
            {
                _root.AddChild(glow);
                _root.AddChild(tendril);
                TaskHelper.RunSafely(AnimateOverlay(tendril, glow));
            }
            catch (Exception)
            {
                glow.QueueFreeSafely();
                tendril.QueueFreeSafely();
                EndOverlay();
            }
        }

        private async Task AnimateOverlay(Line2D tendril, Line2D glow)
        {
            try
            {
                if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree())
                    return;

                var tween = _root.CreateTween().SetParallel();
                tween.TweenProperty(tendril, "modulate:a", 0f, OverlayDuration * 0.42f)
                    .SetDelay(OverlayDuration * 0.58f)
                    .SetEase(Tween.EaseType.In);
                tween.TweenProperty(glow, "modulate:a", 0f, OverlayDuration * 0.42f)
                    .SetDelay(OverlayDuration * 0.58f)
                    .SetEase(Tween.EaseType.In);
                await _root.ToSignal(tween, Tween.SignalName.Finished);
            }
            catch (Exception)
            {
                // Overlay cleanup is fail-open; the well persist state is independent.
            }
            finally
            {
                tendril.QueueFreeSafely();
                glow.QueueFreeSafely();
                EndOverlay();
            }
        }

        private bool TryBeginOverlay()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root))
                return false;
            if (_activeOverlays >= MaxConcurrentOverlays)
                return false;
            _activeOverlays++;
            return true;
        }

        private void EndOverlay() => _activeOverlays = Math.Max(0, _activeOverlays - 1);

        private void Tick(float _)
        {
            if (_disposed)
                return;
            if (!SakuraModConfig.IsCardVfxEnabled() || !IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            FollowAnchor();
        }

        private void FollowAnchor()
        {
            if (CelVfxGeometry.ResolveCaster(_creatureNode) is not { } anchor)
                return;

            var feet = anchor.Floor.Lerp(anchor.BodyCenter, FloorBias);
            _root.GlobalPosition = feet + new Vector2(anchor.FacingSign * HandwardX, HandwardY);
        }

        private Vector2 OverlayOrigin() =>
            GodotObject.IsInstanceValid(_root) ? _root.GlobalPosition : Vector2.Zero;

        private static Vector2? TryCardCenter(CardModel card)
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

            if (_fadeTween is { } current && current.IsValid())
                current.Kill();
            _fadeTween = _root.CreateTween();
            _fadeTween.TweenProperty(_root, "modulate:a", 0f, FadeDuration)
                .SetEase(Tween.EaseType.In);
            _fadeTween.TweenCallback(Callable.From(DisposeAndFree));
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
            if (_fadeTween is { } fade && fade.IsValid())
                fade.Kill();
            if (_pulseTween is { } pulse && pulse.IsValid())
                pulse.Kill();
            _fadeTween = null;
            _pulseTween = null;
        }
    }

    private static Vector2[] ArcPoints(Vector2 start, Vector2 end)
    {
        const int pointCount = 18;
        var points = new Vector2[pointCount];
        var midpoint = (start + end) * 0.5f;
        var distance = start.DistanceTo(end);
        var control = midpoint + Vector2.Down * Mathf.Clamp(distance * 0.16f, 8f, 28f);
        for (var index = 0; index < points.Length; index++)
        {
            var t = index / (float)(pointCount - 1);
            var inverse = 1f - t;
            points[index] = start * inverse * inverse + control * 2f * inverse * t + end * t * t;
        }

        return points;
    }

    private static class WellBuilder
    {
        internal static void Build(Node2D root, float width)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = halfWidth * 0.30f;
            var vanishing = new Vector2(0f, halfWidth * 0.42f);
            var ringCenters = new Vector2[RingCount];
            var ringRadii = new Vector2[RingCount];

            for (var index = 0; index < RingCount; index++)
            {
                var t = index / (float)(RingCount - 1);
                var recede = t * t;
                var scale = Mathf.Lerp(1f, 0.14f, recede);
                ringCenters[index] = Vector2.Zero.Lerp(vanishing, recede * 0.92f);
                ringRadii[index] = new Vector2(halfWidth * scale, halfHeight * scale);
                AddEllipse(root, $"WellGlow{index}", ringCenters[index], ringRadii[index], GlowColor, 4.6f);
                AddEllipse(
                    root,
                    $"WellRing{index}",
                    ringCenters[index],
                    ringRadii[index],
                    LineColor,
                    index == 0 ? 2.1f : 1.45f);
            }

            for (var meridian = 0; meridian < MeridianCount; meridian++)
            {
                var angle = Mathf.Tau * meridian / MeridianCount;
                var points = new Vector2[RingCount];
                for (var index = 0; index < RingCount; index++)
                {
                    points[index] = ringCenters[index] + new Vector2(
                        Mathf.Cos(angle) * ringRadii[index].X,
                        Mathf.Sin(angle) * ringRadii[index].Y);
                }

                AddPolyline(root, $"WellMeridianGlow{meridian}", points, GlowColor, 3.8f);
                AddPolyline(root, $"WellMeridian{meridian}", points, LineColor, 1.35f);
                for (var index = 0; index < RingCount - 1; index++)
                    AddNode(root, points[index], index == 0 ? 3.1f : 2.15f);
            }

            AddNode(root, vanishing, 2.4f);
            AddSpeck(root, new Vector2(-halfWidth * 0.90f, 0f));
            AddSpeck(root, new Vector2(halfWidth * 0.90f, 0f));
        }

        private static void AddEllipse(
            Node2D root,
            string name,
            Vector2 center,
            Vector2 radii,
            Color color,
            float width)
        {
            var ring = new Line2D
            {
                Name = name,
                Width = width,
                DefaultColor = color,
                Closed = true,
                Antialiased = true,
                Position = center,
                BeginCapMode = Line2D.LineCapMode.Round,
                JointMode = Line2D.LineJointMode.Round,
                Points = EllipsePoints(radii.X, radii.Y)
            };
            root.AddChild(ring);
        }

        private static void AddPolyline(
            Node2D root,
            string name,
            Vector2[] points,
            Color color,
            float width)
        {
            var line = new Line2D
            {
                Name = name,
                Width = width,
                DefaultColor = color,
                Antialiased = true,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
                JointMode = Line2D.LineJointMode.Round,
                Points = points
            };
            root.AddChild(line);
        }

        private static void AddNode(Node2D root, Vector2 position, float size)
        {
            var node = new Polygon2D
            {
                Name = "WellNode",
                Color = NodeColor,
                Position = position,
                Polygon =
                [
                    new Vector2(0f, -size),
                    new Vector2(size * 0.55f, 0f),
                    new Vector2(0f, size),
                    new Vector2(-size * 0.55f, 0f)
                ]
            };
            root.AddChild(node);
        }

        private static void AddSpeck(Node2D root, Vector2 position)
        {
            var speck = new Polygon2D
            {
                Name = "WellSpeck",
                Color = SpeckColor,
                Position = position,
                Polygon =
                [
                    new Vector2(0f, -2.4f),
                    new Vector2(2.4f, 0f),
                    new Vector2(0f, 2.4f),
                    new Vector2(-2.4f, 0f)
                ]
            };
            root.AddChild(speck);
        }

        private static Vector2[] EllipsePoints(float radiusX, float radiusY)
        {
            var points = new Vector2[EllipsePointCount];
            for (var index = 0; index < points.Length; index++)
            {
                var angle = Mathf.Tau * index / points.Length;
                points[index] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }

            return points;
        }
    }
}
