using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Choice() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, SakuraKeywords.Manifest];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.RemindTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("ManifestCards", 1), new CardsVar("RemindCards", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
        {
            await Manifest(choiceContext, DynamicVars["ManifestCards"].IntValue);
            await RemindWithDebt(choiceContext, DynamicVars["RemindCards"].IntValue);
            return;
        }

        // With an empty Memory pile the Remind branch has nothing to offer, so
        // Choice resolves its Manifest branch directly.
        if (!Remind.HasRecallChoices(Owner))
        {
            await Manifest(choiceContext, DynamicVars["ManifestCards"].IntValue);
            return;
        }

        var manifestChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceManifestChoice>(this);
        var remindChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceRemindChoice>(this);
        manifestChoice.DynamicVars["ManifestCards"].BaseValue = DynamicVars["ManifestCards"].IntValue;
        remindChoice.DynamicVars["RemindCards"].BaseValue = DynamicVars["RemindCards"].IntValue;
        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [manifestChoice, remindChoice], cancelable: false);
        if (choice is ChoiceRemindChoice)
            await RemindWithDebt(choiceContext, DynamicVars["RemindCards"].IntValue);
        else
            await Manifest(choiceContext, DynamicVars["ManifestCards"].IntValue);
    }

    private async Task Manifest(PlayerChoiceContext choiceContext, int amount)
    {
        var manifested = await SakuraManifestLoop.Manifest(this, choiceContext, amount);
        foreach (var card in manifested)
        {
            if (!card.EnergyCost.CostsX)
                card.EnergyCost.SetThisCombat(Math.Max(0, card.EnergyCost.GetResolved() - 1), reduceOnly: true);
        }
    }

    private async Task RemindWithDebt(PlayerChoiceContext choiceContext, int amount)
    {
        var reminded = await Remind.RecallIntoHand(Owner, choiceContext, amount, upgradeCopies: false);
        if (reminded > 0 && !IsUpgraded)
            await PowerCmd.Apply<ChoiceDrawDebtPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["ManifestCards"].UpgradeValueBy(1);
}
