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

public class ClowLock() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
    internal const int ChargePerSelectedCard = 1;

    private static LocString Prompt => CardLoc<ClowLock>("selectionPrompt");

    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        // Retain this turn; a Forgotten card also skips this turn's move into Memory.
        var selected = await SelectLockedCards(choiceContext);
        foreach (var card in selected)
        {
            card.GiveSingleTurnRetain();
            card.DelayTemporaryRemoval(1);
        }

        await GainUpgradedCharge(choiceContext, selected);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selected = await SelectLockedCards(choiceContext);
        foreach (var card in selected)
        {
            card.AddKeyword(CardKeyword.Retain);
            await card.Stabilize(choiceContext);
        }

        await GainUpgradedCharge(choiceContext, selected);
    }

    private async Task GainUpgradedCharge(PlayerChoiceContext choiceContext, IReadOnlyList<CardModel> selected)
    {
        if (IsUpgraded && selected.Count > 0)
            await SakuraMagicCharge.GainMagic(choiceContext, Owner, selected.Count * ChargePerSelectedCard, this);
    }

    private async Task<IReadOnlyList<CardModel>> SelectLockedCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return [];

        return (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(Prompt, Math.Min(DynamicVars.Cards.IntValue, hand.Count))
            {
                Cancelable = false,
                RequireManualConfirmation = false
            },
            card => hand.Contains(card),
            this)).ToList();
    }
}

public class SakuraLock() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLockSakuraPower>(choiceContext, Owner.Creature, 1);
}

