using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;

public abstract class WindEncounterTemplate(RoomType roomType) : ModEncounterTemplate
{
    protected abstract IReadOnlyDictionary<string, Vector2> SlotPositions { get; }

    public sealed override RoomType RoomType => roomType;
    public sealed override bool ShouldGiveRewards => false;
    public sealed override bool IsValidForAct(ActModel act) => act is SakuraFourthAct;
    public sealed override IReadOnlyList<string> Slots => SlotPositions.Keys.ToArray();
    protected sealed override bool SuppliesEncounterCombatSceneFromFactory => true;
    protected sealed override bool UseProgrammaticCombatBackground => true;

    protected sealed override BackgroundAssets? BuildProgrammaticCombatBackground(ActModel parentAct, Rng rng) =>
        FourthActCombatBackgrounds.CreateWindRooftop();

    protected sealed override Control TryCreateEncounterCombatScene()
    {
        var root = new Control
        {
            Name = "WindEncounterSlots",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(1920f, 1080f)
        };
        foreach (var (slotName, position) in SlotPositions)
        {
            root.AddChild(new Marker2D
            {
                Name = slotName,
                Position = position + WindCombatGrounding.EnemyOffset
            });
        }

        return root;
    }
}

public sealed class FlyEncounter() : WindEncounterTemplate(RoomType.Elite)
{
    private static readonly IReadOnlyDictionary<string, Vector2> EncounterSlotPositions =
        new Dictionary<string, Vector2> { ["CENTER"] = new(1450f, 740f) };

    protected override IReadOnlyDictionary<string, Vector2> SlotPositions => EncounterSlotPositions;
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<FlyMonster>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<FlyMonster>().ToMutable(), "CENTER")];
}

public sealed class IllusionEncounter() : WindEncounterTemplate(RoomType.Elite)
{
    private static readonly IReadOnlyDictionary<string, Vector2> EncounterSlotPositions =
        new Dictionary<string, Vector2>
        {
            ["LEFT"] = new(1120f, 740f),
            ["CENTER"] = new(1420f, 700f),
            ["RIGHT"] = new(1720f, 740f)
        };

    protected override IReadOnlyDictionary<string, Vector2> SlotPositions => EncounterSlotPositions;
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<IllusionMonster>(), ModelDb.Monster<IllusionProjectionMonster>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<IllusionProjectionMonster>().ToMutable(), "LEFT"),
        (ModelDb.Monster<IllusionMonster>().ToMutable(), "CENTER"),
        (ModelDb.Monster<IllusionProjectionMonster>().ToMutable(), "RIGHT")
    ];
}

public sealed class WindyEncounter() : WindEncounterTemplate(RoomType.Boss)
{
    private static readonly IReadOnlyDictionary<string, Vector2> EncounterSlotPositions =
        new Dictionary<string, Vector2>
        {
            ["ATTENDANT"] = new(1120f, 740f),
            ["BOSS"] = new(1480f, 710f)
        };

    protected override IReadOnlyDictionary<string, Vector2> SlotPositions => EncounterSlotPositions;
    public override EncounterAssetProfile AssetProfile => FourthActEncounterAssets.WindBoss;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<WindyMonster>(),
        ModelDb.Monster<DashMonster>(),
        ModelDb.Monster<FloatMonster>(),
        ModelDb.Monster<SleepMonster>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<WindyMonster>().ToMutable(), "BOSS")];
}
