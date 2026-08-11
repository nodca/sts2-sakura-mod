using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Gale's single luminous crescent, launched from Sakura toward the target with
/// its convex edge facing forward.
/// </summary>
internal sealed class GaleWindBladeVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/gale_wind_blade_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/gale_wind_blade.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    private const float FlightDuration = 3f / StepFrequency;
    private const float FormationDuration = 2f / StepFrequency;
    private const float HoldDuration = 2f / StepFrequency;
    private const float DissipateDuration = 0.30f;
    private const float FadeDuration = 0.18f;

    private static readonly Vector2 BladeRegion = new(240f, 300f);

    // The crescent occupies the +X side of its local region. Contact therefore
    // aligns the centre of that visible stroke, not an invented spear tip, with
    // the target.
    private const float CrescentCenterOffsetPx = 56f;
    private const float WakeAttachmentOffsetPx = 38f;
    private const float OvershootPx = 96f;
    private const float LaunchBodyBias = 0.72f;
    private const float LaunchFormation = 0.38f;
    private const int WakePointCount = 10;
    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _material;
    private readonly Node2D _carrier;
    private readonly Line2D _wake;
    private readonly Vector2[] _wakePoints = new Vector2[WakePointCount];
    private readonly Vector2 _launch;
    private readonly Vector2 _flightEnd;
    private readonly Vector2 _overshoot;
    private readonly Vector2 _direction;
    private bool _impacted;
    private bool _faded;

    private GaleWindBladeVfx(
        Node2D root,
        NCombatRoom room,
        CelVfxGeometry.CasterAnchor caster,
        CelVfxGeometry.TargetGeometry target)
        : base(root, room)
    {
        _carrier = root.GetNode<Node2D>("%BladeCarrier");
        _wake = root.GetNode<Line2D>("%Wake");

        // The root stays in combat-container coordinates. The carrier owns only
        // the crescent, while the sibling wake can span the full world-space path.
        root.GlobalPosition = Vector2.Zero;
        root.Scale = Vector2.One;

        var launchY = caster.Floor.Y + (caster.BodyCenter.Y - caster.Floor.Y) * LaunchBodyBias;
        _launch = new Vector2(
            caster.BodyCenter.X + caster.FacingSign * caster.BodySize.X * 0.5f,
            launchY);

        var toTarget = target.Center - _launch;
        _direction = toTarget.LengthSquared() > 1f ? toTarget.Normalized() : Vector2.Right;
        _flightEnd = target.Center - _direction * CrescentCenterOffsetPx;
        _overshoot = _flightEnd + _direction * OvershootPx;

        _carrier.GlobalPosition = _launch;
        // The shader's local +X owns the convex cutting face. A single route
        // rotation handles enemies on either side without a time-varying spin.
        _carrier.Rotation = _direction.Angle();

        var body = root.GetNode<ColorRect>("%CrescentBody");
        _material = CelVfxGeometry.DuplicateMaterial(body, "Gale crescent");
        body.Size = BladeRegion;
        body.Position = -BladeRegion * 0.5f;
        _material.SetShaderParameter("region_size", BladeRegion);
        _material.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _material.SetShaderParameter("formation", LaunchFormation);
        _material.SetShaderParameter("impact", 0f);
        _material.SetShaderParameter("dissolve", 0f);
        _material.SetShaderParameter("opacity", 1f);

        ConfigureWake();
        UpdateWakePath(_launch, 0f);
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 80f,
        MinHeight: 90f,
        MaxWidth: 300f,
        MaxHeight: 340f,
        FallbackWidth: 150f,
        FallbackHeight: 180f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.22f,
        MaxViewportHeightFraction: 0.40f);

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    protected override float MaximumLifetime => 5.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature attacker,
        Creature target,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Gale wind",
            () => TryCreate(attacker, target),
            session => session.PlayPrelude(card, attacker),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<GaleWindBladeVfx> scope)
    {
        internal void Impact() => scope.Invoke("impact", static session => session.Impact());
    }

    private static GaleWindBladeVfx? TryCreate(Creature attacker, Creature target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (!TryPrepare("Gale wind", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(attacker)) is not { } caster)
                return null;

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraGaleWindBladeVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var geometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var session = new GaleWindBladeVfx(root, room, caster, geometry);
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

    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var flight = Track(Root.CreateTween().SetParallel());
        flight.TweenMethod(
                Callable.From<float>(SetFlightProgress),
                0f,
                1f,
                FlightDuration)
            .SetTrans(Tween.TransitionType.Linear);
        flight.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("formation", value)),
                LaunchFormation,
                1f,
                FormationDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        return await WaitActive(FlightDuration);
    }

    private void Impact()
    {
        if (_impacted || !IsActive())
            return;

        _impacted = true;
        _material.SetShaderParameter("impact", 1f);
        BeginHold();

        var pass = Track(Root.CreateTween().SetParallel());
        // Every pass-through tweener waits for the shader hold. BeginHold freezes
        // shader time, not Godot's tween clock.
        pass.TweenMethod(
                Callable.From<float>(SetPassProgress),
                0f,
                1f,
                DissipateDuration)
            .SetDelay(HoldDuration)
            .SetTrans(Tween.TransitionType.Linear);
        pass.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("dissolve", value)),
                0f,
                1f,
                DissipateDuration)
            .SetDelay(HoldDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        pass.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("impact", value)),
                1f,
                0f,
                DissipateDuration)
            .SetDelay(HoldDuration)
            .SetTrans(Tween.TransitionType.Linear);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var settle = _impacted ? HoldDuration + DissipateDuration : 0f;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private void ConfigureWake()
    {
        var widthCurve = new Curve();
        widthCurve.AddPoint(new Vector2(0f, 0.05f));
        widthCurve.AddPoint(new Vector2(0.62f, 0.42f));
        widthCurve.AddPoint(Vector2.One);
        _wake.WidthCurve = widthCurve;
        _wake.Gradient = new Gradient
        {
            Offsets = [0f, 0.58f, 1f],
            Colors =
            [
                new Color(0.62f, 0.92f, 0.98f, 0.02f),
                new Color(0.78f, 0.97f, 1f, 0.38f),
                new Color(0.98f, 1f, 1f, 0.90f)
            ]
        };
    }

    private void SetFlightProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0f, 1f);
        var position = _launch.Lerp(_flightEnd, progress);
        _carrier.GlobalPosition = position;
        var wakeOpacity = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp((progress - 0.05f) / 0.35f, 0f, 1f));
        UpdateWakePath(position, wakeOpacity);
    }

    private void SetPassProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0f, 1f);
        var position = _flightEnd.Lerp(_overshoot, progress);
        _carrier.GlobalPosition = position;
        UpdateWakePath(position, Mathf.Lerp(1f, 0.18f, progress));
    }

    private void UpdateWakePath(Vector2 carrierPosition, float opacity)
    {
        var end = carrierPosition + _direction * WakeAttachmentOffsetPx;
        var control = (_launch + end) * 0.5f
            + Vector2.Up * Math.Min(30f, _launch.DistanceTo(end) * 0.08f);
        for (var i = 0; i < WakePointCount; i++)
        {
            var t = (float)i / (WakePointCount - 1);
            var oneMinusT = 1f - t;
            _wakePoints[i] = oneMinusT * oneMinusT * _launch
                + 2f * oneMinusT * t * control
                + t * t * end;
        }

        _wake.Points = _wakePoints;
        _wake.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(opacity, 0f, 1f));
    }

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Gale wind VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
