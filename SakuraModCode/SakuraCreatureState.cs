using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SakuraMod.SakuraModCode;

internal static class SakuraCreatureState
{
    private const int MaxBlock = 999999999;

    public static async Task RestoreHp(Creature creature, int hp)
    {
        await CreatureCmd.SetCurrentHp(creature, Math.Clamp(hp, 0, creature.MaxHp));
    }

    public static void RestoreBlock(Creature creature, int block)
    {
        var targetBlock = Math.Clamp(block, 0, MaxBlock);
        var delta = targetBlock - creature.Block;
        if (delta > 0)
            creature.GainBlockInternal(delta);
        else if (delta < 0)
            creature.LoseBlockInternal(-delta);
    }
}
