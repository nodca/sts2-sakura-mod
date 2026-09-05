namespace SakuraMod.SakuraModCode.FourthAct.Earth;

public static class EarthEnemyRules
{
    // Shadow
    public const int ShadowHp = 255;
    public const int ShadowToughHp = 270;
    public const int ShadowClawsDamage = 7;
    public const int ShadowClawsA9Damage = 8;
    public const int ShadowClawsHits = 3;
    public const int ShadowVeilBlock = 16;
    public const int ShadowVeilA8Block = 20;
    public const int ShadowVeilHeal = 6;
    public const int ShadowVeilA9Heal = 8;
    public const int ShadowSurgeStrength = 3;
    public const int ShadowSurgeA9Strength = 4;
    public const int ShadowSurgeBlock = 10;
    public const int ShadowSurgeA8Block = 14;
    public const int ShadowBiteDamage = 28;
    public const int ShadowBiteA9Damage = 32;

    // Wood
    public const int WoodHp = 245;
    public const int WoodToughHp = 260;
    public const int WoodStrikeBase = 14;
    public const int WoodStrikeA9Base = 16;
    public const int WoodStrikePerRoot = 2;
    public const int WoodSproutBaseBlock = 10;
    public const int WoodSproutA8BaseBlock = 12;
    public const int WoodSproutStrength = 1;
    public const int WoodSproutA9Strength = 2;

    public static int WoodStrikeDamage(int rootedCount, bool deadly) =>
        (deadly ? WoodStrikeA9Base : WoodStrikeBase) + Math.Max(0, rootedCount) * WoodStrikePerRoot;

    public static int WoodSproutBlock(int rootedCount, bool tough) =>
        (tough ? WoodSproutA8BaseBlock : WoodSproutBaseBlock) + Math.Max(0, rootedCount);

    // Earthy
    public const int EarthyHp = 440;
    public const int EarthyToughHp = 465;
    public const int EarthyTremorBase = 18;
    public const int EarthyTremorA9Base = 20;
    public const int EarthyRockfallDamage = 8;
    public const int EarthyRockfallA9Damage = 9;
    public const int EarthyRockfallHits = 2;
    public const int EarthyChargeStrength = 2;
    public const int EarthyChargeA9Strength = 3;
    public const int EarthyChargeBlock = 16;
    public const int EarthyChargeA8Block = 20;
    public const int EarthyLandslideDamage = 10;
    public const int EarthyLandslideA9Damage = 12;

    public static int EarthyTremorDamage(int sediment, bool deadly) =>
        (deadly ? EarthyTremorA9Base : EarthyTremorBase) + Math.Max(0, sediment);
}
