using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal static class FourthActCombatFeedbackVisuals
{
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

    // A scene that flies from one creature to another (sleep powder); returns once it lands.
    public static async Task PlayTransferAsync(Creature source, Creature target, FourthActFxId fx)
    {
        if (FourthActFxPlayer.SpawnToward(fx, source, FourthActFxAnchor.Center, target) is { } arrival)
            await Cmd.CustomScaledWait(arrival * 0.5f, arrival);
    }

    // Unbroken Wind Bind streams back from each player who left some to Windy.
    public static void PlayWindBindConversion(Creature windy, IReadOnlyList<Creature> players)
    {
        foreach (var player in players)
            FourthActFxPlayer.SpawnToward(FourthActFxId.BindReturn, player, FourthActFxAnchor.Center, windy);
    }

    private sealed class PersistentSession : IDisposable
    {
        private readonly NCreature _creatureNode;
        private readonly Creature _creature;
        private readonly ICombatState _combatState;
        private readonly Node2D _root;
        private Node2D? _windWall;
        private Node2D? _wallImpact;
        private TextureRect? _eternalNightOverlay;
        private ShaderMaterial? _eternalNightMaterial;
        private TsukimineEclipseVisuals? _eclipse;
        private Node2D? _darkContour;
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
            // Darkness lost (Micro Light): the light pierces the veil.
            if (animateLoss && power is DarknessPower)
                FourthActFxPlayer.SpawnAt(FourthActFxId.VeilPierce, _creature);
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

            // The Dark's veil is its catalog ambient layer; the session owns the night and corona.
            if (_creature.Monster is not DarkMonster)
                return;
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
            if (_eternalNightMaterial?.Shader?.ResourcePath == FourthActCombatBackgrounds.EclipseShaderPath
                && NCombatRoom.Instance is { } room)
            {
                _eclipse = TsukimineEclipseVisuals.Attach(_eternalNightOverlay!, room);
            }
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

        // At full Darkness: a slow violet corona around The Dark (alpha follows the night).
        private void EnsureDarkContour()
        {
            if (_darkContour is not null)
                return;

            _darkContour = FourthActFxPlayer.Instantiate(FourthActFxId.DarkCorona);
            if (_darkContour is null)
                return;
            _darkContour.Name = "EternalNightDarkCorona";
            _darkContour.Modulate = new Color(1f, 1f, 1f, 0f);
            _darkContour.ZIndex = -1;
            _root.AddChild(_darkContour);
        }

        public void PlayWindWallImpact()
        {
            if (_windWall is null || !_windWall.IsInsideTree())
                return;
            KillTween(ref _impactTween);
            FreeNode(ref _wallImpact);
            _wallImpact = FourthActFxPlayer.Instantiate(FourthActFxId.WindWallDeflect);
            if (_wallImpact is null)
                return;
            _wallImpact.Name = "WindWallImpact";
            _wallImpact.Position = _windWall.Position;
            _wallImpact.ZIndex = 2;
            _root.AddChild(_wallImpact);
            _impactTween = _wallImpact.CreateTween();
            _impactTween.TweenInterval(FourthActFxPlayer.Meta(_wallImpact, FourthActFxPlayer.LifetimeMeta, 0.8f));
            _impactTween.TweenCallback(Callable.From(_wallImpact.QueueFreeSafely));
        }

        // A looping gale screen in front of Windy; more layers make it more solid.
        private void EnsureWindWall(int amount)
        {
            if (_windWall is null)
            {
                _windWall = FourthActFxPlayer.Instantiate(FourthActFxId.WindWallShield);
                if (_windWall is null)
                    return;
                _windWall.Name = "WindWallBarrier";
                _windWall.Position = new Vector2(-72f, 4f);
                _root.AddChild(_windWall);
            }
            _windWall.Modulate = new Color(1f, 1f, 1f, 0.5f + Math.Min(amount, 4) * 0.125f);
        }

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
            KillTween(ref _impactTween);
            KillTween(ref _nightTween);
            if (_eternalNightMaterial is not null && GodotObject.IsInstanceValid(_eternalNightMaterial))
                SetEternalNightProgress(0f);
            if (_eternalNightOverlay is not null && GodotObject.IsInstanceValid(_eternalNightOverlay))
                _eternalNightOverlay.Visible = _eclipse is not null;
            if (_eclipse is not null && GodotObject.IsInstanceValid(_eclipse))
                _eclipse.StopAmbient();
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
}
