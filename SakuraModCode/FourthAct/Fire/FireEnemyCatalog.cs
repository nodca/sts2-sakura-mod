using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Routing;

namespace SakuraMod.SakuraModCode.FourthAct.Fire;

public static class FireEnemyCatalog
{
    public static IReadOnlyList<FourthActRouteEncounter> EliteEncounters { get; } =
        [new(typeof(SwordEncounter), SourceCardIdentity.Sword), new(typeof(LibraEncounter), SourceCardIdentity.Libra)];
    public static FourthActRouteEncounter BossEncounter { get; } = new(typeof(FireyEncounter), SourceCardIdentity.Firey);
    public static Type EndpointEncounterType => typeof(DarkEncounter);
    public static IReadOnlyList<Type> MonsterTypes { get; } = [typeof(SwordMonster), typeof(LibraPanMonster), typeof(FireyMonster), typeof(LightMonster)];
}
