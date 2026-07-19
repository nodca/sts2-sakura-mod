using MegaCrit.Sts2.Core;
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

public class TrueOrFalse() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [SakuraKeywords.Fire, SakuraKeywords.Stabilize]
        : [SakuraKeywords.Fire, CardKeyword.Exhaust, SakuraKeywords.Stabilize];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(2)
    ];
    protected override bool IsPlayable => SakuraCardModel.UsesMagicChargeExtraEffect(this)
        ? CanCompleteExtraEffect()
        : CanUseVirtual() || CanUseReal();

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
        {
            if (await Virtual(choiceContext))
                await Real(choiceContext);
            return;
        }

        List<CardModel> choices = [];
        if (CanUseVirtual())
        {
            var virtualChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseDrawChoice>(this);
            virtualChoice.DynamicVars.Cards.BaseValue = DynamicVars.Cards.IntValue;
            choices.Add(virtualChoice);
        }
        if (CanUseReal())
        {
            var realChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseEnergyChoice>(this);
            realChoice.DynamicVars.Energy.BaseValue = DynamicVars.Energy.IntValue;
            choices.Add(realChoice);
        }

        var choice = choices.Count == 1
            ? choices[0]
            : await SakuraActions.SelectFromCards(this, choiceContext, choices, cancelable: false);
        if (choice is TrueOrFalseDrawChoice)
            await Virtual(choiceContext);
        else if (choice is TrueOrFalseEnergyChoice)
            await Real(choiceContext);
    }

    private bool CanUseVirtual() => SakuraActions.Hand(this).Any(CanBecomeTemporary);

    private bool CanUseReal() => SakuraActions.StabilizeCandidates(this).Count > 0;

    private bool CanCompleteExtraEffect() => CanUseVirtual();

    private async Task<bool> Virtual(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanBecomeTemporary, cancelable: false);
        if (card is null || !await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card))
            return false;

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        return true;
    }

    private bool CanBecomeTemporary(CardModel card) =>
        card != this && !card.IsTemporary();

    private async Task<bool> Real(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectStabilizeCandidate(this, choiceContext, cancelable: false);
        if (card is null)
            return false;

        await card.Stabilize(choiceContext);
        if (card.IsTemporary())
            return false;

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        return true;
    }

    protected override void OnUpgrade() => RemoveKeywordIfPresent(CardKeyword.Exhaust);
}
