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

public class Choice() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, SakuraKeywords.Manifest];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.RemindTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(9, ValueProp.Move),
        new DamageVar(11, ValueProp.Move),
        new CardsVar("ManifestCards", 1),
        new CardsVar("RemindCards", 1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
        {
            await Guard(choiceContext, play);
            await Strike(choiceContext);
            return;
        }

        // Both branches stay on offer even with an empty Memory pile: the Strike
        // branch still deals its damage when Remind finds nothing.
        var guardChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceManifestChoice>(this);
        var strikeChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceRemindChoice>(this);
        guardChoice.DynamicVars.Block.BaseValue = DynamicVars.Block.BaseValue;
        guardChoice.DynamicVars["ManifestCards"].BaseValue = DynamicVars["ManifestCards"].IntValue;
        strikeChoice.DynamicVars.Damage.BaseValue = DynamicVars.Damage.BaseValue;
        strikeChoice.DynamicVars["RemindCards"].BaseValue = DynamicVars["RemindCards"].IntValue;
        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [guardChoice, strikeChoice], cancelable: false);
        if (choice is ChoiceRemindChoice)
            await Strike(choiceContext);
        else
            await Guard(choiceContext, play);
    }

    private async Task Guard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars["ManifestCards"].IntValue);
    }

    private async Task Strike(PlayerChoiceContext choiceContext)
    {
        await DealDamageToRandomEnemies(choiceContext, DynamicVars.Damage.IntValue, 1);
        await Remind.RecallIntoHand(Owner, choiceContext, DynamicVars["RemindCards"].IntValue, upgradeCopies: false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}
