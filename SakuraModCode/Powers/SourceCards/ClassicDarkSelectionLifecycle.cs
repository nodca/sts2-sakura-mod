using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace SakuraMod.SakuraModCode.Powers;

internal static class ClassicDarkSelectionLifecycle
{
    private static int _activeLocalSelections;

    internal static bool IsActive => Volatile.Read(ref _activeLocalSelections) > 0;

    internal static async Task<List<CardModel>> SelectFromHand(
        PlayerChoiceContext choiceContext,
        Player player,
        int maxSelect,
        AbstractModel source)
    {
        var ownsNativeHandSelection =
            LocalContext.IsMe(player)
            && RunManager.Instance.NetService.Type != NetGameType.Replay;
        if (ownsNativeHandSelection)
            Interlocked.Increment(ref _activeLocalSelections);

        try
        {
            return (await CardSelectCmd.FromHand(
                choiceContext,
                player,
                new CardSelectorPrefs(ClassicDarkPower.SelectionPrompt, 0, maxSelect),
                null,
                source)).ToList();
        }
        finally
        {
            if (ownsNativeHandSelection)
                Interlocked.Decrement(ref _activeLocalSelections);
        }
    }

    internal static bool ShouldCompleteBeforeTeardown(NPlayerHand hand) =>
        IsActive
        && hand.IsInCardSelection
        && hand.CurrentMode == NPlayerHand.Mode.SimpleSelect;

    private const string SelectionCompletionSourceFieldName = "_selectionCompletionSource";
    private static readonly FieldInfo? SelectionCompletionSourceField =
        AccessTools.DeclaredField(typeof(NPlayerHand), SelectionCompletionSourceFieldName);

    internal static bool PrepareTeardownPatch()
    {
        if (SelectionCompletionSourceField is not null)
            return true;

        MainFile.Logger.Warn(
            $"Dark selection teardown compatibility is disabled: " +
            $"NPlayerHand.{SelectionCompletionSourceFieldName} was not found.");
        return false;
    }

    internal static void CompleteBeforeTeardown()
    {
        if (NPlayerHand.Instance is not { } hand || !ShouldCompleteBeforeTeardown(hand))
            return;

        try
        {
            // Complete the native selector normally. Calling the game's cancel helper
            // faults the beta turn loop with TaskCanceledException during room teardown.
            var completionSource = SelectionCompletionSourceField!.GetValue(hand)
                as TaskCompletionSource<IEnumerable<CardModel>>;
            completionSource?.TrySetResult(Array.Empty<CardModel>());
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error(
                $"Failed to complete Dark's hand selection before combat teardown: " +
                $"{exception.GetBaseException()}");
        }
    }
}

[HarmonyPatch(typeof(RunManager), "ExitCurrentRoom")]
internal static class ClassicDarkSelectionRoomExitPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => ClassicDarkSelectionLifecycle.PrepareTeardownPatch();

    [HarmonyPrefix]
    private static void CompleteDarkSelectionBeforeRoomExit() =>
        ClassicDarkSelectionLifecycle.CompleteBeforeTeardown();
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp), [typeof(bool)])]
internal static class ClassicDarkSelectionRunCleanupPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => ClassicDarkSelectionLifecycle.PrepareTeardownPatch();

    [HarmonyPrefix]
    private static void CompleteDarkSelectionBeforeRunCleanup() =>
        ClassicDarkSelectionLifecycle.CompleteBeforeTeardown();
}
