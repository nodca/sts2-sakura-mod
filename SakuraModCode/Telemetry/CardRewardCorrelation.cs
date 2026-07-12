namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed class CardRewardCorrelation<TPayload>
{
    private const int MaxOffers = 16;
    private readonly Queue<CardRewardCorrelationOffer<TPayload>> _offers = new();

    internal void Remember(CardRewardCorrelationOffer<TPayload> offer)
    {
        _offers.Enqueue(offer);
        while (_offers.Count > MaxOffers)
            _offers.Dequeue();
    }

    internal CardRewardCorrelationResult<TPayload>? TakeMatching(
        IReadOnlyList<CardRewardCorrelationChoice> history)
    {
        var match = _offers
            .Reverse()
            .Select(offer => (Offer: offer, Choices: ChoicesForOffer(history, offer)))
            .FirstOrDefault(static candidate => candidate.Choices.Count > 0);
        if (match.Offer is null)
            return null;

        while (_offers.TryPeek(out var stale) && stale.OfferSequence <= match.Offer.OfferSequence)
            _offers.Dequeue();

        return new CardRewardCorrelationResult<TPayload>(match.Offer.Payload, match.Choices);
    }

    internal IReadOnlyList<CardRewardCorrelationResult<TPayload>> DrainSkipped()
    {
        var results = _offers
            .Select(static offer => new CardRewardCorrelationResult<TPayload>(
                offer.Payload,
                offer.OfferedCards
                    .Select(static card => new CardRewardCorrelationChoice(card.CardId, card.UpgradeLevel, WasPicked: false))
                    .ToArray()))
            .ToArray();
        _offers.Clear();
        return results;
    }

    private static IReadOnlyList<CardRewardCorrelationChoice> ChoicesForOffer(
        IReadOnlyList<CardRewardCorrelationChoice> history,
        CardRewardCorrelationOffer<TPayload> offer)
    {
        var offeredKeys = offer.OfferedCards
            .Select(static card => new CardRewardCorrelationCardKey(card.CardId, card.UpgradeLevel))
            .ToHashSet();
        var start = Math.Clamp(offer.InitialCardChoiceHistoryCount, 0, history.Count);

        return history
            .Skip(start)
            .Where(choice => choice.CardId is not null)
            .Select(choice => (Choice: choice, Key: new CardRewardCorrelationCardKey(choice.CardId!, choice.UpgradeLevel)))
            .Where(item => offeredKeys.Contains(item.Key))
            .GroupBy(item => item.Key)
            .Select(static group => new CardRewardCorrelationChoice(
                group.Key.CardId,
                group.Key.UpgradeLevel,
                group.Any(static item => item.Choice.WasPicked)))
            .ToArray();
    }
}

internal sealed record CardRewardCorrelationOffer<TPayload>(
    TPayload Payload,
    int OfferSequence,
    int InitialCardChoiceHistoryCount,
    IReadOnlyList<CardRewardCorrelationCard> OfferedCards);

internal sealed record CardRewardCorrelationResult<TPayload>(
    TPayload Payload,
    IReadOnlyList<CardRewardCorrelationChoice> Choices);

internal readonly record struct CardRewardCorrelationCard(string CardId, int UpgradeLevel);

internal readonly record struct CardRewardCorrelationChoice(string? CardId, int UpgradeLevel, bool WasPicked);

internal readonly record struct CardRewardCorrelationCardKey(string CardId, int UpgradeLevel);
