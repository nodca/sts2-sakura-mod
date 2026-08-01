using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Dark;

public static class DarkEnemyCatalog
{
    public static Type EndpointEncounterType => typeof(DarkEncounter);
    public static IReadOnlyList<Type> MonsterTypes { get; } = [typeof(DarkMonster)];
}
