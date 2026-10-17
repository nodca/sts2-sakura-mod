using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace SakuraMod.SakuraModCode.Telemetry;

internal sealed class SakuraTelemetryLocalRun
{
    public string TeamRunKey { get; set; } = string.Empty;
    public ulong PlayerNetId { get; set; }
    public BalanceRunIdentity? Identity { get; set; }

    internal BalanceRunIdentity Resolve(string teamRunKey, ulong playerNetId)
    {
        if (TeamRunKey != teamRunKey || PlayerNetId != playerNetId || Identity?.IsValid() != true)
        {
            TeamRunKey = teamRunKey;
            PlayerNetId = playerNetId;
            Identity = BalanceRunIdentity.Create();
        }
        return Identity!;
    }
}

internal static class SakuraTelemetryPlayerReport
{
    private const string LocalDataKey = "telemetry_multiplayer_v1";

    internal static void Register()
    {
        using var registration = RitsuLibFramework.BeginModDataRegistration(MainFile.ModId);
        RitsuLibFramework.GetDataStore(MainFile.ModId).Register<SakuraTelemetryLocalRun>(
            LocalDataKey, "telemetry_multiplayer_v1.json", SaveScope.Profile,
            syncToCloud: false);
    }

    internal static BalanceRunIdentity Resolve(string teamRunKey, ulong playerNetId) =>
        RitsuLibFramework.GetDataStore(MainFile.ModId).Get<SakuraTelemetryLocalRun>(LocalDataKey)
            .Resolve(teamRunKey, playerNetId);

    internal static void Persist(BalanceRunIdentity identity)
    {
        var store = RitsuLibFramework.GetDataStore(MainFile.ModId);
        var local = store.Get<SakuraTelemetryLocalRun>(LocalDataKey);
        if (local.Identity?.RunKey != identity.RunKey)
            throw new InvalidOperationException("Multiplayer telemetry identity does not match the local run.");
        local.Identity = identity;
        store.Save(LocalDataKey);
    }

    internal static (ulong PlayerNetId, BalanceRunIdentity Identity)? Read(string teamRunKey)
    {
        var local = RitsuLibFramework.GetDataStore(MainFile.ModId).Get<SakuraTelemetryLocalRun>(LocalDataKey);
        return local.TeamRunKey == teamRunKey && local.Identity?.IsValid() == true
            ? (local.PlayerNetId, local.Identity) : null;
    }

    internal static SerializableRun Project(SerializableRun run, ulong playerNetId)
    {
        var playerIndex = run.Players.FindIndex(player => player.NetId == playerNetId);
        if (playerIndex < 0)
            throw new InvalidOperationException("Reporting player is absent from the run history.");

        // Native anonymization copies the player and history collections before projection.
        var report = run.Anonymized();
        report.Players = [report.Players[playerIndex]];
        report.MapDrawings = null;
        for (var act = 0; act < run.MapPointHistory.Count; act++)
        {
            for (var point = 0; point < run.MapPointHistory[act].Count; point++)
            {
                var source = run.MapPointHistory[act][point];
                var statsIndex = source.PlayerStats.FindIndex(stats => stats.PlayerId == playerNetId);
                if (statsIndex < 0)
                    throw new InvalidOperationException("Reporting player is absent from a history point.");
                var target = report.MapPointHistory[act][point];
                target.PlayerStats = [target.PlayerStats[statsIndex]];
            }
        }
        return report;
    }
}
