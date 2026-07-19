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

public class Action() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new CardsVar("ExtraDraw", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var card = SakuraActions.Hand(this).Any(card => card != this)
            ? await SakuraActions.SelectHandCard(this, choiceContext, card => card != this, cancelable: false)
            : null;

        if (card is not null)
            await CardCmd.Exhaust(choiceContext, card);

        var manifested = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var copy in manifested)
            copy.SetToFreeThisTurn();

        if (activation.IsActive)
            await CardPileCmd.Draw(choiceContext, DynamicVars["ExtraDraw"].IntValue, Owner, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["ExtraDraw"].UpgradeValueBy(1);
    }
}

