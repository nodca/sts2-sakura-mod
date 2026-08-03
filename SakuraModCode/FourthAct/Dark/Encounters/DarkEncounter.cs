using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;

public sealed class DarkEncounter : ModEncounterTemplate
{
    public override EncounterAssetProfile AssetProfile => FourthActEncounterAssets.DarkBoss;
    public override RoomType RoomType => RoomType.Boss;
    public override bool ShouldGiveRewards => false;
    public override bool IsValidForAct(ActModel act) => act is SakuraFourthAct;
    public override IReadOnlyList<string> Slots => ["BOSS"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<DarkMonster>()];
    protected override bool SuppliesEncounterCombatSceneFromFactory => true;
    protected override bool UseProgrammaticCombatBackground => true;

    protected override BackgroundAssets? BuildProgrammaticCombatBackground(ActModel parentAct, Rng rng) =>
        FourthActCombatBackgrounds.CreateDarkStage();

    protected override Control TryCreateEncounterCombatScene()
    {
        var root = new Control
        {
            Name = "DarkEncounterSlots",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(1920f, 1080f)
        };
        root.AddChild(new Marker2D { Name = "BOSS", Position = new Vector2(1480f, 710f) });
        return root;
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<DarkMonster>().ToMutable(), "BOSS")];
}
