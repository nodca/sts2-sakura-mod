using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraCombatVisuals
{
    internal const string ChibiTextureFile = "charui/chibi_combat/sakura_clow_wand_body.png";

    private const float ChibiScale = 0.28f;
    private static readonly Vector2 ChibiVisualPosition = new(-10.56f, -174.08f);
    private static readonly Rect2 ChibiBounds = new(-132f, -353f, 264f, 354f);

    internal static string ChibiVisualPath => ChibiTextureFile.ImagePath();

    internal static NCreatureVisuals CreateSelected(string standardVisualPath)
    {
        if (!SakuraCombatArtFeature.IsEnabled || !SakuraModConfig.IsChibiCombatArtEnabled())
            return SakuraStandeeVisuals.Create(standardVisualPath, "Sakura Kinomoto");

        return SakuraStandeeVisuals.CreateStatic(
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
