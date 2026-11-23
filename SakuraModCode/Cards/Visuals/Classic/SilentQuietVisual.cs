using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Silent's spirit: the hooded figure from the anime floats behind Sakura's
/// shoulder for as long as the Buffer it granted holds. Presentation only; the
/// Buffer stays on its Power.
/// </summary>
/// <remarks>
/// The Buffer amount is re-read from the synced Power on every Power event, never
/// kept as a flag of its own. The single local field, <c>_spiritArmed</c>, only
/// records that the Silent summoned the spirit, so Buffer from another source does
/// not raise one; it never feeds back into gameplay.
/// The spirit never leaves its place behind Sakura. A blocked blow flares it and
/// plays a hush ring in front of her; that ring is its own node, one slot in front
/// of the body, so the creature's health bar, Block and Powers still draw over both.
/// </remarks>
internal static partial class SilentQuietVisual
{
    internal const string SpiritShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/silent_spirit.gdshader";
    internal const string PulseShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/silent_hush_pulse.gdshader";
    internal const string SpiritTexturePath =
        MainFile.ResPath + "/images/card_vfx/silent/silent_spirit.png";
    internal static IReadOnlyList<string> AssetPaths { get; } =
        [SpiritShaderPath, PulseShaderPath, SpiritTexturePath];

    private const string SpiritRootName = "SakuraSilentSpirit";
    private const string PulseRootName = "SakuraSilentHush";

    // Keep in step with scripts/render_silent_vfx.gd. The spirit is a small figure
    // at Sakura's shoulder, not a second character beside her. The fractions are of
    // the hitbox, which in game is about 240 x 460 for the standard outfit while the
    // standee she shows is only about 0.85 of that tall: the spirit stands at roughly
    // three quarters of her visible height, its hat just above her crown.
    private const float SpiritHeightFraction = 0.65f;
    private const float SpiritBehindFraction = 0.22f;
    // Of the spirit's own height, measured from the ground the player sees.
    private const float SpiritLiftFraction = 0.55f;
    private const float PulseAheadFraction = 0.62f;
    // Never closer than this: the field must sit in the space in front of her, not on her skirt.
    private const float PulseAheadMin = 140f;
    private const float PulseRiseFraction = 0.53f;

    // The picture: canvas size, and where the figure's feet and hat sit in it.
    private static readonly Vector2 TextureSize = new(370f, 632f);
    private const float ContentTopPx = 20f;
    private const float ContentBottomPx = 562f;
    private static readonly Vector2 PulseSize = new(300f, 210f);

    private const float FadeInDuration = 0.25f;
    private const float GatherDuration = 0.9f;
    private const float HitDuration = 0.45f;
    private const float PunchRiseDuration = 0.07f;
    private const float PunchSettleDuration = 0.45f;
    private const float PulseDuration = 0.85f;
    private const float PresenceDuration = 0.3f;
    private const float VanishDuration = 1.0f;
    private const float VanishDelayAfterHit = 0.35f;

    // Every uniform a tween drives. Each is written once at creation: a
    // ShaderMaterial reports an unset uniform as Nil rather than the shader
    // default, and a tween from Nil to a float fails and took the game down.
    private static readonly (string Name, float Value)[] InitialSpiritFloats =
    [
        ("elapsed", 0f),
        ("facing", 1f),
        ("spirit_alpha", 0f),
        ("gather", 1f),
        ("vanish", 0f),
        ("presence", 0f),
        ("hit", 0f),
        ("punch", 0f)
    ];

    private static readonly ConditionalWeakTable<Creature, State> States = [];

    /// <summary>
    /// The Silent was just played and its Buffer applied. Raise the spirit, or
    /// flare it if it is already up.
    /// </summary>
    internal static void NotifyVeilSummoned(Creature owner)
    {
        if (TryGetOrMount(owner, out var state))
            state.Summon();
    }

    private static bool TryGetOrMount(Creature creature, out State state)
    {
        state = null!;
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled())
            return false;
        if (States.TryGetValue(creature, out state!))
            return true;

        if (NCombatRoom.Instance is not { } room
            || room.GetCreatureNode(creature) is not { } creatureNode
            || !GodotObject.IsInstanceValid(creatureNode.Visuals)
            || CelVfxGeometry.ResolveCaster(creatureNode) is not { } anchor)
        {
            return false;
        }

        SpiritRoot? spiritRoot = null;
        Node2D? pulseRoot = null;
        try
        {
            var layout = Layout.From(anchor);

            var spiritMaterial = new ShaderMaterial
            {
                Shader = PreloadManager.Cache.GetAsset<Shader>(SpiritShaderPath)
            };
            foreach (var (name, value) in InitialSpiritFloats)
                spiritMaterial.SetShaderParameter(name, value);
            spiritMaterial.SetShaderParameter(
                "spirit_tex", PreloadManager.Cache.GetAsset<Texture2D>(SpiritTexturePath));
            spiritMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            var spiritRect = new ColorRect
            {
                Name = "Spirit",
                Color = Colors.White,
                Material = spiritMaterial,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            spiritRoot = new SpiritRoot { Name = SpiritRootName };
            spiritRoot.AddChild(spiritRect);

            var pulseMaterial = new ShaderMaterial
            {
                Shader = PreloadManager.Cache.GetAsset<Shader>(PulseShaderPath)
            };
            pulseMaterial.SetShaderParameter("region_size", PulseSize);
            pulseMaterial.SetShaderParameter("progress", 0f);
            var pulseRect = new ColorRect
            {
                Name = "Hush",
                Color = Colors.White,
                Material = pulseMaterial,
                Size = PulseSize,
                Position = -PulseSize * 0.5f,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            pulseRoot = new Node2D { Name = PulseRootName };
            pulseRoot.AddChild(pulseRect);

            // Behind the body for the spirit, directly in front of it for the ring:
            // the creature's own health bar, Block and Powers draw over both.
            creatureNode.AddChildSafely(spiritRoot);
            creatureNode.MoveChildSafely(spiritRoot, creatureNode.Visuals.GetIndex());
            creatureNode.AddChildSafely(pulseRoot);
            creatureNode.MoveChildSafely(pulseRoot, creatureNode.Visuals.GetIndex() + 1);

            state = new State(
                spiritRoot, pulseRoot, creature, creatureNode,
                spiritRect, spiritMaterial, pulseRect, pulseMaterial);
            States.Add(creature, state);
            state.Start(layout);
            return true;
        }
        catch (Exception exception)
        {
            States.Remove(creature);
            spiritRoot?.QueueFreeSafely();
            pulseRoot?.QueueFreeSafely();
            MainFile.Logger.Error($"Could not mount Silent spirit: {exception}");
            state = null!;
            return false;
        }
    }

    internal readonly record struct Layout(
        Vector2 RectSize,
        Vector2 RectOffset,
        Vector2 BehindOffset,
        Vector2 PulseOffset)
    {
        internal static Layout From(CelVfxGeometry.CasterAnchor anchor)
        {
            var body = new Vector2(
                Mathf.Clamp(anchor.BodySize.X, 100f, 240f),
                Mathf.Clamp(anchor.BodySize.Y, 220f, 460f));
            var contentHeight = body.Y * SpiritHeightFraction;
            var scale = contentHeight / (ContentBottomPx - ContentTopPx);
            var rectSize = TextureSize * scale;
            return new Layout(
                rectSize,
                // From the figure's foot point up and left to the rect's corner.
                new Vector2(-rectSize.X * 0.5f, -ContentBottomPx * scale),
                // Facing-relative: X is flipped by the caller, Y is the float height.
                new Vector2(body.X * SpiritBehindFraction, -contentHeight * SpiritLiftFraction),
                new Vector2(Mathf.Max(body.X * PulseAheadFraction, PulseAheadMin), -body.Y * PulseRiseFraction));
        }
    }

    private sealed partial class SpiritRoot : Node2D
    {
        internal System.Action<float>? Tick;

        public override void _Process(double delta) => Tick?.Invoke((float)delta);
    }

    private sealed class State : IDisposable
    {
        private readonly SpiritRoot _root;
        private readonly Node2D _pulseRoot;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState? _combatState;
        private readonly ColorRect _rect;
        private readonly ShaderMaterial _material;
        private readonly ColorRect _pulseRect;
        private readonly ShaderMaterial _pulseMaterial;
        private Layout _layout;
        private float _facing = 1f;
        private float _elapsed;
        private float _presence;
        private Tween? _spiritTween;
        private Tween? _hitTween;
        private Tween? _punchTween;
        private Tween? _presenceTween;
        private bool _spiritArmed;
        private bool _spiritShown;
        private int _lastBuffer;
        private bool _disposed;

        internal State(
            SpiritRoot root,
            Node2D pulseRoot,
            Creature creature,
            NCreature creatureNode,
            ColorRect rect,
            ShaderMaterial material,
            ColorRect pulseRect,
            ShaderMaterial pulseMaterial)
        {
            _root = root;
            _pulseRoot = pulseRoot;
            _creature = creature;
            _creatureNode = creatureNode;
            _combatState = creature.CombatState;
            _rect = rect;
            _material = material;
            _pulseRect = pulseRect;
            _pulseMaterial = pulseMaterial;
        }

        internal void Start(Layout layout)
        {
            ApplyLayout(layout);
            _lastBuffer = ReadBuffer();

            _creature.PowerApplied += OnPowerChanged;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerChanged;
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            _root.Tick = Tick;
            FollowAnchor();
        }

        internal void Summon()
        {
            if (_disposed)
                return;

            var buffer = ReadBuffer();
            _lastBuffer = buffer;
            if (buffer <= 0)
            {
                TryRetire();
                return;
            }

            _spiritArmed = true;
            KillTween(ref _spiritTween);
            Set("vanish", 0f);
            SetPresence(buffer);
            if (!_spiritShown)
            {
                // Out of mist: the figure condenses while the fade comes up.
                Set("gather", 0f);
                var tween = _root.CreateTween().SetParallel();
                _spiritTween = tween;
                TweenFloat(tween, "spirit_alpha", 0f, 1f, FadeInDuration)
                    .SetEase(Tween.EaseType.Out);
                TweenFloat(tween, "gather", 0f, 1f, GatherDuration)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            else
            {
                // Played again while already up: a gentle flare, no second entrance.
                Set("spirit_alpha", 1f);
                Set("gather", 1f);
                PlayFlare(0.4f);
            }

            _spiritShown = true;
        }

        /// <summary>
        /// Re-read the Buffer from its synced Power and animate only what changed.
        /// </summary>
        private void Refresh()
        {
            if (_disposed)
                return;

            var buffer = ReadBuffer();
            var spent = buffer < _lastBuffer;
            _lastBuffer = buffer;
            if (_spiritArmed)
            {
                if (buffer > 0)
                    SetPresence(buffer);
                if (spent)
                    PlayHit();
                if (buffer <= 0)
                    PlayVanish(spent ? VanishDelayAfterHit : 0f);
            }

            TryRetire();
        }

        private int ReadBuffer() => Math.Max(0, _creature.GetPowerAmount<BufferPower>());

        /// <summary>One Buffer is a calm presence; two or more a stronger one.</summary>
        private void SetPresence(int buffer)
        {
            var target = buffer >= 2 ? 1f : 0f;
            if (Mathf.IsEqualApprox(target, _presence))
                return;

            KillTween(ref _presenceTween);
            var from = _presence;
            _presence = target;
            _presenceTween = _root.CreateTween();
            TweenFloat(_presenceTween, "presence", from, target, PresenceDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void PlayFlare(float strength)
        {
            KillTween(ref _hitTween);
            _hitTween = _root.CreateTween();
            TweenFloat(_hitTween, "hit", strength, 0f, HitDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        /// <summary>A quick swell and a soft settle: she took the blow for Sakura.</summary>
        private void PlayPunch()
        {
            KillTween(ref _punchTween);
            var tween = _root.CreateTween();
            _punchTween = tween;
            TweenFloat(tween, "punch", 0f, 1f, PunchRiseDuration)
                .SetEase(Tween.EaseType.Out);
            TweenFloat(tween, "punch", 1f, 0f, PunchSettleDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void PlayHit()
        {
            // There is no attacker on a Power event, so the ring closes in front of
            // Sakura on the side she faces, at chest height.
            _pulseRoot.GlobalPosition = GroundPoint() + new Vector2(_facing * _layout.PulseOffset.X, _layout.PulseOffset.Y);
            _pulseMaterial.SetShaderParameter("progress", 0f);
            _pulseRect.Visible = true;

            PlayPunch();
            KillTween(ref _hitTween);
            var tween = _root.CreateTween().SetParallel();
            _hitTween = tween;
            TweenFloat(tween, "hit", 1f, 0f, HitDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            var pulseMaterial = _pulseMaterial;
            tween.TweenMethod(
                Callable.From<float>(value => pulseMaterial.SetShaderParameter("progress", value)),
                0f,
                1f,
                PulseDuration);
            var pulseRect = _pulseRect;
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(pulseRect))
                    pulseRect.Visible = false;
            }));
        }

        private void PlayVanish(float delay)
        {
            _spiritArmed = false;
            KillTween(ref _spiritTween);
            var tween = _root.CreateTween();
            _spiritTween = tween;
            TweenFloat(tween, "vanish", 0f, 1f, VanishDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenCallback(Callable.From(() =>
            {
                Set("spirit_alpha", 0f);
                Set("vanish", 0f);
                _spiritShown = false;
                TryRetire();
            }));
        }

        private void TryRetire()
        {
            if (!_disposed && !_spiritShown)
                DisposeAndFree();
        }

        private void Set(string name, float value) => _material.SetShaderParameter(name, value);

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

        private void OnPowerChanged(PowerModel _) => Refresh();

        private void OnPowerIncreased(PowerModel _, int __, bool ___) => Refresh();

        private void OnPowerDecreased(PowerModel _, bool __) => Refresh();

        private void Tick(float delta)
        {
            if (_disposed)
                return;
            if (!SakuraModConfig.IsCardVfxEnabled() || !IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            // Session-owned time: the shader never reads TIME.
            _elapsed += delta;
            Set("elapsed", _elapsed);
            FollowAnchor();
        }

        private Vector2 GroundPoint(CelVfxGeometry.CasterAnchor? anchor = null)
        {
            var resolved = anchor ?? CelVfxGeometry.ResolveCaster(_creatureNode);
            return resolved is { } a
                ? new Vector2(a.BodyCenter.X, a.Floor.Y - SakuraElementSlotLayout.GroundVisualLift)
                : _root.GlobalPosition;
        }

        private void FollowAnchor()
        {
            if (CelVfxGeometry.ResolveCaster(_creatureNode) is not { } anchor)
                return;

            // Big/Little resize the standee mid-combat; the spirit follows the body.
            var layout = Layout.From(anchor);
            if (layout != _layout)
                ApplyLayout(layout);

            var facing = anchor.FacingSign == 0f ? 1f : anchor.FacingSign;
            if (!Mathf.IsEqualApprox(facing, _facing))
            {
                _facing = facing;
                Set("facing", _facing);
            }

            _root.GlobalPosition = GroundPoint(anchor)
                + new Vector2(-_facing * _layout.BehindOffset.X, _layout.BehindOffset.Y);
        }

        private void ApplyLayout(Layout layout)
        {
            _layout = layout;
            _rect.Size = layout.RectSize;
            _rect.Position = layout.RectOffset;
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creature.CombatState, _combatState);

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
            if (GodotObject.IsInstanceValid(_pulseRoot) && !_pulseRoot.IsQueuedForDeletion())
                _pulseRoot.QueueFreeSafely();
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            States.Remove(_creature);
            _creature.PowerApplied -= OnPowerChanged;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerChanged;
            _creature.Died -= OnCreatureDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            if (GodotObject.IsInstanceValid(_root))
                _root.TreeExiting -= OnTreeExiting;
            _root.Tick = null;
            KillTween(ref _spiritTween);
            KillTween(ref _hitTween);
            KillTween(ref _punchTween);
            KillTween(ref _presenceTween);
        }
    }
}
