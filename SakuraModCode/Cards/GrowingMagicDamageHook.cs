using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.Cards;

public sealed class GrowingMagicDamageHook : RunModel
{
    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource is not GrowingMagic growingMagic
            || !result.WasTargetKilled
            || target.Side != CombatSide.Enemy
            || SakuraEnemyRules.IsMinion(target))
        {
            return;
        }

        await SakuraMagicCharge.GainMagic(
            choiceContext,
            growingMagic.Owner,
            GrowingMagic.MagicChargeOnKill,
            growingMagic);
    }
}
