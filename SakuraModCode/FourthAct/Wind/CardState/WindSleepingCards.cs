using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.CardState;

public sealed class SleepingAffliction : ModAfflictionTemplate
{
    public const string OverlayScenePath =
        MainFile.ResPath + "/scenes/cards/overlays/sleeping_affliction.tscn";

    public override AfflictionAssetProfile AssetProfile => new(OverlayScenePath);

    internal static bool ShouldBlockPlay(
        CardModel candidate,
        CardModel afflictedCard,
        AutoPlayType autoPlayType) =>
        ReferenceEquals(candidate, afflictedCard) && autoPlayType == AutoPlayType.None;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        !ShouldBlockPlay(card, Card, autoPlayType);
}

public static class WindSleepingCards
{
    public static bool IsSleeping(CardModel card) => card.Affliction is SleepingAffliction;

    public static async Task<bool> MarkSleeping(CardModel card) =>
        await CardCmd.Afflict<SleepingAffliction>(card, 1) is not null;

    public static void Wake(CardModel card)
    {
        if (!IsSleeping(card))
            return;

        SleepingCardVisuals.PlayWake(card);
        CardCmd.ClearAffliction(card);
    }

    public static void WakeAll(Player player)
    {
        foreach (var card in player.PlayerCombatState?.AllCards.ToArray() ?? [])
            Wake(card);
    }
}
