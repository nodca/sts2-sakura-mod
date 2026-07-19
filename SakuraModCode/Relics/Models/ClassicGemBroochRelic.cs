using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicGemBroochRelic : SakuraRelicModel
{
    protected override string IconFileName => "gem_brooch.png";
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var plan = BuildDeckPlan(Owner.Deck.Cards);

        foreach (var duplicate in plan.Duplicates)
            await CardPileCmd.RemoveFromDeck(duplicate);

        foreach (var retained in plan.RetainedCards.Where(static card => card.IsUpgradable))
            CardCmd.Upgrade(retained);
    }

    internal static SakuraGemBroochDeckPlan BuildDeckPlan(IEnumerable<CardModel> deckCards)
    {
        var cards = deckCards.ToList();
        List<CardModel> retainedCards = [];
        List<CardModel> duplicates = [];

        AddIdentityPlan(cards.OfType<ClowSword>(), retainedCards, duplicates);
        AddIdentityPlan(cards.OfType<ClowShield>(), retainedCards, duplicates);

        return new SakuraGemBroochDeckPlan(retainedCards, duplicates);
    }

    private static void AddIdentityPlan<TCard>(
        IEnumerable<TCard> cards,
        List<CardModel> retainedCards,
        List<CardModel> duplicates)
        where TCard : CardModel
    {
        using var enumerator = cards.GetEnumerator();
        if (!enumerator.MoveNext())
            return;

        retainedCards.Add(enumerator.Current);
        while (enumerator.MoveNext())
            duplicates.Add(enumerator.Current);
    }
}

