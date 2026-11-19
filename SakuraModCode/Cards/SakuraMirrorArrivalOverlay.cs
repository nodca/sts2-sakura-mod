using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraMirrorArrivalPolicy
{
    // Room around the card, in card-local pixels, for the reflection that
    // starts offset from the card.
    public const float OuterMargin = 44f;
    public const float Duration = 0.9f;

    // Same origin rule as the Forgotten overlay: the native OverlayContainer sits on
    // the vanilla card centre, while Sakura faces use a top-left origin.
    public static (Vector2 RootPosition, Rect2 FrameBox, Vector2 CardSize) Layout(SakuraCardVisualLayout layout)
    {
        var size = SakuraCardGeometry.TryProfile(layout, out var profile)
            ? profile.RootSize
            : SakuraCardGeometry.VanillaLayoutSize;
        var rootPosition = layout == SakuraCardVisualLayout.None
            ? Vector2.Zero
            : size * 0.5f;
        var frame = new Rect2(
            size * -0.5f - Vector2.One * OuterMargin,
            size + Vector2.One * (OuterMargin * 2f));
        return (rootPosition, frame, size);
    }
}

/// <summary>
/// Transparent Mirror's arrival on the Hand card it gave Replay to: the face
/// ripples and an offset reflection slides back onto it with a flash, so the
/// card can be found in a crowded hand. One-shot and presentation only: a
/// Sakura-owned child of the native OverlayContainer (like the Forgotten
/// overlay), hidden except while it plays; it never touches the native
/// Affliction overlay slot.
/// </summary>
internal sealed partial class SakuraMirrorArrivalOverlay : Control
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/mirror_arrival.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const string NodeName = "SakuraMirrorArrivalOverlay";

    // Every uniform a tween drives is written at creation (Nil-uniform tween rule).
    private static readonly StringName FrameSizeParameter = new("frame_size");
    private static readonly StringName CardSizeParameter = new("card_size");
    private static readonly StringName ProgressParameter = new("progress");

    private ColorRect? _frame;
    private ShaderMaterial? _material;
    private Tween? _tween;

    /// <summary>Plays the arrival on the card's node, if this client shows it. Never affects gameplay.</summary>
    public static void Play(CardModel card)
    {
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled())
            return;

        try
        {
            if (NCard.FindOnTable(card) is not { } node
                || !GodotObject.IsInstanceValid(node)
                || node.OverlayContainer is not { } container)
            {
                return;
            }

            var overlay = container.GetNodeOrNull<SakuraMirrorArrivalOverlay>(NodeName) ?? Mount(container);
            if (overlay is null)
                return;

            overlay.ApplyLayout(SakuraCardVisualFamilies.Layout(node));
            overlay.PlayOnce();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Mirror arrival could not play: {exception.Message}");
        }
    }

    /// <summary>The native pool reset point: a reused node never carries a running arrival.</summary>
    public static void ResetForReuse(NCard card)
    {
        if (GodotObject.IsInstanceValid(card)
            && card.OverlayContainer?.GetNodeOrNull<SakuraMirrorArrivalOverlay>(NodeName) is { } overlay)
        {
            overlay.Stop();
        }
    }

    private static SakuraMirrorArrivalOverlay? Mount(Node container)
    {
        if (NCombatRoom.Instance is null)
            return null;

        var material = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath) };
        material.SetShaderParameter(ProgressParameter, 0f);
        var frame = new ColorRect
        {
            Name = "Arrival",
            Color = Colors.White,
            Material = material,
            MouseFilter = MouseFilterEnum.Ignore
        };
        var overlay = new SakuraMirrorArrivalOverlay
        {
            Name = NodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            _frame = frame,
            _material = material
        };
        overlay.AddChild(frame);
        container.AddChildSafely(overlay);
        return overlay;
    }

    private void ApplyLayout(SakuraCardVisualLayout layout)
    {
        if (_frame is null)
            return;

        var (rootPosition, frameBox, cardSize) = SakuraMirrorArrivalPolicy.Layout(layout);
        _material?.SetShaderParameter(FrameSizeParameter, frameBox.Size);
        _material?.SetShaderParameter(CardSizeParameter, cardSize);
        Position = rootPosition;
        _frame.Position = frameBox.Position;
        _frame.Size = frameBox.Size;
    }

    private void PlayOnce()
    {
        _tween?.Kill();
        Visible = true;
        // Phases (ripple, reflection, flash) are shaped in the shader; drive it linearly.
        _tween = CreateTween();
        _tween.TweenMethod(
                Callable.From<float>(value => _material?.SetShaderParameter(ProgressParameter, value)),
                0f,
                1f,
                SakuraMirrorArrivalPolicy.Duration)
            .SetTrans(Tween.TransitionType.Linear);
        _tween.TweenCallback(Callable.From(Stop));
    }

    private void Stop()
    {
        _tween?.Kill();
        _tween = null;
        _material?.SetShaderParameter(ProgressParameter, 0f);
        Visible = false;
    }
}
