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

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Silent's veil: a drape over Sakura for as long as the Buffer it granted
/// holds. Presentation only; the Buffer stays on its Power.
/// </summary>
/// <remarks>
/// The Buffer amount is re-read from the synced Power on every Power event, never
/// kept as a flag of its own. The single local field, <c>_veilArmed</c>, only
/// records that the Silent summoned the veil, so Buffer from another source does
/// not grow one; it never feeds back into gameplay.
/// </remarks>
internal static partial class SilentQuietVisual
{
    internal const string VeilShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/silent_veil.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [VeilShaderPath];

    private const string RootName = "SakuraSilentVeil";
    private const int RootZIndex = 2;

    // Keep in step with scripts/render_silent_vfx.gd. The veil is tall enough to
    // take in her crown and narrow enough to stay clear of the water slot.
    internal static readonly Vector2 VeilRadiiFraction = new(0.55f, 0.62f);
    // Padding in units of the short radius. Taller than wide, because the veil's
    // field is scaled by the short radius and its entry rings start further out
    // along the long axis.
    internal static readonly Vector2 VeilPaddingFraction = new(1.0f, 1.6f);

    private const float VeilFadeInDuration = 0.25f;
    private const float GatherDuration = 0.62f;
    private const float SwallowDuration = 0.36f;
    private const float ShatterDuration = 0.7f;

    // Every uniform a tween drives. Each is written once at creation: a
    // ShaderMaterial reports an unset uniform as Nil rather than the shader
    // default, and a tween from Nil to a float fails and took the game down.
    private static readonly (string Name, float Value)[] InitialFloats =
    [
        ("veil_alpha", 0f),
        ("gather_progress", 1f),
        ("body_reveal", 1f),
        ("swallow_progress", 0f),
        ("shatter_progress", 0f)
    ];

    private static readonly ConditionalWeakTable<Creature, State> States = [];

    /// <summary>
    /// The Silent was just played and its Buffer applied. Raise the veil, or
    /// replay its gathering pulse if it is already up.
    /// </summary>
    internal static void NotifyVeilSummoned(Creature owner)
    {
        if (TryGetOrMount(owner, out var state))
            state.SummonVeil();
    }

    private static bool TryGetOrMount(Creature creature, out State state)
    {
        state = null!;
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled())
            return false;
        if (States.TryGetValue(creature, out state!))
            return true;

        if (NCombatRoom.Instance is not { CombatVfxContainer: { } container } room
            || !GodotObject.IsInstanceValid(container)
            || room.GetCreatureNode(creature) is not { } creatureNode
            || CelVfxGeometry.ResolveCaster(creatureNode) is not { } anchor)
        {
            return false;
        }

        VeilRoot? root = null;
        try
        {
            var shader = PreloadManager.Cache.GetAsset<Shader>(VeilShaderPath);
            var layout = Layout.From(anchor);
            var material = new ShaderMaterial { Shader = shader };
            foreach (var (name, value) in InitialFloats)
                material.SetShaderParameter(name, value);
            material.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            var rect = new ColorRect
            {
                Name = "SilentVeil",
                Color = Colors.White,
                Material = material,
                // In front of the creatures: it must never take the hover or
                // targeting a click on Sakura would otherwise get.
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root = new VeilRoot
            {
                Name = RootName,
                ZAsRelative = false,
                ZIndex = RootZIndex
            };
            root.AddChild(rect);
            container.AddChildSafely(root);

            state = new State(root, creature, creatureNode, rect, material);
            States.Add(creature, state);
            state.Start(layout);
            return true;
        }
        catch (Exception exception)
        {
            States.Remove(creature);
            root?.QueueFreeSafely();
            MainFile.Logger.Error($"Could not mount Silent veil: {exception}");
            state = null!;
            return false;
        }
    }

    internal readonly record struct Layout(Vector2 VeilRadii, Vector2 VeilSize)
    {
        internal static Layout From(CelVfxGeometry.CasterAnchor anchor)
        {
            var body = new Vector2(
                Mathf.Clamp(anchor.BodySize.X, 100f, 240f),
                Mathf.Clamp(anchor.BodySize.Y, 220f, 460f));
            var radii = body * VeilRadiiFraction;
            var padding = radii.X * VeilPaddingFraction;
            return new Layout(radii, radii * 2f + padding * 2f);
        }
    }

    private sealed partial class VeilRoot : Node2D
    {
        internal System.Action? Tick;

        public override void _Process(double delta) => Tick?.Invoke();
    }

    private sealed class State : IDisposable
    {
        private readonly VeilRoot _root;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState? _combatState;
        private readonly ColorRect _rect;
        private readonly ShaderMaterial _material;
        private Layout _layout;
        private Tween? _veilTween;
        private Tween? _swallowTween;
        private bool _veilArmed;
        private bool _veilShown;
        private int _lastBuffer;
        private bool _disposed;

        internal State(VeilRoot root, Creature creature, NCreature creatureNode, ColorRect rect, ShaderMaterial material)
        {
            _root = root;
            _creature = creature;
            _creatureNode = creatureNode;
            _combatState = creature.CombatState;
            _rect = rect;
            _material = material;
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

        internal void SummonVeil()
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

            _veilArmed = true;
            KillTween(ref _veilTween);
            Set("shatter_progress", 0f);
            var tween = _root.CreateTween().SetParallel();
            _veilTween = tween;
            if (!_veilShown)
            {
                // Rings first, then the drape forms behind them as they land.
                Set("body_reveal", 0f);
                TweenFloat(tween, "veil_alpha", 0f, 1f, VeilFadeInDuration)
                    .SetEase(Tween.EaseType.Out);
                TweenFloat(tween, "body_reveal", 0f, 1f, GatherDuration * 0.55f)
                    .SetDelay(GatherDuration * 0.45f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            else
            {
                Set("veil_alpha", 1f);
                Set("body_reveal", 1f);
            }

            _veilShown = true;
            TweenFloat(tween, "gather_progress", 0f, 1f, GatherDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
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
            if (_veilArmed)
            {
                if (spent)
                    PlaySwallow();
                if (buffer <= 0)
                    PlayShatter(spent ? SwallowDuration * 0.7f : 0f);
            }

            TryRetire();
        }

        private int ReadBuffer() => Math.Max(0, _creature.GetPowerAmount<BufferPower>());

        private void PlaySwallow()
        {
            // There is no attacker on a Power event, so the blow lands on the
            // enemy-facing front of the veil at chest height.
            var facing = CelVfxGeometry.ResolveCaster(_creatureNode)?.FacingSign ?? 1f;
            _material.SetShaderParameter(
                "swallow_point",
                new Vector2(_layout.VeilRadii.X * 0.92f * facing, -_layout.VeilRadii.Y * 0.18f));
            KillTween(ref _swallowTween);
            _swallowTween = _root.CreateTween();
            TweenFloat(_swallowTween, "swallow_progress", 0f, 1f, SwallowDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void PlayShatter(float delay)
        {
            _veilArmed = false;
            KillTween(ref _veilTween);
            var tween = _root.CreateTween();
            _veilTween = tween;
            TweenFloat(tween, "shatter_progress", 0f, 1f, ShatterDuration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenCallback(Callable.From(() =>
            {
                Set("veil_alpha", 0f);
                Set("shatter_progress", 0f);
                Set("swallow_progress", 0f);
                _veilShown = false;
                TryRetire();
            }));
        }

        private void TryRetire()
        {
            if (!_disposed && !_veilShown)
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

        private void Tick()
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

            // Big/Little resize the standee mid-combat; the veil follows the body.
            var layout = Layout.From(anchor);
            if (layout != _layout)
                ApplyLayout(layout);

            _root.GlobalPosition = anchor.BodyCenter;
        }

        private void ApplyLayout(Layout layout)
        {
            _layout = layout;
            _rect.Size = layout.VeilSize;
            _rect.Position = -layout.VeilSize * 0.5f;
            _material.SetShaderParameter("region_size", layout.VeilSize);
            _material.SetShaderParameter("veil_radii", layout.VeilRadii);
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
            KillTween(ref _veilTween);
            KillTween(ref _swallowTween);
        }
    }
}
