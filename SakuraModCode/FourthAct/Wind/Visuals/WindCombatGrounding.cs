using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

internal static class WindCombatGrounding
{
    internal static readonly Vector2 AllyOffset = new(0f, 110f);
    internal static readonly Vector2 EnemyOffset = new(80f, 110f);

    internal static bool AppliesTo(EncounterModel? encounter) => encounter is WindEncounterTemplate;

    internal static void ApplyToAllies(IReadOnlyList<NCreature> creatureNodes)
    {
        if (!creatureNodes.Any(static node => AppliesTo(node.Entity.CombatState?.Encounter)))
            return;

        foreach (var node in creatureNodes)
            node.Position += AllyOffset;
    }
}

[HarmonyPatch(
    typeof(NCombatRoom),
    nameof(NCombatRoom.PositionPlayersAndPets),
    [typeof(List<NCreature>), typeof(float), typeof(bool)])]
internal static class WindCombatGroundingPatch
{
    [HarmonyPostfix]
    private static void PositionPlayersAndPetsPostfix(List<NCreature> creatureNodes) =>
        WindCombatGrounding.ApplyToAllies(creatureNodes);
}
