using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Earth.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Earth.Models;
using SakuraMod.SakuraModCode.FourthAct.Routing;

namespace SakuraMod.SakuraModCode.FourthAct.Earth;

public static class EarthEnemyCatalog
{
    public static IReadOnlyList<FourthActRouteEncounter> EliteEncounters { get; } =
        [new(typeof(ShadowEncounter), SourceCardIdentity.Shadow), new(typeof(WoodEncounter), SourceCardIdentity.Wood)];
    public static FourthActRouteEncounter BossEncounter { get; } = new(typeof(EarthyEncounter), SourceCardIdentity.Earthy);
    public static Type EndpointEncounterType => typeof(DarkEncounter);
    public static IReadOnlyList<Type> MonsterTypes { get; } = [typeof(ShadowMonster), typeof(WoodMonster), typeof(EarthyMonster)];
}
