using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

internal enum WeatherMode
{
    Cloud,
    Rain
}

/// <summary>
/// Cloud and Rain share one weather field: a scalloped canopy, with precipitation
/// only in rain mode.
/// </summary>
internal sealed class CloudRainWeatherVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/cloud_rain_weather_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/cloud_rain_weather.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    internal const float CloudFormationDuration = 4f / StepFrequency;
    internal const float RainFormationDuration = 2f / StepFrequency;
    internal const float HoldDuration = 2f / StepFrequency;
    internal const float CloudFadeDuration = 0.18f;
    internal const float RainContactDuration = 0.28f;
    internal const float RainSustainDuration = 0.30f;
    internal const float RainFadeDuration = 0.22f;

    private const float CanopyLiftFraction = 0.52f;
    private const float FacingOffsetPx = 8f;
    private const float CanopyBaseFromCenter = 34f;
    private const float CanopyRegionPadX = 16f;
    private const float CanopyRegionPadY = 22f;
    private const float SplashClearancePx = 14f;
    private const float RainViewportWidthFraction = 0.28f;
    private const float RainMinWidth = 240f;
    private const float RainMaxWidth = 400f;
    private const float CloudLiftPx = 22f;
    private const int VfxZIndex = 1;

    private static readonly Vector2 CanopyHalf = new(108f, 48f);

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _material;
    private readonly WeatherMode _mode;
    private readonly ColorRect _body;
    private bool _impacted;
    private bool _faded;

    private CloudRainWeatherVfx(
        Node2D root,
        NCombatRoom room,
        WeatherMode mode,
        CelVfxGeometry.CasterAnchor caster)
        : base(root, room)
    {
        _mode = mode;
        _body = root.GetNode<ColorRect>("%WeatherBody");
        _material = CelVfxGeometry.DuplicateMaterial(_body, "cloud-rain weather");

        root.Scale = Vector2.One;
        Layout(room, caster);
        _material.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _material.SetShaderParameter("formation", 0f);
        _material.SetShaderParameter("rain", 0f);
        _material.SetShaderParameter("rain_origin", 0f);
        _material.SetShaderParameter("splash", 0f);
        _material.SetShaderParameter("opacity", 1f);
    }

    internal static float CloudFieldSeconds =>
        CloudFormationDuration + HoldDuration + CloudFadeDuration;

    internal static float RainFieldSeconds =>
        RainFormationDuration + RainContactDuration + HoldDuration
        + RainSustainDuration + RainFadeDuration;

    internal static bool CloudFieldIsShorterThanRain() =>
        CloudFieldSeconds < RainFieldSeconds
        && CloudFieldSeconds is >= 0.55f and <= 0.75f
        && RainFieldSeconds is >= 1.0f and <= 1.2f;

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    /// <summary>
    /// Safety net on wall clock. Worst case is the shared prelude, the rain
    /// field, and fade, plus hold. Sized well clear of that envelope.
    /// </summary>
    protected override float MaximumLifetime => 5.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        WeatherMode mode,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Cloud rain weather",
            () => TryCreate(caster, mode),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<CloudRainWeatherVfx> scope)
    {
        internal void Impact() => scope.Invoke("impact", static session => session.Impact());
    }

    private static CloudRainWeatherVfx? TryCreate(Creature? caster, WeatherMode mode)
    {
        if (caster is null)
            return null;
        if (!TryPrepare("Cloud rain weather", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is not { } anchor)
                return null;

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraCloudRainWeatherVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new CloudRainWeatherVfx(root, room, mode, anchor);
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

        var formationDuration = _mode == WeatherMode.Cloud
            ? CloudFormationDuration
            : RainFormationDuration;
        var grow = Track(Root.CreateTween());
        grow.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("formation", value)),
                0f,
                1f,
                formationDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        if (!await WaitActive(formationDuration))
            return false;

        if (_mode != WeatherMode.Rain)
            return IsActive();

        _material.SetShaderParameter("rain_origin", _material.GetShaderParameter("elapsed").AsSingle());
        _material.SetShaderParameter("rain", 1f);
        return await WaitActive(RainContactDuration);
    }

    private void Impact()
    {
        if (_impacted || !IsActive())
            return;

        _impacted = true;
        BeginHold();
        if (_mode != WeatherMode.Rain)
            return;

        var splash = Track(Root.CreateTween());
        splash.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("splash", value)),
                0f,
                1f,
                RainSustainDuration)
            .SetDelay(HoldDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var delay = _impacted ? HoldDuration : 0f;
        if (_mode == WeatherMode.Rain)
            delay += RainSustainDuration;
        var fadeLen = _mode == WeatherMode.Rain ? RainFadeDuration : CloudFadeDuration;
        var liftTarget = Root.Position + Vector2.Up * CloudLiftPx;
        var fade = Track(Root.CreateTween());
        if (delay > 0f)
            fade.TweenInterval(delay);
        fade.TweenProperty(Root, "modulate:a", 0f, fadeLen)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        if (_mode == WeatherMode.Cloud)
        {
            fade.Parallel().TweenProperty(Root, "position", liftTarget, fadeLen)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        fade.Chain().TweenCallback(Callable.From(Dispose));
    }

    private void Layout(NCombatRoom room, CelVfxGeometry.CasterAnchor caster)
    {
        var canopyWorld = new Vector2(
            caster.BodyCenter.X + caster.FacingSign * FacingOffsetPx,
            caster.BodyCenter.Y - caster.BodySize.Y * CanopyLiftFraction);

        Vector2 size;
        Vector2 regionCenter;
        if (_mode == WeatherMode.Cloud)
        {
            size = new Vector2(
                CanopyHalf.X * 2f + CanopyRegionPadX * 2f,
                CanopyHalf.Y * 2f + CanopyRegionPadY * 2f);
            regionCenter = canopyWorld;
        }
        else
        {
            var viewportWidth = room.CombatVfxContainer.GetViewportRect().Size.X;
            var widthCap = float.IsFinite(viewportWidth) && viewportWidth > 0f
                ? viewportWidth * RainViewportWidthFraction
                : RainMaxWidth;
            var maxWidth = Math.Max(RainMinWidth, Math.Min(RainMaxWidth, widthCap));
            var width = Math.Clamp(
                Math.Max(RainMinWidth, caster.BodySize.X * 1.75f),
                RainMinWidth,
                maxWidth);
            var top = canopyWorld.Y - CanopyHalf.Y - CanopyRegionPadY;
            var bottom = caster.Floor.Y - 4f;
            var height = Math.Max(bottom - top, CanopyHalf.Y * 2f + 120f);
            size = new Vector2(width, height);
            regionCenter = new Vector2(canopyWorld.X, top + height * 0.5f);
        }

        Root.GlobalPosition = regionCenter;
        _body.Size = size;
        _body.Position = -size * 0.5f;
        _material.SetShaderParameter("region_size", size);

        var canopyLocal = canopyWorld - regionCenter;
        _material.SetShaderParameter("canopy_center", canopyLocal);
        _material.SetShaderParameter("canopy_half", CanopyHalf);
        _material.SetShaderParameter("canopy_base_y", canopyLocal.Y + CanopyBaseFromCenter);
        _material.SetShaderParameter("ground_y", caster.Floor.Y - SplashClearancePx - regionCenter.Y);
    }

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Cloud/Rain weather VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
