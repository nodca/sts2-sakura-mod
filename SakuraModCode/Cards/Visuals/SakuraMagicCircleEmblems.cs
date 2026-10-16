using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Presentation-only highlight clock for the Clear circle's element emblems, in the
/// mask's quadrant order: fire (top left), wind (top right), earth (bottom left),
/// water (bottom right).
/// </summary>
internal sealed class SakuraMagicCircleEmblems
{
    // Exponential approach; about 95% of a state change lands within 0.15s.
    private const float LitTimeConstant = 0.05f;
    private const float FlashRise = 0.05f;
    private const float FlashFall = 0.30f;
    private const float FlashGain = 0.8f;
    private const float MaxGlow = 1f + FlashGain;
    private const float NoFlash = FlashRise + FlashFall;

    private static readonly SakuraElement[] QuadrantElements =
        [SakuraElement.Fire, SakuraElement.Wind, SakuraElement.Earth, SakuraElement.Water];

    private Vector4 _lit;
    private Vector4 _flashAge = Vector4.One * NoFlash;
    private Vector4 _flashFrom = Vector4.Zero;
    private Vector4 _flash = Vector4.Zero;

    /// <summary>States already active when the circle appears are lit from the start.</summary>
    internal SakuraMagicCircleEmblems(SakuraElementSet active)
    {
        _lit = Target(active);
        Dim = active == SakuraElementSet.None ? 0f : 1f;
        Glow = _lit;
    }

    internal Vector4 Glow { get; private set; }
    /// <summary>How strongly unlit emblems recede; zero while no state is active.</summary>
    internal float Dim { get; private set; }

    /// <summary>Flashes the played card's elements once the foreground layer is visible.</summary>
    internal void Trigger(SakuraElementSet cardElements, float delay)
    {
        delay = float.IsFinite(delay) ? MathF.Max(0f, delay) : 0f;
        for (var i = 0; i < 4; i++)
        {
            if (!cardElements.HasElement(QuadrantElements[i]))
                continue;

            // A flash interrupted mid-way rises again from its current brightness.
            _flashFrom[i] = _flash[i];
            _flashAge[i] = -delay;
        }
    }

    internal void Advance(float delta, SakuraElementSet active)
    {
        if (!float.IsFinite(delta) || delta <= 0f)
            return;

        var approach = 1f - MathF.Exp(-delta / LitTimeConstant);
        _lit += (Target(active) - _lit) * approach;
        var dimTarget = active == SakuraElementSet.None ? 0f : 1f;
        Dim += (dimTarget - Dim) * approach;
        for (var i = 0; i < 4; i++)
        {
            _flashAge[i] = MathF.Min(_flashAge[i] + delta, NoFlash);
            var age = _flashAge[i];
            _flash[i] = age < 0f
                ? _flashFrom[i]
                : age < FlashRise
                    ? _flashFrom[i] + (1f - _flashFrom[i]) * SakuraMagicCircleMotion.Ease(age / FlashRise)
                    : 1f - SakuraMagicCircleMotion.Ease((age - FlashRise) / FlashFall);
        }

        var glow = _lit + _flash * FlashGain;
        Glow = new Vector4(
            Math.Clamp(glow.X, 0f, MaxGlow),
            Math.Clamp(glow.Y, 0f, MaxGlow),
            Math.Clamp(glow.Z, 0f, MaxGlow),
            Math.Clamp(glow.W, 0f, MaxGlow));
    }

    private static Vector4 Target(SakuraElementSet active)
    {
        var target = Vector4.Zero;
        for (var i = 0; i < 4; i++)
            target[i] = active.HasElement(QuadrantElements[i]) ? 1f : 0f;
        return target;
    }
}
