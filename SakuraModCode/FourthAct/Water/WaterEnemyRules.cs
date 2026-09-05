using MegaCrit.Sts2.Core.Random;

namespace SakuraMod.SakuraModCode.FourthAct.Water;

public static class WaterEnemyRules
{
    public static int RollTidalDamage(uint runSeed, uint combatId, int roundNumber, int minimum, int maximum) =>
        new Rng(runSeed, $"tidal/{combatId}/{roundNumber}").NextInt(minimum, maximum + 1);

    public static int RemainingReservoir(int reservoir, int consumed) =>
        Math.Max(0, reservoir - Math.Max(0, consumed));
}
