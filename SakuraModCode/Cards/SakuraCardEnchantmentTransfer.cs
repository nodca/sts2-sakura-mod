using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardEnchantmentTransfer
{
    internal static void TransferEnchantment(CardModel source, CardModel target)
    {
        if (source.Enchantment is not { } enchantment)
            return;

        var enchantmentCopy = (EnchantmentModel)enchantment.MutableClone();
        target.EnchantInternal(enchantmentCopy, enchantmentCopy.Amount);
        enchantmentCopy.ModifyCard();
        target.FinalizeUpgradeInternal();
    }
}
