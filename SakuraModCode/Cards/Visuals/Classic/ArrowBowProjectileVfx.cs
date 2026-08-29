using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>How much presentation one Arrow play gets.</summary>
/// <remarks>
/// The axis is play frequency, not card name or era, which is the same axis
/// <c>SlashWeight</c> and <c>FreezeWeight</c> already sit on. A basic Clow Arrow
/// appears many times per combat and what decays under repetition is
/// description, while short transients keep working; a release-token Sakura
/// Arrow is rare enough to carry the full show.
/// </remarks>
internal enum ArrowWeight
{
    /// <summary><c>ClowArrow</c> played plainly: the most frequent play here.</summary>
    Light,

    /// <summary><c>ClowArrow</c>'s activated path, paid for with Magic Charge.</summary>
    Medium,

    /// <summary><c>SakuraArrow</c>, the rarest of the three.</summary>
    Heavy
}

/// <summary>
/// Arrow's flight: a bow drawn at the caster, then one light arrow per hit
/// travelling to the creature that hit actually struck.
/// </summary>
/// <remarks>
/// Unlike <see cref="HailIceShardVfx"/>, which builds one visual per target
/// before gameplay runs, this session cannot know its targets up front — Arrow
/// strikes random enemies, so the creature is only known once the engine has
/// resolved it. Each <see cref="Cues.Loose"/> therefore resolves the target on
/// arrival, and the visuals are pooled rather than pre-built per creature.
/// <para>
/// <see cref="Cues.Loose"/> is awaitable and completes when the arrow lands,
/// because the card passes it as the engine's before-damage observer: whatever
/// it awaits happens before that hit's damage resolves, which is what puts the
/// contact flash on the same frame as the damage number instead of a beat away
/// from it. A session that is not running returns a completed task, so a player
/// with card VFX off never spends real time waiting for an arrow nobody drew.
/// </para>
/// </remarks>
internal sealed class ArrowBowProjectileVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/arrow_bow_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/arrow_bow_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/arrow_bow.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    /// <summary>How long the bow is drawn before the first arrow leaves.</summary>
    private const float NockDuration = 0.12f;

    /// <summary>
    /// Flight time for the first few arrows. Short enough that the eye reads a
    /// travelled object rather than a teleport, long enough to see the wake.
    /// </summary>
    private const float FullFlightDuration = 0.10f;

    /// <summary>Flight time once the volley is large enough to need compression.</summary>
    private const float RapidFlightDuration = 0.03f;

    /// <summary>Arrows that still get a full flight before compression begins.</summary>
    private const int FullFlightHits = 4;

    /// <summary>The last hit that still travels at all.</summary>
    /// <remarks>
    /// Arrow's hit count is unbounded — a full hand plus X energy can exceed a
    /// dozen — and a fixed flight per hit would turn a big play into a cutscene.
    /// Compression caps the waited time; hits past this still register as a
    /// contact flash, they simply stop holding combat up.
    /// </remarks>
    private const int CappedHits = 12;

    /// <summary>
    /// Concurrent arrow nodes. Past this the volley reads as noise, so hits
    /// cycle through the pool instead of growing the tree.
    /// </summary>
    private const int MaxArrows = 6;

    /// <summary>Two stepped frames, matching <see cref="CelVfxSession.BeginHold"/>.</summary>
    private const float ImpactDuration = 2f / StepFrequency;

    private const float FadeDuration = 0.12f;

    /// <summary>
    /// The arrow's own region, in screen pixels. Fixed, not derived from the
    /// enemy: a drawn arrow has a drawn size, and sizing it to a boss would
    /// produce a giant arrow while the ink weight stayed constant in screen
    /// pixels.
    /// </summary>
    /// <remarks>
    /// The pair is measured off the field this shader draws, not chosen. The
    /// wake reaches local <c>x = -101</c> at full flight reach and the contact
    /// burst reaches <c>x = +118</c> at full impact, measured from the region's
    /// centre, so <c>240</c> wide is the first value that stops clipping a hard
    /// vertical edge across the feathers and cutting the burst's wedges in
    /// half. <c>160</c> tall clears the burst's <c>74</c>px radius. Enlarging
    /// the region costs transparent fragments and does not resize the arrow:
    /// fragment-space <c>p</c> is screen pixels whatever the region is.
    /// <para>
    /// Within that span the head is <c>38</c>px across and the wake strokes
    /// <c>6.8</c>px, both clear of the <c>2 * CEL_INK_WIDTH</c> floor below
    /// which a part reads as a line instead of a surface. The shaft is
    /// deliberately thinner than that floor, because a shaft <em>is</em> a line
    /// and the outline is doing no work at that width on a part that only ever
    /// travels.
    /// </para>
    /// <para>
    /// The shader and the target scene ship this same pair. The session is the
    /// one that has to match them, since it is what overrides the value before
    /// the first frame is drawn.
    /// </para>
    /// </remarks>
    private static readonly Vector2 ArrowRegion = new(240f, 160f);

    /// <summary>Gravity for the flight arc and the spent fragments.</summary>
    private const float Gravity = 980f;

    /// <summary>How far toward the hand the bow sits, along the facing axis.</summary>
    private const float HandOffset = 26f;

    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly Node2D _arrows;
    private readonly Node2D _debris;
    private readonly PackedScene _targetScene;
    private readonly ArrowWeight _weight;
    private readonly float _starHead;
    private readonly Color _ink;
    private readonly Color _accent;
    private readonly Vector2 _origin;
    private readonly Dictionary<Creature, Vector2> _centers = [];
    private readonly List<ArrowVisual> _pool = [];
    private bool _faded;

    private ArrowBowProjectileVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene targetScene,
        CardModel card,
        Creature? caster,
        ArrowWeight weight)
        : base(root, room)
    {
        ArgumentNullException.ThrowIfNull(card);
        _targetScene = targetScene;
        _weight = weight;
        _arrows = root.GetNode<Node2D>("%Arrows");
        _debris = root.GetNode<Node2D>("%Debris");

        var anchor = caster is null ? null : CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster));
        _origin = anchor is { } resolved
            // Chest height, biased toward the hand. The bias belongs here rather
            // than in the helper because a shield plate and a bow want different
            // heights off the same floor/body pair.
            ? resolved.Floor.Lerp(resolved.BodyCenter, 0.55f)
                + new Vector2(resolved.FacingSign * HandOffset, 0f)
            : RoomCenter(room);

        _starHead = SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            && metadata.Era == SourceEraClass.Sakura
                ? 1f
                : 0f;

        // One ink source, the era's seal colour. The wake is the ink pulled
        // toward the warm end, so there is no second palette to keep in step.
        _ink = MagicCircleInkColour(card) ?? new Color(1f, 0.94f, 0.62f);
        _accent = new Color(_ink.R, _ink.G * 0.78f, _ink.B * 0.72f, 1f);
    }

    /// <summary>
    /// The region around a struck creature. Narrower and shorter than Hail's,
    /// because this region hosts the head, the contact flash, and the wake's
    /// near end — never the whole shaft, which belongs to the travelling node.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 12f,
        VerticalPadding: 14f,
        MinWidth: 130f,
        MinHeight: 120f,
        MaxWidth: 300f,
        MaxHeight: 260f,
        FallbackWidth: 180f,
        FallbackHeight: 150f,
        FloorClearance: 6f,
        MaxViewportWidthFraction: 0.26f,
        MaxViewportHeightFraction: 0.36f);

    protected override IEnumerable<ShaderMaterial> Materials =>
        _pool.Select(static arrow => arrow.Material);

    /// <summary>
    /// Safety net, not a timer. Worst case is the shared prelude, the nock, a
    /// dozen compressed flights, the last impact, and the fade — under a second
    /// and a half. Sized clear of that, because a cap set tight stops being a
    /// net and becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 4.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        ArrowWeight weight,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Arrow bow",
            () => TryCreate(card, caster, weight),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<ArrowBowProjectileVfx> scope)
    {
        internal void Nock() =>
            scope.Invoke("nock", session => session.Nock());

        /// <summary>
        /// Sends one arrow to <paramref name="target"/> and completes when it
        /// lands. Awaited by the engine's before-damage observer, so the hit's
        /// damage resolves on the frame this returns.
        /// </summary>
        internal Task Loose(Creature target, int index)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "An arrow index cannot be negative.");
            return scope.InvokeAsync("loose", session => session.LooseAsync(target, index));
        }
    }

    private static ArrowBowProjectileVfx? TryCreate(CardModel card, Creature? caster, ArrowWeight weight)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!TryPrepare("Arrow bow", LoadScenes, out var room, out _, out var scenes))
            return null;

        Node2D? root = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraArrowBowProjectileVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new ArrowBowProjectileVfx(root, room, scenes.Target, card, caster, weight);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the field backing it is
            // still empty.
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

    /// <summary>Shared wand tap, magic circle, and speed lines, then the bow.</summary>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        Nock();
        return await WaitActive(NockDuration);
    }

    /// <summary>Draws the bow: the first arrow forms at the caster's hand.</summary>
    /// <remarks>
    /// No target exists yet, so the drawn arrow points along <c>+X</c>, the way
    /// Sakura faces. The first <see cref="Cues.Loose"/> swings it onto the real
    /// line of travel at the instant it leaves, which is when a bow is aimed
    /// anyway.
    /// </remarks>
    private void Nock()
    {
        if (!IsActive())
            return;

        var arrow = ArrowFor(0);
        arrow.Restore(_origin, 0f, _starHead, _ink, _accent, WeightValue(_weight), 0f);
        Track(arrow.CreateFormationTween(NockDuration));
    }

    private async Task LooseAsync(Creature target, int index)
    {
        if (!IsActive())
            return;

        var arrow = ArrowFor(index);
        var destination = CenterFor(target, index);
        // Already drawn: only the nock animates an arrow into existence, and a
        // later arrow restored at formation 0 would fly the whole way invisible.
        arrow.Restore(
            _origin,
            TravelAngle(_origin, destination),
            _starHead,
            _ink,
            _accent,
            WeightValue(_weight),
            1f);

        var flight = FlightFor(index);
        if (flight <= 0f)
        {
            // Past the compression cap the volley stops waiting, but the hit
            // still has to land on its creature. Placed at the contact point the
            // arrow erodes into the burst there; left at the caster, every late
            // hit would flash beside Sakura's hand while its damage landed
            // across the room.
            arrow.Place(destination);
            Track(arrow.CreateImpactTween(ImpactDuration));
            SpawnDebris(destination);
            return;
        }

        Track(arrow.CreateFlightTween(_origin, destination, flight));
        if (!await WaitActive(flight))
            return;

        Track(arrow.CreateImpactTween(ImpactDuration));
        SpawnDebris(destination);
    }

    /// <summary>
    /// The angle of the line from <paramref name="origin"/> to
    /// <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// The shader draws the head on local <c>+X</c> and says so, so the node has
    /// to carry that angle or an arrow climbing to a high enemy is drawn level
    /// while it travels diagonally. Guarding the degenerate case makes the
    /// intent explicit rather than relying on <c>Vector2.Angle</c> of a zero
    /// vector happening to be zero.
    /// </remarks>
    private static float TravelAngle(Vector2 origin, Vector2 destination)
    {
        var travel = destination - origin;
        return travel.LengthSquared() > 1f ? travel.Angle() : 0f;
    }

    /// <summary>
    /// Flight time for hit <paramref name="index"/>. The whole volley is bounded
    /// rather than each arrow being equal: a twenty-arrow play still finishes
    /// inside the same envelope as a four-arrow one.
    /// </summary>
    private static float FlightFor(int index) =>
        index < FullFlightHits
            ? FullFlightDuration
            : index < CappedHits
                ? RapidFlightDuration
                : 0f;

    private void SpawnDebris(Vector2 origin)
    {
        var count = (_starHead > 0.5f ? 5 : 4) + (int)_weight;
        var tween = Track(Root.CreateTween().SetParallel());
        for (var i = 0; i < count; i++)
        {
            var spread = -0.7f + 1.4f * i / Math.Max(1, count - 1);
            var velocity = new Vector2(spread * 180f, -120f - i % 3 * 40f);
            CelVfxGeometry.AddBallisticDebris(
                tween,
                _debris,
                _starHead > 0.5f ? PetalPoints() : FeatherPoints(),
                i % 2 == 0 ? _ink : _accent,
                origin + new Vector2(spread * 12f, -6f),
                velocity,
                ImpactDuration * 2.2f,
                gravity: Gravity,
                rotationRate: 2.4f + i * 0.3f,
                name: "ArrowDebris");
        }
    }

    /// <summary>
    /// Fades the volley out, then releases. This is the Release beat of the
    /// session contract; the base <c>Dispose</c> it ends in is idempotent and
    /// also covers combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(ImpactDuration * 0.6f);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    /// <summary>
    /// Arrows are pooled, not spawned per hit: a twenty-hit volley must not put
    /// twenty shader rects in the combat VFX container.
    /// </summary>
    private ArrowVisual ArrowFor(int index)
    {
        var slot = index % MaxArrows;
        while (_pool.Count <= slot)
            _pool.Add(new ArrowVisual(_targetScene, _arrows, _pool.Count));

        return _pool[slot];
    }

    private Vector2 CenterFor(Creature target, int index)
    {
        if (_centers.TryGetValue(target, out var cached))
            return cached;
        var geometry = CelVfxGeometry.Resolve(Room, target, index, Budget);
        _centers[target] = geometry.Center;
        return geometry.Center;
    }

    private static float WeightValue(ArrowWeight weight) => weight switch
    {
        ArrowWeight.Light => 0f,
        ArrowWeight.Medium => 0.5f,
        _ => 1f
    };

    private static Vector2 RoomCenter(NCombatRoom room) =>
        room.CombatVfxContainer.GetViewportRect().GetCenter();

    /// <summary>Angular chip, so a spent Clow feather still reads as a feather.</summary>
    private static Vector2[] FeatherPoints() =>
    [
        new(0f, -7f),
        new(4f, -1f),
        new(2f, 6f),
        new(-3f, 4f),
        new(-5f, -3f)
    ];

    /// <summary>Rounded petal, the Sakura wake's counterpart to the feather.</summary>
    private static Vector2[] PetalPoints() =>
    [
        new(0f, -6f),
        new(4f, -2f),
        new(3f, 4f),
        new(0f, 7f),
        new(-3f, 4f),
        new(-4f, -2f)
    ];

    private static (PackedScene Root, PackedScene Target) LoadScenes()
        => (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Arrow bow VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    /// <summary>
    /// One pooled arrow: a shader rect whose position travels from the caster to
    /// the struck creature.
    /// </summary>
    private sealed class ArrowVisual
    {
        private readonly Node2D _root;
        private readonly float _seed;

        /// <summary>
        /// The one tween allowed to move this arrow, so a pooled slot never has
        /// two driving it at once.
        /// </summary>
        private Tween? _motion;

        internal ArrowVisual(PackedScene scene, Node2D parent, int index)
        {
            _root = scene.Instantiate<Node2D>();
            _root.Name = $"Arrow{index + 1}";
            parent.AddChildSafely(_root);

            var body = _root.GetNode<ColorRect>("%ArrowBody");
            Material = CelVfxGeometry.DuplicateMaterial(body, $"arrow {index}");
            body.Size = ArrowRegion;
            body.Position = -ArrowRegion * 0.5f;
            Material.SetShaderParameter("region_size", ArrowRegion);
            // Per-slot, so the pooled arrows do not all flutter on the same
            // phase when several are in the air at once.
            _seed = index * 0.317f + 0.19f;
        }

        internal ShaderMaterial Material { get; }

        /// <summary>
        /// Returns the arrow to a known state at the caster's hand, aimed along
        /// <paramref name="rotation"/>.
        /// </summary>
        /// <param name="rotation">
        /// The angle of the line of travel. The shader draws the head on local
        /// <c>+X</c>, so this is what makes the head lead.
        /// </param>
        /// <param name="formation">
        /// How much of the arrow is drawn. The nock animates this from nothing;
        /// every later arrow in the volley is already whole when it appears.
        /// </param>
        internal void Restore(
            Vector2 origin,
            float rotation,
            float starHead,
            Color ink,
            Color accent,
            float weight,
            float formation)
        {
            StopMotion();
            _root.GlobalPosition = origin;
            _root.Rotation = rotation;
            _root.Modulate = new Color(1f, 1f, 1f, 1f);
            _root.Scale = Vector2.One;
            Material.SetShaderParameter("seed", _seed);
            Material.SetShaderParameter("formation", formation);
            Material.SetShaderParameter("flight", 0f);
            Material.SetShaderParameter("impact", 0f);
            Material.SetShaderParameter("star_head", starHead);
            Material.SetShaderParameter("weight", weight);
            Material.SetShaderParameter("ink_colour", ink);
            Material.SetShaderParameter("accent_colour", accent);
        }

        /// <summary>
        /// Puts the arrow at the contact point without travelling.
        /// </summary>
        /// <remarks>
        /// Only the past-cap hits use this, where the volley has stopped
        /// spending frames on flight but the hit still has to be visible on its
        /// creature rather than at the bow.
        /// </remarks>
        internal void Place(Vector2 destination) => _root.GlobalPosition = destination;

        internal Tween CreateFormationTween(float duration)
        {
            var tween = _root.CreateTween();
            tween.TweenMethod(
                    Callable.From<float>(value => Material.SetShaderParameter("formation", value)),
                    0f,
                    1f,
                    duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            return StartMotion(tween);
        }

        /// <summary>
        /// Travels from the caster to the target along a straight line plus one
        /// arc. The launch velocity is solved so the arc returns to zero exactly
        /// at the end of the flight: the arrow leaves the hand, lifts, and lands
        /// on the creature rather than above it.
        /// </summary>
        internal Tween CreateFlightTween(Vector2 origin, Vector2 destination, float duration)
        {
            var launch = Vector2.Up * (0.5f * Gravity * duration);
            var tween = _root.CreateTween();
            tween.TweenMethod(
                    Callable.From<float>(progress =>
                    {
                        var t = Mathf.Clamp(progress, 0f, 1f);
                        Material.SetShaderParameter("flight", t);
                        var arc = CelVfxGeometry.BallisticOffset(launch, Gravity, t * duration);
                        _root.GlobalPosition = origin.Lerp(destination, t) + arc;
                    }),
                    0f,
                    1f,
                    duration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            return StartMotion(tween);
        }

        internal Tween CreateImpactTween(float duration)
        {
            var tween = _root.CreateTween();
            tween.TweenMethod(
                    Callable.From<float>(value => Material.SetShaderParameter("impact", value)),
                    0f,
                    1f,
                    duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Expo);
            return StartMotion(tween);
        }

        /// <summary>
        /// Takes ownership of this arrow's next motion tween, retiring the
        /// previous one first.
        /// </summary>
        /// <remarks>
        /// The pool cycles, so hit <c>i + MaxArrows</c> reuses the node hit
        /// <c>i</c> used — while that node's contact burst can still be
        /// running. Godot does not retire the first tween when the second
        /// starts, and two live tweens writing the same uniforms fight over
        /// every frame: the stale one paints a contact burst onto an arrow that
        /// has only just left the bow, and drags its position back toward the
        /// previous hit. The compression schedule makes the overlap real — a
        /// <c>0.03s</c> flight is shorter than the two-frame impact.
        /// </remarks>
        private Tween StartMotion(Tween tween)
        {
            StopMotion();
            _motion = tween;
            return tween;
        }

        private void StopMotion()
        {
            if (_motion is { } motion && GodotObject.IsInstanceValid(motion))
                motion.Kill();
            _motion = null;
        }
    }
}
