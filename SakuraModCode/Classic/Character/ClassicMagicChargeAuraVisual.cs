using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Character;

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class ClassicMagicChargeAuraVisualPatch
{
    [HarmonyPostfix]
    private static void ReadyPostfix(NCreature __instance) =>
        ClassicMagicChargeAuraVisual.Mount(__instance);
}

internal static class ClassicMagicChargeAuraVisual
{
    private const string AuraNodeName = "ClassicMagicChargeAura";
    private const string BlurRingPath = "vfx/classic_magic_charge_blur_ring.png";
    private const string ImportPathPrefix = "path=\"";
    private const float MaxAlpha = 0.6f;
    private const float FadeDuration = 0.3f;
    private const float VisibilityEpsilon = 0.001f;
    private const float PulseBaseScale = 3.5f;
    private const float PulseAmplitude = 0.035f;
    private const float PulseSpeed = 2.2f;

    private static readonly Vector2 AuraOffset = new(-10f, -8f);
    private static readonly double PulseQuarterDuration = Math.PI / (PulseSpeed * 2.0);
    private static readonly CanvasItemMaterial AdditiveMaterial = new()
    {
        BlendMode = CanvasItemMaterial.BlendModeEnum.Add
    };

    public static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || creatureNode.Entity.Player is not { Character: ClassicSakura } player
            || player.Creature.CombatState is not { } combatState
            || !GodotObject.IsInstanceValid(creatureNode.Visuals.VfxSpawnPosition))
        {
            return;
        }

        var auraAnchor = creatureNode.Visuals.VfxSpawnPosition;
        if (auraAnchor.GetNodeOrNull<Node2D>(AuraNodeName) is not null)
            return;

        var aura = new Node2D
        {
            Name = AuraNodeName,
            ZAsRelative = true,
            ZIndex = 0,
            Position = AuraOffset,
            Scale = Vector2.One * PulseBaseScale,
            Modulate = Colors.Transparent,
            Visible = false
        };
        BuildGlow(aura);
        auraAnchor.AddChildSafely(aura);
        auraAnchor.MoveChildSafely(aura, 0);

        var state = new AuraState(aura, player, creatureNode, combatState);
        state.Start();
    }

    private sealed class AuraState : IDisposable
    {
        private readonly Node2D _aura;
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState _combatState;
        private Tween? _fadeTween;
        private Tween? _pulseTween;
        private float _targetAlpha = float.NaN;
        private bool _disposed;

        public AuraState(Node2D aura, Player player, NCreature creatureNode, ICombatState combatState)
        {
            _aura = aura;
            _player = player;
            _creature = player.Creature;
            _creatureNode = creatureNode;
            _combatState = combatState;
        }

        public void Start()
        {
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _aura.TreeExiting += OnTreeExiting;

            StartPulse();
            RefreshActivation();
        }

        private void OnPowerApplied(PowerModel power) =>
            RefreshForPower(power);

        private void OnPowerIncreased(PowerModel power, int _, bool __) =>
            RefreshForPower(power);

        private void OnPowerDecreased(PowerModel power, bool _) =>
            RefreshForPower(power);

        private void OnPowerRemoved(PowerModel power) =>
            RefreshForPower(power);

        private void RefreshForPower(PowerModel power)
        {
            if (power is ClassicMagicChargePower or ClassicLockPower)
                RefreshActivation();
        }

        private void RefreshActivation()
        {
            if (_disposed)
                return;
            if (!IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            var targetAlpha = SakuraExtraEffectTransaction.CanActivate(_player) ? MaxAlpha : 0f;
            if (Mathf.IsEqualApprox(_targetAlpha, targetAlpha))
                return;

            _targetAlpha = targetAlpha;
            KillTween(ref _fadeTween);
            var currentAlpha = _aura.Modulate.A;
            if (Mathf.IsEqualApprox(currentAlpha, targetAlpha))
            {
                SetAlpha(targetAlpha);
                return;
            }

            SetAlpha(currentAlpha);
            var duration = Mathf.Abs(targetAlpha - currentAlpha) * FadeDuration;
            var tween = _aura.CreateTween();
            _fadeTween = tween;
            tween.TweenMethod(
                    Callable.From<float>(SetAlpha),
                    currentAlpha,
                    targetAlpha,
                    duration)
                .SetTrans(Tween.TransitionType.Linear);
            tween.TweenCallback(Callable.From(() => FinishFade(tween, targetAlpha)));
        }

        private void FinishFade(Tween tween, float targetAlpha)
        {
            if (_disposed || !ReferenceEquals(_fadeTween, tween))
                return;

            _fadeTween = null;
            SetAlpha(targetAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (_disposed || !GodotObject.IsInstanceValid(_aura))
                return;

            _aura.Modulate = new Color(1f, 1f, 1f, alpha);
            _aura.Visible = alpha > VisibilityEpsilon || _targetAlpha > 0f;
        }

        private void StartPulse()
        {
            _aura.Scale = Vector2.One * PulseBaseScale;
            _pulseTween = _aura.CreateTween().SetLoops();
            _pulseTween.TweenProperty(
                    _aura,
                    "scale",
                    Vector2.One * (PulseBaseScale + PulseAmplitude),
                    PulseQuarterDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            _pulseTween.TweenProperty(
                    _aura,
                    "scale",
                    Vector2.One * (PulseBaseScale - PulseAmplitude),
                    PulseQuarterDuration * 2.0)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            _pulseTween.TweenProperty(
                    _aura,
                    "scale",
                    Vector2.One * PulseBaseScale,
                    PulseQuarterDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_aura)
            && _aura.IsInsideTree()
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creatureNode.Entity.Player, _player)
            && ReferenceEquals(_creature.CombatState, _combatState);

        private void OnCreatureDied(Creature creature)
        {
            if (ReferenceEquals(creature, _creature))
                DisposeAndFree();
        }

        private void OnCombatEnded(CombatRoom _) =>
            DisposeAndFree();

        private void OnTreeExiting() =>
            Dispose();

        private void DisposeAndFree()
        {
            Dispose();
            if (GodotObject.IsInstanceValid(_aura) && !_aura.IsQueuedForDeletion())
                _aura.QueueFreeSafely();
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
            _creature.Died -= OnCreatureDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _aura.TreeExiting -= OnTreeExiting;
            KillTween(ref _fadeTween);
            KillTween(ref _pulseTween);
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }
    }

    private static void BuildGlow(Node2D aura)
    {
        var texture = ResourceLoader.Load<Texture2D>(ImportedTexturePath(BlurRingPath.ImagePath()), null, ResourceLoader.CacheMode.Reuse);
        aura.AddChild(BuildLayer(texture, new Color(0.93f, 0.41f, 0.46f), 1.15f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.57f, 0.64f), 1f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.75f, 0.79f), 0.85f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.57f, 0.64f), 0.7f));
    }

    private static Sprite2D BuildLayer(Texture2D texture, Color color, float scale) =>
        new()
        {
            Texture = texture,
            Centered = true,
            Scale = Vector2.One * scale,
            Modulate = color,
            Material = AdditiveMaterial
        };

    private static string ImportedTexturePath(string sourcePath)
    {
        var importPath = sourcePath + ".import";
        if (!Godot.FileAccess.FileExists(importPath))
            return sourcePath;

        using var file = Godot.FileAccess.Open(importPath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            return sourcePath;

        while (!file.EofReached())
        {
            var line = file.GetLine().Trim();
            if (line.StartsWith(ImportPathPrefix) && line.EndsWith('"'))
                return line[ImportPathPrefix.Length..^1];
        }

        return sourcePath;
    }
}
