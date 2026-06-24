using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class RollerbladeDash() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.HasManifestedThisTurn(Owner))
        {
            await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
            return;
        }

        await PowerCmd.Apply<DuplicationPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class MagicBarrier() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new BlockVar("TemporaryBlock", 3, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var temporaryCards = SakuraActions.Hand(this).Count(card => card.IsTemporary());
        var block = DynamicVars.Block.IntValue + temporaryCards * DynamicVars["TemporaryBlock"].IntValue;
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["TemporaryBlock"].UpgradeValueBy(1);
    }
}

public class BlessingOfTheNamelessBook() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<BlessingOfTheNamelessBookPower>(
            choiceContext,
            Owner.Creature,
            BlessingOfTheNamelessBookPower.Mode(IsUpgraded),
            Owner.Creature,
            this,
            false);

    protected override void OnUpgrade() {}
}
