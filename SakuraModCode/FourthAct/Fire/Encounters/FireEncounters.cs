using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;

public abstract class FireEncounterTemplate(RoomType roomType) : ModEncounterTemplate
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
        FourthActCombatBackgrounds.CreateFireTokyoTower();
    protected sealed override Control TryCreateEncounterCombatScene()
    {
        var root = new Control
        {
            Name = "FireEncounterSlots",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(1920f, 1080f)
        };
        foreach (var slot in Slots)
            root.AddChild(new Marker2D { Name = slot, Position = slot switch { "LEFT" => new(400, 700), "RIGHT" => new(1464, 700), _ => new(1450, 720) } });
        ConfigureEncounterScene(root);
        return root;
    }

    protected virtual void ConfigureEncounterScene(Control root) { }
}

public sealed class SwordEncounter() : FireEncounterTemplate(RoomType.Elite)
{
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<SwordMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<SwordMonster>().ToMutable(), "CENTER")];
}

public sealed class LibraEncounter() : FireEncounterTemplate(RoomType.Elite)
{
    public override bool FullyCenterPlayers => true;
    public override IReadOnlyList<string> Slots => ["LEFT", "RIGHT"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<LibraPanMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters =>
        [(ModelDb.Monster<LibraPanMonster>().ToMutable(), "LEFT"), (ModelDb.Monster<LibraPanMonster>().ToMutable(), "RIGHT")];
    protected override void ConfigureEncounterScene(Control root) =>
        root.AddChild(new LibraVisualController());
}

public sealed class FireyEncounter() : FireEncounterTemplate(RoomType.Boss)
{
    public override IReadOnlyList<string> Slots => ["BOSS"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<FireyMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<FireyMonster>().ToMutable(), "BOSS")];
}

public sealed class LightEncounter() : FireEncounterTemplate(RoomType.Boss)
{
    public override IReadOnlyList<string> Slots => ["BOSS"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<LightMonster>()];
    protected override IReadOnlyList<(MonsterModel, string?)> Monsters => [(ModelDb.Monster<LightMonster>().ToMutable(), "BOSS")];
}
