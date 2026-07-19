using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class SakuraLove() : SakuraEventCard(-2, CardType.Skill, TargetType.None)
{
    internal override bool GrantsMagicCharge => false;
    internal override bool AddsVoidOnNormalSakuraPlay => false;
    protected override bool IsPlayable => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Energy", 1)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);
    }
}

