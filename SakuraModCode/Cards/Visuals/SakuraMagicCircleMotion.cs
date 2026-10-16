using Godot;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Presentation-only clock; renewal preserves the current pose and reveal.</summary>
internal sealed class SakuraMagicCircleMotion
{
    internal const float Lifetime = 1.30f;
    internal const float FadeStart = 1.00f;
    internal const float TransitionDuration = 0.12f;
    internal const float RevealDuration = 0.40f;
    private const float ExitDuration = Lifetime - FadeStart;
    private const float RevivalDuration = 0.20f;
    private const float EntryScale = 0.94f;
    private const float ClearReboundDuration = 0.20f;
    private const float SpinDecayDuration = 0.28f;
    private const float RingDrawDuration = 0.28f;
    private const float ScaffoldDrawStart = 0.10f;
    private const float ScaffoldDrawDuration = 0.24f;
    private const float CelestialRevealStart = 0.26f;
    private const float CelestialRevealDuration = 0.08f;
    private const float ForegroundRevealStart = 0.30f;
    private const float ForegroundRevealDuration = 0.10f;
    // Delays the first pulse so it lands as the celestial and foreground layers appear.
    private const float EntryPulseDelay = 0.30f;
    // The exit plays the draw-on backwards: motifs fade, then scaffold and rings rewind.
    private const float ForegroundExitDuration = 0.10f;
    private const float CelestialExitStart = 0.02f;
    private const float CelestialExitDuration = 0.08f;
    private const float ScaffoldRewindStart = 0.04f;
    private const float ScaffoldRewindDuration = 0.20f;
    private const float RingRewindStart = 0.08f;
    private const float RingRewindDuration = 0.22f;
    private const float ExitScale = 0.94f;
    // Clears what is left at the rings' start point as the lifetime ends.
    private const float ExitVanishDuration = 0.03f;
    private readonly bool _transition;
    private float _age;
    private float _revealAge;
    private float _exitAge;
    private float _revivalFrom;
    private float _revivalAge = RevivalDuration;
    private float _spinAge;
    private float _pulseAge = 1f;
    private float _pulseStart;

    internal SakuraMagicCircleMotion(SourceEraClass era, bool transition = false)
    {
        Era = era;
        _transition = transition;
        // The angular draw-on replaces an alpha fade, so every era enters fully opaque.
        _pulseAge = -EntryPulseDelay;
        Visibility = 1f;
        Scale = EntryScale;
        Draw = new Vector4(0f, 0f, 1f, 1f);
        Gates = new Vector4(1f, 1f, 0f, 0f);
    }

    internal SourceEraClass Era { get; }
    internal float Visibility { get; private set; }
    internal float Scale { get; private set; }
    internal float Pulse { get; private set; }
    internal Vector4 Phases { get; private set; }
    internal Vector4 Gates { get; private set; }
    /// <summary>Angular draw-on progress per layer; only rings and scaffold sweep.</summary>
    internal Vector4 Draw { get; private set; }
    internal bool IsAlive => _age < Lifetime;
    /// <summary>Seconds until the foreground emblems start to appear; zero once they have.</summary>
    internal float TimeUntilForeground => MathF.Max(0f, ForegroundRevealStart - _revealAge);

    internal void Refresh()
    {
        // Renewal during the exit rewinds it: erased strokes redraw along their own path.
        if (_age >= FadeStart)
        {
            _revivalFrom = _exitAge;
            _revivalAge = 0f;
        }
        _age = _spinAge = 0f;
        _pulseStart = Pulse;
        _pulseAge = 0f;
    }

    internal void Advance(float delta)
    {
        if (!float.IsFinite(delta) || delta <= 0f)
            return;

        _age += delta;
        _revealAge += delta;
        _revivalAge += delta;
        _pulseAge += delta;
        // Every era spins up on a play and decays to a slow sustained turn.
        var (initialSpeed, settledSpeed) = SpinSpeeds(Era);
        var nextSpinAge = _spinAge + delta;
        var decayIntegral = SpinDecayDuration
            * (MathF.Exp(-_spinAge / SpinDecayDuration)
                - MathF.Exp(-nextSpinAge / SpinDecayDuration));
        Phases += settledSpeed * delta + (initialSpeed - settledSpeed) * decayIntegral;
        _spinAge = nextSpinAge;

        _exitAge = _age >= FadeStart
            ? _age - FadeStart
            : _revivalFrom * (1f - Ease(_revivalAge / RevivalDuration));
        Scale = EntryScale + (1f - EntryScale) * Ease(_revealAge / RevealDuration);
        // Clear overshoots as the draw-on completes, then settles back.
        var reboundAge = _revealAge - (RevealDuration - ClearReboundDuration * 0.5f);
        if (Era == SourceEraClass.Clear && reboundAge > 0f && reboundAge < ClearReboundDuration)
            Scale += 0.015f * MathF.Sin(MathF.PI * reboundAge / ClearReboundDuration);
        Scale *= 1f - (1f - ExitScale) * Ease(_exitAge / ExitDuration);
        Visibility = 1f - Ease((_exitAge - (ExitDuration - ExitVanishDuration)) / ExitVanishDuration);

        // An interrupted pulse begins at its current brightness, never at zero.
        var peak = Era == SourceEraClass.Clow ? 0.16f : 0.18f;
        Pulse = _pulseAge < 0.04f
            ? _pulseStart + (peak - _pulseStart) * Ease(_pulseAge / 0.04f)
            : peak * (1f - Ease((_pulseAge - 0.04f) / 0.14f));
        // A cross-era scaffold waits until the outgoing skeleton has retired.
        var scaffoldStart = _transition ? TransitionDuration : ScaffoldDrawStart;
        // Rewinding replays the entry ease backwards, so the stroke retreats along its path.
        Draw = new Vector4(
            MathF.Min(
                EaseOut(_revealAge / RingDrawDuration),
                EaseOut(1f - (_exitAge - RingRewindStart) / RingRewindDuration)),
            MathF.Min(
                EaseOut((_revealAge - scaffoldStart) / ScaffoldDrawDuration),
                EaseOut(1f - (_exitAge - ScaffoldRewindStart) / ScaffoldRewindDuration)),
            1f,
            1f);
        Gates = new Vector4(
            1f,
            1f,
            MathF.Min(
                Ease((_revealAge - CelestialRevealStart) / CelestialRevealDuration),
                1f - Ease((_exitAge - CelestialExitStart) / CelestialExitDuration)),
            MathF.Min(
                Ease((_revealAge - ForegroundRevealStart) / ForegroundRevealDuration),
                1f - Ease(_exitAge / ForegroundExitDuration)));
    }

    /// <summary>
    /// Angular speeds (R, G, B, A) in rad/s at a play and once settled. Clow turns at
    /// half Sakura's speed around its fixed celestial layer; Clear turns as one rigid
    /// plate against Sakura's rings, keeping its rays on the sun and moon.
    /// </summary>
    internal static (Vector4 Initial, Vector4 Settled) SpinSpeeds(SourceEraClass era) => era switch
    {
        SourceEraClass.Sakura => (new Vector4(1.20f, -0.80f, 0f, 0.32f), new Vector4(0.30f, -0.20f, 0f, 0.08f)),
        SourceEraClass.Clow => (new Vector4(0.60f, -0.40f, 0f, 0.16f), new Vector4(0.15f, -0.10f, 0f, 0.04f)),
        SourceEraClass.Clear => (Vector4.One * -0.80f, Vector4.One * -0.20f),
        _ => throw new ArgumentOutOfRangeException(nameof(era), era, "Unknown magic-circle era.")
    };

    internal static float Ease(float progress) =>
        (1f - MathF.Cos(MathF.PI * Math.Clamp(progress, 0f, 1f))) * 0.5f;

    /// <summary>Quick stroke that slows into the pen lift.</summary>
    internal static float EaseOut(float progress)
    {
        var remaining = 1f - Math.Clamp(progress, 0f, 1f);
        return 1f - remaining * remaining;
    }
}
