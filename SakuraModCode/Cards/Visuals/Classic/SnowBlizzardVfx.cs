using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Snow's blizzard: one enemy-wide snowfall curtain plus one crystal region per
/// target. Each <see cref="Cues.Impact"/> throws one six-arm snowflake dart that
/// settles onto its target and leaves a creeping frost layer behind; the Clow
/// finale holds the whole field for two stepped frames while every target takes
/// one simultaneous volley dart.
/// </summary>
/// <remarks>
/// A single class deriving from <see cref="CelVfxSession"/> rather than Aqua's
/// outer-static-plus-nested-session pair: <c>TryPrepare</c> is protected, so an
/// outer static class cannot reach it without restating the guard logic.
/// </remarks>
internal sealed class SnowBlizzardVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/snow_blizzard_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/snow_crystal_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/snow_blizzard.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    /// <summary>
    /// The session counts hits per target itself; it never reads the card's
    /// water-card count, the selection RNG, or frostbite stacks. Twelve beats is
    /// the sizing worst case for the lifetime cap and the envelope assertion.
    /// </summary>
    internal const int WorstCaseBeats = 12;

    // The dart tween's split point, mirroring DART_FORM_END in
    // snow_blizzard.gdshader. Below it the shader steps the growth onto whole
    // frames; above it the fall maps linearly onto the target center. The two
    // constants must move together.
    private const float DartFormEnd = 0.30f;

    // Curtain beats, in whole frames of the shared stepped clock.
    private const int CurtainFormationSteps = 4;
    internal const float CurtainFormationDuration = 4f / StepFrequency;
    internal const float CurtainFadeDuration = 0.32f;

    // Beat phases. Bloom is a short transient and never compresses; only the
    // description (formation, fall) compresses as beats accumulate.
    internal const float BloomDuration = 1f / StepFrequency;
    internal const float FrostCreepDuration = 3f / StepFrequency;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at
    /// two stepped frames. Volley tweens wait it out: <c>BeginHold</c> freezes
    /// shader time, not Godot's tween clock, so starting them during the hold
    /// would show a motionless curtain throwing moving darts.
    /// </summary>
    internal const float HoldDuration = 2f / StepFrequency;

    // Fall compression, per design: 0.22 s for the first three beats, 0.16 s from
    // the fourth, stepping down to a 0.12 s floor.
    private const int UncompressedBeats = 3;
    private const float FirstFallSeconds = 0.22f;
    private const float CompressedFallSeconds = 0.16f;
    private const float FloorFallSeconds = 0.12f;
    private const float FallCompressionStep = 0.02f;

    // Curtain geometry, card-owned. Headroom above the target envelope, ground
    // coverage below it, and a viewport-fraction width cap so the band never
    // spans the whole battlefield or crosses every intent.
    private const float CurtainHeadroomPx = 140f;
    private const float CurtainGroundPadPx = 10f;
    private const float CurtainSidePadPx = 32f;
    private const float CurtainMinWidth = 300f;
    private const float CurtainMaxWidth = 640f;
    private const float CurtainMinHeight = 340f;
    private const float CurtainViewportWidthFraction = 0.34f;
    private const float CurtainLiftPx = 26f;

    // Enemy-side layering, per the Hail precedent: the curtain behind, the
    // crystals on top, both under the combat UI.
    private const int CurtainZIndex = 2990;
    private const int CrystalZIndex = 3000;

    /// <summary>
    /// Wall-clock bound on the shared prelude before the curtain starts: the
    /// chibi route is card rise 0.24 s plus wand tap 0.08 s, plus margin.
    /// </summary>
    private const float WorstPreludeSeconds = 0.40f;

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _curtainMaterial;
    private readonly ColorRect _curtainBody;
    private readonly Dictionary<Creature, CrystalVisual> _crystals = [];
    private bool _faded;
    private bool _finale;

    private SnowBlizzardVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> creatures)
        : base(root, room)
    {
        _curtainBody = root.GetNode<ColorRect>("%SnowfallBody");
        _curtainMaterial = CelVfxGeometry.DuplicateMaterial(_curtainBody, "snow blizzard curtain");

        var envelopes = new List<CelVfxGeometry.TargetGeometry>(creatures.Count);
        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (_crystals.ContainsKey(creature))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, creature, index, Budget);
            envelopes.Add(geometry);
            _crystals.Add(creature, new CrystalVisual(targetScene, root, geometry, index));
        }

        LayoutCurtain(room, envelopes);

        _curtainMaterial.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _curtainMaterial.SetShaderParameter("curtain", 0f);
        _curtainMaterial.SetShaderParameter("opacity", 1f);
    }

    /// <summary>
    /// The dart is a wide wheel, so its region is wider than Hail's narrow shard:
    /// the folded reach spans about 0.48 of the region width and must hold six
    /// arms plus their ink, which MinWidth 210 clears with room to spare.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 20f,
        VerticalPadding: 26f,
        MinWidth: 210f,
        MinHeight: 210f,
        MaxWidth: 440f,
        MaxHeight: 470f,
        FallbackWidth: 240f,
        FallbackHeight: 260f,
        FloorClearance: 8f,
        MaxViewportWidthFraction: 0.30f,
        MaxViewportHeightFraction: 0.56f);

    protected override IEnumerable<ShaderMaterial> Materials =>
        [_curtainMaterial, .. _crystals.Values.Select(static crystal => crystal.Material)];

    /// <summary>
    /// Safety net on wall clock, not a timer. The compressed worst case is the
    /// prelude, the curtain, twelve beats (three uncompressed then the floor),
    /// the held finale volley, and the fade — about 7.7 s of field plus the
    /// gameplay awaits that pace the beats. Sized well clear of that envelope,
    /// because a cap set tight becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 15f;

    /// <summary>
    /// One beat's total occupancy: stepped formation, linear fall, bloom flare.
    /// The first three beats take four formation frames and a 0.22 s fall; from
    /// the fourth beat the description compresses toward the floor of three
    /// frames and 0.12 s. Monotonically non-increasing, and the bloom transient
    /// never compresses.
    /// </summary>
    internal static float BeatSeconds(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return FormationSeconds(index) + FallSeconds(index) + BloomDuration;
    }

    /// <summary>Formation span, counted in whole frames of the stepped clock.</summary>
    internal static float FormationSeconds(int index) =>
        index < UncompressedBeats ? 4f / StepFrequency : 3f / StepFrequency;

    /// <summary>Fall span: terminal velocity, so the tween is linear in time.</summary>
    internal static float FallSeconds(int index) =>
        index < UncompressedBeats
            ? FirstFallSeconds
            : Math.Max(
                FloorFallSeconds,
                CompressedFallSeconds - FallCompressionStep * (index - UncompressedBeats));

    /// <summary>
    /// The whole field's wall-clock envelope for a given gameplay beat count,
    /// including the Clow finale's held volley. The curve test asserts the total
    /// stays bounded as beats accumulate.
    /// </summary>
    internal static float TotalEnvelopeSeconds(int beatCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(beatCount);
        var beats = 0f;
        for (var i = 0; i < beatCount; i++)
            beats += BeatSeconds(i);
        return WorstPreludeSeconds
            + CurtainFormationDuration
            + beats
            + HoldDuration
            + BeatSeconds(beatCount)
            + CurtainFadeDuration;
    }

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Snow blizzard",
            () => TryCreate(targets),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<SnowBlizzardVfx> scope)
    {
        internal void Impact(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("impact", session => session.Impact(target));
        }

        internal void Finale() => scope.Invoke("finale", static session => session.Finale());
    }

    private static SnowBlizzardVfx? TryCreate(IReadOnlyList<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return null;
        if (!TryPrepare(
                "Snow blizzard",
                LoadScenes,
                out var room,
                out _,
                out var scenes))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            // The curtain is added first so every crystal draws above it; both
            // layers stay under the combat UI at the Hail precedent's depths.
            root = scenes.Curtain.Instantiate<Node2D>();
            root.Name = "SakuraSnowBlizzardVfx";
            root.ZAsRelative = false;
            root.ZIndex = CurtainZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new SnowBlizzardVfx(root, room, scenes.Target, targets);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing
            // it is still empty.
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
    /// Shared wand tap, magic circle, and speed lines, then the curtain steps
    /// down over four whole frames. A zero-beat play stops here: the snow still
    /// falls once, no dart is ever thrown, and the outro fades the band.
    /// </summary>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var formation = Track(Root.CreateTween());
        formation.TweenMethod(
                Callable.From<float>(value => _curtainMaterial.SetShaderParameter(
                    "curtain",
                    Mathf.Floor(Mathf.Clamp(value, 0f, 1f) * CurtainFormationSteps)
                        / CurtainFormationSteps)),
                0f,
                1f,
                CurtainFormationDuration);
        if (!await WaitActive(CurtainFormationDuration))
            return false;

        return IsActive();
    }

    /// <summary>
    /// One hit against one target: the dart crystallizes open on the stepped
    /// clock, settles onto the target center at terminal velocity while spinning
    /// one slow half-turn, flares, and a frost layer creeps outward. Hit
    /// counting is the session's own state — reading the card's segment loop
    /// would put gameplay numbers behind a presentation decision.
    /// </summary>
    private void Impact(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsActive() || !target.IsAlive || !_crystals.TryGetValue(target, out var crystal))
            return;

        PlayBeat(crystal, delay: 0f);
    }

    /// <summary>
    /// The Clow all-enemy finale: hold the whole field for two stepped frames,
    /// then every target takes one simultaneous dart. The hold freezes shader
    /// time, so the volley's tweens are delayed past it rather than overlapped.
    /// </summary>
    private void Finale()
    {
        if (_finale || !IsActive())
            return;

        _finale = true;
        BeginHold(2);
        foreach (var crystal in _crystals.Values)
            PlayBeat(crystal, HoldDuration);
    }

    /// <summary>
    /// One dart beat on one crystal, replacing whatever beat is still in flight
    /// there. All tweens are created fresh and fully configured before the first
    /// frame can process them; the frost runs on its own tween so a following
    /// beat restarts the dart without freezing a half-grown lace.
    /// </summary>
    private void PlayBeat(CrystalVisual crystal, float delay)
    {
        var index = crystal.Hits;
        crystal.Hits = index + 1;

        var formation = FormationSeconds(index);
        var fall = FallSeconds(index);
        var material = crystal.Material;

        crystal.KillBeatTweens();
        material.SetShaderParameter("dart", 0f);
        material.SetShaderParameter("bloom", 0f);

        var dart = Track(crystal.Root.CreateTween());
        crystal.DartTween = dart;
        dart.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter("dart", value)),
                0f,
                DartFormEnd,
                formation)
            .SetDelay(delay);
        dart.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter("dart", value)),
                DartFormEnd,
                1f,
                fall);
        dart.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter("bloom", value)),
                0f,
                1f,
                BloomDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);

        // The spin is session-owned and continuous: one signed half-turn per
        // beat, sine-eased, alternating direction so repeated darts tumble both
        // ways. Half a cycle over at least 0.37 s stays near 1.4 Hz, well under
        // the shared 4 Hz ceiling for legible oscillation.
        var spinStart = crystal.Spin;
        var spinTarget = spinStart + crystal.NextSpinSign() * Mathf.Pi;
        var spin = Track(crystal.Root.CreateTween());
        crystal.SpinTween = spin;
        spin.TweenMethod(
                Callable.From<float>(value =>
                {
                    crystal.Spin = value;
                    material.SetShaderParameter("dart_spin", value);
                }),
                spinStart,
                spinTarget,
                formation + fall)
            .SetDelay(delay)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);

        // Frost settles one layer outward after the hit. The uniform moves
        // linearly; the shader steps the creep onto whole frames itself.
        var frostStart = crystal.FrostValue;
        var frostTarget = MathF.Floor(frostStart) + 1f;
        var frost = Track(crystal.Root.CreateTween());
        crystal.FrostTween = frost;
        frost.TweenMethod(
                Callable.From<float>(value =>
                {
                    crystal.FrostValue = value;
                    material.SetShaderParameter("frost", value);
                }),
                frostStart,
                frostTarget,
                FrostCreepDuration)
            .SetDelay(delay + formation + fall + BloomDuration);
    }

    /// <summary>
    /// Thins the curtain upward and drifts it up while the frost sublimates:
    /// opacity retracts the lace tip-first in the shader, so no shard cracks and
    /// nothing melts. This is the Release beat of the session contract; the base
    /// <c>Dispose</c> it ends in is idempotent and also covers combat end, tree
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
        var fade = Track(Root.CreateTween().SetParallel());
        fade.TweenMethod(
                Callable.From<float>(value => _curtainMaterial.SetShaderParameter("curtain", value)),
                1f,
                0f,
                CurtainFadeDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Sine);
        fade.TweenMethod(
                Callable.From<float>(value => _curtainMaterial.SetShaderParameter("opacity", value)),
                1f,
                0f,
                CurtainFadeDuration);
        foreach (var crystal in _crystals.Values)
        {
            var material = crystal.Material;
            fade.TweenMethod(
                    Callable.From<float>(value => material.SetShaderParameter("opacity", value)),
                    1f,
                    0f,
                    CurtainFadeDuration);
        }

        fade.TweenProperty(Root, "position", Root.Position + Vector2.Up * CurtainLiftPx, CurtainFadeDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        fade.Chain().TweenCallback(Callable.From(Dispose));
    }

    /// <summary>
    /// The curtain spans the union envelope of every target region, with
    /// headroom above, ground coverage below, and a viewport-fraction width cap.
    /// The offsets live here, not in <c>CelVfxGeometry</c>: they are this card's
    /// weather reading, not facts about a creature.
    /// </summary>
    private void LayoutCurtain(NCombatRoom room, IReadOnlyList<CelVfxGeometry.TargetGeometry> envelopes)
    {
        var minLeft = float.MaxValue;
        var maxRight = float.MinValue;
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var envelope in envelopes)
        {
            minLeft = Math.Min(minLeft, envelope.Center.X - envelope.Size.X * 0.5f);
            maxRight = Math.Max(maxRight, envelope.Center.X + envelope.Size.X * 0.5f);
            top = Math.Min(top, envelope.Center.Y - envelope.Size.Y * 0.5f);
            bottom = Math.Max(bottom, envelope.Center.Y + envelope.Size.Y * 0.5f);
        }

        var viewportWidth = room.CombatVfxContainer.GetViewportRect().Size.X;
        var widthCap = float.IsFinite(viewportWidth) && viewportWidth > 0f
            ? viewportWidth * CurtainViewportWidthFraction
            : CurtainMaxWidth;
        var maxWidth = Math.Max(CurtainMinWidth, Math.Min(CurtainMaxWidth, widthCap));
        var width = Math.Clamp(
            Math.Max(CurtainMinWidth, maxRight - minLeft + CurtainSidePadPx * 2f),
            CurtainMinWidth,
            maxWidth);

        var bandTop = top - CurtainHeadroomPx;
        var height = Math.Max(bottom + CurtainGroundPadPx - bandTop, CurtainMinHeight);
        var size = new Vector2(width, height);

        Root.GlobalPosition = new Vector2((minLeft + maxRight) * 0.5f, bandTop + height * 0.5f);
        _curtainBody.Size = size;
        _curtainBody.Position = -size * 0.5f;
        _curtainMaterial.SetShaderParameter("region_size", size);
    }

    private static (PackedScene Curtain, PackedScene Target) LoadScenes()
        => (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Snow blizzard VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    /// <summary>
    /// One target's crystal region: the falling dart wheel and the accumulating
    /// frost, both drawn by the layer-1 path of the shared snow shader.
    /// </summary>
    private sealed class CrystalVisual
    {
        internal CrystalVisual(
            PackedScene scene,
            Node2D parent,
            CelVfxGeometry.TargetGeometry geometry,
            int index)
        {
            Root = scene.Instantiate<Node2D>();
            Root.Name = $"SnowCrystal{index + 1}";
            Root.ZAsRelative = false;
            Root.ZIndex = CrystalZIndex;
            parent.AddChildSafely(Root);
            Root.GlobalPosition = geometry.Center;

            // The root never scales. Sizing travels to the shader as
            // region_size, which is what holds ink weight constant in screen
            // pixels across every enemy size.
            Root.Scale = Vector2.One;
            var body = Root.GetNode<ColorRect>("%CrystalBody");
            Material = CelVfxGeometry.DuplicateMaterial(body, $"snow crystal {index}");
            body.Size = geometry.Size;
            body.Position = -geometry.Size * 0.5f;
            Material.SetShaderParameter("region_size", geometry.Size);
            Material.SetShaderParameter("seed", index * 0.317f + 0.19f);
            Material.SetShaderParameter("dart", 0f);
            Material.SetShaderParameter("dart_spin", 0f);
            Material.SetShaderParameter("bloom", 0f);
            Material.SetShaderParameter("frost", 0f);
            Material.SetShaderParameter("opacity", 1f);
            _spinSign = index % 2 == 0 ? 1 : -1;
        }

        internal Node2D Root { get; }
        internal ShaderMaterial Material { get; }
        internal int Hits { get; set; }
        internal float FrostValue { get; set; }
        internal float Spin { get; set; }
        internal Tween? DartTween { get; set; }
        internal Tween? SpinTween { get; set; }
        internal Tween? FrostTween { get; set; }

        private int _spinSign;

        /// <summary>Alternating rotation direction, seeded per target index.</summary>
        internal int NextSpinSign()
        {
            _spinSign = -_spinSign;
            return _spinSign;
        }

        /// <summary>
        /// Stops the in-flight beat so a new one starts from rest. Session
        /// disposal kills every tracked tween regardless.
        /// </summary>
        internal void KillBeatTweens()
        {
            Kill(DartTween);
            Kill(SpinTween);
            Kill(FrostTween);
            DartTween = null;
            SpinTween = null;
            FrostTween = null;
        }

        private static void Kill(Tween? tween)
        {
            if (tween is not null && GodotObject.IsInstanceValid(tween) && tween.IsValid())
                tween.Kill();
        }
    }
}
