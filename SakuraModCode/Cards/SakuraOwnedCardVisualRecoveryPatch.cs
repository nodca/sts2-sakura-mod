using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
public static class SakuraOwnedCardVisualRecoveryPatch
{
    private const string RecoverableObjectName = "Godot.CompressedTexture2D";

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static Exception? UpdateVisualsFinalizer(NCard __instance, Exception? __exception)
    {
        return RecoverSakuraOwnedCardVisuals(__instance, __exception, nameof(NCard.UpdateVisuals));
    }

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(NCard._EnterTree))]
    public static Exception? EnterTreeFinalizer(NCard __instance, Exception? __exception)
    {
        return RecoverSakuraOwnedCardVisuals(__instance, __exception, nameof(NCard._EnterTree));
    }

    private static Exception? RecoverSakuraOwnedCardVisuals(NCard card, Exception? exception, string source)
    {
        var family = SakuraCardVisualFamilies.Family(card);
        if (exception is null
            || !IsRecoverableGodotObjectLifetimeException(exception)
            || family == SakuraCardVisualFamily.Vanilla)
        {
            return exception;
        }

        try
        {
            ApplySakuraOwnedVisuals(card, family);
            MainFile.Logger.Warn(
                $"Recovered disposed card texture during {source} for {card.Model?.Id} ({family}).");
            return null;
        }
        catch (Exception recoveryException)
        {
            MainFile.Logger.Error(
                $"Failed to recover disposed card texture during {source} for {card.Model?.Id} ({family}): {recoveryException}");
            return exception;
        }
    }

    private static void ApplySakuraOwnedVisuals(NCard card, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.Apply(card);
                break;
            case SakuraCardVisualFamily.Kinomoto:
                SakuraNonClearFrameApplier.ApplyTextureRecovery(card);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.Apply(card);
                break;
        }
    }

    private static bool IsRecoverableGodotObjectLifetimeException(Exception exception) =>
        exception is ObjectDisposedException { ObjectName: RecoverableObjectName }
        || exception.InnerException is not null && IsRecoverableGodotObjectLifetimeException(exception.InnerException);
}
