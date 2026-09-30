using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Vanilla hit effects picked per card meaning, the same way vanilla cards pick from
/// the shared hit library instead of one unified effect.
/// </summary>
/// <remarks>
/// Every scene here is already preloaded by <c>VfxCmd.AssetPaths</c> except
/// <see cref="NLineBurstVfx"/>, whose cards declare <see cref="LineBurstAssetPaths"/>.
/// Temporary SFX names follow vanilla's own vfx/sfx pairings.
/// </remarks>
internal static class SakuraNativeHitFx
{
    public const string Slash = VfxCmd.slashPath;
    public const string Blunt = VfxCmd.bluntPath;
    public const string HeavyBlunt = VfxCmd.heavyBluntPath;
    public const string FlyingSlash = VfxCmd.flyingSlashPath;
    public const string RockShatter = VfxCmd.rockShatterPath;
    public const string SandyImpact = VfxCmd.sandyImpactPath;

    // Vanilla Defect Lightning Orb strike (same path as LightningOrb.ApplyLightningDamage).
    public const string Lightning = VfxCmd.lightningPath;
    public const string LightningSfx = "event:/sfx/characters/defect/defect_lightning_evoke";

    public const string BluntTmpSfx = "blunt_attack.mp3";
    public const string HeavyTmpSfx = "heavy_attack.mp3";

    private static readonly Color WaterTint = new(0.35f, 0.72f, 1f);
    private static readonly Color EraseTint = new(0.78f, 0.74f, 0.96f);

    public static IEnumerable<string> LineBurstAssetPaths => NLineBurstVfx.AssetPaths;

    // Same factory and scale as vanilla Cinder.
    public static Node2D? FireBurst(Creature target) => NFireBurstVfx.Create(target, 0.75f);

    public static Node2D? WaterSplash(Creature target) =>
        NCombatRoom.Instance?.GetCreatureNode(target) is { } node
            ? NSplashVfx.Create(node.VfxSpawnPosition, WaterTint)
            : null;

    public static Node2D? EraseSmoke(Creature target) => NGaseousImpactVfx.Create(target, EraseTint);

    public static Node2D? LineBurst(Creature target) =>
        NCombatRoom.Instance?.GetCreatureNode(target) is not null
            ? NLineBurstVfx.Create(target)
            : null;

    /// <summary>
    /// Plays a factory effect outside an attack, into the same container
    /// <c>AttackCommand.WithHitVfxNode</c> uses.
    /// </summary>
    public static void PlayOn(Creature target, Func<Creature, Node2D?> create) =>
        target.GetVfxContainer()?.AddChildSafely(create(target));
}
