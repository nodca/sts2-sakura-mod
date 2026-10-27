using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Remind() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static LocString SelectionPrompt => CardLoc<Remind>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.RemindTipKey];
    protected override bool HasEnergyCostX => true;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await RecallIntoHand(Owner, choiceContext, 2 * ResolveEnergyXValue(), upgradeCopies: IsUpgraded);

        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext);
    }

    // Shared Remind resolution: choose up to count Memory cards (all of them when
    // there are not more than count) and move those cards into the hand, free this turn.
    internal static async Task<int> RecallIntoHand(
        Player owner,
        PlayerChoiceContext choiceContext,
        int count,
        bool upgradeCopies)
    {
        var choices = RecallChoices(SakuraMemoryPile.Get(owner)?.Cards ?? []);
        if (count <= 0 || choices.Count == 0)
            return 0;

        var cards = choices.Count <= count
            ? choices.ToList()
            : (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                choices,
                owner,
                new CardSelectorPrefs(SelectionPrompt, count, count) { Cancelable = false })).ToList();

        return (await SakuraMemoryPile.RecallToHand(owner, cards, upgradeCopies)).Count;
    }

    internal static bool HasRecallChoices(Player owner) =>
        RecallChoices(SakuraMemoryPile.Get(owner)?.Cards ?? []).Count > 0;

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext)
    {
        var combatState = CombatState
            ?? throw new InvalidOperationException("Remind extra effect requires combat.");
        var templates = new CardModel[]
        {
            combatState.CreateCard<Time>(Owner),
            combatState.CreateCard<Rewind>(Owner)
        };

        CardModel? selected = null;
        try
        {
            selected = await SakuraActions.SelectFromCardPreviews(
                this,
                choiceContext,
                templates,
                cancelable: false);
            if (selected is null)
                return;

            await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(
                selected,
                choiceContext,
                refreshGeneratedTransparentHandVisual: false);
            selected.EnergyCost.AddThisCombat(-1);
        }
        finally
        {
            foreach (var template in templates)
            {
                if (template != selected && template.Pile is null)
                    template.CardScope?.RemoveCard(template);
            }
        }
    }

    internal static IReadOnlyList<CardModel> RecallChoices(IEnumerable<CardModel> memoryCards) =>
        memoryCards
            .Where(SakuraSourceCardRules.CanBeTargetedByClearCardEffects)
            .ToList();

    protected override void OnUpgrade() { }
}
