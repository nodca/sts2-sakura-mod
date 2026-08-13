using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraCombatVisuals
{
    internal const string ChibiTextureFile = "charui/chibi_combat/sakura_clow_wand_body.png";
    internal const string RedCapeTextureFile = "charui/outfits/red_cape_standee.png";
    internal const string RedCapeChibiTextureFile = "charui/outfits/red_cape_chibi_standee.png";
    internal const string FrogRaincoatTextureFile = "charui/outfits/frog_raincoat_standee.png";
    internal const string FrogRaincoatChibiTextureFile =
        "charui/outfits/frog_raincoat_chibi_standee.png";
    internal const string PinkTransformationTextureFile = "charui/outfits/pink_transformation_standee.png";
    internal const string PinkTransformationChibiTextureFile =
        "charui/outfits/pink_transformation_chibi_standee.png";

    private const float ChibiScale = 0.28f;
    private static readonly Vector2 ChibiVisualPosition = new(-10.56f, -174.08f);
    private static readonly Rect2 ChibiBounds = new(-132f, -353f, 264f, 354f);

    private const float RedCapeScale = 0.355f;
    private static readonly Vector2 RedCapeVisualPosition = new(0f, -208.25f);
    private static readonly Rect2 RedCapeBounds = new(-165.43f, -382.69f, 330.86f, 382.69f);

    private const float RedCapeChibiScale = 0.265f;
    private static readonly Vector2 RedCapeChibiVisualPosition = new(0f, -174.08f);
    private static readonly Rect2 RedCapeChibiBounds = new(-129.45f, -310.58f, 258.91f, 310.58f);

    private const float FrogRaincoatScale = 0.31f;
    private static readonly Vector2 FrogRaincoatVisualPosition = new(0f, -208.25f);
    private static readonly Rect2 FrogRaincoatBounds = new(-151.5f, -416.5f, 303f, 416.5f);

    private const float FrogRaincoatChibiScale = 0.265f;
    private static readonly Vector2 FrogRaincoatChibiVisualPosition = new(0f, -174.08f);
    private static readonly Rect2 FrogRaincoatChibiBounds =
        new(-122.19f, -325.16f, 244.38f, 325.16f);

    private const float PinkTransformationScale = 0.26f;
    private static readonly Vector2 PinkTransformationVisualPosition = new(0f, -208.25f);
    private static readonly Rect2 PinkTransformationBounds = new(-127.4f, -387.92f, 254.8f, 387.92f);

    private const float PinkTransformationChibiScale = 0.265f;
    private static readonly Vector2 PinkTransformationChibiVisualPosition = new(0f, -174.08f);
    private static readonly Rect2 PinkTransformationChibiBounds =
        new(-116.87f, -300.25f, 233.73f, 300.25f);

    internal static string ChibiVisualPath => ChibiTextureFile.ImagePath();
    internal static string RedCapeVisualPath => RedCapeTextureFile.ImagePath();
    internal static string RedCapeChibiVisualPath => RedCapeChibiTextureFile.ImagePath();
    internal static string FrogRaincoatVisualPath => FrogRaincoatTextureFile.ImagePath();
    internal static string FrogRaincoatChibiVisualPath => FrogRaincoatChibiTextureFile.ImagePath();
    internal static string PinkTransformationVisualPath => PinkTransformationTextureFile.ImagePath();
    internal static string PinkTransformationChibiVisualPath =>
        PinkTransformationChibiTextureFile.ImagePath();

    internal static SakuraCombatVisualVariant ResolveVariant(
        bool combatArtFeatureEnabled,
        bool useChibi,
        bool hasRedCape,
        bool hasFrogRaincoat,
        bool hasPinkTransformationCostume = false)
    {
        if (combatArtFeatureEnabled && useChibi)
            return hasRedCape
                ? SakuraCombatVisualVariant.RedCapeChibi
                : hasPinkTransformationCostume
                    ? SakuraCombatVisualVariant.PinkTransformationChibi
                    : hasFrogRaincoat
                        ? SakuraCombatVisualVariant.FrogRaincoatChibi
                        : SakuraCombatVisualVariant.Chibi;

        return hasRedCape
            ? SakuraCombatVisualVariant.RedCape
            : hasFrogRaincoat
                ? SakuraCombatVisualVariant.FrogRaincoat
            : hasPinkTransformationCostume
                ? SakuraCombatVisualVariant.PinkTransformation
                : SakuraCombatVisualVariant.Standard;
    }

    internal static NCreatureVisuals CreateSelected(
        string standardVisualPath,
        bool useChibi,
        bool hasRedCape,
        bool hasFrogRaincoat,
        bool hasPinkTransformationCostume = false)
    {
        var variant = ResolveVariant(
            SakuraCombatArtFeature.IsEnabled,
            useChibi,
            hasRedCape,
            hasFrogRaincoat,
            hasPinkTransformationCostume);

        if (variant == SakuraCombatVisualVariant.Standard)
            return SakuraStandeeVisuals.CreateWithLayeredIdle(standardVisualPath, "Sakura Kinomoto");

        if (variant == SakuraCombatVisualVariant.RedCape)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                RedCapeVisualPath,
                "Sakura Kinomoto red cape costume",
                RedCapeScale,
                RedCapeVisualPosition,
                RedCapeBounds,
                centerPosition: RedCapeVisualPosition,
                intentPosition: new Vector2(0f, -456.5f),
                orbPosition: new Vector2(0f, -169f),
                talkPosition: new Vector2(0f, -374f));
        }

        if (variant == SakuraCombatVisualVariant.RedCapeChibi)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                RedCapeChibiVisualPath,
                "Sakura Kinomoto chibi red cape costume",
                RedCapeChibiScale,
                RedCapeChibiVisualPosition,
                RedCapeChibiBounds,
                centerPosition: RedCapeChibiVisualPosition,
                intentPosition: new Vector2(0f, -393f),
                orbPosition: new Vector2(0f, -145f),
                talkPosition: new Vector2(0f, -320f));
        }

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

        if (variant == SakuraCombatVisualVariant.FrogRaincoatChibi)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                FrogRaincoatChibiVisualPath,
                "Sakura Kinomoto chibi frog raincoat",
                FrogRaincoatChibiScale,
                FrogRaincoatChibiVisualPosition,
                FrogRaincoatChibiBounds,
                centerPosition: FrogRaincoatChibiVisualPosition,
                intentPosition: new Vector2(0f, -393f),
                orbPosition: new Vector2(0f, -145f),
                talkPosition: new Vector2(0f, -320f));
        }

        if (variant == SakuraCombatVisualVariant.PinkTransformation)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                PinkTransformationVisualPath,
                "Sakura Kinomoto pink transformation costume",
                PinkTransformationScale,
                PinkTransformationVisualPosition,
                PinkTransformationBounds,
                centerPosition: PinkTransformationVisualPosition,
                intentPosition: new Vector2(0f, -456.5f),
                orbPosition: new Vector2(0f, -169f),
                talkPosition: new Vector2(0f, -374f));
        }

        if (variant == SakuraCombatVisualVariant.PinkTransformationChibi)
        {
            return SakuraStandeeVisuals.CreateWithWholeSpriteIdle(
                PinkTransformationChibiVisualPath,
                "Sakura Kinomoto chibi pink transformation costume",
                PinkTransformationChibiScale,
                PinkTransformationChibiVisualPosition,
                PinkTransformationChibiBounds,
                centerPosition: PinkTransformationChibiVisualPosition,
                intentPosition: new Vector2(0f, -393f),
                orbPosition: new Vector2(0f, -145f),
                talkPosition: new Vector2(0f, -320f));
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
    RedCape,
    RedCapeChibi,
    FrogRaincoat,
    FrogRaincoatChibi,
    PinkTransformation,
    PinkTransformationChibi
}
