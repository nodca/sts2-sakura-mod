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

public class ClassicDarkSakuraPower : SakuraPowerModel
{
    protected override string IconFileName => "dark_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || Owner.Player is not { } player)
            return;

        var hand = CardPile.GetCards(player, PileType.Hand).ToList();
        if (hand.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(ClassicDarkPower.SelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            hand.Contains,
            this)).ToList();

        if (selected.Count == 0)
            return;

        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card, false);

        var replacements = new List<CardModel>();
        for (var i = 0; i < selected.Count; i++)
        {
            var card = SakuraSourceCardRules.CreateRandomDarkClowCard(player);
            if (card.IsUpgradable)
                card.UpgradeInternal();
            replacements.Add(card);
        }
        CardCmd.PreviewCardPileAdd(
            await SakuraGeneratedCardLifecycle.AddGeneratedCardsToCombatWithResults(replacements, PileType.Discard, player, CardPilePosition.Bottom),
            style: CardPreviewStyle.GridLayout);
    }
}

