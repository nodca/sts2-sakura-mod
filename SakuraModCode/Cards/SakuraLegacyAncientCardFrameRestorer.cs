using MegaCrit.Sts2.Core.Nodes.Cards;
using SakuraMod.SakuraModCode.Classic.Cards;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraLegacyAncientCardFrameRestorer
{
    private static readonly MethodInfo? ReloadMethod =
        typeof(NCard).GetMethod("Reload", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void RestoreIfSakuraLegacy(NCard card)
    {
        if (card.Model is not SakuraLegacy || ReloadMethod is null)
            return;

        ReloadMethod.Invoke(card, null);
    }
}
