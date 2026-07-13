using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraHandHighlightKind
{
    None,
    ExtraEffect,
    Temporary,
}

internal static class SakuraHandHighlightPolicy
{
    public static SakuraHandHighlightKind Select(
        CardModel? model,
        bool hasNativePlayableHighlight,
        bool extraEffectActive,
        bool isTemporary)
    {
        if (!hasNativePlayableHighlight || model is null)
            return SakuraHandHighlightKind.None;

        if (isTemporary && SakuraTransparentCardCatalog.IsTransparentCard(model))
            return SakuraHandHighlightKind.Temporary;

        if (extraEffectActive && SupportsGoldExtraEffectHighlight(model))
            return SakuraHandHighlightKind.ExtraEffect;

        return SakuraHandHighlightKind.None;
    }

    private static bool SupportsGoldExtraEffectHighlight(CardModel model) =>
        model is ClassicExtraClowCard
        || SakuraModCard.HasMagicChargeExtraEffect(model);
}

internal static class SakuraHandHighlightVisual
{
    private static readonly FieldInfo? HighlightCurrentTweenField =
        PrivateAccess.DeclaredField(typeof(NCardHighlight), "_curTween");
    private static readonly StringName HighlightWidthParameterName = new("width");
    private static readonly Color ExtraEffectColor = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color TemporaryColor = new(1f, 0.25f, 0.25f, 1f);
    private const float HighlightWidth = 0.12f;
    private const float HighlightShowDuration = 0.32f;

    public static void Apply(NCardHolder holder, SakuraCardMutationLedger ledger)
    {
        if (holder is not NHandCardHolder
            || holder.CardNode is not { Model: { } model, CardHighlight: { } highlight })
        {
            return;
        }

        ledger.Borrow(highlight, SakuraControlProperty.Modulate);
        ledger.YieldShaderStateToNative(highlight);

        var currentColor = highlight.Modulate;
        var hasNativePlayableHighlight = Approximately(currentColor, NCardHighlight.playableColor);
        var hasOwnedHighlight = IsOwnedColor(currentColor);
        var kind = SakuraHandHighlightPolicy.Select(
            model,
            hasNativePlayableHighlight || hasOwnedHighlight,
            SakuraExtraEffectTransaction.ShouldShowAsActive(model),
            model.IsTemporary());

        if (kind == SakuraHandHighlightKind.None)
        {
            if (hasOwnedHighlight)
                highlight.Modulate = NCardHighlight.playableColor;
            return;
        }

        var targetColor = ColorFor(kind);
        var shouldAnimateWidth = ShaderWidthValue(highlight) <= 0.001f;
        if (highlight.Modulate != targetColor)
            highlight.Modulate = targetColor;

        if (shouldAnimateWidth)
            AnimateWidth(highlight);
        else
            SetShaderWidth(highlight, HighlightWidth);
    }

    private static Color ColorFor(SakuraHandHighlightKind kind) =>
        kind switch
        {
            SakuraHandHighlightKind.ExtraEffect => ExtraEffectColor,
            SakuraHandHighlightKind.Temporary => TemporaryColor,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Highlight kind has no owned color."),
        };

    private static bool IsOwnedColor(Color color) =>
        Approximately(color, ExtraEffectColor)
        || Approximately(color, TemporaryColor);

    private static float ShaderWidthValue(NCardHighlight highlight)
    {
        if (highlight.Material is not ShaderMaterial material)
            return 0f;

        return material.GetShaderParameter(HighlightWidthParameterName).AsSingle();
    }

    private static void AnimateWidth(NCardHighlight highlight)
    {
        if (highlight.Material is not ShaderMaterial material)
            return;

        KillTween(highlight);
        var tween = highlight.CreateTween();
        tween.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter(HighlightWidthParameterName, value)),
                ShaderWidthValue(highlight),
                HighlightWidth,
                HighlightShowDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        HighlightCurrentTweenField?.SetValue(highlight, tween);
    }

    private static void KillTween(NCardHighlight highlight)
    {
        if (HighlightCurrentTweenField?.GetValue(highlight) is Tween tween)
            tween.Kill();
    }

    private static void SetShaderWidth(NCardHighlight highlight, float width)
    {
        if (highlight.Material is not ShaderMaterial material)
            return;

        var currentWidth = material.GetShaderParameter(HighlightWidthParameterName).AsSingle();
        if (Mathf.Abs(currentWidth - width) > 0.001f)
            material.SetShaderParameter(HighlightWidthParameterName, width);
    }

    private static bool Approximately(Color left, Color right)
    {
        const float tolerance = 0.001f;
        return Mathf.Abs(left.R - right.R) <= tolerance
            && Mathf.Abs(left.G - right.G) <= tolerance
            && Mathf.Abs(left.B - right.B) <= tolerance;
    }
}
