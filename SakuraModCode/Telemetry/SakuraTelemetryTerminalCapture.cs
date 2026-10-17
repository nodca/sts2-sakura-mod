using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using STS2RitsuLib.Telemetry;

namespace SakuraMod.SakuraModCode.Telemetry;

internal static class SakuraTelemetryTerminalCapture
{
    private static readonly ConditionalWeakTable<SerializableRun, object> AttemptedAbandons = new();
    [ThreadStatic] private static BalanceRunIdentity? _terminalRun;

    internal static bool IsCapturingTerminal => _terminalRun is not null;

    internal static JsonNode? BuildTerminalContribution(JsonNode? basePayload = null)
    {
        if (_terminalRun is not { } data) return null;
        var version = SakuraTelemetryContract.SakuraModVersion(
            SakuraTelemetryContract.GameplayMods(STS2RitsuLib.Compat.RitsuModManager.GetKnownMods()));
        return SakuraTelemetryReport.Build(data, basePayload,
            reference => data.Context?.SakuraModVersion == version ? SakuraTelemetryReport.Resolve(reference) : null,
            includeCatalogCards: true);
    }

    internal static void WithTerminalContribution(BalanceRunIdentity data, Action capture)
    {
        var previous = _terminalRun;
        try
        {
            _terminalRun = data;
            capture();
        }
        finally
        {
            _terminalRun = previous;
        }
    }

    internal static BalanceRunIdentity? ReadSavedIdentity(SerializableRun run)
    {
        var entry = ReadSavedEntry(run);
        return run.Players is { Count: > 1 }
            ? ReadLocalIdentity(entry)?.Identity : ReadSavedIdentityEntry(entry);
    }

    private static (ulong PlayerNetId, BalanceRunIdentity Identity)? ReadLocalIdentity(JsonObject? entry) =>
        entry?["schema"]?.GetValue<int>() == 1 && entry["kind"]?.GetValue<string>() == "run"
            && entry["data"]?["run_key"]?.GetValue<string>() is { } teamRunKey
            && Guid.TryParseExact(teamRunKey, "D", out _)
                ? SakuraTelemetryPlayerReport.Read(teamRunKey) : null;

    private static ulong? ReportingPlayerId(SerializableRun run) =>
        run.Players.Count == 1 ? run.Players[0].NetId : ReadLocalIdentity(ReadSavedEntry(run))?.PlayerNetId;

    private static JsonObject? ReadSavedEntry(SerializableRun run)
    {
        // RitsuLib 0.5.19 exposes RunState slots only. Read its attached save
        // document without loading a run or reading a different save from disk.
        var runtime = typeof(RitsuLibFramework).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRuntime", true)!;
        object?[] documentArgs = [run, null];
        if (!(bool)AccessTools.Method(runtime, "TryGetDocument").Invoke(null, documentArgs)!)
            return null;
        var document = documentArgs[1]!;
        object?[] entryArgs = [MainFile.ModId, SakuraTelemetry.RunSavedDataKey, null];
        if (!(bool)AccessTools.Method(document.GetType(), "TryGetRaw").Invoke(document, entryArgs)!)
            return null;
        return entryArgs[2] as JsonObject;
    }

    internal static BalanceRunIdentity? ReadSavedIdentityEntry(JsonObject? entry)
    {
        if (entry?["schema"]?.GetValue<int>() != 1 || entry["kind"]?.GetValue<string>() != "run")
            return null;
        var data = entry["data"]?.Deserialize<BalanceRunIdentity>();
        return data is { Context: { } context, Usage: not null }
            && data.IsValid() && context.RunKey == data.RunKey
            && context.BalanceContractVersion is 1 or SakuraTelemetryContract.Version
            && SakuraTelemetryContract.ContextChecksum(context) == data.ContextChecksum
                ? data : null;
    }

    internal static BalanceRunIdentity? PrepareAbandon(SerializableRun run)
    {
        var client = RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId);
        if (!SakuraTelemetry.IsSakuraSerializableRun(run, ReportingPlayerId(run))
            || !client.IsEnabled(SakuraTelemetry.RunHistoryRequestId)
            || AttemptedAbandons.TryGetValue(run, out _))
            return null;
        var data = ReadSavedIdentity(run);
        if (data?.Context is not { } context || context.PlayerCount != 1
            || context.Ascension != run.Ascension || context.GameMode != run.GameMode.ToString())
            throw new InvalidOperationException("Abandoned save has no valid matching telemetry identity.");

        AttemptedAbandons.Add(run, new object());
        var coverage = new SakuraTelemetryCoverageAccumulator();
        SakuraTelemetryCoverage.CaptureIfEnabled(client, SakuraTelemetryCoverage.CoverageEventName,
            SakuraTelemetryCoverage.BuildCoveragePayload(data.RunKey,
                SakuraTelemetryCoverage.StageTerminalAttempted, 1, 1, coverage),
            properties: new Dictionary<string, object?>
            {
                ["report_scope"] = "player", ["party_player_count"] = run.Players.Count
            });
        return data;
    }

    internal static void CaptureAbandon(SerializableRun run, BalanceRunIdentity data) =>
        CapturePlayerRun(run, data,
            ReportingPlayerId(run) ?? throw new InvalidOperationException("Abandoned save has no reporting player."),
            isVictory: false, isAbandoned: true, "main_menu_abandon");

    internal static void CapturePlayerRun(SerializableRun run, BalanceRunIdentity data,
        ulong playerNetId, bool isVictory, bool isAbandoned, string captureSource)
    {
        if (!RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId).IsEnabled(SakuraTelemetry.RunHistoryRequestId))
            return;
        var report = SakuraTelemetryPlayerReport.Project(run, playerNetId);
        if (!SakuraTelemetry.IsSakuraSerializableRun(report, report.Players[0].NetId))
            return;
        WithTerminalContribution(data, () =>
            TelemetryApi.CaptureVanillaRunHistory(SakuraTelemetry.ApplicantId,
                JsonSerializer.SerializeToNode(report, JsonSerializationUtility.GetTypeInfo<SerializableRun>())!,
                properties: new Dictionary<string, object?>
                {
                    ["capture_source"] = captureSource,
                    ["is_victory"] = isVictory,
                    ["is_abandoned"] = isAbandoned,
                    ["occurred_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["run_game_mode"] = run.GameMode.ToString(),
                    ["run_is_daily"] = false,
                    ["run_player_count"] = 1,
                    ["run_character_ids"] = new[] { report.Players[0].CharacterId!.ToString() },
                    ["report_scope"] = "player",
                    ["party_player_count"] = run.Players.Count,
                    ["run_floor_reached"] = run.FloorReached,
                    ["run_ascension"] = run.Ascension,
                    ["run_time_seconds"] = run.RunTime,
                    ["run_win_time_seconds"] = run.WinTime
                }));
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(RitsuLibFramework.FlushTelemetryAsync());
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
internal static class SakuraTelemetryBeforeRunEndedPatch
{
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, bool ____runHistoryWasUploaded)
    {
        if (!____runHistoryWasUploaded)
            SakuraTelemetry.TryExecute(
                () => SakuraTelemetryRunHooks.ObserveTerminal(__instance.DebugOnlyGetState()),
                exception => SakuraTelemetry.LogCaptureFailure("before run serialization", exception));
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.AbandonRun))]
internal static class SakuraTelemetryMainMenuAbandonPatch
{
    [HarmonyPrefix]
    private static void Prefix(ReadSaveResult<SerializableRun>? ____readRunSaveResult,
        out (SerializableRun Run, BalanceRunIdentity Data)? __state)
    {
        (SerializableRun, BalanceRunIdentity)? capture = null;
        SakuraTelemetry.TryExecute(() =>
        {
            if (____readRunSaveResult is { Success: true, SaveData: { } run }
                && SakuraTelemetryTerminalCapture.PrepareAbandon(run) is { } data)
                capture = (run, data);
        }, exception => SakuraTelemetry.LogCaptureFailure("before main menu abandon", exception));
        __state = capture;
    }

    [HarmonyPostfix]
    private static void Postfix((SerializableRun Run, BalanceRunIdentity Data)? __state)
    {
        if (__state is { } capture)
            SakuraTelemetry.TryExecute(
                () => SakuraTelemetryTerminalCapture.CaptureAbandon(capture.Run, capture.Data),
                exception => SakuraTelemetry.LogCaptureFailure("main menu abandon serialization", exception));
    }
}
