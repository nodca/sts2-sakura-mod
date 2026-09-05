using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal static class FourthActCombatFeedbackVisuals
{
    internal const float TransferDuration = 0.28f;
    internal const float DarknessPulseDuration = 0.24f;
    internal const float NightRegionTransitionDuration = 0.35f;
    private static readonly ConditionalWeakTable<Creature, PersistentSession> Sessions = new();

    public static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || creatureNode.Entity.Monster is not WindyMonster and not DarkMonster
            || !GodotObject.IsInstanceValid(creatureNode.Visuals.VfxSpawnPosition))
        {
            return;
        }

        if (Sessions.TryGetValue(creatureNode.Entity, out var previous))
        {
            previous.DisposeAndFree();
            Sessions.Remove(creatureNode.Entity);
        }

        var root = new Node2D
        {
            Name = "SakuraFourthActPersistentFeedback",
            ZIndex = 4,
            ZAsRelative = true
        };
        creatureNode.Visuals.VfxSpawnPosition.AddChildSafely(root);
        var session = new PersistentSession(creatureNode, root);
        Sessions.Add(creatureNode.Entity, session);
        session.Start();
    }

    public static void BeginWindWallInterception(Creature owner)
    {
        FourthActEnemyAudio.Play(FourthActAudioCue.WindWallBlock);
        if (Sessions.TryGetValue(owner, out var session))
            session.PlayWindWallImpact();
    }

    public static async Task PlayTransferAsync(Creature source, Creature target, Color color)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;
        var sourceNode = room.GetCreatureNode(source);
        var targetNode = room.GetCreatureNode(target);
        if (sourceNode is null || targetNode is null)
            return;

        var start = sourceNode.VfxSpawnPosition;
        var end = targetNode.VfxSpawnPosition;
        var root = new Node2D
        {
            Name = "SakuraFourthActTransferVfx",
            GlobalPosition = start,
            ZIndex = 20,
            ZAsRelative = false
        };
        room.CombatVfxContainer.AddChildSafely(root);
        var delta = end - start;
        var trail = new Line2D
        {
            Width = 5f,
            DefaultColor = new Color(color, 0.58f),
            Antialiased = true,
            Points = [Vector2.Zero, delta]
        };
        var spark = new Polygon2D
        {
            Color = color,
            Polygon = CirclePoints(9f, 10)
        };
        root.AddChild(trail);
        root.AddChild(spark);

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(spark, "position", delta, TransferDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(trail, "modulate:a", 0f, TransferDuration * 0.42f)
            .SetDelay(TransferDuration * 0.58f);
        tween.TweenProperty(spark, "modulate:a", 0f, TransferDuration * 0.3f)
            .SetDelay(TransferDuration * 0.7f);
        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    public static void PlayWindBindConversion(Creature windy, int unresolved, int attackBonus, int wallGain)
    {
        if (TestMode.IsOn || unresolved <= 0 || NCombatRoom.Instance?.GetCreatureNode(windy) is not { } node)
            return;

        var root = new Node2D
        {
            Name = "SakuraWindBindConversionVfx",
            Position = new Vector2(-38f, 0f),
            ZIndex = 10
        };
        node.Visuals.VfxSpawnPosition.AddChildSafely(root);
        var color = wallGain > 0
            ? new Color(0.48f, 0.94f, 0.86f, 0.9f)
            : new Color(0.86f, 0.9f, 0.98f, 0.88f);
        for (var index = 0; index < Math.Clamp(unresolved, 1, 8); index++)
        {
            var radius = 34f + index * 5f;
            root.AddChild(new Line2D
            {
                Width = 2.4f,
                DefaultColor = color,
                Antialiased = true,
                Closed = true,
                Points = ArcPoints(radius, 0f, Mathf.Tau, 26)
            });
        }
        root.Scale = Vector2.One * 0.72f;
        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * (1f + Math.Min(attackBonus, 12) * 0.01f), 0.36f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, 0.22f).SetDelay(0.18f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private sealed class PersistentSession : IDisposable
    {
        private readonly NCreature _creatureNode;
        private readonly Creature _creature;
        private readonly ICombatState _combatState;
        private readonly Node2D _root;
        private Node2D? _windWall;
        private Node2D? _wallImpact;
        private Node2D? _darkVeil;
        private Node2D? _veilRemnants;
        private TextureRect? _eternalNightOverlay;
        private ShaderMaterial? _eternalNightMaterial;
        private Node2D? _darkContour;
        private Tween? _idleTween;
        private Tween? _impactTween;
        private Tween? _nightTween;
        private bool _disposed;

        public PersistentSession(NCreature creatureNode, Node2D root)
        {
            _creatureNode = creatureNode;
            _creature = creatureNode.Entity;
            _combatState = _creature.CombatState
                ?? throw new InvalidOperationException("Fourth-act combat feedback requires a live combat state.");
            _root = root;
        }

        public void Start()
        {
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
            _creature.Died += OnDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            RefreshPersistent(animateNight: false);
        }

        private void OnPowerApplied(PowerModel power) => RefreshFor(power);
        private void OnPowerIncreased(PowerModel power, int _, bool __) => RefreshFor(power);
        private void OnPowerDecreased(PowerModel power, bool _) => RefreshFor(power, animateLoss: true);
        private void OnPowerRemoved(PowerModel power) => RefreshFor(power);

        private void RefreshFor(PowerModel power, bool animateLoss = false)
        {
            if (power is not WindWallPower and not DarknessPower)
                return;
            if (animateLoss && power is DarknessPower)
                PlayVeilThinning();
            RefreshPersistent();
        }

        public void RefreshPersistent(bool animateNight = true)
        {
            if (_disposed || !IsCurrent())
                return;

            var wallAmount = _creature.GetPower<WindWallPower>()?.Amount ?? 0;
            if (wallAmount > 0)
                EnsureWindWall(wallAmount);
            else
                FreeNode(ref _windWall);

            if (_creature.Monster is not DarkMonster dark)
                return;
            var darkness = _creature.GetPower<DarknessPower>()?.Amount ?? 1;
            EnsureDarkVeil(darkness);
            RefreshEternalNight(animateNight);
        }

        private void RefreshEternalNight(bool animate)
        {
            var target = DarkEnemyRules.ClampDarkness(
                _creature.GetPower<DarknessPower>()?.Amount ?? 1);
            if (!TryBindEternalNightOverlay())
                return;

            KillTween(ref _nightTween);
            var current = _eternalNightMaterial!
                .GetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName)
                .AsSingle();
            if (target == DarkEnemyRules.DarknessMaximum || current > DarkEnemyRules.DarknessMaximum - 1f)
                EnsureDarkContour();

            _eternalNightOverlay!.Visible = target > 0 || current > 0.001f;
            if (!animate
                || !_eternalNightOverlay.IsInsideTree()
                || Mathf.IsEqualApprox(current, target))
            {
                SetEternalNightProgress(target);
                CompleteEternalNightTransition(target);
                return;
            }

            var duration = NightRegionTransitionDuration * Mathf.Abs(target - current);
            _nightTween = _eternalNightOverlay.CreateTween();
            _nightTween.TweenMethod(
                    Callable.From<float>(SetEternalNightProgress),
                    current,
                    target,
                    duration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            _nightTween.TweenCallback(Callable.From(() => CompleteEternalNightTransition(target)));
        }

        private bool TryBindEternalNightOverlay()
        {
            if (_eternalNightOverlay is not null
                && GodotObject.IsInstanceValid(_eternalNightOverlay)
                && _eternalNightMaterial is not null
                && GodotObject.IsInstanceValid(_eternalNightMaterial))
            {
                return true;
            }

            _eternalNightOverlay = NCombatRoom.Instance?.FindChild(
                    FourthActCombatBackgrounds.EternalNightOverlayNodeName,
                    recursive: true,
                    owned: false) as TextureRect;
            _eternalNightMaterial = _eternalNightOverlay?.Material as ShaderMaterial;
            return _eternalNightMaterial is not null;
        }

        private void SetEternalNightProgress(float progress)
        {
            if (_eternalNightMaterial is not null && GodotObject.IsInstanceValid(_eternalNightMaterial))
            {
                _eternalNightMaterial.SetShaderParameter(
                    FourthActCombatBackgrounds.EternalNightProgressParameterName,
                    progress);
            }
            if (_darkContour is not null && GodotObject.IsInstanceValid(_darkContour))
            {
                _darkContour.Modulate = new Color(
                    1f,
                    1f,
                    1f,
                    Mathf.Clamp(progress - (DarkEnemyRules.DarknessMaximum - 1f), 0f, 1f));
            }
        }

        private void CompleteEternalNightTransition(float target)
        {
            if (target <= 0f && _eternalNightOverlay is not null
                && GodotObject.IsInstanceValid(_eternalNightOverlay))
            {
                _eternalNightOverlay.Visible = false;
            }
            if (target < DarkEnemyRules.DarknessMaximum)
                FreeNode(ref _darkContour);
        }

        private void EnsureDarkContour()
        {
            if (_darkContour is not null)
                return;

            _darkContour = new Node2D
            {
                Name = "EternalNightDarkContour",
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = -1
            };
            _darkContour.AddChild(new Line2D
            {
                Name = "Contour",
                Width = 4f,
                DefaultColor = new Color(0.56f, 0.48f, 0.84f, 0.62f),
                Antialiased = true,
                Closed = true,
                Points = EllipsePoints(118f, 202f, 52)
            });
            _root.AddChild(_darkContour);
        }

        public void PlayWindWallImpact()
        {
            if (_windWall is null || !_windWall.IsInsideTree())
                return;
            KillTween(ref _impactTween);
            FreeNode(ref _wallImpact);
            _wallImpact = new Node2D
            {
                Name = "WindWallImpact",
                Position = _windWall.Position,
                ZIndex = 2
            };
            var ripple = new Line2D
            {
                Name = "ImpactRipple",
                Position = new Vector2(-70f, 0f),
                Width = 5f,
                DefaultColor = new Color(0.76f, 1f, 0.96f, 0.96f),
                Antialiased = true,
                Closed = true,
                Points = ArcPoints(22f, 0f, Mathf.Tau, 20),
                Scale = Vector2.One * 0.45f
            };
            var barrierFlash = new Line2D
            {
                Name = "BarrierFlash",
                Width = 6f,
                DefaultColor = new Color(0.72f, 1f, 0.95f, 0.82f),
                Antialiased = true,
                Points = ArcPoints(118f, -1.18f, 1.18f, 28)
                    .Select(static point => new Vector2(point.X * 0.56f, point.Y))
                    .ToArray()
            };
            _wallImpact.AddChild(ripple);
            _wallImpact.AddChild(barrierFlash);
            _root.AddChild(_wallImpact);
            _impactTween = _wallImpact.CreateTween().SetParallel();
            _impactTween.TweenProperty(ripple, "scale", Vector2.One * 1.65f, 0.22f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            _impactTween.TweenProperty(ripple, "modulate:a", 0f, 0.18f).SetDelay(0.05f);
            _impactTween.TweenProperty(barrierFlash, "scale", new Vector2(0.92f, 1.04f), 0.08f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _impactTween.TweenProperty(barrierFlash, "modulate:a", 0f, 0.16f).SetDelay(0.08f);
            _impactTween.Chain().TweenProperty(barrierFlash, "scale", Vector2.One, 0.12f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            _impactTween.Chain().TweenCallback(Callable.From(_wallImpact.QueueFreeSafely));
        }

        private void EnsureWindWall(int amount)
        {
            if (_windWall is null)
            {
                _windWall = new Node2D { Name = "WindWallBarrier", Position = new Vector2(-72f, 4f) };
                var membrane = new Polygon2D
                {
                    Color = new Color(0.28f, 0.76f, 0.72f, 0.12f),
                    Polygon = BarrierPolygon()
                };
                var rim = new Line2D
                {
                    Width = 5f,
                    DefaultColor = new Color(0.57f, 0.96f, 0.9f, 0.54f),
                    Antialiased = true,
                    Points = ArcPoints(118f, -1.18f, 1.18f, 28).Select(static point => new Vector2(point.X * 0.56f, point.Y)).ToArray()
                };
                var circulation = new Line2D
                {
                    Width = 2.5f,
                    DefaultColor = new Color(0.78f, 1f, 0.96f, 0.28f),
                    Antialiased = true,
                    Points = ArcPoints(93f, -1.05f, 1.05f, 22).Select(static point => new Vector2(point.X * 0.54f, point.Y)).ToArray()
                };
                _windWall.AddChild(membrane);
                _windWall.AddChild(rim);
                _windWall.AddChild(circulation);
                _root.AddChild(_windWall);
                _idleTween = circulation.CreateTween().SetLoops();
                _idleTween.TweenProperty(circulation, "modulate:a", 0.5f, 1.2f)
                    .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
                _idleTween.TweenProperty(circulation, "modulate:a", 1f, 1.2f)
                    .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            }
            _windWall.Modulate = new Color(1f, 1f, 1f, 0.48f + Math.Min(amount, 4) * 0.08f);
        }

        private void EnsureDarkVeil(int amount)
        {
            if (_darkVeil is null)
            {
                _darkVeil = new Node2D { Name = "DarkVeilMembrane" };
                _darkVeil.AddChild(new Polygon2D
                {
                    Color = new Color(0.025f, 0.018f, 0.05f, 0.58f),
                    Polygon = EllipsePoints(122f, 148f, 42)
                });
                _darkVeil.AddChild(new Line2D
                {
                    Width = 5f,
                    DefaultColor = new Color(0.31f, 0.23f, 0.45f, 0.74f),
                    Antialiased = true,
                    Closed = true,
                    Points = EllipsePoints(122f, 148f, 42)
                });
                _root.AddChild(_darkVeil);
            }
            var ratio = Math.Clamp(amount, 1, DarkEnemyRules.DarknessMaximum)
                / (float)DarkEnemyRules.DarknessMaximum;
            _darkVeil.Modulate = new Color(1f, 1f, 1f, 0.42f + ratio * 0.48f);
            _darkVeil.Scale = Vector2.One * (0.96f + ratio * 0.04f);
        }

        private void EnsureVeilRemnants(int remainingSides)
        {
            if (_veilRemnants is null)
            {
                _veilRemnants = new Node2D { Name = "DarkVeilRemnants" };
                _veilRemnants.AddChild(CreateRemnant(-1f));
                _veilRemnants.AddChild(CreateRemnant(1f));
                _root.AddChild(_veilRemnants);
            }
            _veilRemnants.Modulate = new Color(1f, 1f, 1f, remainingSides == 1 ? 0.72f : 0.46f);
            _veilRemnants.Scale = remainingSides == 1 ? Vector2.One * 1.02f : Vector2.One;
        }

        private void PlayVeilThinning()
        {
            if (_darkVeil is null || !_darkVeil.IsInsideTree())
                return;
            var pulse = _darkVeil.CreateTween();
            pulse.TweenProperty(_darkVeil, "self_modulate", new Color(0.68f, 0.92f, 0.98f, 1f), 0.08f);
            pulse.TweenProperty(_darkVeil, "self_modulate", Colors.White, 0.16f);
        }

        private void PlaySeal()
        {
            if (_darkVeil is null || !_darkVeil.IsInsideTree())
                return;
            var tween = _darkVeil.CreateTween();
            tween.TweenProperty(_darkVeil, "scale", Vector2.One * 1.05f, 0.08f).From(Vector2.One * 0.88f);
            tween.TweenProperty(_darkVeil, "scale", Vector2.One, 0.16f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }

        private static Line2D CreateRemnant(float direction) => new()
        {
            Position = new Vector2(direction * 104f, 0f),
            Width = 12f,
            DefaultColor = new Color(0.08f, 0.05f, 0.13f, 0.72f),
            Antialiased = true,
            Points = [new(direction * 8f, -128f), new(direction * -4f, -56f), new(direction * 5f, 16f), new(direction * -8f, 126f)]
        };

        private bool IsCurrent() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creature.CombatState, _combatState);

        private void OnDied(Creature _) => DisposeAndFree();
        private void OnCombatEnded(CombatRoom _) => DisposeAndFree();
        private void OnTreeExiting() => Dispose();

        public void DisposeAndFree()
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
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerRemoved;
            _creature.Died -= OnDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _root.TreeExiting -= OnTreeExiting;
            KillTween(ref _idleTween);
            KillTween(ref _impactTween);
            KillTween(ref _nightTween);
            if (_eternalNightMaterial is not null && GodotObject.IsInstanceValid(_eternalNightMaterial))
                SetEternalNightProgress(0f);
            if (_eternalNightOverlay is not null && GodotObject.IsInstanceValid(_eternalNightOverlay))
                _eternalNightOverlay.Visible = false;
            FreeNode(ref _darkContour);
            FreeNode(ref _wallImpact);
            _eternalNightMaterial = null;
            _eternalNightOverlay = null;
        }

        private static void FreeNode(ref Node2D? node)
        {
            if (node is not null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                node.QueueFreeSafely();
            node = null;
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }
    }

    private static Vector2[] CirclePoints(float radius, int count) =>
        ArcPoints(radius, 0f, Mathf.Tau, count);

    private static Vector2[] ArcPoints(float radius, float start, float end, int count) =>
        Enumerable.Range(0, count)
            .Select(index => Mathf.Lerp(start, end, index / (float)(count - 1)))
            .Select(angle => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius)
            .ToArray();

    private static Vector2[] EllipsePoints(float radiusX, float radiusY, int count) =>
        ArcPoints(1f, 0f, Mathf.Tau, count)
            .Select(point => new Vector2(point.X * radiusX, point.Y * radiusY))
            .ToArray();

    private static Vector2[] BarrierPolygon()
    {
        var outside = ArcPoints(118f, -1.18f, 1.18f, 28)
            .Select(static point => new Vector2(point.X * 0.56f, point.Y));
        var inside = ArcPoints(82f, 1.18f, -1.18f, 24)
            .Select(static point => new Vector2(point.X * 0.46f, point.Y));
        return outside.Concat(inside).ToArray();
    }
}
