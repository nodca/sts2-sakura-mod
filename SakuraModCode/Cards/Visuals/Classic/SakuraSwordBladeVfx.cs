using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>One blade or two crossing ones.</summary>
internal enum SwordMode
{
    Single,
    Dual
}

/// <summary>
/// The sword and twin-blade cel effect: a rigid weapon swung on a pivot, and the cut
/// it leaves behind two stepped frames later.
/// </summary>
/// <remarks>
/// Shared by three cards. <c>ClowSword</c> and <c>SakuraSword</c> pass identical
/// arguments — the user confirmed they share one effect, and their only difference is
/// how many targets gameplay hands over, which is not a visual difference.
/// <c>Blade</c> passes <see cref="SwordMode.Dual"/> and its hit count.
/// <para>
/// The weapon's motion lives entirely in node transforms, never in the shader clock.
/// That is Hail's rule: a rigid body's faces do not deform, so rotation and travel
/// belong to the transform, and the field stays still. It is also why this card's
/// shader declares no <c>elapsed</c> uniform at all.
/// </para>
/// <para>
/// The cut lags the blade rather than landing with it. Hail's cracks and Blaze's
/// flames appear on the hit frame; steel accumulates shear before material gives way,
/// and the classic drawn treatment lands the wound after the blade has passed. That
/// lag is this card's cheapest point of difference, so it is a fixed beat rather
/// than a tunable.
/// </para>
/// </remarks>
internal sealed class SakuraSwordBladeVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/sakura_sword_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/sakura_sword_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_sword_blade.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    // Beats, in seconds.
    private const float DrawDuration = 0.14f;
    private const float SwingDuration = 0.22f;
    private const float FadeDuration = 0.20f;

    /// <summary>
    /// The opening's length in stepped frames, and the beat in seconds it works out to.
    /// </summary>
    /// <remarks>
    /// Counted in frames rather than picked in seconds, because the opening is sampled
    /// onto the 12 Hz grid and only whole frames are ever drawn. Three is the floor: the
    /// first frame is the shut wound, and a damped opening needs two more to read as
    /// rising rather than as one abrupt width. The design's beat table said 0.16 s, which
    /// spans 1.92 frames — the cut would have appeared at 84% of its width and stopped
    /// there, so it never finished opening. That is the same class of mistake the
    /// shield's Nyquist bound guards against, caught here by the envelope contract below.
    /// </remarks>
    private const float CutOpenSteps = 3f;
    private const float CutDuration = CutOpenSteps / StepFrequency;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at two
    /// stepped frames. Tweens that must look frozen wait it out, because a hold stops
    /// shader time and not Godot's tween clock.
    /// </summary>
    private const float HoldDuration = 2f / StepFrequency;

    /// <summary>
    /// How long the cut waits after the blade has passed, in stepped frames. Two is
    /// the shortest lag that still reads as a separate event at 12 Hz; one frame
    /// reads as the same frame drawn late.
    /// </summary>
    private const float CutLagSteps = 2f;

    /// <summary>
    /// Twin-blade crossings all fit inside this, whatever the hit count. The envelope
    /// is fixed and only its density changes, so two crossings and four are visually
    /// distinguishable without four taking twice the wall time.
    /// </summary>
    private const float CrossingEnvelope = 0.30f;

    /// <summary>Serial offset between targets when one swing passes through several.</summary>
    private const float TargetStagger = 0.05f;

    /// <summary>
    /// Distance from the pivot to the target's centre, in pixels. The scene puts the
    /// grip 113 px out and the tip 436 px out, so at 340 the target sits about seven
    /// tenths along the blade — struck by the fast part of the edge rather than by the
    /// hilt or by empty air past the tip.
    /// </summary>
    /// <remarks>
    /// Grew with the blade. Left at its previous 250 the target would have sat 42 %
    /// along a longer edge, which puts the impact back toward the guard: the same
    /// reading as the missing-blade bug, arrived at by different means.
    /// </remarks>
    private const float SwingRadiusPx = 340f;

    /// <summary>
    /// Total sweep, in radians. About 66 degrees: wide enough that the tip visibly
    /// travels, narrow enough that the blade does not leave the target's neighbourhood
    /// and start reading as a windmill.
    /// </summary>
    private const float SwingSpanRadians = 1.15f;

    /// <summary>
    /// Tilt of the single-blade stroke away from straight down. A vertical chop has
    /// no lateral component to read the arc against.
    /// </summary>
    private const float SingleTiltRadians = 0.28f;

    /// <summary>
    /// Half-angle between the two converging blades, in radians. This mirrors the
    /// shader's <c>CUT_DEG</c> of 24 degrees on purpose: the X the blades trace has to
    /// be the X the cut shows, or the wound looks unrelated to the weapon that made it.
    /// </summary>
    private const float CrossTiltRadians = 0.419f;

    /// <summary>
    /// How far the blade slides in along its own axis while drawing, in pixels. Along
    /// the axis rather than across it, so the motion reads as being drawn from
    /// somewhere off-screen instead of being carried in sideways.
    /// </summary>
    private const float DrawSlidePx = 220f;

    /// <summary>
    /// Stepped afterimage frames per blade. Cel animation shows a fast object as a few
    /// discrete ghosts, not as continuous motion blur, so these are sampled on the
    /// same 12 Hz grid the rest of the family draws on.
    /// </summary>
    private const int AfterimageCount = 3;

    /// <summary>Opacity of the first afterimage; each further one takes this again.</summary>
    private const float AfterimageFalloff = 0.58f;
    private const float AfterimageLeadAlpha = 0.42f;

    /// <summary>
    /// Decay rate of the cut's opening, in reciprocal seconds. Critically damped, so
    /// the width rises to its full value and stops — a wound that oscillates would
    /// read as something still pushing on it. At 40 the opening is essentially
    /// complete inside <see cref="CutDuration"/>, which at 12 Hz means it lands in two
    /// drawn frames.
    /// </summary>
    private const float CutDecay = 40f;

    private const int FragmentCount = 5;
    private const float FragmentGravity = 980f;
    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly Node2D _debris;
    private readonly Dictionary<Creature, BladeVisual> _blades = [];
    private readonly SwordMode _mode;
    private readonly int _crossings;
    private bool _faded;

    private SakuraSwordBladeVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> creatures,
        SwordMode mode,
        int crossings)
        : base(root, room)
    {
        _mode = mode;
        _crossings = crossings;
        _debris = root.GetNode<Node2D>("%Debris");
        var blades = root.GetNode<Node2D>("%Blades");

        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (_blades.ContainsKey(creature))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, creature, index, Budget);
            _blades.Add(creature, new BladeVisual(targetScene, blades, geometry, index, mode));
        }
    }

    /// <summary>
    /// Budget for the cut, not for the weapon. The blade's own region is fixed at
    /// 168x380 by the guard-legibility gate and must not track enemy size: ink width
    /// is constant in screen pixels, so a sword scaled down to a small enemy stops
    /// reading as a sword. Only the wound belongs to the body it is in.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 12f,
        VerticalPadding: 12f,
        MinWidth: 160f,
        MinHeight: 140f,
        MaxWidth: 320f,
        MaxHeight: 280f,
        FallbackWidth: 240f,
        FallbackHeight: 200f,
        FloorClearance: 6f,
        MaxViewportWidthFraction: 0.24f,
        MaxViewportHeightFraction: 0.40f);

    protected override IEnumerable<ShaderMaterial> Materials =>
        _blades.Values.SelectMany(static blade => blade.Materials);

    /// <summary>
    /// Safety net, not a timer, and on wall clock because a hold decouples the two.
    /// Worst case is five enemies struck serially at four crossings each: roughly
    /// 2.8 s plus the shared prelude and the fade. Sized about threefold clear of
    /// that, because a cap set tight stops being a net and becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 9.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        SwordMode mode,
        Func<Cues, Task> resolveGameplay,
        int crossings = 1)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Sword blade",
            () => TryCreate(targets, mode, crossings),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<SakuraSwordBladeVfx> scope)
    {
        internal void Impact(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("impact", session => session.Impact(target));
        }
    }

    /// <summary>
    /// Prepares the effect, or returns null and leaves the gameplay path untouched.
    /// </summary>
    /// <param name="targets">Everything this play will strike, already resolved.</param>
    /// <param name="mode">One blade or two crossing ones.</param>
    /// <param name="crossings">
    /// How many times the twin blades cross. Read once from the card's resolved hit
    /// count; ignored for <see cref="SwordMode.Single"/>, which is one stroke by
    /// definition.
    /// </param>
    private static SakuraSwordBladeVfx? TryCreate(
        IReadOnlyList<Creature> targets,
        SwordMode mode,
        int crossings = 1)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return null;
        if (!TryPrepare("Sword blade", LoadScenes, out var room, out _, out var scenes))
            return null;

        // Clamped rather than trusted: the hit count comes from gameplay and can grow
        // with future powers, while the envelope is fixed. Past four crossings the
        // blades would cross faster than the stepped clock can draw, so extra hits
        // stop being visible instead of becoming a blur.
        var strokes = mode == SwordMode.Dual ? Math.Clamp(crossings, 1, 4) : 1;

        Node2D? root = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraSwordBladeVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new SakuraSwordBladeVfx(root, room, scenes.Target, targets, mode, strokes);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing it
            // is still empty.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            return null;
        }
    }

    /// <summary>
    /// Shared wand tap, magic circle, and speed lines, then the blade draws and swings
    /// through every target.
    /// </summary>
    /// <remarks>
    /// The swing finishes here rather than in <see cref="Impact"/>. The cut has to lag
    /// the blade, and <see cref="Impact"/> is called immediately before damage
    /// resolves, so the edge must already have passed by the time it runs — otherwise
    /// the damage number would land on the stroke instead of on the wound.
    /// </remarks>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!await PlayCelPrelude(card, caster))
            return false;

        TaskHelper.RunSafely(TrackAfterimages());

        var index = 0;
        foreach (var blade in _blades.Values)
        {
            var delay = index * TargetStagger;
            Track(blade.CreateDrawTween(DrawDuration, delay));
            Track(_mode == SwordMode.Dual
                ? blade.CreateCrossingTween(_crossings, CrossingEnvelope, DrawDuration + delay, BeginCrossingHold)
                : blade.CreateSwingTween(SwingDuration, DrawDuration + delay));
            index++;
        }

        var stroke = _mode == SwordMode.Dual ? CrossingEnvelope : SwingDuration;
        return await WaitActive(
            DrawDuration + stroke + Math.Max(0, _blades.Count - 1) * TargetStagger);
    }

    /// <summary>
    /// The wound opens on one target. The blade has already passed; this holds the
    /// drawn detail for two stepped frames, waits two more, then splits the body along
    /// the line the edge travelled and throws chips out of it.
    /// </summary>
    /// <remarks>
    /// Idempotent per target. <c>SakuraSword</c> deals two separate damages to each
    /// target and <c>ClowSword</c>'s activated path deals a third afterwards; a second
    /// wound would read as a second stroke that never happened, so later hits land
    /// inside the same cut. That extra damage reads as the cut deepening during the
    /// fade, which is what the beat table budgets for it.
    /// </remarks>
    private void Impact(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsActive() || !_blades.TryGetValue(target, out var blade) || blade.HasCut)
            return;

        blade.HasCut = true;
        // The shared signature: drawn detail freezes while the speed lines burst, then
        // motion continues from where it stopped rather than jumping forward.
        BeginHold();
        Track(blade.CreateCutTween(_debris, CutLagSeconds, CutDuration, FragmentCount));
    }

    /// <summary>
    /// Sheathes and releases. This is the Release beat of the session contract; the
    /// base <c>Dispose</c> it ends in is idempotent and also covers combat end, tree
    /// exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        // Waits out the lag and the whole opening. Fading during it would hide the
        // lag, which is the one beat separating this card from the other three. Tween
        // time keeps running through a hold, so the interval covers that too.
        var settle = _blades.Values.Any(static blade => blade.HasCut)
            ? HoldDuration + CutLagSeconds + CutDuration
            : 0f;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    /// <summary>How long the cut waits after the blade passes, in seconds.</summary>
    internal static float CutLagSeconds => CutLagSteps / StepFrequency;

    /// <summary>
    /// How far the cut has opened, as a fraction of its full width, at
    /// <paramref name="seconds"/> after the opening starts.
    /// </summary>
    /// <remarks>
    /// The damping lives here rather than in the shader so one place owns the rate and
    /// the quantization; the surface receives a fraction and needs to know nothing
    /// else. This is the same split the shield's ring uses.
    /// <para>
    /// Time is snapped to the stepped clock before the curve is evaluated, not after.
    /// A smooth opening sampled onto steps would still glide between them; stepping the
    /// input is what makes the material give way on drawn frames.
    /// </para>
    /// </remarks>
    internal static float CutOpenFraction(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return 0f;

        var stepped = MathF.Floor(seconds * StepFrequency) / StepFrequency;
        // Critically damped step response: rises to one, never past it.
        var remaining = (1f + CutDecay * stepped) * MathF.Exp(-CutDecay * stepped);
        return Math.Clamp(1f - remaining, 0f, 1f);
    }

    /// <summary>
    /// The opening contract: shut at the start, visibly open within one drawn frame,
    /// and complete before the beat ends. A cut still widening at fade-out would read
    /// as a wound that never finished, and one that crept open over many frames would
    /// read as a decal fading in.
    /// </summary>
    internal static bool CutOpensWithinEnvelope() =>
        CutOpenFraction(0f) == 0f
        && CutOpenFraction(1f / StepFrequency) > 0.5f
        && CutOpenFraction(CutDuration) > 0.97f;

    /// <summary>
    /// One hold per crossing, so the speed lines burst on each pass rather than once
    /// for the whole flurry.
    /// </summary>
    /// <remarks>
    /// A hold here freezes the shared clock only; it never pauses the blades. Four
    /// crossings inside a 0.30 s envelope leave 0.075 s each, less than a hold lasts,
    /// so pausing motion per crossing would either stretch the envelope or stop the
    /// flurry dead. Holds accumulate by <c>Math.Max</c>, which means the burst reads as
    /// one continuous freeze of drawn detail over blades that keep moving — which is
    /// what a flurry looks like when it is drawn rather than simulated.
    /// </remarks>
    private void BeginCrossingHold()
    {
        if (IsActive())
            BeginHold();
    }

    /// <summary>
    /// Samples every blade onto the 12 Hz grid and hands the past few samples to its
    /// ghosts. Sampling here rather than tweening each ghost separately keeps one
    /// source for the trail: the ghosts are literally where the blade was, so they
    /// cannot drift out of step with it.
    /// </summary>
    private async Task TrackAfterimages()
    {
        try
        {
            // The session's own clock, not process uptime: the grid has to start when
            // this effect does, or which instants get drawn would depend on how long
            // the game had been running.
            var elapsed = 0f;
            while (IsActive())
            {
                foreach (var blade in _blades.Values)
                    blade.SampleAfterimages(elapsed);
                elapsed += await Root.AwaitProcessFrame();
            }
        }
        catch (OperationCanceledException) when (!IsActive())
        {
        }
    }

    private static (PackedScene Root, PackedScene Target) LoadScenes()
        => (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create the sword cel VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    /// <summary>
    /// One target's hardware: the blade or blades that strike it, their ghosts, and
    /// the cut they leave.
    /// </summary>
    private sealed class BladeVisual
    {
        private readonly Node2D _root;
        private readonly ShaderMaterial _bladeMaterial;
        private readonly ShaderMaterial _cutMaterial;
        private readonly Node2D _cutAnchor;
        private readonly Node2D _fragments;
        private readonly Vector2 _center;
        private readonly Vector2 _size;
        private readonly Stroke[] _strokes;
        private readonly List<Sample> _samples = [];
        private int _lastStep = int.MinValue;

        internal BladeVisual(
            PackedScene scene,
            Node2D parent,
            CelVfxGeometry.TargetGeometry geometry,
            int index,
            SwordMode mode)
        {
            _root = scene.Instantiate<Node2D>();
            _root.Name = $"SakuraSword{index + 1}";
            parent.AddChildSafely(_root);

            _center = geometry.Center;
            _size = geometry.Size;

            var pivot = _root.GetNode<Node2D>("%BladePivot");
            var body = _root.GetNode<ColorRect>("%BladeBody");
            var cut = _root.GetNode<ColorRect>("%CutBody");
            _cutAnchor = _root.GetNode<Node2D>("%CutAnchor");
            _fragments = _root.GetNode<Node2D>("%Fragments");

            _bladeMaterial = CelVfxGeometry.DuplicateMaterial(body, $"blade {index}");
            _cutMaterial = CelVfxGeometry.DuplicateMaterial(cut, $"cut {index}");

            // The root never scales. The blade's proportions are fixed by its own
            // region, and the cut's sizing travels to the shader as region_size, which
            // is what holds ink weight constant in screen pixels across enemy sizes.
            _root.Scale = Vector2.One;
            _cutAnchor.GlobalPosition = _center;
            cut.Size = _size;
            cut.Position = -_size * 0.5f;
            _cutMaterial.SetShaderParameter("region_size", _size);
            _cutMaterial.SetShaderParameter("blade_count", mode == SwordMode.Dual ? 2f : 1f);
            _cutMaterial.SetShaderParameter("cut_open", 0f);

            _strokes = BuildStrokes(mode, pivot, body);
            foreach (var stroke in _strokes)
                stroke.Reset();
        }

        internal bool HasCut { get; set; }

        internal IEnumerable<ShaderMaterial> Materials => [_bladeMaterial, _cutMaterial];

        /// <summary>
        /// The blade slides in along its own axis while it draws out of the hilt.
        /// Quadratic Out decelerates into the ready position, so the stop reads as the
        /// weapon being set rather than as the motion running out.
        /// </summary>
        /// <remarks>
        /// Drives the shader's <c>extend</c> as well as the transform. That parameter is
        /// not optional: the scene ships it at 0 because the weapon starts sheathed, and
        /// at 0 the shader puts the tip on the gem, so the edge has no length at all.
        /// Leaving it undriven swings a bare guard and grip through the target, which is
        /// exactly what it looked like — the blade was absent rather than short.
        /// </remarks>
        internal Tween CreateDrawTween(float duration, float delay)
        {
            var tween = _root.CreateTween().SetParallel();
            tween.TweenMethod(
                    Callable.From<float>(SetExtend),
                    0f,
                    1f,
                    duration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            foreach (var stroke in _strokes)
            {
                tween.TweenMethod(
                        Callable.From<float>(stroke.SetDraw),
                        0f,
                        1f,
                        duration)
                    .SetDelay(delay)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Quad);
            }
            return tween;
        }

        /// <summary>
        /// One stroke in three segments: angular acceleration from rest, constant
        /// angular velocity through the target, then deceleration that overshoots and
        /// settles. The pivot sits at the wrist rather than at the blade's middle,
        /// which is what makes the tip travel several times faster than the grip.
        /// </summary>
        internal Tween CreateSwingTween(float duration, float delay)
        {
            var tween = _root.CreateTween().SetParallel();
            foreach (var stroke in _strokes)
                stroke.Chain(tween, stroke.Start, stroke.End, duration, delay);
            return tween;
        }

        /// <summary>
        /// The twin blades close on the target together and cross, then reopen and
        /// cross again, as many times as the hit count says, all inside one envelope.
        /// Reversing rather than resetting is what keeps the flurry continuous: the
        /// blades never jump back to where they started.
        /// </summary>
        internal Tween CreateCrossingTween(
            int crossings,
            float envelope,
            float delay,
            // Fully qualified: this namespace has its own gameplay Action type.
            System.Action onCrossing)
        {
            var tween = _root.CreateTween().SetParallel();
            var each = envelope / Math.Max(1, crossings);
            foreach (var stroke in _strokes)
            {
                for (var i = 0; i < crossings; i++)
                {
                    // Alternating ends, so crossing i+1 starts where crossing i stopped.
                    var forward = i % 2 == 0;
                    stroke.Chain(
                        tween,
                        forward ? stroke.Start : stroke.End,
                        forward ? stroke.End : stroke.Start,
                        each,
                        delay + i * each);
                }
            }

            // On the pass itself, once per crossing rather than once per blade: both
            // blades reach the centre together, so a hold per blade would just be the
            // same freeze requested twice.
            var pulse = _root.CreateTween();
            for (var i = 0; i < crossings; i++)
            {
                pulse.TweenInterval(i == 0 ? delay + each * 0.5f : each);
                pulse.TweenCallback(Callable.From(onCrossing));
            }
            return tween;
        }

        /// <summary>
        /// The wound opens after the lag, along the line the edge travelled, and throws
        /// chips out of it under gravity.
        /// </summary>
        internal Tween CreateCutTween(Node2D debrisParent, float lag, float duration, int fragmentCount)
        {
            var tween = _root.CreateTween().SetParallel();
            // Delayed past the hold as well as the lag: a hold stops shader time, not
            // tween time, so an opening that started during it would widen a wound in
            // a frame that is meant to be frozen.
            var start = HoldDuration + lag;
            tween.TweenMethod(
                    Callable.From<float>(SetCutSeconds),
                    0f,
                    duration,
                    duration)
                .SetDelay(start);

            for (var i = 0; i < fragmentCount; i++)
            {
                var spread = -0.6f + 1.2f * i / Math.Max(1, fragmentCount - 1);
                // Thrown across the cut, not along it: material leaves a wound
                // perpendicular to the surface that opened.
                var normal = new Vector2(
                    -MathF.Sin(CrossTiltRadians),
                    -MathF.Cos(CrossTiltRadians));
                var along = new Vector2(normal.Y, -normal.X);
                var velocity = normal * (150f + i % 3 * 40f) + along * (spread * 190f);
                var origin = _center + along * (spread * _size.X * 0.24f);
                CelVfxGeometry.AddBallisticDebris(
                    tween,
                    debrisParent,
                    FragmentPoints(4.0f + i % 2 * 1.6f, 9f + i % 3 * 3f),
                    i % 2 == 0 ? new Color(0.93f, 0.97f, 1f) : new Color(0.60f, 0.70f, 0.82f),
                    origin,
                    velocity,
                    duration + FadeDuration,
                    start,
                    FragmentGravity,
                    2.4f + i * 0.35f,
                    "SwordChip");
            }
            return tween;
        }

        /// <summary>
        /// Records where each blade is on the stepped grid and moves its ghosts to the
        /// previous samples.
        /// </summary>
        internal void SampleAfterimages(float seconds)
        {
            if (!GodotObject.IsInstanceValid(_root))
                return;

            var step = (int)MathF.Floor(seconds * StepFrequency);
            if (step == _lastStep)
                return;
            _lastStep = step;

            _samples.Insert(0, Sample.Capture(_strokes));
            if (_samples.Count > AfterimageCount + 1)
                _samples.RemoveAt(_samples.Count - 1);

            for (var ghost = 0; ghost < AfterimageCount; ghost++)
            {
                // Sample ghost+1 back, because sample 0 is where the blade is now.
                var source = ghost + 1;
                if (source >= _samples.Count)
                {
                    foreach (var stroke in _strokes)
                        stroke.HideGhost(ghost);
                    continue;
                }

                for (var i = 0; i < _strokes.Length; i++)
                    _strokes[i].ShowGhost(ghost, _samples[source].Poses[i]);
            }
        }

        private void SetCutSeconds(float seconds) =>
            _cutMaterial.SetShaderParameter("cut_open", CutOpenFraction(seconds));

        /// <summary>
        /// How far the edge has drawn out of the hilt. One material serves the blade and
        /// all of its ghosts, so this reaches every copy at once — a ghost showing a
        /// different blade length than the blade would read as a second weapon.
        /// </summary>
        private void SetExtend(float progress) =>
            _bladeMaterial.SetShaderParameter("extend", Math.Clamp(progress, 0f, 1f));

        /// <summary>
        /// Builds one stroke for a single blade or two converging ones. The scene owns
        /// the blade rect's size and its offset from the pivot — both derived from the
        /// shader's proportions — so extra blades and ghosts copy those numbers rather
        /// than restating them.
        /// </summary>
        private Stroke[] BuildStrokes(SwordMode mode, Node2D pivot, ColorRect body)
        {
            var bladeSize = body.Size;
            var bladeOffset = body.Position;

            if (mode == SwordMode.Single)
            {
                return
                [
                    new Stroke(
                        pivot,
                        _root,
                        _center,
                        MathF.PI + SingleTiltRadians,
                        SwingSpanRadians,
                        bladeSize,
                        bladeOffset,
                        _bladeMaterial)
                ];
            }

            // The scene's pivot serves the first blade; the second is built beside it
            // from the same numbers. Both draw the same steel with the same parameters,
            // so they share one material and differ only in transform.
            var second = new Node2D { Name = "BladePivotB" };
            _root.AddChildSafely(second);
            second.AddChildSafely(new ColorRect
            {
                Name = "BladeBodyB",
                Material = _bladeMaterial,
                Size = bladeSize,
                Position = bladeOffset,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Color = Colors.White
            });

            return
            [
                new Stroke(
                    pivot,
                    _root,
                    _center,
                    MathF.PI - CrossTiltRadians,
                    SwingSpanRadians,
                    bladeSize,
                    bladeOffset,
                    _bladeMaterial),
                new Stroke(
                    second,
                    _root,
                    _center,
                    MathF.PI + CrossTiltRadians,
                    -SwingSpanRadians,
                    bladeSize,
                    bladeOffset,
                    _bladeMaterial)
            ];
        }

        /// <summary>
        /// Angular chip outline. Straight edges meeting at sharp corners, so a piece of
        /// steel still reads as steel at far-field size.
        /// </summary>
        private static Vector2[] FragmentPoints(float radius, float height) =>
        [
            new(0f, -height),
            new(radius, -height * 0.18f),
            new(radius * 0.34f, height * 0.82f),
            new(-radius * 0.52f, height * 0.44f),
            new(-radius * 0.88f, -height * 0.40f)
        ];

        /// <summary>Every blade's pose on one stepped frame.</summary>
        private readonly record struct Sample(Pose[] Poses)
        {
            internal static Sample Capture(Stroke[] strokes) =>
                new([.. strokes.Select(static stroke => stroke.CurrentPose)]);
        }
    }

    /// <summary>A blade's position and angle at one instant.</summary>
    private readonly record struct Pose(Vector2 Position, float Rotation);

    /// <summary>
    /// One blade: the pivot it turns on, the arc it travels, and its stepped ghosts.
    /// </summary>
    /// <remarks>
    /// The arc is expressed as a base angle plus a span, and the pivot is then placed
    /// so that at the base angle the blade crosses the target's centre. Solving for the
    /// pivot rather than for the angles is what guarantees the edge actually passes
    /// through the target, whatever the arc.
    /// </remarks>
    private sealed class Stroke
    {
        private readonly Node2D _pivot;
        private readonly Vector2 _rest;
        private readonly Vector2 _entry;
        private readonly List<Node2D> _ghosts = [];

        internal Stroke(
            Node2D pivot,
            Node2D ghostParent,
            Vector2 targetCenter,
            float baseRotation,
            float span,
            Vector2 bladeSize,
            Vector2 bladeOffset,
            ShaderMaterial bladeMaterial)
        {
            _pivot = pivot;
            Start = baseRotation - span * 0.5f;
            End = baseRotation + span * 0.5f;

            // At rotation r the blade points along up turned by r. Backing the target's
            // centre off along that direction puts the centre on the edge's path.
            var heading = Heading(baseRotation);
            _rest = targetCenter - heading * SwingRadiusPx;
            _entry = _rest - heading * DrawSlidePx;

            for (var i = 0; i < AfterimageCount; i++)
            {
                var ghost = new Node2D
                {
                    Name = $"Afterimage{i + 1}",
                    // Behind the blade itself, so the trail never draws over the edge
                    // that made it. Relative, because the root sets absolute Z.
                    ZIndex = -1 - i,
                    Modulate = new Color(1f, 1f, 1f, 0f)
                };
                ghostParent.AddChildSafely(ghost);
                ghost.AddChildSafely(new ColorRect
                {
                    Name = "GhostBody",
                    // The same material as the blade: a ghost is the blade at an
                    // earlier instant, so anything that differed would be a second
                    // place to change the steel.
                    Material = bladeMaterial,
                    Size = bladeSize,
                    Position = bladeOffset,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Color = Colors.White
                });
                _ghosts.Add(ghost);
            }
        }

        internal float Start { get; }
        internal float End { get; }

        internal Pose CurrentPose =>
            GodotObject.IsInstanceValid(_pivot)
                ? new Pose(_pivot.GlobalPosition, _pivot.Rotation)
                : default;

        internal void Reset()
        {
            if (!GodotObject.IsInstanceValid(_pivot))
                return;
            _pivot.Rotation = Start;
            _pivot.GlobalPosition = _entry;
        }

        internal void SetDraw(float progress)
        {
            if (!GodotObject.IsInstanceValid(_pivot))
                return;
            _pivot.GlobalPosition = _entry.Lerp(_rest, Math.Clamp(progress, 0f, 1f));
        }

        /// <summary>
        /// Appends the three-segment angular profile to <paramref name="tween"/>:
        /// constant angular acceleration, constant angular velocity, then a decelerating
        /// segment that overshoots and settles back.
        /// </summary>
        internal void Chain(Tween tween, float from, float to, float duration, float delay)
        {
            if (!GodotObject.IsInstanceValid(_pivot))
                return;

            var delta = to - from;
            tween.TweenProperty(_pivot, "rotation", from + delta * 0.25f, duration * 0.27f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(_pivot, "rotation", from + delta * 0.70f, duration * 0.32f)
                .SetDelay(delay + duration * 0.27f)
                .SetTrans(Tween.TransitionType.Linear);
            // Back Out carries the blade past its stop and brings it home, which is the
            // wrist absorbing the swing rather than the arc simply ending.
            tween.TweenProperty(_pivot, "rotation", to, duration * 0.41f)
                .SetDelay(delay + duration * 0.59f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }

        internal void ShowGhost(int index, Pose pose)
        {
            if (index >= _ghosts.Count || !GodotObject.IsInstanceValid(_ghosts[index]))
                return;
            var ghost = _ghosts[index];
            ghost.GlobalPosition = pose.Position;
            ghost.Rotation = pose.Rotation;
            ghost.Modulate = new Color(1f, 1f, 1f, GhostAlpha(index));
        }

        internal void HideGhost(int index)
        {
            if (index >= _ghosts.Count || !GodotObject.IsInstanceValid(_ghosts[index]))
                return;
            _ghosts[index].Modulate = new Color(1f, 1f, 1f, 0f);
        }

        private static float GhostAlpha(int index) =>
            AfterimageLeadAlpha * MathF.Pow(AfterimageFalloff, index);

        private static Vector2 Heading(float rotation) =>
            new(MathF.Sin(rotation), -MathF.Cos(rotation));
    }
}
