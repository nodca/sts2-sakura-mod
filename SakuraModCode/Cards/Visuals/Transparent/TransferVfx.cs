using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Transfer: paired lock rings, a connecting thread, and a short
/// crossed afterimage. The session owns presentation only; the card owns PowerCmd.
/// </summary>
internal sealed class TransferVfx : CelVfxSession
{
    private const float LockDuration = 0.24f;
    private const float LockStagger = 0.055f;
    private const float CompressionDuration = 0.12f;
    private const float ExchangeDuration = 0.28f;
    private const float FadeDuration = 0.18f;
    private const int VfxZIndex = 3000;
    private const int RingPointCount = 48;
    private const float RingWidth = 2.4f;
    private const float ConnectionWidth = 1.7f;
    private static readonly Color CasterColor = new("9fffe0");
    private static readonly Color TargetColor = new("bde8ff");
    private static readonly Color ConnectionColor = new("d6fff3");
    private static readonly Color StrengthColor = new("f8b7c8");
    private static readonly Color DexterityColor = new("b9dcff");

    private readonly List<PairVisual> _pairs;
    private bool _faded;

    private TransferVfx(Node2D root, NCombatRoom room, IReadOnlyList<PairVisual> pairs)
        : base(root, room)
    {
        _pairs = pairs.ToList();
    }

    protected override IEnumerable<ShaderMaterial> Materials => [];

    protected override float MaximumLifetime => 5.5f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        IReadOnlyList<Creature> targets,
        bool enhanced,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Transfer",
            () => TryCreate(caster, targets),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope, enhanced)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<TransferVfx> scope, bool enhanced)
    {
        internal void Exchange(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("exchange", session => session.Exchange(target, enhanced));
        }
    }

    private static TransferVfx? TryCreate(
        Creature caster,
        IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0
            || !TryPrepare("Transfer", static () => true, out var room, out _, out _))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is not { } casterAnchor)
                return null;

            root = new Node2D
            {
                Name = "SakuraTransferVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex,
                Modulate = Colors.White
            };
            room.CombatVfxContainer.AddChildSafely(root);

            var casterPoint = casterAnchor.BodyCenter.Lerp(casterAnchor.Floor, 0.40f);
            var pairs = new List<PairVisual>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (!target.IsAlive)
                    continue;

                var geometry = CelVfxGeometry.Resolve(room, target, index, Budget);
                var pair = PairVisual.Create(target, root, casterPoint, geometry.Center, casterAnchor.BodySize, geometry.Size, index);
                pairs.Add(pair);
            }

            if (pairs.Count == 0)
            {
                root.QueueFreeSafely();
                return null;
            }

            var session = new TransferVfx(root, room, pairs);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Transfer VFX: {exception}");
            root?.QueueFreeSafely();
            return null;
        }
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 8f,
        VerticalPadding: 8f,
        MinWidth: 84f,
        MinHeight: 98f,
        MaxWidth: 260f,
        MaxHeight: 330f,
        FallbackWidth: 150f,
        FallbackHeight: 190f,
        FloorClearance: 4f,
        MaxViewportWidthFraction: 0.24f,
        MaxViewportHeightFraction: 0.44f);

    private async Task<bool> PlayPrelude(CardModel card, Creature caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        for (var index = 0; index < _pairs.Count; index++)
        {
            var pair = _pairs[index];
            var tween = Track(Root.CreateTween().SetParallel());
            var delay = index * LockStagger;
            tween.TweenProperty(pair.CasterRing, "modulate:a", 0.82f, LockDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(pair.TargetRing, "modulate:a", 0.82f, LockDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(pair.Connection, "modulate:a", 0.54f, LockDuration)
                .SetDelay(delay + LockDuration * 0.35f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(pair.CasterRing, "rotation", Mathf.Tau * 0.16f, LockDuration + 0.18f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Linear);
            tween.TweenProperty(pair.TargetRing, "rotation", -Mathf.Tau * 0.16f, LockDuration + 0.18f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Linear);
        }

        return await WaitActive(LockDuration + Math.Max(0, _pairs.Count - 1) * LockStagger);
    }

    private void Exchange(Creature target, bool enhanced)
    {
        if (!IsActive())
            return;

        var pair = _pairs.FirstOrDefault(candidate => ReferenceEquals(candidate.Target, target));
        if (pair is null || pair.Exchanged)
            return;

        pair.Exchanged = true;
        BeginHold();
        PlayExchangePulse(pair, enhanced);
    }

    private void PlayExchangePulse(PairVisual pair, bool enhanced)
    {
        var pulseCount = enhanced ? 2 : 1;
        var pulseDuration = CompressionDuration + ExchangeDuration;
        for (var pulseIndex = 0; pulseIndex < pulseCount; pulseIndex++)
        {
            var delay = pulseIndex * (pulseDuration + 0.05f);
            var pulse = Track(Root.CreateTween().SetParallel());
            pulse.TweenProperty(pair.CasterRing, "scale", Vector2.One * 0.68f, CompressionDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            pulse.TweenProperty(pair.TargetRing, "scale", Vector2.One * 0.68f, CompressionDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            pulse.TweenProperty(pair.Connection, "modulate:a", 0.10f, CompressionDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(pair.CasterRing, "scale", Vector2.One, ExchangeDuration)
                .SetDelay(delay + CompressionDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            pulse.TweenProperty(pair.TargetRing, "scale", Vector2.One, ExchangeDuration)
                .SetDelay(delay + CompressionDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            pulse.TweenProperty(pair.CrossA, "modulate:a", 0f, ExchangeDuration * 0.72f)
                .SetDelay(delay + CompressionDuration * 0.78f)
                .SetEase(Tween.EaseType.In);
            pulse.TweenProperty(pair.CrossB, "modulate:a", 0f, ExchangeDuration * 0.72f)
                .SetDelay(delay + CompressionDuration * 0.78f)
                .SetEase(Tween.EaseType.In);

            var crossScale = pulseIndex == 0 ? 1.0f : 1.24f;
            pulse.TweenProperty(pair.CrossA, "scale", Vector2.One * 0.14f, 0.01f)
                .SetDelay(delay + CompressionDuration * 0.70f);
            pulse.TweenProperty(pair.CrossA, "scale", Vector2.One * crossScale, ExchangeDuration * 0.32f)
                .SetDelay(delay + CompressionDuration * 0.72f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            pulse.TweenProperty(pair.CrossB, "scale", Vector2.One * 0.14f, 0.01f)
                .SetDelay(delay + CompressionDuration * 0.70f);
            pulse.TweenProperty(pair.CrossB, "scale", Vector2.One * crossScale, ExchangeDuration * 0.32f)
                .SetDelay(delay + CompressionDuration * 0.72f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);

            pulse.TweenProperty(pair.StrengthParticle, "position", pair.TargetCenter, ExchangeDuration)
                .SetDelay(delay + CompressionDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            pulse.TweenProperty(pair.DexterityParticle, "position", pair.CasterCenter, ExchangeDuration)
                .SetDelay(delay + CompressionDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            pulse.TweenProperty(pair.StrengthParticle, "modulate:a", 0f, ExchangeDuration * 0.52f)
                .SetDelay(delay + CompressionDuration + ExchangeDuration * 0.48f);
            pulse.TweenProperty(pair.DexterityParticle, "modulate:a", 0f, ExchangeDuration * 0.52f)
                .SetDelay(delay + CompressionDuration + ExchangeDuration * 0.48f);
            pulse.TweenProperty(pair.AfterCaster, "modulate:a", 0f, ExchangeDuration)
                .SetDelay(delay + CompressionDuration);
            pulse.TweenProperty(pair.AfterTarget, "modulate:a", 0f, ExchangeDuration)
                .SetDelay(delay + CompressionDuration);
        }

        pair.StrengthParticle.Position = pair.CasterCenter;
        pair.DexterityParticle.Position = pair.TargetCenter;
        pair.StrengthParticle.Modulate = Colors.White;
        pair.DexterityParticle.Modulate = Colors.White;
        pair.CrossA.Modulate = new Color(1f, 1f, 1f, 0.82f);
        pair.CrossB.Modulate = new Color(1f, 1f, 1f, 0.82f);
        pair.AfterCaster.Modulate = new Color(1f, 1f, 1f, 0.42f);
        pair.AfterTarget.Modulate = new Color(1f, 1f, 1f, 0.42f);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(0.34f);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static Line2D Ring(string name, Vector2 radii, Color color)
    {
        var ring = new Line2D
        {
            Name = name,
            Width = RingWidth,
            DefaultColor = color,
            Closed = true,
            Antialiased = true,
            Points = EllipsePoints(radii.X, radii.Y)
        };
        ring.Modulate = Colors.Transparent;
        return ring;
    }

    private static Line2D Afterimage(string name, Vector2 radii, Color color)
    {
        var ring = Ring(name, radii, color);
        ring.Width = 1.2f;
        ring.Modulate = Colors.Transparent;
        return ring;
    }

    private static Vector2[] EllipsePoints(float radiusX, float radiusY)
    {
        var points = new Vector2[RingPointCount];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = Mathf.Tau * index / points.Length;
            points[index] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private static Polygon2D Diamond(string name, Color color)
    {
        var particle = new Polygon2D
        {
            Name = name,
            Polygon =
            [
                new Vector2(0f, -5f),
                new Vector2(5f, 0f),
                new Vector2(0f, 5f),
                new Vector2(-5f, 0f)
            ],
            Color = color,
            Modulate = Colors.Transparent,
            Scale = Vector2.One * 0.15f
        };
        return particle;
    }

    private sealed class PairVisual
    {
        internal Creature Target { get; }
        internal Vector2 CasterCenter { get; }
        internal Vector2 TargetCenter { get; }
        internal Line2D CasterRing { get; }
        internal Line2D TargetRing { get; }
        internal Line2D Connection { get; }
        internal Line2D CrossA { get; }
        internal Line2D CrossB { get; }
        internal Line2D AfterCaster { get; }
        internal Line2D AfterTarget { get; }
        internal Polygon2D StrengthParticle { get; }
        internal Polygon2D DexterityParticle { get; }
        internal bool Exchanged { get; set; }

        private PairVisual(
            Creature target,
            Vector2 casterCenter,
            Vector2 targetCenter,
            Line2D casterRing,
            Line2D targetRing,
            Line2D connection,
            Line2D crossA,
            Line2D crossB,
            Line2D afterCaster,
            Line2D afterTarget,
            Polygon2D strengthParticle,
            Polygon2D dexterityParticle)
        {
            Target = target;
            CasterCenter = casterCenter;
            TargetCenter = targetCenter;
            CasterRing = casterRing;
            TargetRing = targetRing;
            Connection = connection;
            CrossA = crossA;
            CrossB = crossB;
            AfterCaster = afterCaster;
            AfterTarget = afterTarget;
            StrengthParticle = strengthParticle;
            DexterityParticle = dexterityParticle;
        }

        internal static PairVisual Create(
            Creature target,
            Node2D root,
            Vector2 casterCenter,
            Vector2 targetCenter,
            Vector2 casterSize,
            Vector2 targetSize,
            int index)
        {
            var casterRadii = new Vector2(
                Mathf.Clamp(casterSize.X * 0.38f, 30f, 66f),
                Mathf.Clamp(casterSize.Y * 0.11f, 18f, 34f));
            var targetRadii = new Vector2(
                Mathf.Clamp(targetSize.X * 0.46f, 30f, 82f),
                Mathf.Clamp(targetSize.Y * 0.12f, 18f, 38f));

            var casterRing = Ring($"CasterRing{index}", casterRadii, CasterColor);
            var targetRing = Ring($"TargetRing{index}", targetRadii, TargetColor);
            casterRing.Position = casterCenter;
            targetRing.Position = targetCenter;
            root.AddChild(casterRing);
            root.AddChild(targetRing);

            var connection = new Line2D
            {
                Name = $"Connection{index}",
                Width = ConnectionWidth,
                DefaultColor = ConnectionColor,
                Antialiased = true,
                Points = [casterCenter, targetCenter]
            };
            connection.Modulate = Colors.Transparent;
            root.AddChild(connection);

            var midpoint = casterCenter.Lerp(targetCenter, 0.5f);
            var crossA = new Line2D
            {
                Name = $"CrossA{index}",
                Width = 2.4f,
                DefaultColor = StrengthColor,
                Antialiased = true,
                Points = [new Vector2(-15f, -15f), new Vector2(15f, 15f)],
                Position = midpoint,
                Modulate = Colors.Transparent,
                Scale = Vector2.One * 0.14f
            };
            var crossB = new Line2D
            {
                Name = $"CrossB{index}",
                Width = 2.4f,
                DefaultColor = DexterityColor,
                Antialiased = true,
                Points = [new Vector2(-15f, 15f), new Vector2(15f, -15f)],
                Position = midpoint,
                Modulate = Colors.Transparent,
                Scale = Vector2.One * 0.14f
            };
            root.AddChild(crossA);
            root.AddChild(crossB);

            var afterCaster = Afterimage($"AfterCaster{index}", casterRadii * 1.08f, CasterColor);
            var afterTarget = Afterimage($"AfterTarget{index}", targetRadii * 1.08f, TargetColor);
            afterCaster.Position = casterCenter;
            afterTarget.Position = targetCenter;
            root.AddChild(afterCaster);
            root.AddChild(afterTarget);

            var strengthParticle = Diamond($"StrengthParticle{index}", StrengthColor);
            var dexterityParticle = Diamond($"DexterityParticle{index}", DexterityColor);
            strengthParticle.Position = casterCenter;
            dexterityParticle.Position = targetCenter;
            root.AddChild(strengthParticle);
            root.AddChild(dexterityParticle);

            return new PairVisual(
                target,
                casterCenter,
                targetCenter,
                casterRing,
                targetRing,
                connection,
                crossA,
                crossB,
                afterCaster,
                afterTarget,
                strengthParticle,
                dexterityParticle);
        }
    }
}
