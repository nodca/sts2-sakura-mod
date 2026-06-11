using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NMerchantCharacter))]
internal static class SakuraMerchantCharacterVisualPatch
{
    private static readonly string MerchantVisualPath = "charui/sakura_battle_standee.png".ImagePath();
    private const string MerchantVisualNodeName = "Visuals";
    private static readonly Vector2 MerchantVisualCenter = new(0f, -234f);
    private const float MerchantVisualScale = 0.28f;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMerchantCharacter._Ready))]
    public static void ReadyPostfix(NMerchantCharacter __instance)
    {
        ApplySakuraLayout(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NMerchantCharacter.PlayAnimation))]
    public static bool PlayAnimationPrefix(NMerchantCharacter __instance)
    {
        if (!IsSakuraMerchantVisual(__instance))
            return true;

        ApplySakuraLayout(__instance);
        return false;
    }

    private static void ApplySakuraLayout(NMerchantCharacter character)
    {
        if (!TryGetSakuraSprite(character, out var sprite))
            return;

        if (sprite.Position != MerchantVisualCenter)
            sprite.Position = MerchantVisualCenter;
        var scale = Vector2.One * MerchantVisualScale;
        if (sprite.Scale != scale)
            sprite.Scale = scale;
        if (sprite.Modulate != Colors.White)
            sprite.Modulate = Colors.White;
        if (!sprite.Visible)
            sprite.Visible = true;
    }

    private static bool IsSakuraMerchantVisual(NMerchantCharacter character) =>
        TryGetSakuraSprite(character, out _);

    private static bool TryGetSakuraSprite(NMerchantCharacter character, out Sprite2D sprite)
    {
        if (character.GetNodeOrNull<Sprite2D>(MerchantVisualNodeName) is { } namedSprite
            && IsSakuraSprite(namedSprite))
        {
            sprite = namedSprite;
            return true;
        }

        return TryGetSakuraSprite((Node)character, out sprite);
    }

    private static bool TryGetSakuraSprite(Node node, out Sprite2D sprite)
    {
        sprite = null!;

        if (node is Sprite2D childSprite && IsSakuraSprite(childSprite))
        {
            sprite = childSprite;
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode && TryGetSakuraSprite(childNode, out sprite))
                return true;
        }

        return false;
    }

    private static bool IsSakuraSprite(Sprite2D sprite)
    {
        var resourcePath = sprite.Texture?.ResourcePath;
        return resourcePath == MerchantVisualPath
            || resourcePath?.EndsWith("/sakura_battle_standee.png", StringComparison.Ordinal) == true
            || resourcePath?.Contains("sakura_battle_standee.png-", StringComparison.Ordinal) == true;
    }
}
