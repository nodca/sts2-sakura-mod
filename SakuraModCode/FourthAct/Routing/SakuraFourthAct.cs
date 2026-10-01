using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
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

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player.RunState.Act is not SakuraFourthAct
            || !SakuraStarterCompatibility.IsKinomotoSakura(player)
            || room is not CombatRoom combatRoom
            || FourthActRouteCatalog.RewardEncounterFor(combatRoom.Encounter.GetType()) is not { } encounter)
        {
            return false;
        }

        var cardType = SakuraSourceCardRules.SakuraTypeFor(encounter.RewardIdentity)
            ?? throw new InvalidOperationException($"Missing Sakura Card reward for {encounter.RewardIdentity}.");
        var template = ModelDb.GetById<CardModel>(ModelDb.GetId(cardType));
        var card = player.RunState.CreateCard(template, player);
        // Fixed-card rewards cannot be serialized in CombatRoom.ExtraRewards; regenerate through this hook on load.
        rewards.Add(new CardReward(
            [card],
            CardCreationSource.Encounter,
            player,
            CardCreationOptions.ForRoom(player, room.RoomType)));
        return true;
    }

    internal static async Task OfferCardRewardsAsync(CombatRoom room, CancellationToken cancellationToken)
    {
        await MegaCrit.Sts2.Core.Commands.Cmd.Wait(1f, cancellationToken);
        var generated = new List<RewardsSet>();
        foreach (var player in room.CombatState.Players)
        {
            var rewards = new RewardsSet(player).EmptyForRoom(room);
            if (room.ExtraRewards.TryGetValue(player, out var extraRewards))
                rewards.WithCustomRewards(extraRewards);
            // Match CombatRoom.OfferRoomEndRewards: generate before returning,
            // then let the player handle rewards independently of room loading.
            await rewards.GenerateWithoutOffering();
            generated.Add(rewards);
        }
        foreach (var rewards in generated)
        {
            TaskHelper.RunSafely(rewards.Offer());
        }
    }

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
