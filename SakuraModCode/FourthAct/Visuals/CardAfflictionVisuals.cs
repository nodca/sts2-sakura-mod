using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Earth.CardState;
using SakuraMod.SakuraModCode.FourthAct.Water.CardState;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum CardAfflictionRelease
{
    Wake,
    Unbury,
    Thaw
}

// One-shot feedback for the mod-owned card Afflictions. Release effects are detached from
// the card before the Affliction clears, so a hidden or pooled card clears safely.
internal static class CardAfflictionVisuals
{
    internal const float ReleaseDuration = 0.42f;
    private const int VfxZIndex = 3001;
    private const string TextureRoot = MainFile.ResPath + "/images/card_vfx/afflictions";
    private static readonly string VeilPath = $"{TextureRoot}/sleeping/veil.png";
    private static readonly string StarPath = $"{TextureRoot}/sleeping/star.png";
    private static readonly string SoilPath = $"{TextureRoot}/buried/soil.png";
    private static readonly string SpeckPath = $"{TextureRoot}/speck.png";
    private static readonly string GlintPath = $"{TextureRoot}/frozen/glint.png";
    private static readonly string FrostPath = $"{TextureRoot}/frozen/frost_corner.png";

    internal static bool RejectsManualPlay(CardModel? card) =>
        card?.Affliction is SleepingAffliction or FrozenAffliction;

    public static void PlayRejected(CardModel card)
    {
        if (TestMode.IsOn
            || NCard.FindOnTable(card) is not { } cardNode
            || cardNode.FindChild(SakuraAfflictionVisualLayout.ModOverlayFrameName, recursive: true, owned: false) is not Control frame
            || !frame.IsInsideTree())
        {
            return;
        }

        var flash = card.Affliction is FrozenAffliction
            ? new Color(1.25f, 1.35f, 1.5f, 1f)
            : new Color(0.8f, 0.74f, 1.1f, 1f);
        var tween = frame.CreateTween();
        tween.TweenProperty(frame, "position:x", 4f, 0.05f).AsRelative();
        tween.Parallel().TweenProperty(frame, "modulate", flash, 0.05f);
        tween.TweenProperty(frame, "position:x", -8f, 0.08f).AsRelative();
        tween.TweenProperty(frame, "position:x", 4f, 0.07f).AsRelative()
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.Parallel().TweenProperty(frame, "modulate", Colors.White, 0.12f);
    }

    public static void PlayRelease(CardModel card, CardAfflictionRelease release)
    {
        if (TestMode.IsOn
            || NCombatRoom.Instance?.Ui is not { } ui
            || NCard.FindOnTable(card) is not { } cardNode
            || !cardNode.IsVisibleInTree())
        {
            return;
        }

        var size = cardNode.GetCurrentSize() * cardNode.Scale;
        var root = new Node2D
        {
            Name = $"SakuraAffliction{release}Vfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false,
            GlobalPosition = cardNode.GlobalPosition + size * 0.5f
        };
        ui.AddChildSafely(root);
        switch (release)
        {
            case CardAfflictionRelease.Wake:
                Lift(root, VeilPath, size, new Vector2(0f, -size.Y * 0.3f), new Vector2(0f, -size.Y * 0.12f), 0.4f);
                Burst(root, StarPath, 6, size * 0.32f, new Vector2(0f, -size.Y * 0.28f), 0.5f, new Color(1f, 0.92f, 0.7f));
                break;
            case CardAfflictionRelease.Unbury:
                Lift(root, SoilPath, size, new Vector2(0f, size.Y * 0.36f), new Vector2(0f, size.Y * 0.1f), 0.36f);
                Burst(root, SpeckPath, 10, size * 0.4f, new Vector2(0f, size.Y * 0.3f), 0.55f, new Color(0.78f, 0.6f, 0.38f));
                break;
            case CardAfflictionRelease.Thaw:
                foreach (var (corner, flip) in new[] { (new Vector2(-1, -1), Vector2.One), (new Vector2(1, -1), new Vector2(-1, 1)), (new Vector2(-1, 1), new Vector2(1, -1)), (new Vector2(1, 1), -Vector2.One) })
                    Lift(root, FrostPath, new Vector2(size.X * 0.6f, size.X * 0.6f), corner * size * 0.5f - corner * size.X * 0.3f, corner * 18f, 0.35f, flip);
                Burst(root, GlintPath, 9, size * 0.5f, Vector2.Zero, 0.8f, new Color(0.9f, 0.97f, 1f));
                break;
        }

        var tween = root.CreateTween();
        tween.TweenInterval(ReleaseDuration + 0.05f);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    // A full-width band of the overlay motif that drifts away and fades.
    private static void Lift(Node2D root, string path, Vector2 size, Vector2 start, Vector2 drift, float alpha, Vector2? flip = null)
    {
        if (ResourceLoader.Load<Texture2D>(path) is not { } texture)
            return;
        var scale = size.X / texture.GetWidth();
        var sprite = new Sprite2D
        {
            Texture = texture,
            Position = start,
            Scale = new Vector2(scale, scale) * (flip ?? Vector2.One),
            Modulate = new Color(1f, 1f, 1f, alpha)
        };
        root.AddChild(sprite);
        var tween = sprite.CreateTween().SetParallel();
        tween.TweenProperty(sprite, "position", start + drift, ReleaseDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(sprite, "modulate:a", 0f, ReleaseDuration)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
    }

    private static void Burst(Node2D root, string path, int count, Vector2 reach, Vector2 origin, float scale, Color tint)
    {
        if (ResourceLoader.Load<Texture2D>(path) is not { } texture)
            return;
        for (var index = 0; index < count; index++)
        {
            var angle = Mathf.Tau * index / count + 0.35f * (index % 2);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = origin,
                Scale = Vector2.One * scale * (index % 3 == 0 ? 1.2f : 0.8f),
                Modulate = tint
            };
            root.AddChild(sprite);
            var tween = sprite.CreateTween().SetParallel();
            tween.TweenProperty(sprite, "position", origin + direction * reach, ReleaseDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(sprite, "modulate:a", 0f, ReleaseDuration)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(sprite, "rotation", angle * 0.5f, ReleaseDuration);
        }
    }
}

[HarmonyPatch(typeof(NMouseCardPlay), nameof(NMouseCardPlay.Start))]
internal static class CardAfflictionMouseCardPlayPatch
{
    [HarmonyPrefix]
    private static void PlayRejectedCue(NMouseCardPlay __instance)
    {
        if (__instance.Holder?.CardModel is { } card && CardAfflictionVisuals.RejectsManualPlay(card))
            CardAfflictionVisuals.PlayRejected(card);
    }
}

[HarmonyPatch(typeof(NControllerCardPlay), nameof(NControllerCardPlay.Start))]
internal static class CardAfflictionControllerCardPlayPatch
{
    [HarmonyPrefix]
    private static void PlayRejectedCue(NControllerCardPlay __instance)
    {
        if (__instance.Holder?.CardModel is { } card && CardAfflictionVisuals.RejectsManualPlay(card))
            CardAfflictionVisuals.PlayRejected(card);
    }
}
