using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Earth.Models;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Earth.Encounters;

public abstract class EarthEncounterTemplate(RoomType roomType) : ModEncounterTemplate
{
    public sealed override RoomType RoomType => roomType;
    public sealed override bool ShouldGiveRewards => false;
    public sealed override bool IsValidForAct(ActModel act) => act is SakuraFourthAct;
    public override IReadOnlyList<string> Slots => ["CENTER"];
    protected sealed override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => Monsters;
    protected abstract IReadOnlyList<(MonsterModel, string?)> Monsters { get; }
    protected sealed override bool SuppliesEncounterCombatSceneFromFactory => true;
    protected sealed override bool UseProgrammaticCombatBackground => true;
    protected sealed override BackgroundAssets? BuildProgrammaticCombatBackground(ActModel parentAct, Rng rng) =>
        FourthActCombatBackgrounds.CreateEarthPenguinPark();

    protected sealed override Control TryCreateEncounterCombatScene()
    {
        var root = new Control
        {
            Name = "EarthEncounterSlots",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(1920f, 1080f)
        };
        foreach (var slot in Slots)
            root.AddChild(new Marker2D { Name = slot, Position = new(1450, 720) });
        return root;
    }
}

public sealed class ShadowEncounter() : EarthEncounterTemplate(RoomType.Elite)
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<ShadowMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<ShadowMonster>().ToMutable(), "CENTER")];
}

public sealed class WoodEncounter() : EarthEncounterTemplate(RoomType.Elite)
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<WoodMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<WoodMonster>().ToMutable(), "CENTER")];
}

public sealed class EarthyEncounter() : EarthEncounterTemplate(RoomType.Boss)
{
    public override IReadOnlyList<string> Slots => ["BOSS"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<EarthyMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<EarthyMonster>().ToMutable(), "BOSS")];
}
