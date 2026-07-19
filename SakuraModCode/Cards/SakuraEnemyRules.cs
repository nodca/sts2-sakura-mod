using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraEnemyRules
{
    public static bool IsMinion(Creature target) =>
        target.IsSecondaryEnemy || target.HasPower<MinionPower>();

    public static bool IsEliteOrBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Elite or RoomType.Boss;

    public static bool IsEliteOrBossTarget(Creature target) =>
        IsEliteOrBossCombat(target) && !IsMinion(target);

    public static bool IsBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Boss;
}
