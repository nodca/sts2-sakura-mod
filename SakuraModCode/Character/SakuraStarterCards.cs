using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStarterCards
{
    private static readonly HashSet<ModelId> StarterCardIds =
    [
        ModelDb.Card<Gale>().Id,
        ModelDb.Card<Siege>().Id,
        ModelDb.Card<Stabilize>().Id,
        ModelDb.Card<DreamWand>().Id
    ];

    public static bool IsSakura(Player player) =>
        player.Character.Id == ModelDb.Character<SakuraMod>().Id;

    public static bool IsSakuraRun(IRunState runState) =>
        runState.Players.All(IsSakura);

    public static bool IsStarterCard(CardModel card) =>
        StarterCardIds.Contains(card.Id);

    public static bool IsStarterCard<T>(CardModel card) where T : CardModel =>
        card.Id == ModelDb.Card<T>().Id;

    public static bool IsRemovableStarterCard(CardModel card) =>
        IsStarterCard(card) && card.IsRemovable;

    public static bool IsTransformableStarterCard(CardModel card) =>
        IsStarterCard(card) && card.IsTransformable;

    public static bool CanReplaceStrikeOrDefendPair(Player player) =>
        CountRemovable<Gale>(player) >= 2 || CountRemovable<Siege>(player) >= 2;

    public static int CountRemovable<T>(Player player) where T : CardModel =>
        player.Deck.Cards.Count(card => IsStarterCard<T>(card) && card.IsRemovable);
}
