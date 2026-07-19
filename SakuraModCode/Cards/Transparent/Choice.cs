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
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("ManifestCards", 1), new CardsVar("DrawCards", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var manifestChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceManifestChoice>(this);
        var drawChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceDrawChoice>(this);
        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [manifestChoice, drawChoice], cancelable: false);
        if (choice is ChoiceDrawChoice)
            await Draw(choiceContext, ChoiceRepeatCount(activation));
        else
            await Manifest(choiceContext, ChoiceRepeatCount(activation));
    }

    private static int ChoiceRepeatCount(SakuraExtraEffectActivation activation) =>
        activation.IsActive ? 2 : 1;

    private async Task Manifest(PlayerChoiceContext choiceContext, int repeats)
    {
        for (var i = 0; i < repeats; i++)
            await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars["ManifestCards"].IntValue);
    }

    private async Task Draw(PlayerChoiceContext choiceContext, int repeats) =>
        await CardPileCmd.Draw(choiceContext, DynamicVars["DrawCards"].IntValue * repeats, Owner, false);

    protected override void OnUpgrade() => DynamicVars["ManifestCards"].UpgradeValueBy(1);
}

