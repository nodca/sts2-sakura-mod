using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public enum FourthActEndpoint
{
    Dark,
    Light
}

public sealed record FourthActRouteEncounter(Type EncounterType, SourceCardIdentity RewardIdentity);

public sealed record FourthActEndpointEncounter(FourthActEndpoint Endpoint, Type? EncounterType);

public sealed class FourthActRouteDefinition
{
    public FourthActRouteDefinition(
        SakuraElement element,
        IReadOnlyList<FourthActRouteEncounter> eliteCandidates,
        FourthActRouteEncounter? elementalBoss,
        FourthActEndpointEncounter endpoint)
    {
        Element = element;
        EliteCandidates = eliteCandidates.ToArray();
        ElementalBoss = elementalBoss;
        Endpoint = endpoint;
        // Production routes are built during mod registration, before ModelDb owns canonical models.
        IsComplete = EvaluateCompleteness();
    }

    public SakuraElement Element { get; }
    public IReadOnlyList<FourthActRouteEncounter> EliteCandidates { get; }
    public FourthActRouteEncounter? ElementalBoss { get; }
    public FourthActEndpointEncounter Endpoint { get; }

    public bool IsComplete { get; }

    private bool EvaluateCompleteness() =>
        EliteCandidates.Count > 0
        && EliteCandidates.All(candidate => IsValidRewardEncounter(candidate, RoomType.Elite))
        && ElementalBoss is { } boss
        && IsValidRewardEncounter(boss, RoomType.Boss)
        && Endpoint.EncounterType is { } endpointType
        && IsEncounterOfRoomType(endpointType, RoomType.Boss);

    internal int StableOrder => Element switch
    {
        SakuraElement.Wind => 0,
        SakuraElement.Water => 1,
        SakuraElement.Fire => 2,
        SakuraElement.Earth => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(Element), Element, null)
    };

    private bool IsValidRewardEncounter(FourthActRouteEncounter encounter, RoomType roomType) =>
        IsEncounterOfRoomType(encounter.EncounterType, roomType)
        && RewardIdentityHasRouteElement(encounter.RewardIdentity);

    private bool RewardIdentityHasRouteElement(SourceCardIdentity identity)
    {
        var cardType = SakuraSourceCardRules.ClowTypeFor(identity);
        return cardType is not null
            && Activator.CreateInstance(cardType) is SakuraSourceCard card
            && card.Elements.HasElement(Element);
    }

    private static bool IsEncounterOfRoomType(Type encounterType, RoomType roomType) =>
        typeof(EncounterModel).IsAssignableFrom(encounterType)
        && !encounterType.IsAbstract
        && Activator.CreateInstance(encounterType) is EncounterModel encounter
        && encounter.RoomType == roomType;
}
