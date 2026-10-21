using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraForgottenOverlayPolicy
{
    public static bool ShouldShow(bool isTemporary, bool isInHand) =>
        isTemporary && isInHand;

    // Only the card art fades; names, cost, and the description region stay opaque so the
    // card remains readable without restoring anything on hover.
    public const float FadedArtAlpha = 0.6f;
    public const float PulseArtAlpha = 0.35f;

    public static float ArtAlpha(float reveal, float boost) =>
        Mathf.Lerp(1f, Mathf.Lerp(FadedArtAlpha, PulseArtAlpha, boost), reveal);

    // Sakura renderers draw the whole face as one art node; vanilla cards fade only the
    // portrait window (resolved from the native NCard field).
    public static string? ArtNodeName(SakuraCardVisualLayout layout) =>
        layout switch
        {
            SakuraCardVisualLayout.Classic => ClassicCardLayout.FaceNodeName,
            SakuraCardVisualLayout.Clear => ClearCardLayout.ArtNodeName,
            _ => null
        };

    // Room above the card, as a fraction of the card height, where motes evaporate off the top
    // edge: in the overlapped hand little more than each card's top is visible.
    public const float MoteMarginFraction = 0.2f;

    // Mirrors the mod-owned Affliction overlay wrapper: the native OverlayContainer sits on the
    // vanilla card centre, while Sakura faces use a top-left origin. The frame extends above the
    // card by the mote margin; TopMarginFraction is that margin's share of the frame height.
    public static (Vector2 RootPosition, Rect2 FrameBox, float TopMarginFraction) Layout(SakuraCardVisualLayout layout)
    {
        var size = SakuraCardGeometry.TryProfile(layout, out var profile)
            ? profile.RootSize
            : SakuraCardGeometry.VanillaLayoutSize;
        var rootPosition = layout == SakuraCardVisualLayout.None
            ? Vector2.Zero
            : size * 0.5f;
        var margin = size.Y * MoteMarginFraction;
        var frame = new Rect2(
            new Vector2(size.X * -0.5f, size.Y * -0.5f - margin),
            new Vector2(size.X, size.Y + margin));
        return (rootPosition, frame, margin / frame.Size.Y);
    }
}

/// <summary>
/// The Forgotten card state, drawn as a Sakura-owned child of the native card
/// OverlayContainer. It does not take the native Affliction overlay slot, so it
/// coexists with Affliction overlays. Presentation only: it re-reads the card's
/// model every frame and never feeds back into gameplay.
/// </summary>
internal sealed partial class SakuraForgottenOverlay : Control
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/forgotten_overlay.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const string NodeName = "SakuraForgottenOverlay";
    private const float RevealDuration = 0.3f;
    private const float PulseRiseDuration = 0.15f;
    private const float PulseFallDuration = 0.35f;

    // Every uniform a tween drives is written once at creation: a ShaderMaterial reports an
    // unset uniform as Nil, and a tween from Nil fails.
    private static readonly StringName RevealParameter = new("reveal");
    private static readonly StringName BoostParameter = new("boost");
    private static readonly StringName GlassParameter = new("glass");
    private static readonly StringName SeedParameter = new("seed");
    private static readonly StringName MotesEnabledParameter = new("motes_enabled");
    private static readonly StringName TopMarginParameter = new("top_margin_frac");
    private static readonly FieldInfo? PortraitCanvasGroupField =
        PrivateAccess.DeclaredField(typeof(NCard), "_portraitCanvasGroup");

    private NCard? _card;
    private Control? _frame;
    private ShaderMaterial? _material;
    // The art node whose SelfModulate alpha this overlay owns while the card is Forgotten.
    private CanvasItem? _art;
    private CardModel? _shownModel;
    private bool _shown;
    private bool _vfxEnabled = true;
    // While a recall develops the card out of the glass wash, the Forgotten state no longer decides
    // visibility. The transit belongs to one model: a pooled node switching cards ends it.
    private bool _inTransit;
    private CardModel? _transitModel;
    private Tween? _revealTween;
    private Tween? _pulseTween;

    /// <summary>Mounts the overlay on a combat card and refreshes its layout for the current face.</summary>
    public static void Sync(NCard card)
    {
        if (TestMode.IsOn
            || NCombatRoom.Instance is null
            || !GodotObject.IsInstanceValid(card)
            || card.OverlayContainer is not { } container)
        {
            return;
        }

        var overlay = container.GetNodeOrNull<SakuraForgottenOverlay>(NodeName) ?? Mount(card, container);
        if (overlay is null)
            return;

        var layout = SakuraCardVisualFamilies.Layout(card);
        overlay.ApplyLayout(layout);
        overlay.BindArt(ResolveArt(card, layout));
        overlay.RefreshVfxSetting();
    }

    /// <summary>End-turn warning: briefly strengthens the wash and motes on a shown overlay.</summary>
    public static void Pulse(NCard? card)
    {
        Find(card)?.PlayPulse();
    }

    /// <summary>Recall: a blank glass card develops back into its face, then the overlay resumes its normal rule.</summary>
    public static bool PlayDevelop(NCard card, float duration) =>
        Find(card)?.PlayDevelopTransit(duration) == true;

    /// <summary>
    /// The native pool reset point (NCard.OnReturnedFromPool): drop every transient state so a reused
    /// node never carries the previous card's overlay or art alpha.
    /// </summary>
    public static void ResetForReuse(NCard card) => Find(card)?.Reset();

    private static SakuraForgottenOverlay? Find(NCard? card) =>
        card is not null && GodotObject.IsInstanceValid(card)
            ? card.OverlayContainer?.GetNodeOrNull<SakuraForgottenOverlay>(NodeName)
            : null;

    private static SakuraForgottenOverlay? Mount(NCard card, Node container)
    {
        Shader shader;
        try
        {
            shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Forgotten overlay shader is unavailable: {exception.Message}");
            return null;
        }

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter(RevealParameter, 0f);
        material.SetShaderParameter(BoostParameter, 0f);
        material.SetShaderParameter(GlassParameter, 0f);
        material.SetShaderParameter(SeedParameter, Random.Shared.NextSingle() * 7f);
        material.SetShaderParameter(MotesEnabledParameter, 1f);

        var frame = new Control
        {
            Name = "Frame",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddChild(new ColorRect
        {
            Name = "Veil",
            Color = Colors.White,
            Material = material,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f
        });

        var overlay = new SakuraForgottenOverlay
        {
            Name = NodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            _card = card,
            _frame = frame,
            _material = material
        };
        overlay.AddChild(frame);
        container.AddChildSafely(overlay);
        return overlay;
    }

    private static CanvasItem? ResolveArt(NCard card, SakuraCardVisualLayout layout) =>
        SakuraForgottenOverlayPolicy.ArtNodeName(layout) is { } name
            ? card.Body?.GetNodeOrNull<CanvasItem>(name)
            : PortraitCanvasGroupField?.GetValue(card) as CanvasItem;

    private void BindArt(CanvasItem? art)
    {
        if (ReferenceEquals(art, _art))
            return;

        SetArtAlpha(_art, 1f);
        _art = art;
        ApplyArtAlpha();
    }

    private void ApplyArtAlpha()
    {
        var alpha = !Visible
            ? 1f
            : _inTransit
                ? Mathf.Lerp(1f, SakuraForgottenOverlayPolicy.FadedArtAlpha, Parameter(GlassParameter))
                : SakuraForgottenOverlayPolicy.ArtAlpha(Parameter(RevealParameter), Parameter(BoostParameter));
        SetArtAlpha(_art, alpha);
    }

    private static void SetArtAlpha(CanvasItem? art, float alpha)
    {
        if (art is null || !GodotObject.IsInstanceValid(art))
            return;

        var color = art.SelfModulate;
        if (Mathf.Abs(color.A - alpha) > 0.001f)
            art.SelfModulate = color with { A = alpha };
    }

    private void ApplyLayout(SakuraCardVisualLayout layout)
    {
        if (_frame is null)
            return;

        var (rootPosition, frameBox, topMarginFraction) = SakuraForgottenOverlayPolicy.Layout(layout);
        _material?.SetShaderParameter(TopMarginParameter, topMarginFraction);
        if (Position != rootPosition)
            Position = rootPosition;
        if (_frame.Position != frameBox.Position)
            _frame.Position = frameBox.Position;
        if (_frame.Size != frameBox.Size)
            _frame.Size = frameBox.Size;
    }

    private void RefreshVfxSetting()
    {
        _vfxEnabled = SakuraModConfig.IsCardVfxEnabled();
        _material?.SetShaderParameter(MotesEnabledParameter, _vfxEnabled ? 1f : 0f);
    }

    public override void _Process(double delta)
    {
        if (_inTransit)
        {
            if (ReferenceEquals(_card?.Model, _transitModel))
            {
                ApplyArtAlpha();
                return;
            }

            EndTransit();
        }

        var model = _card?.Model;
        var shouldShow = model is not null
            && SakuraForgottenOverlayPolicy.ShouldShow(model.IsTemporary(), model.Pile?.Type == PileType.Hand);

        if (shouldShow == _shown && ReferenceEquals(model, _shownModel))
        {
            ApplyArtAlpha();
            return;
        }

        // A pooled card switching to another model, or a card leaving the hand, hides at once;
        // losing Forgotten while staying in the hand fades out.
        var sameCardStillInHand = ReferenceEquals(model, _shownModel) && model?.Pile?.Type == PileType.Hand;
        _shown = shouldShow;
        _shownModel = model;
        if (shouldShow)
            Reveal();
        else if (sameCardStillInHand)
            FadeOut();
        else
            HideNow();
        ApplyArtAlpha();
    }

    private void Reveal()
    {
        KillTweens();
        SetParameter(BoostParameter, 0f);
        Visible = true;
        _revealTween = CreateTween();
        _revealTween.TweenMethod(Callable.From<float>(value => SetParameter(RevealParameter, value)), 0f, 1f, RevealDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    private void FadeOut()
    {
        KillTweens();
        var from = Parameter(RevealParameter);
        _revealTween = CreateTween();
        _revealTween.TweenMethod(Callable.From<float>(value => SetParameter(RevealParameter, value)), from, 0f, RevealDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        _revealTween.TweenCallback(Callable.From(() => Visible = false));
    }

    private void HideNow()
    {
        KillTweens();
        SetParameter(RevealParameter, 0f);
        SetParameter(BoostParameter, 0f);
        Visible = false;
    }

    private bool PlayDevelopTransit(float duration)
    {
        KillTweens();
        _inTransit = true;
        _transitModel = _card?.Model;
        Visible = true;
        SetParameter(RevealParameter, 1f);
        SetParameter(BoostParameter, 0f);
        SetParameter(GlassParameter, 1f);
        _revealTween = CreateTween();
        _revealTween.TweenMethod(Callable.From<float>(value => SetParameter(GlassParameter, value)), 1f, 0f, Mathf.Max(duration, 0.01f))
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        _revealTween.TweenCallback(Callable.From(EndTransit));
        return true;
    }

    private void EndTransit()
    {
        _inTransit = false;
        _transitModel = null;
        SetParameter(GlassParameter, 0f);
        // Forget the last decision so the next frame re-evaluates the card from scratch.
        _shownModel = null;
        _shown = false;
        HideNow();
        ApplyArtAlpha();
    }

    private void Reset()
    {
        _inTransit = false;
        _transitModel = null;
        SetParameter(GlassParameter, 0f);
        _shownModel = null;
        _shown = false;
        HideNow();
        SetArtAlpha(_art, 1f);
    }

    private void PlayPulse()
    {
        if (!_shown || !_vfxEnabled)
            return;

        _pulseTween?.Kill();
        _pulseTween = CreateTween();
        _pulseTween.TweenMethod(Callable.From<float>(value => SetParameter(BoostParameter, value)), Parameter(BoostParameter), 1f, PulseRiseDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _pulseTween.TweenMethod(Callable.From<float>(value => SetParameter(BoostParameter, value)), 1f, 0f, PulseFallDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }

    private void KillTweens()
    {
        _revealTween?.Kill();
        _revealTween = null;
        _pulseTween?.Kill();
        _pulseTween = null;
    }

    private float Parameter(StringName name) =>
        _material?.GetShaderParameter(name).AsSingle() ?? 0f;

    private void SetParameter(StringName name, float value) =>
        _material?.SetShaderParameter(name, value);
}
