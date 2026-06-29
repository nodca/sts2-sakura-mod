using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraCaptureRules
{
    public static bool CaptureModeEnabled { get; set; } = true;
}

internal static class SakuraCaptureRunHooks
{
    private const string RunHookId = "SakuraMod.Capture.Run";
    private const string CombatHookId = "SakuraMod.Capture.Combat";
    private static readonly SakuraCaptureHook Hook = new();
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        ModHelper.SubscribeForRunStateHooks(RunHookId, _ => [Hook]);
        ModHelper.SubscribeForCombatStateHooks(CombatHookId, _ => [Hook]);
        _registered = true;
    }
}

internal sealed class SakuraCaptureHook : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public override IEnumerable<CardModel> ModifyMerchantCardPool(Player player, IEnumerable<CardModel> options)
    {
        return SakuraCaptureRewardHandoff.ModifyMerchantCardPool(player, options);
    }

    public override bool TryModifyCardRewardOptions(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions)
    {
        return SakuraCaptureRewardHandoff.TryModifyCardRewardOptions(player, cardRewardOptions, creationOptions);
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!SakuraCaptureRules.CaptureModeEnabled)
            return Task.CompletedTask;

        foreach (var player in room.CombatState.Players.Where(SakuraStarterCompatibility.IsSakura))
        {
            SakuraCaptureRewardHandoff.ClearOfferedPendingCaptureCandidates(player);
            SakuraCaptureRewardHandoff.PrepareCaptureCandidatesForReward(player, room.CombatState);
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (ShouldUseCaptureRules(player))
            SakuraCaptureRewardHandoff.RememberPendingCaptureRewardOffers(player, rewards);

        return false;
    }

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        if (ShouldUseCaptureRules(player))
            SakuraCaptureRewardHandoff.ClearPendingCaptureCandidatesForReward(player, reward);

        return Task.CompletedTask;
    }

    private static bool ShouldUseCaptureRules(Player player) =>
        SakuraCaptureRules.CaptureModeEnabled && SakuraStarterCompatibility.IsSakura(player);
}
