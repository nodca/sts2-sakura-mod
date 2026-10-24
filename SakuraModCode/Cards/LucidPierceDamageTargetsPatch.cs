using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
    [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)])]
internal static class LucidPierceDamageTargetsPatch
{
    private static void Prefix(ref IEnumerable<Creature> targets, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (LucidPiercePower.ExpandDamageTargets(targets, props, dealer, cardSource) is { } expanded)
            targets = expanded;
    }
}
