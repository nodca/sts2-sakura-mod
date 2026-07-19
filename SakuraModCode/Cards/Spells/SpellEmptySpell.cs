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

public class SpellEmptySpell() : SpellCard(0, CardType.Skill, CardRarity.Token, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellEmptySpell>("selectionPrompt");
    private static readonly Type[] ElementSpellTypes =
    [
        typeof(SpellHuoShen),
        typeof(SpellLeiDi),
        typeof(SpellFengHua),
        typeof(SpellShuiLong)
    ];

    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust, SakuraKeywords.Purge];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Empty Spell requires an active combat.");
        var choices = ElementSpellTypes
            .Select(type => combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), Owner))
            .ToList();

        try
        {
            var selected = choices.Count == 1
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

            var card = combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(selected.GetType())), Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
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

public abstract class ElementSpellCard(int cost, CardType type, TargetType target, SakuraElementSet element) :
    SpellCard(cost, type, CardRarity.Token, target)
{
    public override int MaxUpgradeLevel => 0;
    public override SakuraElementSet Elements => element;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
}

