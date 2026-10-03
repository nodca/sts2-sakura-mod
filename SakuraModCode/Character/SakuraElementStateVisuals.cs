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
    Vector2 Water,
    Vector2 FireAmbient,
    Vector2 WindAmbient,
    Vector2 WaterAmbient)
{
    /// <summary>
    /// The hitbox floor sits below the standee's visible feet, level with the HP/Block
    /// strip. Anything that has to stand on the ground the player sees — the earth
    /// cluster and the ambient layers rooted at the feet — stands on this line instead,
    /// which also keeps it above that strip. One shared lift, so every ground-rooted
    /// mark agrees on where the ground is.
    /// </summary>
    private const float GroundVisualLift = 44f;
    /// <summary>
    /// Gap between the hitbox's right edge and the earth cluster's axis. The HP bar is
    /// as wide as the hitbox, so measuring from that edge keeps the cluster beside the
    /// standee's legs at every outfit width, where a fraction of a clamped width could
    /// not: outfit hitboxes run from about 230 to 330 pixels wide.
    /// </summary>
    private const float EarthEdgeClearance = 4f;

    /// <summary>
    /// Fire owns the centre axis above the head. Wind and water hover in the open air to
    /// the left, which is the one side of the standee that is dark and empty at chest
    /// height and above: the wings fall to the lower left and the staff stands to the
    /// right. Wind takes the upper of the two, beside the head, because it is the
    /// lightest; water sits lower, at the chest, still clear above Kero's waist-height
    /// perch. Neither crosses the body any more — the old wind mark did, and against the
    /// white costume it vanished.
    ///
    /// Earth is rooted on the visual ground just past the hitbox's right edge. It is a
    /// tall cluster, so beside the legs is the only place it is not drawn over the
    /// standee, and the ground line keeps it clear of the HP/Block strip below.
    ///
    /// The ambient layers belong to places, not to spirits: fire's embers and water's
    /// rings sit on the visual ground, wind's gusts cross the chest. Water's rings take
    /// the left of the floor and earth's cracks the right, so the two ground layers never
    /// share space.
    ///
    /// Nothing flips with the standee. Position is read once when the slot is laid
    /// out, and <see cref="CelVfxGeometry.CasterAnchor.FacingSign"/> is deliberately not
    /// consulted: it is republished every frame by the idle controllers' SyncFlip, so a
    /// mirrored slot would make persistent marks jump across the character mid-combat.
    /// Only a one-shot beat may face — earth's wall reads facing when it starts and
    /// mirrors inside its own shader.
    ///
    /// Ground-rooted slots are measured from the floor instead of from body height. A
    /// fraction of body height cannot express "on the floor": the mount point's own
    /// height above the ground is not knowable from body size, so any such fraction is
    /// a guess. The caster anchor already carries the real floor, and
    /// <paramref name="floorY"/> is it.
    /// </summary>
    /// <param name="bodySize">
    /// Hitbox size. The vertical slots clamp it; earth reads the raw width, because it
    /// has to clear the real HP bar, which is exactly that wide.
    /// </param>
    /// <param name="bodyCentreX">
    /// Hitbox centre in the same local space as the returned slots. The mount point is
    /// not guaranteed to sit on it, and earth measures from the hitbox's edge.
    /// </param>
    /// <param name="floorY">
    /// Floor position in the same local space as the returned slots, i.e. the caster
    /// anchor's floor converted into the visuals root.
    /// </param>
    /// <param name="earthContactInset">
    /// Distance from the earth rect's centre down to the contact line its shader roots
    /// the cluster on. The slot is raised by this much so that line lands on the
    /// visual ground.
    /// </param>
    internal static SakuraElementSlotLayout FromBody(
        Vector2 bodySize,
        float bodyCentreX,
        float floorY,
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
        var visualGroundY = groundY - GroundVisualLift;
        var hitboxRight = bodyCentreX + Math.Max(bodySize.X, 100f) * 0.5f;
        return new(
            Fire: new(0f, -height * 0.58f),
            Wind: new(-width * 0.62f, -height * 0.52f),
            Earth: new(hitboxRight + EarthEdgeClearance, visualGroundY - earthContactInset),
            Water: new(-width * 0.44f, -height * 0.36f),
            FireAmbient: new(bodyCentreX, visualGroundY),
            WindAmbient: new(bodyCentreX, -height * 0.28f),
            WaterAmbient: new(bodyCentreX - width * 0.42f, visualGroundY));
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
    internal const string FireAmbientShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_firey_ambient.gdshader";
    internal const string WindAmbientShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_windy_ambient.gdshader";
    internal const string WaterAmbientShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_watery_ambient.gdshader";
    internal const string CommonShaderIncludePath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_element_state_common.gdshaderinc";

    private const string RootName = "SakuraElementStateVisuals";
    private const string FireSlotName = "FireSlot";
    private const string FireyEmberName = "FireyEmber";
    private const string WindSlotName = "WindSlot";
    private const string WindySpiritName = "WindySpirit";
    private const string WaterSlotName = "WaterSlot";
    private const string WaterySpiritName = "WaterySpirit";
    private const string EarthSlotName = "EarthSlot";
    private const string EarthySpireName = "EarthySpire";
    private const string FireAmbientSlotName = "FireAmbientSlot";
    private const string FireyAmbientName = "FireyAmbient";
    private const string WindAmbientSlotName = "WindAmbientSlot";
    private const string WindyAmbientName = "WindyAmbient";
    private const string WaterAmbientSlotName = "WaterAmbientSlot";
    private const string WateryAmbientName = "WateryAmbient";
    private const float QuickRevealDuration = 0.3f;
    private const float SummonDuration = 0.98f;
    private const float DismissDuration = 0.24f;
    /// <summary>
    /// Resting strength of each ambient layer. Up to four states can be up at once,
    /// so these stay faint: the spirits are what has to read, the ambient only says
    /// the air has changed. Entry and trigger lift them briefly through
    /// <c>ambient_boost</c> and they settle back here.
    /// </summary>
    private const float FireAmbientRestAlpha = 0.42f;
    private const float EarthAmbientRestAlpha = 0.5f;
    /// <summary>Wind's gust is already intermittent, so it can rest a little stronger.</summary>
    private const float WindAmbientRestAlpha = 0.55f;
    private const float WaterAmbientRestAlpha = 0.45f;
    private const float AmbientBoostRise = 0.12f;
    private const float AmbientBoostFall = 0.62f;
    /// <summary>Entering a state is the bigger statement; a trigger is a reminder.</summary>
    private const float EntryAmbientBoost = 1f;
    private const float TriggerAmbientBoost = 0.6f;
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
    /// Where the earth shader roots its cluster, as a fraction of the region height
    /// measured down from the rect's centre. Must stay equal to <c>CONTACT_Y</c> in
    /// <c>sakura_element_state_earthy.gdshader</c>: the slot is raised by this distance
    /// so the drawn contact line lands on the visual ground, and the two drifting apart
    /// floats the cluster.
    /// </summary>
    private const float EarthContactSurfaceFraction = 0.36f;
    private const int MaxTriggerTargets = 8;
    private const int WindStreamCount = 3;

    private static readonly ConditionalWeakTable<Creature, State> States = new();

    internal static IEnumerable<string> AssetPaths =>
        [
            ScenePath, ShaderPath, WindShaderPath, WaterShaderPath, EarthShaderPath,
            FireAmbientShaderPath, WindAmbientShaderPath, WaterAmbientShaderPath,
            CommonShaderIncludePath
        ];

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
            // Stay in the creature's draw order so combat UI and card grids cover the marks.
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
        private readonly ColorRect _windSpirit;
        private readonly ShaderMaterial _windMaterial;
        private readonly Node2D _waterSlot;
        private readonly ColorRect _waterSpirit;
        private readonly ShaderMaterial _waterMaterial;
        private readonly Node2D _earthSlot;
        private readonly ColorRect _spire;
        private readonly ShaderMaterial _earthMaterial;
        private readonly Node2D _fireAmbientSlot;
        private readonly ColorRect _fireAmbient;
        private readonly ShaderMaterial _fireAmbientMaterial;
        private readonly Node2D _windAmbientSlot;
        private readonly ColorRect _windAmbient;
        private readonly ShaderMaterial _windAmbientMaterial;
        private readonly Node2D _waterAmbientSlot;
        private readonly ColorRect _waterAmbient;
        private readonly ShaderMaterial _waterAmbientMaterial;
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
        /// <summary>
        /// Ambient boosts are held for the same reason as <see cref="_earthTriggerTween"/>:
        /// a trigger can land while the entry boost is still falling, and two tweens on
        /// one <c>ambient_boost</c> would fight over it.
        /// </summary>
        private Tween? _fireBoostTween;
        private Tween? _earthBoostTween;
        private Tween? _windBoostTween;
        private Tween? _waterBoostTween;
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
            _windSpirit = root.GetNode<ColorRect>($"{WindSlotName}/{WindySpiritName}");
            _windMaterial = _windSpirit.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura wind state VFX requires a ShaderMaterial.");
            _windSpirit.Material = _windMaterial;
            _waterSlot = root.GetNode<Node2D>(WaterSlotName);
            _waterSpirit = root.GetNode<ColorRect>($"{WaterSlotName}/{WaterySpiritName}");
            _waterMaterial = _waterSpirit.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura water state VFX requires a ShaderMaterial.");
            _waterSpirit.Material = _waterMaterial;
            _earthSlot = root.GetNode<Node2D>(EarthSlotName);
            _spire = root.GetNode<ColorRect>($"{EarthSlotName}/{EarthySpireName}");
            _earthMaterial = _spire.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura earth state VFX requires a ShaderMaterial.");
            _spire.Material = _earthMaterial;
            _fireAmbientSlot = root.GetNode<Node2D>(FireAmbientSlotName);
            _fireAmbient = root.GetNode<ColorRect>($"{FireAmbientSlotName}/{FireyAmbientName}");
            _fireAmbientMaterial = _fireAmbient.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Sakura fire ambient VFX requires a ShaderMaterial.");
            _fireAmbient.Material = _fireAmbientMaterial;
            (_windAmbientSlot, _windAmbient, _windAmbientMaterial) =
                ResolveAmbient(root, WindAmbientSlotName, WindyAmbientName);
            (_waterAmbientSlot, _waterAmbient, _waterAmbientMaterial) =
                ResolveAmbient(root, WaterAmbientSlotName, WateryAmbientName);
        }

        private static (Node2D Slot, ColorRect Rect, ShaderMaterial Material) ResolveAmbient(
            Node2D root, string slotName, string rectName)
        {
            var slot = root.GetNode<Node2D>(slotName);
            var rect = root.GetNode<ColorRect>($"{slotName}/{rectName}");
            var material = rect.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException($"Sakura {rectName} VFX requires a ShaderMaterial.");
            rect.Material = material;
            return (slot, rect, material);
        }

        internal void Start()
        {
            var geometry = CelVfxGeometry.ResolveCaster(_creatureNode)
                ?? throw new InvalidOperationException("Sakura fire state VFX requires caster geometry.");
            // The anchor's floor is global; slot positions are local to this root.
            var floorY = _root.ToLocal(geometry.Floor).Y;
            var bodyCentreX = _root.ToLocal(geometry.BodyCenter).X;
            var layout = SakuraElementSlotLayout.FromBody(
                geometry.BodySize,
                bodyCentreX,
                floorY,
                _spire.Size.Y * EarthContactSurfaceFraction);
            _fireSlot.Position = layout.Fire;
            _windSlot.Position = layout.Wind;
            _earthSlot.Position = layout.Earth;
            _waterSlot.Position = layout.Water;
            _fireAmbientSlot.Position = layout.FireAmbient;
            _windAmbientSlot.Position = layout.WindAmbient;
            _waterAmbientSlot.Position = layout.WaterAmbient;
            _material.SetShaderParameter("region_size", _ember.Size);
            _material.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _material.SetShaderParameter("state_alpha", 0f);
            _material.SetShaderParameter("summon_progress", 1f);
            _material.SetShaderParameter("summon_hold", 0f);
            _material.SetShaderParameter("trigger_progress", 0f);
            _windMaterial.SetShaderParameter("region_size", _windSpirit.Size);
            _windMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _windMaterial.SetShaderParameter("state_alpha", 0f);
            _windMaterial.SetShaderParameter("summon_progress", 1f);
            _windMaterial.SetShaderParameter("summon_hold", 0f);
            _windMaterial.SetShaderParameter("trigger_progress", 0f);
            _waterMaterial.SetShaderParameter("region_size", _waterSpirit.Size);
            _waterMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _waterMaterial.SetShaderParameter("state_alpha", 0f);
            _waterMaterial.SetShaderParameter("summon_progress", 1f);
            _waterMaterial.SetShaderParameter("summon_hold", 0f);
            _waterMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("region_size", _spire.Size);
            _earthMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _earthMaterial.SetShaderParameter("state_alpha", 0f);
            _earthMaterial.SetShaderParameter("summon_progress", 1f);
            _earthMaterial.SetShaderParameter("summon_hold", 0f);
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("facing", ResolveFacingSign());
            // Every uniform a tween will drive is written once here first: a ShaderMaterial
            // reports a never-set uniform as Nil, and tweening from Nil crashes.
            _earthMaterial.SetShaderParameter("ambient_alpha", 0f);
            _earthMaterial.SetShaderParameter("ambient_boost", 0f);
            _fireAmbientMaterial.SetShaderParameter("region_size", _fireAmbient.Size);
            _fireAmbientMaterial.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
            _fireAmbientMaterial.SetShaderParameter("state_alpha", 0f);
            _fireAmbientMaterial.SetShaderParameter("ambient_alpha", 0f);
            _fireAmbientMaterial.SetShaderParameter("ambient_boost", 0f);
            foreach (var (rect, material) in new[]
                     {
                         (_windAmbient, _windAmbientMaterial),
                         (_waterAmbient, _waterAmbientMaterial)
                     })
            {
                material.SetShaderParameter("region_size", rect.Size);
                material.SetShaderParameter("seed", Random.Shared.NextSingle() * 6.1f);
                material.SetShaderParameter("state_alpha", 0f);
                material.SetShaderParameter("ambient_alpha", 0f);
                material.SetShaderParameter("ambient_boost", 0f);
            }

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
            ApplyAmbientPreference();

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
                {
                    PlayQuickReveal();
                    PlayAmbientBoost(_fireAmbientMaterial, ref _fireBoostTween, EntryAmbientBoost);
                }
                else
                {
                    SetStateAlpha(1f);
                }
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
                {
                    PlayWindQuickReveal();
                    PlayAmbientBoost(_windAmbientMaterial, ref _windBoostTween, EntryAmbientBoost);
                }
                else
                {
                    SetWindStateAlpha(1f);
                }
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
                {
                    PlayWaterQuickReveal();
                    PlayAmbientBoost(_waterAmbientMaterial, ref _waterBoostTween, EntryAmbientBoost);
                }
                else
                {
                    SetWaterStateAlpha(1f);
                }
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
                {
                    PlayEarthQuickReveal();
                    PlayAmbientBoost(_earthMaterial, ref _earthBoostTween, EntryAmbientBoost);
                }
                else
                {
                    SetEarthStateAlpha(1f);
                }
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
            SetStateAlpha(1f);
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
            PlayAmbientBoost(_fireAmbientMaterial, ref _fireBoostTween, EntryAmbientBoost);
        }

        internal void PlayWindSummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Wind))
                return;

            KillTween(ref _windEntryTween);
            KillTween(ref _windExitTween);
            _windSpirit.Visible = true;
            _windSpirit.Scale = Vector2.One;
            _windSpirit.Position = Vector2.Zero;
            SetWindStateAlpha(1f);
            _windMaterial.SetShaderParameter("summon_progress", 0f);
            _windMaterial.SetShaderParameter("summon_hold", 0f);
            _windEntryTween = _root.CreateTween();
            // The vortex gathers in from wide and the ribbons are flung out last; the
            // shader reads summon_progress for both, so no node position is reset.
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
            PlayAmbientBoost(_windAmbientMaterial, ref _windBoostTween, EntryAmbientBoost);
        }

        internal void PlayWindTrigger()
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root) || !SakuraModConfig.IsCardVfxEnabled())
                return;

            _windMaterial.SetShaderParameter("trigger_progress", 0f);
            PlayAmbientBoost(_windAmbientMaterial, ref _windBoostTween, TriggerAmbientBoost);
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
            _waterSpirit.Visible = true;
            _waterSpirit.Scale = Vector2.One;
            _waterSpirit.Position = Vector2.Zero;
            SetWaterStateAlpha(1f);
            _waterMaterial.SetShaderParameter("summon_progress", 0f);
            _waterMaterial.SetShaderParameter("summon_hold", 0f);
            _waterEntryTween = _root.CreateTween();
            // Scattered drops draw in and fuse into the spirit's body; the shader reads
            // summon_progress as both the gather distance and the body's own mass, so
            // nothing here repositions a node.
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
            PlayAmbientBoost(_waterAmbientMaterial, ref _waterBoostTween, EntryAmbientBoost);
        }

        internal void PlayEarthSummon()
        {
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            if (!SakuraElementState.ReadActive(_player).HasElement(SakuraElement.Earth))
                return;

            KillTween(ref _earthEntryTween);
            KillTween(ref _earthExitTween);
            _spire.Visible = true;
            _spire.Scale = Vector2.One;
            _spire.Position = Vector2.Zero;
            _earthMaterial.SetShaderParameter("state_alpha", 1f);
            _earthMaterial.SetShaderParameter("summon_progress", 0f);
            _earthMaterial.SetShaderParameter("summon_hold", 0f);
            _earthEntryTween = _root.CreateTween();
            // The cracks open, then the shards heave up through them one by one and
            // settle. Every beat is a segment of summon_progress inside the shader, so
            // this is one linear drive: easing it would slide those boundaries around
            // and blur the cracks into the heave. Nothing here moves a node — the shards
            // start below the shader's contact line and are revealed by its ground clip.
            _earthEntryTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("summon_progress", value)),
                0f, 1f, SummonDuration);
            _earthEntryTween.TweenCallback(Callable.From(() => _earthEntryTween = null));
        }

        /// <summary>
        /// Raises a low wall out of the ground in front of the cluster, holds it, then
        /// breaks it apart.
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
            PlayAmbientBoost(_earthMaterial, ref _earthBoostTween, TriggerAmbientBoost);
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
            PlayAmbientBoost(_waterAmbientMaterial, ref _waterBoostTween, TriggerAmbientBoost);
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
            PlayAmbientBoost(_fireAmbientMaterial, ref _fireBoostTween, TriggerAmbientBoost);
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
            // Through SetStateAlpha so the embers leave with the flame.
            _exitTween.TweenMethod(Callable.From<float>(SetStateAlpha),
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
            _windSpirit.Visible = true;
            _windSpirit.Scale = Vector2.One;
            _windSpirit.Position = Vector2.Zero;
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
            _windExitTween.TweenMethod(Callable.From<float>(SetWindStateAlpha),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // Wind thins outward instead of drifting up the way the ember does.
            _windExitTween.TweenProperty(_windSpirit, "scale", new Vector2(1.22f, 0.68f), DismissDuration)
                .SetEase(Tween.EaseType.Out);
            _windExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _windSpirit.Scale = Vector2.One;
                _windSpirit.Visible = false;
                _windExitTween = null;
            }));
        }

        private void PlayWaterQuickReveal()
        {
            KillTween(ref _waterEntryTween);
            _waterSpirit.Visible = true;
            _waterSpirit.Scale = Vector2.One;
            _waterSpirit.Position = Vector2.Zero;
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
            _waterExitTween.TweenMethod(Callable.From<float>(SetWaterStateAlpha),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // Water runs down and spreads as it goes, where the ember drifts up and
            // wind thins sideways. Each element leaves by its own logic.
            _waterExitTween.TweenProperty(_waterSpirit, "scale", new Vector2(1.14f, 0.76f), DismissDuration)
                .SetEase(Tween.EaseType.Out);
            _waterExitTween.TweenProperty(_waterSpirit, "position", new Vector2(0f, 9f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _waterExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _waterSpirit.Scale = Vector2.One;
                _waterSpirit.Position = Vector2.Zero;
                _waterSpirit.Visible = false;
                _waterExitTween = null;
            }));
        }

        private void PlayEarthQuickReveal()
        {
            KillTween(ref _earthEntryTween);
            _spire.Visible = true;
            _spire.Scale = Vector2.One;
            _spire.Position = Vector2.Zero;
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
            KillTween(ref _earthBoostTween);
            _earthMaterial.SetShaderParameter("trigger_progress", 0f);
            _earthMaterial.SetShaderParameter("ambient_boost", 0f);
            _earthExitTween = _root.CreateTween().SetParallel();
            _earthExitTween.TweenMethod(Callable.From<float>(value =>
                    _earthMaterial.SetShaderParameter("state_alpha", value)),
                1f, 0f, DismissDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            // The cluster sinks back as it goes: the ember drifts up, wind thins sideways,
            // water runs down. Each element leaves by its own logic, and only earth leaves
            // by going back into the ground it came out of.
            _earthExitTween.TweenProperty(_spire, "scale", new Vector2(1.04f, 0.82f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _earthExitTween.TweenProperty(_spire, "position", new Vector2(0f, 5f), DismissDuration)
                .SetEase(Tween.EaseType.In);
            _earthExitTween.Chain().TweenCallback(Callable.From(() =>
            {
                _spire.Scale = Vector2.One;
                _spire.Position = Vector2.Zero;
                _spire.Visible = false;
                _earthExitTween = null;
            }));
        }

        private void SetStateAlpha(float value)
        {
            var alpha = Mathf.Clamp(value, 0f, 1f);
            _material.SetShaderParameter("state_alpha", alpha);
            _fireAmbientMaterial.SetShaderParameter("state_alpha", alpha);
        }

        /// <summary>
        /// Ambient layers are atmosphere, so they follow the optional card-VFX switch:
        /// with it off the spirits keep the state readable on their own and the ambient
        /// rests at zero. Re-read on every refresh, because the switch can change
        /// between combats' state changes without remounting.
        /// </summary>
        private void ApplyAmbientPreference()
        {
            var enabled = SakuraModConfig.IsCardVfxEnabled();
            _fireAmbient.Visible = enabled;
            _windAmbient.Visible = enabled;
            _waterAmbient.Visible = enabled;
            _fireAmbientMaterial.SetShaderParameter("ambient_alpha", enabled ? FireAmbientRestAlpha : 0f);
            _windAmbientMaterial.SetShaderParameter("ambient_alpha", enabled ? WindAmbientRestAlpha : 0f);
            _waterAmbientMaterial.SetShaderParameter("ambient_alpha", enabled ? WaterAmbientRestAlpha : 0f);
            _earthMaterial.SetShaderParameter("ambient_alpha", enabled ? EarthAmbientRestAlpha : 0f);
        }

        /// <summary>
        /// Lifts an ambient layer to <paramref name="peak"/> and lets it fall back to rest.
        /// Restarts cleanly when called again mid-fall.
        /// </summary>
        private void PlayAmbientBoost(ShaderMaterial material, ref Tween? tween, float peak)
        {
            KillTween(ref tween);
            if (_disposed || !SakuraModConfig.IsCardVfxEnabled())
                return;
            material.SetShaderParameter("ambient_boost", 0f);
            var boost = _root.CreateTween();
            boost.TweenMethod(Callable.From<float>(value =>
                    material.SetShaderParameter("ambient_boost", value)),
                0f, peak, AmbientBoostRise)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            boost.TweenMethod(Callable.From<float>(value =>
                    material.SetShaderParameter("ambient_boost", value)),
                peak, 0f, AmbientBoostFall)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            tween = boost;
        }

        private void SetWindStateAlpha(float value)
        {
            var alpha = Mathf.Clamp(value, 0f, 1f);
            _windMaterial.SetShaderParameter("state_alpha", alpha);
            _windAmbientMaterial.SetShaderParameter("state_alpha", alpha);
        }

        private void SetWaterStateAlpha(float value)
        {
            var alpha = Mathf.Clamp(value, 0f, 1f);
            _waterMaterial.SetShaderParameter("state_alpha", alpha);
            _waterAmbientMaterial.SetShaderParameter("state_alpha", alpha);
        }

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
            KillTween(ref _fireBoostTween);
            KillTween(ref _earthBoostTween);
            KillTween(ref _windBoostTween);
            KillTween(ref _waterBoostTween);
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
