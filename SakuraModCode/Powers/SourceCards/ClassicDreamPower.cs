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

public class ClassicDreamPower : SakuraPowerModel
{
    private readonly List<DreamSwap> _swaps = [];

    protected override string IconFileName => "dream.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        ConvertCurrentHand();

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        await ReturnOriginalClowCards();
        await PowerCmd.Remove(this);
    }

    public async Task ConvertCurrentHand()
    {
        if (Owner.Player is not { } player)
            return;

        var hand = CardPile.Get(PileType.Hand, player);
        if (hand is null)
            return;

        foreach (var original in hand.Cards.ToList().OfType<ClowCard>())
        {
            if (original.Identity is not { } identity
                || SakuraSourceCardRules.SakuraTypeFor(identity) is not { } sakuraType)
                continue;

            var template = Owner.CombatState!.CreateCard(
                ModelDb.GetById<CardModel>(ModelDb.GetId(sakuraType)),
                player);
            if (await ReplaceInPile(hand, original, template))
                _swaps.Add(new DreamSwap(identity, original, template));
        }
    }

    private async Task ReturnOriginalClowCards()
    {
        foreach (var swap in _swaps.ToList())
        {
            if (swap.Template.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust } pile)
            {
                await ReplaceInPile(pile, swap.Template, swap.Original);
                swap.Template.CardScope?.RemoveCard(swap.Template);
                continue;
            }

            if (swap.Original.Pile is null)
                swap.Original.CardScope?.RemoveCard(swap.Original);
        }

        _swaps.Clear();
    }

    private async Task<bool> ReplaceInPile(CardPile pile, CardModel oldCard, CardModel newCard)
    {
        if (!pile.Cards.Contains(oldCard))
            return false;

        await CardPileCmd.RemoveFromCombat(oldCard, skipVisuals: false);
        await CardPileCmd.Add(newCard, pile, CardPilePosition.Random, this, skipVisuals: false);
        return true;
    }

    private sealed record DreamSwap(SourceCardIdentity Identity, CardModel Original, CardModel Template);
}

