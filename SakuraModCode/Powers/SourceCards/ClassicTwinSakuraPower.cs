using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class ClassicTwinSakuraPower : SakuraPowerModel
{
    protected override string IconFileName => "twin_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card.Owner?.Creature == Owner && IsClow(card) && !card.IsClone && !card.IsDupe
            ? playCount + Math.Max(0, Amount)
            : playCount;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshKnownClowCardCosts();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            RefreshKnownClowCardCosts();

        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner.Player)
            RefreshCost(card);

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (card.Owner?.Creature != Owner || Owner.GetPower<ClassicNothingPower>() is not null)
        {
            newCost = currentCost;
            return false;
        }

        return TryIncreaseClowCardCost(card, Amount, currentCost, out newCost);
    }

    internal static bool TryIncreaseClowCardCost(CardModel card, int amount, decimal currentCost, out decimal newCost)
    {
        var costIncrease = Math.Max(0, amount);
        if (!IsCostedClow(card, currentCost) || costIncrease <= 0)
        {
            newCost = currentCost;
            return false;
        }

        newCost = currentCost + costIncrease;
        return true;
    }

    private void RefreshKnownClowCardCosts()
    {
        if (Owner.Player is not { } player)
            return;

        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            RefreshCost(card);
    }

    private static void RefreshCost(CardModel card)
    {
        if (IsCostedClow(card, card.EnergyCost.GetWithModifiers(CostModifiers.Local)))
            card.InvokeEnergyCostChanged();
    }

    private static bool IsCostedClow(CardModel card, decimal currentCost) =>
        IsClow(card)
        && !card.EnergyCost.CostsX
        && currentCost >= 0;

    private static bool IsClow(CardModel card) =>
        card is SakuraSourceCard { IsClowCard: true };
}

