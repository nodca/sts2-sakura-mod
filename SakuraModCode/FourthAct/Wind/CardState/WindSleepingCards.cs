using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Content;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.CardState;

public sealed class SleepingCardCapability : CardCapability, ICardPlayStateContributor, ICardHoverTipContributor
{
    public bool? CanPlay(CardModel card) => false;

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card) =>
    [
        new HoverTip(
            new LocString("static_hover_tips", "SAKURAMOD-SLEEPING.title"),
            new LocString("static_hover_tips", "SAKURAMOD-SLEEPING.description").GetFormattedText())
    ];
}

public static class WindSleepingCards
{
    public static void Register() =>
        ModContentRegistry.For(MainFile.ModId).RegisterModelCapability<SleepingCardCapability>();

    public static bool IsSleeping(CardModel card) =>
        card.Capabilities().All.OfType<SleepingCardCapability>().Any();

    public static void MarkSleeping(CardModel card)
    {
        if (!IsSleeping(card))
            card.AddCapability(ModelCapabilityRegistry.Create<SleepingCardCapability>(), allowMerge: false);
    }

    public static void Wake(CardModel card)
    {
        foreach (var capability in card.Capabilities().All.OfType<SleepingCardCapability>().ToArray())
            card.Capabilities().Remove(capability);
    }

    public static void WakeAll(Player player)
    {
        foreach (var card in player.PlayerCombatState?.AllCards ?? [])
            Wake(card);
    }
}
