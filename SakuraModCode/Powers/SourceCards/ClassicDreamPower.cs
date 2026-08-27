using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
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
    private sealed class Data
    {
        public readonly List<DreamSwap> Swaps = [];
    }

    private sealed class DreamSwap
    {
        public required CardModel RestoredClow { get; init; }
        public uint TemplateCombatCardIndex;
    }

    protected override string IconFileName => "dream.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    private List<DreamSwap> Swaps => GetInternalData<Data>().Swaps;

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
            var restoredClow = Owner.CombatState.CloneCard(original);
            restoredClow.DeckVersion = original.DeckVersion;
            if (await ReplaceInPile(hand, original, template))
            {
                Swaps.Add(new DreamSwap
                {
                    RestoredClow = restoredClow,
                    TemplateCombatCardIndex = NetCombatCard.FromModel(template).CombatCardIndex
                });
            }
            else
            {
                restoredClow.CardScope?.RemoveCard(restoredClow);
                template.CardScope?.RemoveCard(template);
            }
        }
    }

    private async Task ReturnOriginalClowCards()
    {
        foreach (var swap in Swaps.ToList())
        {
            var template = ResolveCombatCard(swap.TemplateCombatCardIndex);
            if (template is null)
            {
                RemoveRestoredCloneIfDetached(swap.RestoredClow);
                continue;
            }

            if (template.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust } pile)
            {
                await ReplaceInPile(pile, template, swap.RestoredClow);
                continue;
            }

            RemoveRestoredCloneIfDetached(swap.RestoredClow);
        }

        Swaps.Clear();
    }

    private static CardModel? ResolveCombatCard(uint combatCardIndex) =>
        NetCombatCardDb.Instance.TryGetCard(combatCardIndex, out var card) ? card : null;

    private static void RemoveRestoredCloneIfDetached(CardModel restoredClow)
    {
        if (restoredClow.Pile is null)
            restoredClow.CardScope?.RemoveCard(restoredClow);
    }

    private async Task<bool> ReplaceInPile(CardPile pile, CardModel oldCard, CardModel newCard)
    {
        if (!pile.Cards.Contains(oldCard))
            return false;

        await CardPileCmd.RemoveFromCombat(oldCard, skipVisuals: false);
        var result = await CardPileCmd.Add(newCard, pile, CardPilePosition.Top, this, skipVisuals: false);
        if (!result.success || !ReferenceEquals(newCard.Pile, pile))
            throw new InvalidOperationException($"Failed to replace {oldCard.Id.Entry} with {newCard.Id.Entry}.");

        return true;
    }
}
