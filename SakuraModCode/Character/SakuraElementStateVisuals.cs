using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Character;

internal readonly record struct SakuraElementSlotLayout(
    Vector2 Fire,
    Vector2 Wind,
    Vector2 Earth,
    Vector2 Water)
{
    // Keep the full water mark (droplets and its surface line) slightly above the
    // floor anchor so it remains readable against the standee's feet and HP bar.
    // Earth keeps its internal contact line grounded, then applies a separate visual
    // lift so the complete mark clears the HP/Block strip in the combat layout.
    private const float WaterVisualLift = 30f;
    // The earth mark uses the real floor for its internal contact line, but the
    // complete mark must sit above the HP/Block strip in the live combat layout.
    // Keep this as a slot-level lift instead of changing CONTACT_Y in the shader so
    // summon emergence and the wall remain grounded relative to one another.
    private const float EarthVisualLift = 32f;

    /// <summary>
    /// Fire owns the centre axis, water the left of the floor, earth the right of it,
    /// and wind crosses the torso. Wind is in no side slot because it is the one
    /// element with no location of its own: fire is a source, earth is low and heavy,
    /// water flows but has a body. Crossing the body says "diffuse" in a way a patch
    /// beside the shoulder cannot. Its rise is kept short enough to clear the fire slot
    /// above and only graze the ground, and it carries a small leftward bias so the
    /// rise reads against the standee instead of splitting it down the middle. The bias
    /// stays far short of the water slot.
    ///
    /// The two ground marks take opposite sides so neither has to yield size to the
    /// other. Splitting them vertically was the alternative and it is strictly worse:
    /// both belong on the floor, so stacking them would push one of them off it. Their
    /// separation is horizontal and their heights are free.
    ///
    /// Neither side flips with the standee. Position is read once when the slot is laid
    /// out, and <see cref="CelVfxGeometry.CasterAnchor.FacingSign"/> is deliberately not
    /// consulted: it is republished every frame by the idle controllers' SyncFlip, so a
    /// mirrored slot would make persistent marks jump across the character mid-combat.
    /// Only a one-shot beat may face — earth's wall reads facing when it starts and
    /// mirrors inside its own shader, leaving the fragments where they were.
    ///
    /// Water and earth are both measured from the floor instead of from body height,
    /// and for the same reason. Every cue either one owns — falling, pooling and a
    /// horizontal surface for water; cracks, contact shadows and fragments resting on
    /// something for earth — belongs to the ground, and any of them floating at waist
    /// height reads as a stray mark, which is worse than not drawing it at all. A
    /// fraction of body height cannot express "on the floor": the mount point's own
    /// height above the ground is not knowable from body size, so any such fraction is
    /// a guess. The caster anchor already carries the real floor, and
    /// <paramref name="floorY"/> is it. Earth was that guess (-0.08 of body height, so
    /// roughly hip level, on the centre axis) until this became the slot that had to
    /// draw a crack in the ground.
    /// </summary>
    /// <param name="floorY">
    /// Floor position in the same local space as the returned slots, i.e. the caster
    /// anchor's floor converted into the visuals root.
    /// </param>
    /// <param name="waterSurfaceInset">
    /// Distance from the water rect's centre down to the surface line its shader
    /// draws. The slot is raised by this much so that line lands on the floor.
    /// </param>
    /// <param name="earthContactInset">
    /// Distance from the earth rect's centre down to the contact line its shader rests
    /// the fragments on. The slot is raised by this much so that line lands on the
    /// floor, exactly as <paramref name="waterSurfaceInset"/> does for the pool.
    /// </param>
    internal static SakuraElementSlotLayout FromBody(
        Vector2 bodySize,
        float floorY,
        float waterSurfaceInset,
        float earthContactInset)
    {
        var width = Math.Clamp(bodySize.X, 100f, 240f);
        var height = Math.Clamp(bodySize.Y, 220f, 460f);
        // A degenerate hitbox can report a floor level with or above the mount point,
        // which would fling both ground marks up through the standee. Clamping the
        // floor once — rather than having each ground slot clamp against the other —
        // keeps them deriving from one shared, already-safe value: a bad floor then
        // degrades both to mount height together instead of dragging one to wherever
        // the other happens to sit.
        var groundY = Math.Max(floorY, 0f);
        return new(
            Fire: new(0f, -height * 0.58f),
            Wind: new(-width * 0.12f, -height * 0.22f),
            // Further inboard than water's 0.58: earth is short and sits on the ground,
            // where the HP bar and Block readout are, so it stays nearer the feet.
            Earth: new(width * 0.42f, groundY - earthContactInset - EarthVisualLift),
            Water: new(-width * 0.58f, groundY - waterSurfaceInset - WaterVisualLift));
    }
}

internal static class SakuraElementStateVisuals
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/sakura_element_state_visuals.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_firey.gdshader";
    internal const string WindShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_windy.gdshader";
    internal const string WaterShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_watery.gdshader";
    internal const string EarthShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_earthy.gdshader";

    private const string RootName = "SakuraElementStateVisuals";
    private const string FireSlotName = "FireSlot";
    private const string FireyEmberName = "FireyEmber";
    private const string WindSlotName = "WindSlot";
    private const string WindyCurrentsName = "WindyCurrents";
    private const string WaterSlotName = "WaterSlot";
    private const string WateryDropletsName = "WateryDroplets";
    private const string EarthSlotName = "EarthSlot";
    private const string EarthyFragmentsName = "EarthyFragments";
    private const float QuickRevealDuration = 0.3f;
    private const float SummonDuration = 0.98f;
    private const float DismissDuration = 0.24f;
    private const float TriggerDuration = 0.26f;
    private const float WindTriggerDuration = 0.3f;
    /// <summary>
    /// Longer than the other two because the merge has to read as three beats —
    /// draw together, coalesce, overshoot and settle. Compressed to fire's 0.26s
    /// the rebound lands inside a couple of frames and reads as a glitch.
    /// </summary>
    private const float WaterTriggerDuration = 0.42f;
    // Fire state can hit every enemy at once, so its travelling sparks need enough
    // screen-space mass to remain readable among concurrent trails.
    private const float FireTriggerSparkRadius = 9f;
    /// <summary>
    /// Long enough that the wall's dead hold — <c>WALL_HOLD_END - WALL_HOLD_START</c>
    /// of this span in <c>sakura_element_state_earthy.gdshader</c>, i.e. 0.26 of it —
    /// outlasts one <c>CEL_STEP_HZ</c> tick. A freeze shorter than the detail clock is
    /// not a freeze, and the hold is what makes the wall read as enduring rather than
    /// as a shape passing through.
    /// </summary>
    private const float EarthTriggerDuration = 0.38f;
    /// <summary>
    /// Where the water shader draws its surface line, as a fraction of the region
    /// height measured down from the rect's centre. Must stay equal to
    /// <c>POOL_Y</c> in <c>sakura_element_state_watery.gdshader</c>: the slot is
    /// raised by this distance so the drawn line lands on the real floor, so the two
    /// values drifting apart would float the pool again.
    /// </summary>
    private const float WaterPoolSurfaceFraction = 0.46f;
    /// <summary>
    /// Where the earth shader rests its fragments, as a fraction of the region height
    /// measured down from the rect's centre. Must stay equal to <c>CONTACT_Y</c> in
    /// <c>sakura_element_state_earthy.gdshader</c>, for the same reason
    /// <see cref="WaterPoolSurfaceFraction"/> must match its own shader: the slot is
    /// raised by this distance so the drawn contact line lands on the real floor, and
    /// the two drifting apart floats the stones again.
    /// </summary>
    private const float EarthContactSurfaceFraction = 0.30f;
    private const int MaxTriggerTargets = 8;
    private const int WindStreamCount = 3;

    private static readonly ConditionalWeakTable<Creature, State> States = new();

    internal static IEnumerable<string> AssetPaths =>
        [ScenePath, ShaderPath, WindShaderPath, WaterShaderPath, EarthShaderPath];

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
            root.ZIndex = 2;
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

    internal static void NotifyIconicFireyPlayed(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.PlaySummon();
    }

    internal static void NotifyFireTriggered(Creature owner, IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0 || targets.Count > MaxTriggerTargets)
            return;
        if (SakuraModConfig.IsCardVfxEnabled() && States.TryGetValue(owner, out var state))
            state.PlayTrigger(targets);
    }

    internal static void NotifyIconicWindyPlayed(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.PlayWindSummon();
    }

    /// <summary>
    /// Presentation-only signal that the wind state just spent its counter on a
    /// draw. Gameplay owns the counter and the draw command; this never inspects
    /// either and never blocks them.
    /// </summary>
    internal static void NotifyWindTriggered(Creature owner)
    {
        if (SakuraModConfig.IsCardVfxEnabled() && States.TryGetValue(owner, out var state))
            state.PlayWindTrigger();
    }

    internal static void NotifyIconicWateryPlayed(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.PlayWaterSummon();
    }

    internal static void NotifyIconicEarthyPlayed(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.PlayEarthSummon();
    }

    /// <summary>
    /// Presentation-only signal that the earth state is about to pay out Block.
    /// Gameplay owns the Block and the command; this never reads the amount and never
    /// blocks them.
    /// </summary>
    /// <remarks>
    /// Unlike wind and water, earth has no counter to reach — every earth card played
    /// while the state is up triggers — so this arrives far more often and will
    /// routinely re-enter a wall that is still playing. <see cref="State.PlayEarthTrigger"/>
    /// owns that restart.
    /// </remarks>
    internal static void NotifyEarthTriggered(Creature owner)
    {
        if (SakuraModConfig.IsCardVfxEnabled() && States.TryGetValue(owner, out var state))
            state.PlayEarthTrigger();
    }

    /// <summary>
    /// Presentation-only signal that the water state just spent its counter on
    /// energy. Gameplay owns the counter and the energy command; this never reads
    /// either and never blocks them.
    /// </summary>
    internal static void NotifyWaterTriggered(Creature owner)
    {
        if (SakuraModConfig.IsCardVfxEnabled() && States.TryGetValue(owner, out var state))
            state.PlayWaterTrigger();
    }

    private sealed class State : IDisposable
    {
        private readonly Node2D _root;
        private readonly NCreature _creatureNode;
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly ICombatState _combatState;
        private readonly Node2D _fireSlot;
        private readonly ColorRect _ember;
        private readonly ShaderMaterial _material;
        private readonly Node2D _windSlot;
        private readonly ColorRect _currents;
        private readonly ShaderMaterial _windMaterial;
        private readonly Node2D _waterSlot;
        private readonly ColorRect _droplets;
        private readonly ShaderMaterial _waterMaterial;
        private readonly Node2D _earthSlot;
        private readonly ColorRect _fragments;
        private readonly ShaderMaterial _earthMaterial;
        private Tween? _entryTween;
        private Tween? _exitTween;
        private Tween? _windEntryTween;
        private Tween? _windExitTween;
        private Tween? _waterEntryTween;
        private Tween? _waterExitTween;
        private Tween? _earthEntryTween;
        private Tween? _earthExitTween;
        /// <summary>
        /// Held as a field, unlike the other three elements' triggers. Earth has no
        /// counter to reach, so consecutive earth cards re-enter this while the wall is
        /// still forming; without a handle to kill, several tweens would drive one
        /// <c>trigger_progress</c> at once and drag the wall back to its start. Fire has
        /// the same cadence and gets away with a local tween only because its 0.26s ring
        /// has no formed shape to lose — that is worth tidying separately, not copying.
        /// </summary>
        private Tween? _earthTriggerTween;
        private SakuraElementSet _activeStates;
        private bool _disposed;

        internal State(Node2D root, NCreature creatureNode, Player player, ICombatState combatState)
        {
            _root = root;
            _creatureNode = creatureNode;
            _player = player;
            _creature = player.Creature;
            _combatState = combatState;
            _fireSlot = root.GetNode<Node2D>(FireSlotName);
            _ember = root.GetNode<ColorRect>($"{FireSlotName}/{FireyEmberName}");
            _material = _ember.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura fire state VFX requires a ShaderMaterial.");
            _ember.Material = _material;
            _windSlot = root.GetNode<Node2D>(WindSlotName);
            _currents = root.GetNode<ColorRect>($"{WindSlotName}/{WindyCurrentsName}");
            _windMaterial = _currents.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura wind state VFX requires a ShaderMaterial.");
            _currents.Material = _windMaterial;
            _waterSlot = root.GetNode<Node2D>(WaterSlotName);
            _droplets = root.GetNode<ColorRect>($"{WaterSlotName}/{WateryDropletsName}");
            _waterMaterial = _droplets.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura water state VFX requires a ShaderMaterial.");
            _droplets.Material = _waterMaterial;
            _earthSlot = root.GetNode<Node2D>(EarthSlotName);
            _fragments = root.GetNode<ColorRect>($"{EarthSlotName}/{EarthyFragmentsName}");
            _earthMaterial = _fragments.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura earth state VFX requires a ShaderMaterial.");
            _fragments.Material = _earthMaterial;
        }

        internal void Start()
        {
            var geometry = CelVfxGeometry.ResolveCaster(_creatureNode)
                ?? throw new InvalidOperationException("Sakura fire state VFX requires caster geometry.");
            // The anchor's floor is global; slot positions are local to this root.
            var floorY = _root.ToLocal(geometry.Floor).Y;
            var layout = SakuraElementSlotLayout.FromBody(
                geometry.BodySize,
                floorY,
                _droplets.Size.Y * WaterPoolSurfaceFraction,
                _fragments.Size.Y * EarthContactSurfaceFraction);
            _fireSlot.Position = layout.Fire;
            _windSlot.Position = layout.Wind;
            _earthSlot.Position = layout.Earth;
            _waterSlot.Position = layout.Water;
            _material.SetShaderParameter("region_size", _ember.Size);
            _material.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _material.SetShaderParameter("state_alpha", 0f);
            _material.SetShaderParameter("summon_progress", 1f);
            _material.SetShaderParameter("summon_hold", 0f);
            _material.SetShaderParameter("trigger_progress", 0f);
            _windMaterial.SetShaderParameter("region_size", _currents.Size);
            _windMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _windMaterial.SetShaderParameter("state_alpha", 0f);
            _windMaterial.SetShaderParameter("summon_progress", 1f);
            _windMaterial.SetShaderParameter("summon_hold", 0f);
            _windMaterial.SetShaderParameter("trigger_progress", 0f);
            _waterMaterial.SetShaderParameter("region_size", _droplets.Size);
            _waterMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _waterMaterial.SetShaderParameter("state_alpha", 0f);
            _waterMaterial.SetShaderParameter("summon_progress", 1f);
            _waterMaterial.SetShaderParameter("summon_hold", 0f);
            _waterMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("region_size", _fragments.Size);
            _earthMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _earthMaterial.SetShaderParameter("state_alpha", 0f);
            _earthMaterial.SetShaderParameter("summon_progress", 1f);
            _earthMaterial.SetShaderParameter("summon_hold", 0f);
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("facing", ResolveFacingSign());

            _creature.PowerApplied += OnPowerChanged;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerChanged;
            _creature.Died += OnDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            Refresh(animateEntry: false);
        }

        private void OnPowerChanged(PowerModel power)
        {
            if (IsElementPower(power))
                Refresh(animateEntry: true);
        }

        private void OnPowerIncreased(PowerModel power, int _, bool __) => OnPowerChanged(power);
        private void OnPowerDecreased(PowerModel power, bool _) => OnPowerChanged(power);

        private void Refresh(bool animateEntry)
        {
            if (_disposed || !IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            var next = SakuraElementState.ReadActive(_player);
            var previous = _activeStates;
            _activeStates = next;

            RefreshFire(previous, next, animateEntry);
            RefreshWind(previous, next, animateEntry);
            RefreshWater(previous, next, animateEntry);
            RefreshEarth(previous, next, animateEntry);
        }

        /// <summary>
        /// Which way the standee faces, for the wall to form toward. Read at the start
        /// of a beat and written into the shader as a uniform, never sampled per frame:
        /// SyncFlip republishes the sign continuously, so reading it live would let the
        /// wall mirror itself mid-formation.
        /// </summary>
        private float ResolveFacingSign() =>
            CelVfxGeometry.ResolveCaster(_creatureNode) is { } anchor ? anchor.FacingSign : 1f;

        private void RefreshFire(
            SakuraElementSet previous,
            SakuraElementSet next,
            bool animateEntry)
        {
            var wasActive = previous.HasElement(SakuraElement.Fire);
            var isActive = next.HasElement(SakuraElement.Fire);

            if (isActive && !wasActive)
            {
                if (animateEntry && SakuraModConfig.IsCardVfxEnabled())
                    PlayQuickReveal();
                else
                    SetStateAlpha(1f);
            }
            else if (!isActive && wasActive)
            {
                PlayDismiss();
            }
            else if (isActive)
            {
                SetStateAlpha(1f);
            }
        }

        private void RefreshWind(
            SakuraElementSet previous,
            SakuraElementSet next,
            bool animateEntry)
        {
            var wasActive = previous.HasElement(SakuraElement.Wind);
            var isActive = next.HasElement(SakuraElement.Wind);

            if (isActive && !wasActive)
            {
                if (animateEntry && SakuraModConfig.IsCardVfxEnabled())
                    PlayWindQuickReveal();
                else
                    SetWindStateAlpha(1f);
            }
            else if (!isActive && wasActive)
            {
                PlayWindDismiss();
            }
            else if (isActive)
            {
                SetWindStateAlpha(1f);
            }
        }

        private void RefreshWater(
            SakuraElementSet previous,
            SakuraElementSet next,
            bool animateEntry)
        {
            var wasActive = previous.HasElement(SakuraElement.Water);
            var isActive = next.HasElement(SakuraElement.Water);

            if (isActive && !wasActive)
            {
                if (animateEntry && SakuraModConfig.IsCardVfxEnabled())
                    PlayWaterQuickReveal();
                else
                    SetWaterStateAlpha(1f);
            }
            else if (!isActive && wasActive)
            {
                PlayWaterDismiss();
            }
            else if (isActive)
            {
                SetWaterStateAlpha(1f);
            }
        }

        private void RefreshEarth(
            SakuraElementSet previous,
            SakuraElementSet next,
            bool animateEntry)
        {
            var wasActive = previous.HasElement(SakuraElement.Earth);
            var isActive = next.HasElement(SakuraElement.Earth);

            if (isActive && !wasActive)
            {
                if (animateEntry && SakuraModConfig.IsCardVfxEnabled())
                    PlayEarthQuickReveal();
                else
                    SetEarthStateAlpha(1f);
            }
            else if (!isActive && wasActive)
            {
                PlayEarthDismiss();
            }
            else if (isActive)
            {
                SetEarthStateAlpha(1f);
            }
        }

        internal void PlaySummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Fire))
                return;

            KillTween(ref _entryTween);
            KillTween(ref _exitTween);
            _ember.Visible = true;
            _material.SetShaderParameter("state_alpha", 1f);
            _material.SetShaderParameter("summon_progress", 0f);
            _material.SetShaderParameter("summon_hold", 0f);
            _ember.Scale = Vector2.One * 0.55f;
            _entryTween = _root.CreateTween().SetParallel();
            _entryTween.TweenMethod(Callable.From<float>(value =>
                    _material.SetShaderParameter("summon_progress", value)),
                0f, 1f, SummonDuration * 0.72f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            _entryTween.TweenProperty(_ember, "scale", Vector2.One, SummonDuration * 0.72f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            _entryTween.Chain().TweenMethod(Callable.From<float>(value =>
                    _material.SetShaderParameter("summon_hold", value)),
                0f, 1f, SummonDuration * 0.1f);
            _entryTween.Chain().TweenMethod(Callable.From<float>(value =>
                    _material.SetShaderParameter("summon_hold", value)),
                1f, 0f, SummonDuration * 0.18f);
            _entryTween.Chain().TweenCallback(Callable.From(() => _entryTween = null));
        }

        internal void PlayWindSummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Wind))
                return;

            KillTween(ref _windEntryTween);
            KillTween(ref _windExitTween);
            _currents.Visible = true;
            _currents.Scale = Vector2.One;
            _currents.Position = Vector2.Zero;
            _windMaterial.SetShaderParameter("state_alpha", 1f);
            _windMaterial.SetShaderParameter("summon_progress", 0f);
            _windMaterial.SetShaderParameter("summon_hold", 0f);
            _windEntryTween = _root.CreateTween();
            // Currents sweep in wide and tighten onto the orbit; the shader reads
            // summon_progress as the gather radius, so no node position is reset.
            _windEntryTween.TweenMethod(Callable.From<float>(value =>
                    _windMaterial.SetShaderParameter("summon_progress", value)),
                0f, 1f, SummonDuration * 0.74f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _windEntryTween.TweenMethod(Callable.From<float>(value =>
                    _windMaterial.SetShaderParameter("summon_hold", value)),
                0f, 1f, SummonDuration * 0.09f);
            _windEntryTween.TweenMethod(Callable.From<float>(value =>
                    _windMaterial.SetShaderParameter("summon_hold", value)),
                1f, 0f, SummonDuration * 0.17f);
            _windEntryTween.TweenCallback(Callable.From(() => _windEntryTween = null));
        }

        internal void PlayWindTrigger()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root) || !SakuraModConfig.IsCardVfxEnabled())
                return;

            _windMaterial.SetShaderParameter("trigger_progress", 0f);
            var triggerTween = _root.CreateTween();
            triggerTween.TweenMethod(Callable.From<float>(value =>
                    _windMaterial.SetShaderParameter("trigger_progress", value)),
                0f, 1f, WindTriggerDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);

            if (NCombatRoom.Instance is not { } room
                || NPlayerHand.Instance is not { } hand
                || !GodotObject.IsInstanceValid(hand.CardHolderContainer))
            {
                return;
            }

            // Hand geometry is presentation-only; when it is missing the slot rush
            // above still plays and the draw itself is untouched.
            var target = hand.CardHolderContainer.GetGlobalRect().GetCenter();
            AddWindStream(room, _windSlot.GlobalPosition, target);
        }

        private static void AddWindStream(NCombatRoom room, Vector2 origin, Vector2 target)
        {
            var root = new Node2D
            {
                Name = "SakuraWindyTriggerStream",
                GlobalPosition = origin,
                ZIndex = 20,
                ZAsRelative = false
            };
            room.CombatVfxContainer.AddChildSafely(root);
            var delta = target - origin;
            var arcHeight = Mathf.Clamp(delta.Length() * 0.16f, 22f, 86f);
            var lines = new Line2D[WindStreamCount];
            var paths = new Vector2[WindStreamCount][];
            for (var index = 0; index < WindStreamCount; index++)
            {
                // Each strand takes a slightly different arc so the stream reads as
                // banded air rather than one solid ribbon.
                var lift = arcHeight * (1f - index * 0.34f);
                var control = delta * (0.46f + index * 0.05f) + Vector2.Up * lift;
                paths[index] = BezierPoints(control, delta, 14);
                var line = new Line2D
                {
                    Width = 5f - index * 1.3f,
                    DefaultColor = new Color(0.82f, 0.98f, 1f, 0.66f - index * 0.14f),
                    Antialiased = true,
                    Points = [Vector2.Zero, Vector2.Zero]
                };
                lines[index] = line;
                root.AddChild(line);
            }

            var tween = root.CreateTween().SetParallel();
            tween.TweenMethod(Callable.From<float>(progress =>
            {
                for (var index = 0; index < lines.Length; index++)
                    lines[index].Points = TrailPoints(paths[index], progress);
            }),
                0f, 1f, WindTriggerDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            foreach (var line in lines)
            {
                tween.TweenProperty(line, "modulate:a", 0f, WindTriggerDuration * 0.4f)
                    .SetDelay(WindTriggerDuration * 0.6f);
            }
            tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        }

        internal void PlayWaterSummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Water))
                return;

            KillTween(ref _waterEntryTween);
            KillTween(ref _waterExitTween);
            _droplets.Visible = true;
            _droplets.Scale = Vector2.One;
            _droplets.Position = Vector2.Zero;
            _waterMaterial.SetShaderParameter("state_alpha", 1f);
            _waterMaterial.SetShaderParameter("summon_progress", 0f);
            _waterMaterial.SetShaderParameter("summon_hold", 0f);
            _waterEntryTween = _root.CreateTween();
            // Scattered threads draw in and coalesce; the shader reads
            // summon_progress as both the gather distance and the droplets' own
            // mass, so nothing here repositions a node.
            _waterEntryTween.TweenMethod(Callable.From<float>(value =>
                    _waterMaterial.SetShaderParameter("summon_progress", value)),
                0f, 1f, SummonDuration * 0.74f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _waterEntryTween.TweenMethod(Callable.From<float>(value =>
                    _waterMaterial.SetShaderParameter("summon_hold", value)),
                0f, 1f, SummonDuration * 0.09f);
            _waterEntryTween.TweenMethod(Callable.From<float>(value =>
                    _waterMaterial.SetShaderParameter("summon_hold", value)),
                1f, 0f, SummonDuration * 0.17f);
            _waterEntryTween.TweenCallback(Callable.From(() => _waterEntryTween = null));
        }

        internal void PlayEarthSummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Earth))
                return;

            KillTween(ref _earthEntryTween);
            KillTween(ref _earthExitTween);
            _fragments.Visible = true;
            _fragments.Scale = Vector2.One;
            _fragments.Position = Vector2.Zero;
            _earthMaterial.SetShaderParameter("state_alpha", 1f);
            _earthMaterial.SetShaderParameter("summon_progress", 0f);
            _earthMaterial.SetShaderParameter("summon_hold", 0f);
            _earthEntryTween = _root.CreateTween();
            // A crack opens, the stones heave up through it, the construct holds, then
            // it splits back into the resting three. All four beats are segments of
            // summon_progress inside the shader, so this is one linear drive: easing it
            // would slide those boundaries around and blur the prelude into the heave.
            // Nothing here moves a node — the stones start below the shader's contact
            // line and are revealed by its ground clip, not by a position.
            _earthEntryTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("summon_progress", value)),
                0f, 1f, SummonDuration);
            _earthEntryTween.TweenCallback(Callable.From(() => _earthEntryTween = null));
        }

        /// <summary>
        /// Gathers the resting stones into a low wall in front of the character, holds
        /// it, then breaks it apart.
        /// </summary>
        /// <remarks>
        /// Earth is the one element that needs restart handling. It has no counter, so
        /// each earth card played under the state triggers, and consecutive plays land
        /// inside this beat routinely: the previous tween has to die before the next one
        /// drives the same uniform, or the wall visibly snaps back mid-formation.
        /// <para>
        /// Also the one trigger with no projectile. Fire flies to enemies, wind to the
        /// hand, water to the energy counter, because each of those pays out somewhere
        /// else on screen; Block lands on the character, so the wall in the slot is the
        /// whole statement and no combat-room or UI geometry is needed.
        /// </para>
        /// </remarks>
        internal void PlayEarthTrigger()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root) || !SakuraModConfig.IsCardVfxEnabled())
                return;

            KillTween(ref _earthTriggerTween);
            // Sampled once per beat, so a standee turning mid-formation cannot mirror a
            // wall that is already forming.
            _earthMaterial.SetShaderParameter("facing", ResolveFacingSign());
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthTriggerTween = _root.CreateTween();
            // Linear, for the same reason the summon is: gather, snap, hold and shatter
            // are fixed spans of trigger_progress in the shader, and the dead hold is
            // exactly what an ease would smear.
            _earthTriggerTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("trigger_progress", value)),
                0f, 1f, EarthTriggerDuration);
            // Settling at 0 rather than 1 is what makes a kill safe at any point: the
            // wall can never be left standing half-formed.
            _earthTriggerTween.TweenCallback(Callable.From(() =>
            {
                _earthMaterial.SetShaderParameter("trigger_progress", 0f);
                _earthTriggerTween = null;
            }));
        }

        internal void PlayWaterTrigger()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root) || !SakuraModConfig.IsCardVfxEnabled())
                return;

            _waterMaterial.SetShaderParameter("trigger_progress", 0f);
            var triggerTween = _root.CreateTween();
            triggerTween.TweenMethod(Callable.From<float>(value =>
                    _waterMaterial.SetShaderParameter("trigger_progress", value)),
                0f, 1f, WaterTriggerDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);

            if (NCombatRoom.Instance is not { } room || ResolveEnergyTarget() is not { } target)
                return;

            AddWaterDrop(room, _waterSlot.GlobalPosition, target);
        }

        /// <summary>
        /// Locates the live energy counter purely as a presentation anchor. A miss
        /// only costs the flying droplet: the in-slot merge still plays and the
        /// energy gain is never gated on UI geometry.
        /// </summary>
        private static Vector2? ResolveEnergyTarget()
        {
            if (NCombatRoom.Instance?.Ui is not { } ui
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

        private static void AddWaterDrop(NCombatRoom room, Vector2 origin, Vector2 target)
        {
            var root = new Node2D
            {
                Name = "SakuraWateryTriggerDrop",
                GlobalPosition = origin,
                ZIndex = 20,
                ZAsRelative = false
            };
            room.CombatVfxContainer.AddChildSafely(root);
            var delta = target - origin;
            var arcHeight = Mathf.Clamp(delta.Length() * 0.14f, 20f, 78f);
            var control = delta * 0.5f + Vector2.Up * arcHeight;
            var path = BezierPoints(control, delta, 14);
            var trail = new Line2D
            {
                Width = 4.5f,
                DefaultColor = new Color(0.42f, 0.76f, 0.96f, 0.6f),
                Antialiased = true,
                Points = [Vector2.Zero, Vector2.Zero]
            };
            var drop = new Polygon2D
            {
                Color = new Color(0.62f, 0.88f, 1f, 0.95f),
                Polygon = CirclePoints(7.5f, 10)
            };
            // The rim is what separates water from a blue glow on a dark stage, so
            // the flying drop keeps the same white edge the shader gives the pair.
            var rim = new Line2D
            {
                Width = 1.6f,
                DefaultColor = new Color(0.92f, 0.99f, 1f, 0.9f),
                Antialiased = true,
                Closed = true,
                Points = CirclePoints(7.5f, 10)
            };
            root.AddChild(trail);
            root.AddChild(drop);
            root.AddChild(rim);

            var tween = root.CreateTween().SetParallel();
            tween.TweenMethod(Callable.From<float>(progress =>
            {
                var point = QuadraticBezier(Vector2.Zero, control, delta, progress);
                drop.Position = point;
                rim.Position = point;
                trail.Points = TrailPoints(path, progress);
            }),
                0f, 1f, WaterTriggerDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            // Surface tension: the drop overshoots as it leaves, then settles.
            tween.TweenProperty(drop, "scale", Vector2.One * 1.22f, WaterTriggerDuration * 0.26f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(drop, "scale", Vector2.One * 0.86f, WaterTriggerDuration * 0.5f)
                .SetDelay(WaterTriggerDuration * 0.26f)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(rim, "scale", Vector2.One * 1.22f, WaterTriggerDuration * 0.26f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(rim, "scale", Vector2.One * 0.86f, WaterTriggerDuration * 0.5f)
                .SetDelay(WaterTriggerDuration * 0.26f)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(trail, "modulate:a", 0f, WaterTriggerDuration * 0.42f)
                .SetDelay(WaterTriggerDuration * 0.58f);
            tween.TweenProperty(drop, "modulate:a", 0f, WaterTriggerDuration * 0.24f)
                .SetDelay(WaterTriggerDuration * 0.76f);
            tween.TweenProperty(rim, "modulate:a", 0f, WaterTriggerDuration * 0.24f)
                .SetDelay(WaterTriggerDuration * 0.76f);
            tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        }

        internal void PlayTrigger(IReadOnlyList<Creature> targets)
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root) || !SakuraModConfig.IsCardVfxEnabled())
                return;

            _material.SetShaderParameter("trigger_progress", 0f);
            var triggerTween = _root.CreateTween();
            triggerTween.TweenMethod(Callable.From<float>(value =>
                    _material.SetShaderParameter("trigger_progress", value)),
                0f, 1f, TriggerDuration)
                .SetEase(Tween.EaseType.Out);

            if (NCombatRoom.Instance is not { } room
                || room.GetCreatureNode(_creature) is null)
            {
                return;
            }

            var origin = _fireSlot.GlobalPosition;
            foreach (var target in targets)
            {
                if (room.GetCreatureNode(target) is not { } targetNode)
                    continue;
                AddSpark(room, origin, targetNode.Visuals.VfxSpawnPosition.GlobalPosition);
            }
        }

        private static void AddSpark(NCombatRoom room, Vector2 origin, Vector2 target)
        {
            var root = new Node2D
            {
                Name = "SakuraFireyTriggerSpark",
                GlobalPosition = origin,
                ZIndex = 20,
                ZAsRelative = false
            };
            room.CombatVfxContainer.AddChildSafely(root);
            var delta = target - origin;
            var arcHeight = Mathf.Clamp(delta.Length() * 0.12f, 18f, 70f);
            var control = delta * 0.5f + Vector2.Up * arcHeight;
            var path = BezierPoints(control, delta, 14);
            var trail = new Line2D
            {
                Width = 4f,
                DefaultColor = new Color(1f, 0.2f, 0.035f, 0.72f),
                Antialiased = true,
                Points = [Vector2.Zero, Vector2.Zero]
            };
            var spark = new Polygon2D
            {
                Color = new Color(1f, 0.1f, 0.025f, 0.98f),
                Polygon = CirclePoints(FireTriggerSparkRadius, 8)
            };
            root.AddChild(trail);
            root.AddChild(spark);
            var tween = root.CreateTween().SetParallel();
            tween.TweenMethod(Callable.From<float>(progress =>
            {
                spark.Position = QuadraticBezier(Vector2.Zero, control, delta, progress);
                trail.Points = TrailPoints(path, progress);
            }),
                0f, 1f, TriggerDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(spark, "scale", Vector2.One * 1.18f, TriggerDuration * 0.28f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(spark, "scale", Vector2.One, TriggerDuration * 0.5f)
                .SetDelay(TriggerDuration * 0.28f)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(trail, "modulate:a", 0f, TriggerDuration * 0.45f)
                .SetDelay(TriggerDuration * 0.55f);
            tween.TweenProperty(spark, "modulate:a", 0f, TriggerDuration * 0.25f)
                .SetDelay(TriggerDuration * 0.75f);
            tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        }

        private void PlayQuickReveal()
        {
            KillTween(ref _entryTween);
            _ember.Visible = true;
            _ember.Scale = Vector2.One * 0.64f;
            SetStateAlpha(0f);
            _entryTween = _root.CreateTween().SetParallel();
            _entryTween.TweenMethod(Callable.From<float>(SetStateAlpha), 0f, 1f, QuickRevealDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            _entryTween.TweenProperty(_ember, "scale", Vector2.One, QuickRevealDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            _entryTween.Chain().TweenCallback(Callable.From(() => _entryTween = null));
        }

        private void PlayDismiss()
        {
            KillTween(ref _entryTween);
            KillTween(ref _exitTween);
            _exitTween = _root.CreateTween().SetParallel();
            _exitTween.TweenMethod(Callable.From<float>(value =>
                    _material.SetShaderParameter("state_alpha", value)),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
            _exitTween.TweenProperty(_ember, "position", new Vector2(0f, -18f), DismissDuration)
                .SetEase(Tween.EaseType.Out);
            _exitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _ember.Position = Vector2.Zero;
                _ember.Visible = false;
                _exitTween = null;
            }));
        }

        private void PlayWindQuickReveal()
        {
            KillTween(ref _windEntryTween);
            _currents.Visible = true;
            _currents.Scale = Vector2.One;
            _currents.Position = Vector2.Zero;
            _windMaterial.SetShaderParameter("summon_progress", 1f);
            SetWindStateAlpha(0f);
            _windEntryTween = _root.CreateTween();
            _windEntryTween.TweenMethod(Callable.From<float>(SetWindStateAlpha), 0f, 1f, QuickRevealDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _windEntryTween.TweenCallback(Callable.From(() => _windEntryTween = null));
        }

        private void PlayWindDismiss()
        {
            KillTween(ref _windEntryTween);
            KillTween(ref _windExitTween);
            _windExitTween = _root.CreateTween().SetParallel();
            _windExitTween.TweenMethod(Callable.From<float>(value =>
                    _windMaterial.SetShaderParameter("state_alpha", value)),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // Wind thins outward instead of drifting up the way the ember does.
            _windExitTween.TweenProperty(_currents, "scale", new Vector2(1.22f, 0.68f), DismissDuration)
                .SetEase(Tween.EaseType.Out);
            _windExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _currents.Scale = Vector2.One;
                _currents.Visible = false;
                _windExitTween = null;
            }));
        }

        private void PlayWaterQuickReveal()
        {
            KillTween(ref _waterEntryTween);
            _droplets.Visible = true;
            _droplets.Scale = Vector2.One;
            _droplets.Position = Vector2.Zero;
            _waterMaterial.SetShaderParameter("summon_progress", 1f);
            SetWaterStateAlpha(0f);
            _waterEntryTween = _root.CreateTween();
            _waterEntryTween.TweenMethod(Callable.From<float>(SetWaterStateAlpha), 0f, 1f, QuickRevealDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _waterEntryTween.TweenCallback(Callable.From(() => _waterEntryTween = null));
        }

        private void PlayWaterDismiss()
        {
            KillTween(ref _waterEntryTween);
            KillTween(ref _waterExitTween);
            _waterExitTween = _root.CreateTween().SetParallel();
            _waterExitTween.TweenMethod(Callable.From<float>(value =>
                    _waterMaterial.SetShaderParameter("state_alpha", value)),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // Water runs down and spreads as it goes, where the ember drifts up and
            // wind thins sideways. Each element leaves by its own logic.
            _waterExitTween.TweenProperty(_droplets, "scale", new Vector2(1.14f, 0.76f), DismissDuration)
                .SetEase(Tween.EaseType.Out);
            _waterExitTween.TweenProperty(_droplets, "position", new Vector2(0f, 9f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _waterExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _droplets.Scale = Vector2.One;
                _droplets.Position = Vector2.Zero;
                _droplets.Visible = false;
                _waterExitTween = null;
            }));
        }

        private void PlayEarthQuickReveal()
        {
            KillTween(ref _earthEntryTween);
            _fragments.Visible = true;
            _fragments.Scale = Vector2.One;
            _fragments.Position = Vector2.Zero;
            _earthMaterial.SetShaderParameter("summon_progress", 1f);
            SetEarthStateAlpha(0f);
            _earthEntryTween = _root.CreateTween();
            _earthEntryTween.TweenMethod(Callable.From<float>(SetEarthStateAlpha), 0f, 1f, QuickRevealDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _earthEntryTween.TweenCallback(Callable.From(() => _earthEntryTween = null));
        }

        private void PlayEarthDismiss()
        {
            KillTween(ref _earthEntryTween);
            KillTween(ref _earthExitTween);
            // A live wall would otherwise keep driving the field while the mark fades.
            KillTween(ref _earthTriggerTween);
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthExitTween = _root.CreateTween().SetParallel();
            _earthExitTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("state_alpha", value)),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // Stone sinks and settles wider as it goes: the ember drifts up, wind thins
            // sideways, water runs down. Each element leaves by its own logic, and only
            // earth leaves by going back into the ground it came out of.
            _earthExitTween.TweenProperty(_fragments, "scale", new Vector2(1.04f, 0.82f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _earthExitTween.TweenProperty(_fragments, "position", new Vector2(0f, 5f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _earthExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _fragments.Scale = Vector2.One;
                _fragments.Position = Vector2.Zero;
                _fragments.Visible = false;
                _earthExitTween = null;
            }));
        }

        private void SetStateAlpha(float value) =>
            _material.SetShaderParameter("state_alpha", Mathf.Clamp(value, 0f, 1f));

        private void SetWindStateAlpha(float value) =>
            _windMaterial.SetShaderParameter("state_alpha", Mathf.Clamp(value, 0f, 1f));

        private void SetWaterStateAlpha(float value) =>
            _waterMaterial.SetShaderParameter("state_alpha", Mathf.Clamp(value, 0f, 1f));

        private void SetEarthStateAlpha(float value) =>
            _earthMaterial.SetShaderParameter("state_alpha", Mathf.Clamp(value, 0f, 1f));

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
            States.Remove(_creature);
            if (GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
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
            _creature.Died -= OnDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _root.TreeExiting -= OnTreeExiting;
            KillTween(ref _entryTween);
            KillTween(ref _exitTween);
            KillTween(ref _windEntryTween);
            KillTween(ref _windExitTween);
            KillTween(ref _waterEntryTween);
            KillTween(ref _waterExitTween);
            KillTween(ref _earthEntryTween);
            KillTween(ref _earthExitTween);
            KillTween(ref _earthTriggerTween);
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current && current.IsValid())
                current.Kill();
            tween = null;
        }
    }

    private static bool IsElementPower(PowerModel power) => power is
        ClassicEarthyPower or ClassicFireyPower or ClassicWateryPower or ClassicWindyPower
        or ClassicEarthyPermanentPower or ClassicFireyPermanentPower
        or ClassicWateryPermanentPower or ClassicWindyPermanentPower;

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

    private static Vector2[] BezierPoints(Vector2 control, Vector2 end, int count)
    {
        var points = new Vector2[count];
        for (var index = 0; index < count; index++)
            points[index] = QuadraticBezier(Vector2.Zero, control, end, index / (float)(count - 1));
        return points;
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        var inverse = 1f - progress;
        return inverse * inverse * start
            + 2f * inverse * progress * control
            + progress * progress * end;
    }

    private static Vector2[] TrailPoints(IReadOnlyList<Vector2> path, float progress)
    {
        var end = Math.Clamp((int)MathF.Round(progress * (path.Count - 1)), 1, path.Count - 1);
        var start = Math.Max(0, end - 4);
        var points = new Vector2[end - start + 1];
        for (var index = 0; index < points.Length; index++)
            points[index] = path[start + index];
        return points;
    }
}
