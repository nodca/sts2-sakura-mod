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

public class GrowingMagic() :
    ClassicSakuraAncientCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    internal const int MagicChargeOnKill = 5;

    protected override string AncientPortraitFileName => "growing_magic.png";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicDamageVar(18, ValueProp.Move),
        new DynamicVar("Magic", MagicChargeOnKill)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6);
}

public class AnotherMe() :
    ClassicSakuraAncientCard(2, CardType.Power, TargetType.None)
{
    internal const int MagicChargeAmount = 5;

    protected override string AncientPortraitFileName => "another_me.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Magic", MagicChargeAmount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ClassicSakuraMagic.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
        await ApplyPower<AnotherMePower>(choiceContext, Owner.Creature, ReleasedMagic());
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
