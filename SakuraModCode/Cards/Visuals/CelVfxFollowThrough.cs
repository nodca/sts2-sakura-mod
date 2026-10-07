namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Follow-through for a drawn creature's loose ends (feather tips, plumes,
/// whiskers): turns the motion a session actually applied to the creature's
/// node into two lag signals for its shader, each in [-1, 1].
/// <list type="bullet">
/// <item><see cref="Sway"/> lags the node's rotation, in the creature's own
/// unmirrored frame.</item>
/// <item><see cref="Flex"/> lags the wing spread opening or closing.</item>
/// </list>
/// Each signal is an underdamped spring chasing a target proportional to the
/// rate of change, so it is zero while the creature holds still, trails a turn,
/// overshoots a little and settles. Pure arithmetic: no node access, so the
/// session owns what it measures and plain tests can drive it.
/// </summary>
internal sealed class CelVfxFollowThrough
{
    /// <summary>Spring stiffness (rad/s): settles in about 0.3s.</summary>
    internal const float Omega = 24f;
    /// <summary>Below 1, so the loose ends overshoot slightly before settling.</summary>
    internal const float Damping = 0.45f;
    /// <summary>Integration substep cap, so a frame hitch cannot destabilise the spring.</summary>
    internal const float MaxStep = 1f / 120f;
    /// <summary>A turn this fast (rad/s) saturates <see cref="Sway"/>.</summary>
    internal const float TurnRate = 9f;
    /// <summary>A spread change this fast (per second) saturates <see cref="Flex"/>.</summary>
    internal const float SpreadRate = 6f;

    private float _sway;
    private float _swayVelocity;
    private float _flex;
    private float _flexVelocity;
    private float _lastRotation;
    private float _lastSpread;
    private bool _primed;

    internal float Sway => _sway;
    internal float Flex => _flex;

    /// <summary>
    /// Advances by one frame. <paramref name="facing"/> is the sign of the node's
    /// X scale: a mirrored node turns the other way in its own frame.
    /// </summary>
    internal void Update(float delta, float rotation, float spread, float facing)
    {
        if (!float.IsFinite(delta) || delta <= 0f)
            return;

        if (!_primed)
        {
            _primed = true;
            _lastRotation = rotation;
            _lastSpread = spread;
            return;
        }

        var turn = WrapAngle(rotation - _lastRotation) / delta;
        var open = (spread - _lastSpread) / delta;
        _lastRotation = rotation;
        _lastSpread = spread;

        var mirror = facing < 0f ? -1f : 1f;
        Spring(ref _sway, ref _swayVelocity, Math.Clamp(-turn * mirror / TurnRate, -1f, 1f), delta);
        Spring(ref _flex, ref _flexVelocity, Math.Clamp(-open / SpreadRate, -1f, 1f), delta);
    }

    internal static void Spring(ref float value, ref float velocity, float target, float delta)
    {
        var remaining = delta;
        while (remaining > 0f)
        {
            var step = Math.Min(remaining, MaxStep);
            remaining -= step;
            velocity += (Omega * Omega * (target - value) - 2f * Damping * Omega * velocity) * step;
            value += velocity * step;
            if (value is > 1f or < -1f)
            {
                value = Math.Clamp(value, -1f, 1f);
                velocity = 0f;
            }
        }
    }

    private static float WrapAngle(float angle)
    {
        var wrapped = (angle + MathF.PI) % (2f * MathF.PI);
        if (wrapped < 0f)
            wrapped += 2f * MathF.PI;
        return wrapped - MathF.PI;
    }
}
