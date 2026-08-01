using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Wind;

public static class WindEnemyCatalog
{
    public static IReadOnlyList<FourthActRouteEncounter> EliteEncounters { get; } =
    [
        new(typeof(FlyEncounter), SourceCardIdentity.Fly),
        new(typeof(IllusionEncounter), SourceCardIdentity.Illusion)
    ];

    public static FourthActRouteEncounter BossEncounter { get; } =
        new(typeof(WindyEncounter), SourceCardIdentity.Windy);

    public static IReadOnlyList<Type> EliteEncounterTypes { get; } =
        EliteEncounters.Select(static encounter => encounter.EncounterType).ToArray();

    public static Type BossEncounterType => BossEncounter.EncounterType;

    public static IReadOnlyList<Type> WindyEncounterMonsterTypes { get; } =
    [
        typeof(WindyMonster),
        typeof(DashMonster),
        typeof(FloatMonster),
        typeof(SleepMonster)
    ];

    public static IReadOnlyList<Type> MonsterTypes { get; } =
    [
        typeof(WindyMonster),
        typeof(FlyMonster),
        typeof(IllusionMonster),
        typeof(IllusionProjectionMonster),
        typeof(DashMonster),
        typeof(FloatMonster),
        typeof(SleepMonster)
    ];

}
