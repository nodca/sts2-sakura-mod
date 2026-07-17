using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models;

namespace SakuraMod.SakuraModCode.Classic.Cards;

public sealed class GrowingMagicDamageHook : HookedSingletonModel
{
    public GrowingMagicDamageHook() : base(HookType.Combat)
    {
    }

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
            || ClassicEnemyRules.IsMinion(target))
        {
            return;
        }

        await ClassicSakuraMagic.GainMagic(
            choiceContext,
            growingMagic.Owner,
            GrowingMagic.MagicChargeOnKill,
            growingMagic);
    }
}
