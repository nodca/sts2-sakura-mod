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
        ArgumentNullException.ThrowIfNull(eliteCandidates);
        ArgumentNullException.ThrowIfNull(endpoint);
        Element = element;
        EliteCandidates = Array.AsReadOnly(eliteCandidates.ToArray());
        ElementalBoss = elementalBoss;
        Endpoint = endpoint;
    }

    public SakuraElement Element { get; }
    public IReadOnlyList<FourthActRouteEncounter> EliteCandidates { get; }
    public FourthActRouteEncounter? ElementalBoss { get; }
    public FourthActEndpointEncounter Endpoint { get; }

    internal int StableOrder => Element switch
    {
        SakuraElement.Wind => 0,
        SakuraElement.Water => 1,
        SakuraElement.Fire => 2,
        SakuraElement.Earth => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(Element), Element, null)
    };
}
