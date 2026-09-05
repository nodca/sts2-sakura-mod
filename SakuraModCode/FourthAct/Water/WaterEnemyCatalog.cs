using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Water.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Water.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Water;

public static class WaterEnemyCatalog
{
    public static IReadOnlyList<FourthActRouteEncounter> EliteEncounters { get; } =
        [new(typeof(FreezeEncounter), SourceCardIdentity.Freeze), new(typeof(RainEncounter), SourceCardIdentity.Rain)];
    public static FourthActRouteEncounter BossEncounter { get; } = new(typeof(WateryEncounter), SourceCardIdentity.Watery);
    public static IReadOnlyList<Type> MonsterTypes { get; } = [typeof(FreezeMonster), typeof(RainMonster), typeof(WateryMonster)];
}
