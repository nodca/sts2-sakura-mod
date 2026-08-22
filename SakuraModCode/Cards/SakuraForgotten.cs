using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraForgotten
{
    private static readonly List<Func<PlayerChoiceContext, CardModel, Task>> StabilizeObservers = [];
    private static readonly List<Action<CardModel>> ClearedObservers = [];

    internal static void AddStabilizeObserver(Func<PlayerChoiceContext, CardModel, Task> observer)
    {
        if (!StabilizeObservers.Contains(observer))
            StabilizeObservers.Add(observer);
    }

    internal static void AddClearedObserver(Action<CardModel> observer)
    {
        if (!ClearedObservers.Contains(observer))
            ClearedObservers.Add(observer);
    }

    internal static Task<bool> GrantTemporary(PlayerChoiceContext context, CardModel card)
    {
        if (card.IsTemporary() || card is ISakuraForgottenImmune)
            return Task.FromResult(false);

        card.MakeTemporary();
        return Task.FromResult(true);
    }

    internal static async Task NotifyStabilized(PlayerChoiceContext context, CardModel card)
    {
        foreach (var observer in StabilizeObservers)
            await observer(context, card);
    }

    internal static void NotifyCleared(CardModel card)
    {
        foreach (var observer in ClearedObservers)
            observer(card);
    }
}
