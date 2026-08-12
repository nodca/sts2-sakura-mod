using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraCombatVisuals
{
    internal const string ChibiTextureFile = "charui/chibi_combat/sakura_clow_wand_body.png";
    internal const string FrogRaincoatTextureFile = "charui/outfits/frog_raincoat_standee.png";

    private const float ChibiScale = 0.28f;
    private static readonly Vector2 ChibiVisualPosition = new(-10.56f, -174.08f);
    private static readonly Rect2 ChibiBounds = new(-132f, -353f, 264f, 354f);

    private const float FrogRaincoatScale = 0.31f;
    private static readonly Vector2 FrogRaincoatVisualPosition = new(0f, -208.25f);
    private static readonly Rect2 FrogRaincoatBounds = new(-151.5f, -416.5f, 303f, 416.5f);

    internal static string ChibiVisualPath => ChibiTextureFile.ImagePath();
    internal static string FrogRaincoatVisualPath => FrogRaincoatTextureFile.ImagePath();

    internal static SakuraCombatVisualVariant ResolveVariant(
        bool combatArtFeatureEnabled,
        bool useChibi,
        bool hasFrogRaincoat)
    {
        if (combatArtFeatureEnabled && useChibi)
            return SakuraCombatVisualVariant.Chibi;

        return hasFrogRaincoat
            ? SakuraCombatVisualVariant.FrogRaincoat
            : SakuraCombatVisualVariant.Standard;
    }

    internal static NCreatureVisuals CreateSelected(
        string standardVisualPath,
        bool useChibi,
        bool hasFrogRaincoat)
    {
        var variant = ResolveVariant(
            SakuraCombatArtFeature.IsEnabled,
            useChibi,
            hasFrogRaincoat);

        if (variant == SakuraCombatVisualVariant.Standard)
            return SakuraStandeeVisuals.CreateWithLayeredIdle(standardVisualPath, "Sakura Kinomoto");

        if (variant == SakuraCombatVisualVariant.FrogRaincoat)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                FrogRaincoatVisualPath,
                "Sakura Kinomoto frog raincoat",
                FrogRaincoatScale,
                FrogRaincoatVisualPosition,
                FrogRaincoatBounds,
                centerPosition: FrogRaincoatVisualPosition,
                intentPosition: new Vector2(0f, -456.5f),
                orbPosition: new Vector2(0f, -169f),
                talkPosition: new Vector2(0f, -374f));
        }

        return SakuraStandeeVisuals.CreateWithChibiLayeredIdle(
            ChibiVisualPath,
            "Sakura Kinomoto chibi",
            ChibiScale,
            ChibiVisualPosition,
            ChibiBounds,
            centerPosition: new Vector2(0f, -176f),
            intentPosition: new Vector2(0f, -393f),
            orbPosition: new Vector2(0f, -145f),
            talkPosition: new Vector2(0f, -320f));
    }
}

internal enum SakuraCombatVisualVariant
{
    Standard,
    Chibi,
    FrogRaincoat
}
