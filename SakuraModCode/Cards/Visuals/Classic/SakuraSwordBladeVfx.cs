using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>One arc or several crossing ones.</summary>
internal enum SwordMode
{
    Single,
    Dual
}

/// <summary>
/// How much presentation a play gets, chosen by how often that play happens.
/// </summary>
/// <remarks>
/// The axis is play frequency, not card name or era. A Basic attack appears many times
/// per combat, and what decays under that repetition is description — the reading of
/// an object, the time it occupies — while short transients keep working. So the
/// cheapest tier goes to the most frequent play and the axis buys width, brightness,
/// and lay time together rather than through separate code paths.
/// </remarks>
internal enum SlashWeight
{
    /// <summary><c>ClowSword</c> played plainly: a Basic attack, the most frequent play here.</summary>
    Light,

    /// <summary><c>ClowSword</c>'s activated path and <c>Blade</c>.</summary>
    Medium,

    /// <summary><c>SakuraSword</c>, a release token and the rarest of the three.</summary>
    Heavy
}

/// <summary>
/// The sword and twin-blade effect: the trace a fast edge leaves, with no weapon drawn.
/// </summary>
/// <remarks>
/// Shared by three cards. <c>ClowSword</c> and <c>SakuraSword</c> differ only in weight
/// and in how many targets gameplay hands over; <c>Blade</c> passes
/// <see cref="SwordMode.Dual"/> and its hit count.
/// <para>
/// Nothing is drawn but the stroke. The previous revision built a six-part weapon, three
/// stepped afterimages per blade, a wound that opened two frames after the edge passed,
/// and five ballistic chips. All of that described the object, and the object is what
/// stops being looked at once the card has been played a few times. What remains is the
/// mark, one freeze, and one contact flash.
/// </para>
/// <para>
/// The freeze lands on the frame damage resolves rather than two frames later. The old
/// beat deliberately delayed the wound so the damage number would land on it, which
/// meant the whole chain stretched to accommodate a decoration. The game already owns
/// damage numbers, health bars, and hit reactions; this only has to land on the same
/// beat as them.
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

    /// <summary>
    /// One stroke's whole life, in seconds: laid down, held, gone. Short enough that a
    /// second play never overlaps the first.
    /// </summary>
    private const float ArcLife = 0.26f;

    /// <summary>
    /// How long after a stroke launches its edge reaches the target's centre. The
    /// session waits this out before gameplay resolves, so damage lands on a mark that
    /// has arrived rather than on one still travelling.
    /// </summary>
    private const float ContactDelay = 0.10f;

    /// <summary>
    /// The contact transient's length, in seconds — two stepped frames. Bounded and
    /// local: brightness and a small disc at the contact point, never a screen read.
    /// </summary>
    private const float FlashDuration = 2f / StepFrequency;

    private const float FadeDuration = 0.12f;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at two
    /// stepped frames. Tweens that must look frozen wait it out, because a hold stops
    /// shader time and not Godot's tween clock.
    /// </summary>
    private const float HoldDuration = 2f / StepFrequency;

    /// <summary>
    /// Twin-blade crossings all fit inside this, whatever the hit count. The envelope is
    /// fixed and only its density changes, so two crossings and four are visually
    /// distinguishable without four taking twice the wall time.
    /// </summary>
    private const float CrossingEnvelope = 0.30f;

    /// <summary>Serial offset between targets when one stroke passes through several.</summary>
    private const float TargetStagger = 0.05f;

    /// <summary>
    /// Half-angle between crossing strokes, in degrees. Sent to the shader rather than
    /// restated there, so the X that is drawn is the X specified here.
    /// </summary>
    private const float CrossTiltDegrees = 24f;

    /// <summary>
    /// Tilt of a lone stroke away from horizontal. A stroke with no lateral lean has
    /// nothing to read its own direction against.
    /// </summary>
    private const float SingleTiltDegrees = 18f;

    /// <summary>Highest crossing count the fixed envelope can still resolve.</summary>
    private const int MaxCrossings = 4;

    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly Dictionary<Creature, SlashVisual> _slashes = [];
    private readonly SwordMode _mode;
    private readonly SlashWeight _weight;
    private readonly int _crossings;
    private bool _faded;

    private SakuraSwordBladeVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> creatures,
        SwordMode mode,
        SlashWeight weight,
        int crossings)
        : base(root, room)
    {
        _mode = mode;
        _weight = weight;
        _crossings = crossings;
        var slashes = root.GetNode<Node2D>("%Slashes");

        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (_slashes.ContainsKey(creature))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, creature, index, Budget);
            _slashes.Add(
                creature,
                new SlashVisual(targetScene, slashes, geometry, index, ArcCount(mode, crossings), weight));
        }
    }

    /// <summary>
    /// The stroke is sized to the body it crosses. Nothing here needs a fixed pixel
    /// region: the previous revision's 460px floor came from the crossguard legibility
    /// gate, and with no crossguard to read there is nothing that must clear a constant
    /// ink width.
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
        _slashes.Values.Select(static slash => slash.Material);

    /// <summary>
    /// Safety net, not a timer, and on wall clock because a hold decouples the two.
    /// Worst case is five enemies struck serially at four crossings each: the shared
    /// prelude, then roughly 0.2s of stagger plus the crossing envelope, one stroke's
    /// life, the flash, and the fade. Sized well clear of that, because a cap set tight
    /// stops being a net and becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 4.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        SwordMode mode,
        SlashWeight weight,
        Func<Cues, Task> resolveGameplay,
        int crossings = 1)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Sword blade",
            () => TryCreate(targets, mode, weight, crossings),
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
    /// <param name="mode">One stroke or several crossing ones.</param>
    /// <param name="weight">How much presentation this play gets.</param>
    /// <param name="crossings">
    /// How many times the strokes cross. Read once from the card's resolved hit count;
    /// ignored for <see cref="SwordMode.Single"/>, which is one stroke by definition.
    /// </param>
    private static SakuraSwordBladeVfx? TryCreate(
        IReadOnlyList<Creature> targets,
        SwordMode mode,
        SlashWeight weight,
        int crossings = 1)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return null;
        if (!TryPrepare("Sword blade", LoadScenes, out var room, out _, out var scenes))
            return null;

        Node2D? root = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraSwordBladeVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new SakuraSwordBladeVfx(
                root, room, scenes.Target, targets, mode, weight, ArcCount(mode, crossings));
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing it is
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

    /// <summary>
    /// How many strokes a play draws. Clamped rather than trusted: the hit count comes
    /// from gameplay and can grow with future powers, while the envelope is fixed. Past
    /// four the strokes would overlap faster than they can be told apart, so extra hits
    /// stop being visible instead of turning the mark into a smear.
    /// </summary>
    internal static int ArcCount(SwordMode mode, int crossings) =>
        mode == SwordMode.Dual ? Math.Clamp(crossings, 1, MaxCrossings) : 1;

    /// <summary>
    /// The intensity axis as the shader consumes it. One number drives stroke width,
    /// brightness, and lay time, which is what keeps the four tiers a parameter rather
    /// than four implementations.
    /// </summary>
    internal static float WeightValue(SlashWeight weight) => weight switch
    {
        SlashWeight.Light => 0f,
        SlashWeight.Medium => 0.5f,
        _ => 1f
    };

    /// <summary>
    /// Shared wand tap, magic circle, and speed lines, then the strokes launch and the
    /// session waits only until the edge has arrived.
    /// </summary>
    /// <remarks>
    /// Waits <see cref="ContactDelay"/> rather than the whole stroke, unlike the previous
    /// revision. That one waited out the entire swing so the wound could open after
    /// gameplay resolved; with no wound to lag, the freeze and the flash belong on the
    /// frame the game's own damage number lands.
    /// </remarks>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!await PlayCelPrelude(card, caster))
            return false;

        var index = 0;
        foreach (var slash in _slashes.Values)
        {
            Track(slash.CreateLaunchTween(
                _mode == SwordMode.Dual ? CrossingEnvelope : 0f, ArcLife, index * TargetStagger));
            index++;
        }

        return await WaitActive(ContactDelay);
    }

    /// <summary>
    /// One target is struck: drawn detail freezes for two stepped frames while the
    /// contact transient fires. Idempotent per target, since two cards here deal several
    /// damages to the same target and a second flash would read as a second stroke that
    /// never happened.
    /// </summary>
    private void Impact(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsActive() || !_slashes.TryGetValue(target, out var slash) || slash.HasStruck)
            return;

        slash.HasStruck = true;
        // A real freeze, not BeginHold. That helper stops the base session's stepped
        // clock and pushes `held` to the shaders that read it; this card declares no
        // clock uniforms and drives every stroke from a tween, and the base class is
        // explicit that a hold does not pause tween time. Calling it here would have
        // satisfied the shape of a hit-stop while freezing nothing.
        Track(slash.CreateFreezeTween(HoldDuration));
        Track(slash.CreateFlashTween(FlashDuration));
    }

    /// <summary>
    /// Lets the last stroke finish, then releases. This is the Release beat of the
    /// session contract; the base <c>Dispose</c> it ends in is idempotent and also covers
    /// combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        // Waits out the hold, whatever remains of the stroke, and the flash. Tween time
        // keeps running through a hold, so the interval has to cover that too.
        var envelope = _mode == SwordMode.Dual ? CrossingEnvelope : 0f;
        var remaining = Math.Max(0f, envelope + ArcLife - ContactDelay);
        var settle = _slashes.Values.Any(static slash => slash.HasStruck)
            ? HoldDuration + Math.Max(remaining, FlashDuration)
            : remaining;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    /// <summary>
    /// The presentation contract: a play is over quickly and leaves nothing behind.
    /// </summary>
    /// <remarks>
    /// Asserted on the constants rather than checked by eye, because the whole point of
    /// this revision is the total length. The previous one ran about 1.15s per target for
    /// a Basic attack; a later re-tune that crept back over budget should fail a test.
    /// </remarks>
    internal static bool StrokeFitsBudget() =>
        SingleTargetSeconds() < 0.6f
        && DualTargetSeconds() < 0.9f
        && ContactDelay < ArcLife;

    /// <summary>Card-specific time for one plain stroke, excluding the shared prelude.</summary>
    internal static float SingleTargetSeconds() =>
        ContactDelay + Math.Max(ArcLife - ContactDelay, HoldDuration + FlashDuration) + FadeDuration;

    /// <summary>The same for a full four-crossing flurry.</summary>
    internal static float DualTargetSeconds() =>
        ContactDelay
        + Math.Max(CrossingEnvelope + ArcLife - ContactDelay, HoldDuration + FlashDuration)
        + FadeDuration;

    private static (PackedScene Root, PackedScene Target) LoadScenes()
        => (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    /// <summary>
    /// Logs a load failure once per process. Bounded because this runs on a card play: a
    /// missing resource would otherwise log on every play for the rest of the run.
    /// </summary>
    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create the sword arc VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    /// <summary>One target's mark: the strokes that cross it and the contact transient.</summary>
    private sealed class SlashVisual
    {
        private readonly Node2D _root;
        private readonly Node2D _anchor;
        private readonly int _arcCount;
        private readonly float[] _phases = new float[4];

        internal SlashVisual(
            PackedScene scene,
            Node2D parent,
            CelVfxGeometry.TargetGeometry geometry,
            int index,
            int arcCount,
            SlashWeight weight)
        {
            _root = scene.Instantiate<Node2D>();
            _root.Name = $"SakuraSlash{index + 1}";
            parent.AddChildSafely(_root);

            _arcCount = arcCount;
            _anchor = _root.GetNode<Node2D>("%SlashAnchor");
            var body = _root.GetNode<ColorRect>("%SlashBody");
            Material = CelVfxGeometry.DuplicateMaterial(body, $"slash {index}");

            // The root never scales. Stroke width is absolute pixels, so a mark keeps its
            // weight across enemy sizes; only the region it is drawn in tracks the body.
            _root.Scale = Vector2.One;
            _anchor.GlobalPosition = geometry.Center;
            body.Size = geometry.Size;
            body.Position = -geometry.Size * 0.5f;

            Material.SetShaderParameter("region_size", geometry.Size);
            Material.SetShaderParameter("arc_count", (float)arcCount);
            Material.SetShaderParameter("weight", WeightValue(weight));
            Material.SetShaderParameter("cross_tilt_deg", CrossTiltDegrees);
            Material.SetShaderParameter("single_tilt_deg", SingleTiltDegrees);
            Material.SetShaderParameter("flash", 0f);
            PushPhases();
        }

        internal ShaderMaterial Material { get; }

        internal bool HasStruck { get; set; }

        /// <summary>
        /// Launches every stroke: the first immediately, the rest spread over
        /// <paramref name="envelope"/> so a flurry gets denser rather than longer.
        /// </summary>
        internal Tween CreateLaunchTween(float envelope, float life, float delay)
        {
            var tween = _root.CreateTween().SetParallel();
            // Kept so the hit-stop has something to pause. The session tracks it for
            // disposal; this reference only ever pauses and resumes it.
            _launch = tween;
            var each = _arcCount > 1 ? envelope / _arcCount : 0f;
            for (var i = 0; i < _arcCount; i++)
            {
                var arc = i;
                tween.TweenMethod(
                        Callable.From<float>(value => SetPhase(arc, value)),
                        0f,
                        1f,
                        life)
                    .SetDelay(delay + i * each);
            }

            return tween;
        }

        /// <summary>
        /// Pauses this target's strokes for <paramref name="duration"/>, then resumes them
        /// from where they stopped.
        /// </summary>
        /// <remarks>
        /// This is the hit-stop, and it has to act on the launch tween because that tween
        /// is the only thing moving: the strokes advance by phase, not by a shader clock.
        /// Resuming rather than restarting is the whole point — a stroke that jumped
        /// forward by the freeze's length would read as a dropped frame.
        /// </remarks>
        internal Tween CreateFreezeTween(float duration)
        {
            var launch = _launch;
            var tween = _root.CreateTween();
            tween.TweenCallback(Callable.From(() =>
            {
                if (launch is not null && GodotObject.IsInstanceValid(launch))
                    launch.Pause();
            }));
            tween.TweenInterval(duration);
            tween.TweenCallback(Callable.From(() =>
            {
                if (launch is not null && GodotObject.IsInstanceValid(launch))
                    launch.Play();
            }));
            return tween;
        }

        /// <summary>
        /// The contact transient: full on the frame damage resolves, then gone. Driven as
        /// a decaying value rather than a fade of the whole mark, so the stroke itself
        /// keeps its own envelope.
        /// </summary>
        internal Tween CreateFlashTween(float duration)
        {
            var tween = _root.CreateTween();
            tween.TweenMethod(
                Callable.From<float>(value => SetParameter("flash", value)),
                1f,
                0f,
                duration);
            return tween;
        }

        private Tween? _launch;

        private void SetPhase(int arc, float value)
        {
            if (arc < 0 || arc >= _phases.Length)
                return;
            _phases[arc] = Math.Clamp(value, 0f, 1f);
            PushPhases();
        }

        private void PushPhases() =>
            SetParameter(
                "arc_phase",
                new Vector4(_phases[0], _phases[1], _phases[2], _phases[3]));

        private void SetParameter(string name, Variant value)
        {
            if (!GodotObject.IsInstanceValid(Material))
                return;
            Material.SetShaderParameter(name, value);
        }
    }
}
