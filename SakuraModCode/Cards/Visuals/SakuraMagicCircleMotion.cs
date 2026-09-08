using Godot;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Presentation-only clock; renewal preserves the current pose and reveal.</summary>
internal sealed class SakuraMagicCircleMotion
{
    internal const float Lifetime = 1.15f;
    internal const float TransitionDuration = 0.12f;
    private const float SpinDecayDuration = 0.28f;
    private readonly bool _transition;
    private float _age;
    private float _revealAge;
    private float _envelopeAge;
    private float _spinAge;
    private float _pulseAge = 1f;
    private float _pulseStart;
    private float _entryVisibility;
    private float _entryScale;
    private Vector4 _velocity;
    private Vector4 _rotation = Vector4.Zero;

    internal SakuraMagicCircleMotion(SourceEraClass era, bool transition = false)
    {
        Era = era;
        _transition = transition;
        _entryScale = era == SourceEraClass.Sakura ? 0.78f : 0.84f;
        _entryVisibility = era == SourceEraClass.Sakura ? 0f : 1f;
        Visibility = _entryVisibility;
        Scale = _entryScale;
        Impulse();
    }

    internal SourceEraClass Era { get; }
    internal float Visibility { get; private set; }
    internal float Scale { get; private set; }
    internal float Pulse { get; private set; }
    internal Vector4 Phases { get; private set; }
    internal Vector4 Gates { get; private set; }
    internal bool IsAlive => _age < Lifetime;

    internal void Refresh()
    {
        if (_age >= 0.85f)
        {
            _entryVisibility = Visibility;
            _entryScale = Scale;
            _envelopeAge = 0f;
        }
        _age = _spinAge = 0f;
        _pulseStart = Pulse;
        _pulseAge = 0f;
        Impulse();
    }

    internal void Advance(float delta)
    {
        if (!float.IsFinite(delta) || delta <= 0f)
            return;

        _age += delta;
        _revealAge += delta;
        _envelopeAge += delta;
        _pulseAge += delta;
        if (Era == SourceEraClass.Sakura)
        {
            var nextSpinAge = _spinAge + delta;
            var decayIntegral = SpinDecayDuration
                * (MathF.Exp(-_spinAge / SpinDecayDuration)
                    - MathF.Exp(-nextSpinAge / SpinDecayDuration));
            Phases += new Vector4(0.30f, -0.20f, 0f, 0.08f) * delta
                + new Vector4(0.90f, -0.60f, 0f, 0.24f) * decayIntegral;
            _spinAge = nextSpinAge;
        }
        else
        {
            // Exact damped motion keeps the same trajectory at different frame rates.
            var omega = Era == SourceEraClass.Clow ? 4f : 6f;
            var damping = Era == SourceEraClass.Clow ? 1f : 0.55f;
            for (var i = 0; i < 2; i++)
            {
                var x = _rotation[i];
                var v = _velocity[i];
                var decay = MathF.Exp(-damping * omega * delta);
                if (damping == 1f)
                {
                    var b = v + omega * x;
                    _rotation[i] = (x + b * delta) * decay;
                    _velocity[i] = (v - omega * b * delta) * decay;
                }
                else
                {
                    var frequency = omega * MathF.Sqrt(1f - damping * damping);
                    var b = (v + damping * omega * x) / frequency;
                    var cosine = MathF.Cos(frequency * delta);
                    var sine = MathF.Sin(frequency * delta);
                    _rotation[i] = (x * cosine + b * sine) * decay;
                    _velocity[i] = (v * cosine
                        - (damping * omega * b + frequency * x) * sine) * decay;
                }
            }
            var yLimit = Era == SourceEraClass.Clow ? 0.09f : 0.065f;
            Phases = new Vector4(0.16f * MathF.Tanh(_rotation.X / 0.16f),
                yLimit * MathF.Tanh(_rotation.Y / yLimit), 0f, 0f);
        }

        var entry = Ease(_envelopeAge / 0.20f);
        Visibility = _entryVisibility + (1f - _entryVisibility) * entry;
        Scale = _entryScale + (1f - _entryScale) * entry;
        if (Era == SourceEraClass.Clear && _revealAge < 0.20f)
            Scale += 0.015f * MathF.Sin(MathF.PI * _revealAge / 0.20f);
        if (_age >= 0.85f)
        {
            var fade = Ease((_age - 0.85f) / 0.30f);
            Visibility = 1f - fade;
            Scale = 1f - 0.18f * fade;
        }

        // An interrupted pulse begins at its current brightness, never at zero.
        var peak = Era == SourceEraClass.Clow ? 0.16f : 0.18f;
        Pulse = _pulseAge < 0.04f
            ? _pulseStart + (peak - _pulseStart) * Ease(_pulseAge / 0.04f)
            : peak * (1f - Ease((_pulseAge - 0.04f) / 0.14f));
        Gates = new Vector4(
            Ease(_revealAge / 0.10f),
            Ease((_revealAge - (_transition ? 0.12f : 0.06f)) / 0.10f),
            Ease((_revealAge - (_transition ? 0.12f : 0.10f)) / 0.10f),
            Ease((_revealAge - 0.12f) / 0.10f));
        if (Era == SourceEraClass.Sakura && !_transition)
            Gates = Vector4.One;
    }

    private void Impulse()
    {
        if (Era == SourceEraClass.Clow)
        {
            _velocity.X = Math.Clamp(_velocity.X + 0.44f * Math.Max(0f, 1f - MathF.Abs(_rotation.X) / 0.16f), -0.44f, 0.44f);
            _velocity.Y = Math.Clamp(_velocity.Y - 0.28f * Math.Max(0f, 1f - MathF.Abs(_rotation.Y) / 0.09f), -0.28f, 0.28f);
        }
        else if (Era == SourceEraClass.Clear)
            _velocity.Y = Math.Clamp(_velocity.Y + 0.20f * Math.Max(0f, 1f - MathF.Abs(_rotation.Y) / 0.065f), -0.20f, 0.20f);
    }

    internal static float Ease(float progress) =>
        (1f - MathF.Cos(MathF.PI * Math.Clamp(progress, 0f, 1f))) * 0.5f;
}
