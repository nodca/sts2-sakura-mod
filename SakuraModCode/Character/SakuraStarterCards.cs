using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStarterCards
{
    public static bool IsSakura(Player player) =>
        player.Character.Id == ModelDb.Character<SakuraMod>().Id;

    public static bool IsSakuraRun(IRunState runState) =>
        runState.Players.All(IsSakura);

    public static bool IsStarterCard(CardModel card) =>
        SakuraCardCatalog.IsStarterCard(card);

    public static bool IsStarterCard<T>(CardModel card) where T : CardModel =>
        SakuraCardCatalog.IsStarterCard<T>(card);

    public static bool IsRemovableStarterCard(CardModel card) =>
        SakuraCardCatalog.IsRemovableStarterCard(card);

    public static bool IsTransformableStarterCard(CardModel card) =>
        SakuraCardCatalog.IsTransformableStarterCard(card);

    public static bool CanReplaceStrikeOrDefendPair(Player player) =>
        SakuraCardCatalog.CanReplaceStrikeOrDefendPair(player);

    public static int CountRemovable<T>(Player player) where T : CardModel =>
        SakuraCardCatalog.CountRemovable<T>(player);
}
