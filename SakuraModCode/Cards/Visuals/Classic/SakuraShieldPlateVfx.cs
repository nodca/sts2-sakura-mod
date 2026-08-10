using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Shield's plate: a caster-side cel effect shared by the Clow and Sakura versions.
/// </summary>
/// <remarks>
/// The first effect in this family anchored to Sakura rather than to an enemy, so it
/// competes with the shared prelude for the same screen space. The plate sits in
/// front of her lower body while the magic circle rotates behind her, which is what
/// keeps the two from reading as one mass.
/// <para>
/// One-shot by contract. The plate never tracks remaining Block, never subscribes to
/// incoming attacks, and never reads gameplay state: a persistent shield would put
/// presentation behind gameplay and is a separate product decision.
/// </para>
/// </remarks>
internal sealed class SakuraShieldPlateVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/sakura_shield_plate_vfx.tscn";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    /// <summary>Plate extent in pixels, matching the scene's rect.</summary>
    private const float PlateWidth = 320f;
    private const float PlateHeight = 380f;

    /// <summary>
    /// Horizontal offset from the caster's centre, as a fraction of her half-width,
    /// multiplied by her facing sign. Puts the plate in the forward-lower quadrant
    /// instead of over her head and torso.
    /// </summary>
    private const float ForwardOffsetFraction = 0.42f;

    /// <summary>
    /// Vertical anchor between hitbox centre and floor. Much higher than the magic
    /// circle's 0.62, which is why the shared helper reports centre and floor rather
    /// than one finished point.
    /// </summary>
    private const float FloorBias = 0.30f;

    /// <summary>
    /// In front of the standee, while the magic circle sits behind it at -1. The
    /// sandwich is what separates the two layers in depth; both behind her would read
    /// as one mass. Kept small so combat UI still draws above the plate.
    /// </summary>
    private const int VfxZIndex = 1;

    // Beats, in seconds.
    private const float FormDuration = 0.18f;
    private const float HoldDuration = 2f / 12f;
    private const float RingDuration = 0.70f;
    private const float FadeDuration = 0.22f;

    /// <summary>
    /// Ring frequency in Hz. Bounded from above by the shared stepped clock, not by
    /// taste: detail is sampled at <c>CEL_STEP_HZ</c> = 12, so Nyquist caps a legible
    /// oscillation below 6 Hz, and 6 Hz itself lands two samples per cycle and reads
    /// as alternating flicker. Four gives three samples per cycle, the sparsest rate
    /// that still reads as ringing rather than as jitter.
    /// </summary>
    private const float RingFrequencyHz = 4.0f;

    /// <summary>
    /// Decay rate in reciprocal seconds, chosen so the ring is visibly finished
    /// before the envelope ends. At <see cref="RingCheckSeconds"/> the amplitude is
    /// <c>e^(-3.9)</c> of its start — about 2%, or a quarter pixel at the amplitude
    /// below. A ring still moving at fade-out would read as something invisible
    /// striking the plate, which is the persistent shield this card is not.
    /// </summary>
    private const float RingDecay = 7.1f;

    /// <summary>Initial normal displacement in pixels.</summary>
    private const float RingAmplitudePx = 14.0f;

    /// <summary>
    /// The instant the bounded-amplitude contract is measured at, well inside the
    /// envelope so the guarantee does not rest on the envelope's own end.
    /// </summary>
    private const float RingCheckSeconds = 0.55f;

    /// <summary>
    /// Amplitude at <see cref="RingCheckSeconds"/> must stay under this, in pixels.
    /// Sub-pixel is the threshold because that is when motion stops being visible.
    /// </summary>
    private const float RingCheckMaxPx = 0.5f;

    private static bool _loadFailureLogged;

    private readonly Node2D _anchor;
    private readonly ColorRect _plate;
    private readonly ShaderMaterial _material;
    private readonly Creature _caster;
    private bool _faded;

    private SakuraShieldPlateVfx(Node2D root, NCombatRoom room, Creature caster)
        : base(root, room)
    {
        _caster = caster;
        _anchor = root.GetNode<Node2D>("%ShieldAnchor");
        _plate = root.GetNode<ColorRect>("%ShieldBody");
        _material = CelVfxGeometry.DuplicateMaterial(_plate, "shield plate");
        _material.SetShaderParameter("region_size", new Vector2(PlateWidth, PlateHeight));
        _material.SetShaderParameter("seed", Random.Shared.NextSingle());
        UpdateAnchor();
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    /// <summary>
    /// Safety net, not a timer, and measured against wall clock rather than elapsed
    /// because a hold decouples the two. The beat chain runs about 1.37 s including
    /// the shared prelude; sized well clear of that, because a tight cap becomes a
    /// truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 6.0f;

    internal static SakuraShieldPlateVfx? TryCreate(Creature caster)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (!TryPrepare("Shield plate", LoadScene, out var room, out _, out var scene))
            return null;

        // No usable caster node means nothing to anchor to. Failing here keeps the
        // gameplay path identical rather than dropping a plate at the origin.
        if (room.GetCreatureNode(caster) is not { } casterNode
            || CelVfxGeometry.ResolveCaster(casterNode) is null)
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraShieldPlateVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            // The combat VFX container, never the standee subtree: both idle
            // controllers publish their flip by negating their own Scale, so a child
            // there would have its size and ink width mirrored along with its
            // position.
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new SakuraShieldPlateVfx(root, room, caster);
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

    /// <summary>Shared wand tap, magic circle, and speed lines, then the plate forms.</summary>
    internal async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        ArgumentNullException.ThrowIfNull(card);
        ApplyFacePattern(card);

        if (!await PlayCelPrelude(card, caster))
            return false;

        TaskHelper.RunSafely(TrackAnchor());

        // The crown sweeps up out of the seal. Quadratic Out decelerates into the
        // stop, which is what makes the stop itself read as an impulse.
        var form = Track(Root.CreateTween());
        form.TweenMethod(
                Callable.From<float>(SetFormation),
                0f,
                1f,
                FormDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);

        return await WaitActive(FormDuration);
    }

    /// <summary>
    /// The plate snaps to a stop. That stop is the impulse the ring decays from, and
    /// the hold is the shared signature element the beat is built on.
    /// </summary>
    internal void Impact()
    {
        if (!IsActive())
            return;

        BeginHold();

        // Delayed past the hold: BeginHold freezes shader time, not Godot's tween
        // clock, so a ring overlapping it would flex a plate that should look frozen.
        var ring = Track(Root.CreateTween());
        ring.TweenInterval(HoldDuration);
        ring.TweenMethod(
            Callable.From<float>(SetRing),
            0f,
            RingDuration,
            RingDuration);
    }

    /// <summary>
    /// Fades the plate out, then releases. The base <c>Dispose</c> it ends in is
    /// idempotent and also covers combat end, tree exit, exceptions, and the cap.
    /// </summary>
    internal void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        // Waits out the hold and the whole ring: fading while the plate still flexes
        // would hide the decay that makes it read as one-shot. Tween time keeps
        // running through a hold, so the interval covers both.
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(HoldDuration + RingDuration);
        fade.TweenProperty(_plate, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    /// <summary>
    /// Follows the caster every frame so the plate stays with her as she moves and
    /// re-reads her facing, which can flip mid-effect.
    /// </summary>
    private async Task TrackAnchor()
    {
        try
        {
            while (IsActive())
            {
                UpdateAnchor();
                await Root.AwaitProcessFrame();
            }
        }
        catch (OperationCanceledException) when (!IsActive())
        {
        }
    }

    /// <summary>
    /// Points the plate's face at the same two seal masks the prelude circle uses,
    /// in the era's ink colour.
    /// </summary>
    /// <remarks>
    /// The face stays disabled when the card has no era. That is the same condition
    /// <c>ShouldPlayCelPrelude</c> gates on, so a card reaching here without one
    /// would show a seal the prelude never drew — and the shield's face is a
    /// quotation of that circle, not decoration that stands on its own.
    /// </remarks>
    private void ApplyFacePattern(CardModel card)
    {
        if (MagicCircleInkColour(card) is not { } inkColour)
        {
            _material.SetShaderParameter("face_enabled", 0f);
            return;
        }

        var (ink, knockout) = LoadMagicCircleMasks();
        _material.SetShaderParameter("face_ink", ink);
        _material.SetShaderParameter("face_knockout", knockout);
        _material.SetShaderParameter("face_colour", inkColour);
        _material.SetShaderParameter("face_enabled", 1f);
    }

    private void SetFormation(float value) =>
        _material.SetShaderParameter("formation", Math.Clamp(value, 0f, 1f));

    private void SetRing(float seconds) =>
        _material.SetShaderParameter("ring_px", RingOffsetPx(seconds));

    /// <summary>
    /// Signed edge displacement of the damped oscillator, in pixels, at
    /// <paramref name="seconds"/> after the forming impulse.
    /// </summary>
    /// <remarks>
    /// The physics lives here rather than in the shader so there is one owner for the
    /// frequency, the decay, and the quantization; the surface receives a sign and a
    /// magnitude and needs to know nothing else.
    /// <para>
    /// Time is snapped to the stepped clock before the cosine is taken, not after. A
    /// smooth oscillation sampled onto steps would still glide between them; stepping
    /// the input is what makes the flex land on drawn frames.
    /// </para>
    /// </remarks>
    internal static float RingOffsetPx(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return 0f;

        var stepped = MathF.Floor(seconds * StepFrequency) / StepFrequency;
        return RingAmplitudePx
            * MathF.Exp(-RingDecay * stepped)
            * MathF.Cos(MathF.Tau * RingFrequencyHz * stepped);
    }

    /// <summary>
    /// The bounded-amplitude contract: the ring must be visually finished before the
    /// envelope ends, or the plate reads as a persistent shield being struck.
    /// </summary>
    internal static bool RingSettlesWithinEnvelope() =>
        RingAmplitudePx * MathF.Exp(-RingDecay * RingCheckSeconds) < RingCheckMaxPx
        && RingCheckSeconds < RingDuration
        && RingFrequencyHz < StepFrequency * 0.5f;

    private void UpdateAnchor()
    {
        if (!GodotObject.IsInstanceValid(_anchor)
            || Room.GetCreatureNode(_caster) is not { } casterNode
            || CelVfxGeometry.ResolveCaster(casterNode) is not { } anchor)
        {
            return;
        }

        // Only the sign is taken from the standee. Mirroring the plate itself would
        // be a no-op — it is symmetric — so the single decision the flip drives is
        // which side of her the plate sits on.
        var forward = anchor.BodySize.X * 0.5f * ForwardOffsetFraction * anchor.FacingSign;
        _anchor.GlobalPosition = new Vector2(
            anchor.BodyCenter.X + forward,
            Mathf.Lerp(anchor.BodyCenter.Y, anchor.Floor.Y, FloorBias));
    }

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not present the Shield plate cel VFX: {exception}");
    }
}
