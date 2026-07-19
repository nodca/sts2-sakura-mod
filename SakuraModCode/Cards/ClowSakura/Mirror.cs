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

public class ClowMirror() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private static LocString Prompt => CardLoc<ClowMirror>("selectionPrompt");

    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [CardKeyword.Exhaust]
        : [CardKeyword.Ethereal, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Copies", 1), new DynamicVar("ExtraCopies", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CopyMirrorTarget(choiceContext, DynamicVars["Copies"].IntValue);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CopyMirrorTarget(choiceContext, DynamicVars["Copies"].IntValue + DynamicVars["ExtraCopies"].IntValue);

    protected override void OnUpgrade()
    {
        if (Keywords.Contains(CardKeyword.Ethereal))
            RemoveKeyword(CardKeyword.Ethereal);
    }

    private async Task CopyMirrorTarget(PlayerChoiceContext choiceContext, int copies)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(IsMirrorCopyCandidate)
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

        if (selected is null)
            return;

        for (var i = 0; i < copies; i++)
        {
            var copy = SakuraSourceCardRules.CreateMirrorCopySource(selected);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    private bool IsMirrorCopyCandidate(CardModel card) =>
        card != this
        && card is SakuraSourceCard { IsClassicSourceCard: true, Identity: { } identity }
        && identity is not SourceCardIdentity.Mirror and not SourceCardIdentity.Create;
}

public class SakuraMirror() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    private static LocString Prompt => CardLoc<SakuraMirror>("selectionPrompt");

    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Mirror card choices require an active combat.");
        var choices = SakuraSourceCardRules.AllClowTemplates()
            .Select(template => combatState.CreateCard(template, Owner))
            .ToList();
        if (choices.Count == 0)
            return;

        CardModel? selected = null;
        try
        {
            selected = choices.Count == 1
                ? choices[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(Prompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    })).FirstOrDefault();

            if (selected is null)
                return;

            var copy = SakuraSourceCardRules.CreateMirrorCopySource(selected);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner, CardPilePosition.Random);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }
}

