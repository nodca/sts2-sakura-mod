using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Classic.Powers;

namespace SakuraMod.SakuraModCode.Classic.Cards;

public class ClowNothing() : ClassicClowCard(2, CardType.Power, CardRarity.Event, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.None;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicDamageVar(4, ValueProp.Unblockable | ValueProp.Unpowered),
        new ClassicBlockVar(3, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicNothingPower>() is null)
            await ApplyPower<ClassicNothingPower>(choiceContext, Owner.Creature, 1);

        Owner.Creature.GetPower<ClassicNothingPower>()?.SetValues(ReleasedDamage(), ReleasedBlock(), IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Damage"].UpgradeValueBy(2);
        DynamicVars["Block"].UpgradeValueBy(2);
    }
}

public class SakuraLove() : ClassicSpecialSakuraCard(-2, CardType.Skill, TargetType.None)
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

public class SakuraHope() : ClassicSpecialSakuraCard(4, CardType.Power, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicHopePower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class SakuraLegacy() : ClassicSakuraCard(1, CardType.Skill, CardRarity.Ancient, TargetType.None)
{
    internal override bool GrantsMagicCharge => false;
    internal override bool AddsVoidOnNormalSakuraPlay => false;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await ClassicSakuraMagic.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
