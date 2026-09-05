using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Water;

public abstract class WaterMonsterTemplate(RoomType roomType) : ModEncounterTemplate
{
    protected abstract IReadOnlyDictionary<string, Vector2> SlotPositions { get; }
    public sealed override RoomType RoomType => roomType;
    public sealed override bool ShouldGiveRewards => false;
    public sealed override bool IsValidForAct(ActModel act) => act is SakuraFourthAct;
    public sealed override IReadOnlyList<string> Slots => SlotPositions.Keys.ToArray();
    protected sealed override bool SuppliesEncounterCombatSceneFromFactory => true;
    protected sealed override bool UseProgrammaticCombatBackground => true;
    protected sealed override BackgroundAssets? BuildProgrammaticCombatBackground(ActModel parentAct, Rng rng) =>
        FourthActCombatBackgrounds.CreateWaterAquarium();
    protected sealed override Control TryCreateEncounterCombatScene()
    {
        var root = new Control { Name = "WaterEncounterSlots", MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var (name, position) in SlotPositions)
            root.AddChild(new Marker2D { Name = name, Position = position });
        return root;
    }
}
