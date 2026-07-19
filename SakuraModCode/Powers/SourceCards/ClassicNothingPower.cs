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

public class ClassicNothingPower : SakuraPowerModel
{
    private int _damage = 4;
    private int _block = 3;
    private bool _upgraded;

    protected override string IconFileName => "nothing.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", _damage),
        new DynamicVar("Block", _block)
    ];

    public void SetValues(int damage, int block, bool upgraded)
    {
        if (_upgraded)
            return;

        _damage = damage;
        _block = block;
        _upgraded = upgraded;
        DynamicVars["Damage"].BaseValue = _damage;
        DynamicVars["Block"].BaseValue = _block;
        InvokeDisplayAmountChanged();
        RefreshKnownMagicCardCosts();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshKnownMagicCardCosts();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (IsCostedOwnedMagic(card) && currentCost > 0)
        {
            newCost = 0;
            return true;
        }

        newCost = currentCost;
        return false;
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!IsOwnedClowOrSakura(card))
            return false;

        return keywords.Add(CardKeyword.Exhaust);
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position) =>
        IsOwnedClowOrSakura(card)
            ? (PileType.Exhaust, position)
            : (pileType, position);

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        RefreshCost(card);
        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator?.Creature == Owner)
            RefreshCost(card);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!IsOwnedClowOrSakura(play.Card))
            return;

        await CreatureCmd.GainBlock(Owner, _block, SakuraPowerValueProps.Block, null, false);

        var combatState = Owner.CombatState
            ?? throw new InvalidOperationException("Sakura Nothing requires an active combat.");
        foreach (var enemy in combatState.HittableEnemies.ToList())
            await CreatureCmd.Damage(choiceContext, enemy, _damage, SakuraPowerValueProps.HpLoss, Owner, null);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (_upgraded || Owner.Player is not { } player)
            return;

        var candidates = player.Deck.Cards
            .Where(static card => card is not ClowNothing)
            .ToList();
        var swallowed = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (swallowed is not null)
            await CardPileCmd.RemoveFromDeck(swallowed, showPreview: true);
    }

    private void RefreshKnownMagicCardCosts()
    {
        if (Owner.Player is not { } player)
            return;

        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            RefreshCost(card);
    }

    private static void RefreshCost(CardModel card)
    {
        if (IsCostedMagic(card))
            card.InvokeEnergyCostChanged();
    }

    private bool IsCostedOwnedMagic(CardModel card) =>
        card.Owner?.Creature == Owner && IsCostedMagic(card);

    private static bool IsCostedMagic(CardModel card) =>
        card is SakuraSourceCard { IsClassicSourceCard: true }
        && !card.EnergyCost.CostsX
        && card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0;

    private bool IsOwnedClowOrSakura(CardModel? card) =>
        card?.Owner?.Creature == Owner
        && card is SakuraSourceCard { IsClassicSourceCard: true };
}

