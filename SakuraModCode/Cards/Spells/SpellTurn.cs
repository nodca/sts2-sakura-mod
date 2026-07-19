using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class SpellTurn() : SpellCard(-2, CardType.Skill, CardRarity.Token, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellTurn>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    public override int MaxUpgradeLevel => 0;
    protected override bool IsPlayable => CardPile.GetCards(Owner, PileType.Hand).Any(SakuraSourceCardRules.IsEligibleClowForTurn);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(SakuraSourceCardRules.IsEligibleClowForTurn)
            .ToList();
        if (choices.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(Prompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = false
            },
            card => choices.Contains(card),
            this)).FirstOrDefault();

        if (selected is not ClowCard { Identity: { } identity } selectedClow)
            return;

        var canonicalSakura = SakuraSourceCardRules.SakuraTemplateFor(identity);
        if (canonicalSakura is null || SakuraSourceCardRules.HasSakuraIdentity(Owner, identity))
            return;

        var deckCard = selectedClow.DeckVersion;
        if (deckCard is null || deckCard.Pile?.Type != PileType.Deck)
            return;

        var vfx = SpellTurnTransformationVfx.TryCreate(selectedClow);
        try
        {
            if (vfx is not null && !await vfx.PlayPrelude())
                return;

            var handCard = await TransformSelected(selectedClow, deckCard, canonicalSakura);
            if (handCard is null)
                return;

            if (vfx is not null)
                await vfx.PlayReveal(handCard);
        }
        finally
        {
            vfx?.Dispose();
        }

        if (DeckVersion?.Pile?.Type == PileType.Deck)
            await CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false);
    }

    private async Task<CardModel?> TransformSelected(
        ClowCard selected,
        CardModel deckCard,
        CardModel canonicalSakura)
    {
        var deckReplacement = Owner.RunState.CreateCard(canonicalSakura, Owner);
        var results = (await CardCmd.Transform(
            [new CardTransformation(deckCard, deckReplacement)],
            null,
            CardPreviewStyle.None)).ToList();
        if (results.Count == 0)
            return null;

        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Spell Turn requires an active combat.");
        var handCard = combatState.CreateCard(canonicalSakura, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(handCard, PileType.Hand, Owner, CardPilePosition.Random);
        SakuraReleaseState.Reset(selected);
        await CardPileCmd.RemoveFromCombat(selected, skipVisuals: false);
        return handCard;
    }
}

