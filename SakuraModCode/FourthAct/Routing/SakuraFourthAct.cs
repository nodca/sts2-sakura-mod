using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public sealed class SakuraFourthAct : ModActTemplate
{
    private static readonly ActAssetProfile GloryAssets =
        ContentAssetProfiles.FromVanillaActId("glory");

    public override ActAssetProfile AssetProfile => GloryAssets;
    public override int Index => FourthActEntryRegistration.FourthActSlotIndex;
    public override bool IsDefault => false;
    public override bool IsUnlocked(UnlockState unlockState) => true;
    public override Color MapTraveledColor => new("1D1E2F");
    public override Color MapUntraveledColor => new("60717C");
    public override Color MapBgColor => new("819A97");
    public override string[] BgMusicOptions => ["event:/music/act3_a1_v1", "event:/music/act3_a2_v1"];
    public override string[] MusicBankPaths => ["res://banks/desktop/act3_a1.bank", "res://banks/desktop/act3_a2.bank"];
    public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";
    public override string ChestSpineSkinNameNormal => "act3";
    public override string ChestSpineSkinNameStroke => "act3_stroke";
    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act3";
    protected override int NumberOfWeakEncounters => 0;
    protected override int BaseNumberOfRooms => 4;

    public override IEnumerable<EncounterModel> BossDiscoveryOrder =>
        FourthActRouteCatalog.Resolve().CompleteRoutes is [var route, ..]
            ? [Encounter(RequiredElementalBoss(route).EncounterType)]
            : [];

    public override IEnumerable<AncientEventModel> AllAncients =>
        ModelDb.Act<Glory>().AllAncients;

    public override IEnumerable<EventModel> AllEvents => [];

    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
        FourthActRouteCatalog.Resolve().CompleteEncounterTypes.Select(Encounter);

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState unlockState) =>
        AllAncients;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(0, 1);

    internal void ConfigureRouteBosses()
    {
        var route = FourthActRouteCatalog.Resolve().CompleteRoutes.FirstOrDefault()
            ?? throw new InvalidOperationException("A complete fourth-act route is required to configure Sakura's fourth act.");
        var endpoint = route.Endpoint.EncounterType
            ?? throw new InvalidOperationException("A complete fourth-act route requires an endpoint encounter.");
        SetBossEncounter(Encounter(RequiredElementalBoss(route).EncounterType));
        SetSecondBossEncounter(Encounter(endpoint));
    }

    protected override void ApplyActDiscoveryOrderModifications(UnlockState unlockState) =>
        ConfigureRouteBosses();

    private static EncounterModel Encounter(Type encounterType) =>
        ModelDb.GetById<EncounterModel>(ModelDb.GetId(encounterType));

    private static FourthActRouteEncounter RequiredElementalBoss(FourthActRouteDefinition route) =>
        route.ElementalBoss
        ?? throw new InvalidOperationException("A complete fourth-act route requires an elemental boss.");
}
