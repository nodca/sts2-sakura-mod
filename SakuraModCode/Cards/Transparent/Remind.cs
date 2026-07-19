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

public class Remind() : TransparentCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static LocString SelectionPrompt => CardLoc<Remind>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.RemindTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var choices = SakuraMemoryPile.Get(Owner)?.Cards.ToList() ?? [];
        var recallCount = DynamicVars.Cards.IntValue;
        var cards = (choices.Count <= recallCount
            ? choices.ToList()
            : await SakuraActions.SelectUpToFromCards(
                this,
                choiceContext,
                choices,
                recallCount,
                cancelable: false,
                prompt: SelectionPrompt,
                minSelect: recallCount)).ToList();

        var copies = await SakuraMemoryPile.Consume(Owner, cards);

        try
        {
            foreach (var copy in copies)
            {
                await SakuraGeneratedCardLifecycle.AddTemporaryRememberedCardToHand(
                    copy,
                    freeThisTurn: true,
                    context: choiceContext);
            }
        }
        finally
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(copies);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
