using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Water.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Encounters;

public sealed class FreezeEncounter() : WaterMonsterTemplate(RoomType.Elite)
{
    protected override IReadOnlyDictionary<string, Vector2> SlotPositions { get; } = new Dictionary<string, Vector2> { ["CENTER"] = new(1450, 740) };
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<FreezeMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => [(ModelDb.Monster<FreezeMonster>().ToMutable(), "CENTER")];
}

public sealed class RainEncounter() : WaterMonsterTemplate(RoomType.Elite)
{
    protected override IReadOnlyDictionary<string, Vector2> SlotPositions { get; } = new Dictionary<string, Vector2> { ["CENTER"] = new(1450, 740) };
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<RainMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => [(ModelDb.Monster<RainMonster>().ToMutable(), "CENTER")];
}

public sealed class WateryEncounter() : WaterMonsterTemplate(RoomType.Boss)
{
    protected override IReadOnlyDictionary<string, Vector2> SlotPositions { get; } = new Dictionary<string, Vector2> { ["BOSS"] = new(1450, 710) };
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<WateryMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => [(ModelDb.Monster<WateryMonster>().ToMutable(), "BOSS")];
}
