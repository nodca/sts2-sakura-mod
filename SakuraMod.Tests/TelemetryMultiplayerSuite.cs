using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SakuraMod.SakuraModCode.Telemetry;
using STS2RitsuLib;
using STS2RitsuLib.Telemetry;

public sealed class TelemetryMultiplayerSuite
{
    [Fact]
    public void EligibilityFollowsTheLocalPlayerInAMixedParty()
    {
        var run = MixedRun();
        Assert.True(SakuraTelemetry.IsSakuraSerializableRun(run, 20242));
        Assert.False(SakuraTelemetry.IsSakuraSerializableRun(run, 10142));
        Assert.False(SakuraTelemetry.IsSakuraSerializableRun(run, 999));
        Assert.False(SakuraTelemetry.IsSakuraSerializableRun(run, null));
        run.GameMode = GameMode.Custom;
        Assert.False(SakuraTelemetry.IsSakuraSerializableRun(run, 20242));
    }

    [Fact]
    public void PlayerReportExcludesTeammatesAndKeepsTheNativeHistoryIntact()
    {
        var run = MixedRun();
        var report = SakuraTelemetryPlayerReport.Project(run, 20242);
        Assert.Equal(SakuraTelemetry.SakuraCharacterEntry,
            Assert.IsType<ModelId>(Assert.Single(report.Players).CharacterId).Entry);
        Assert.Equal("OWN_CARD", Assert.IsType<ModelId>(Assert.Single(report.Players[0].Deck).Id).Entry);
        var stats = Assert.Single(report.MapPointHistory[0][0].PlayerStats);
        Assert.Equal("OWN_GAIN", Assert.IsType<ModelId>(Assert.Single(stats.CardsGained).Id).Entry);
        Assert.Equal(report.Players[0].NetId, stats.PlayerId);
        Assert.NotEqual(20242UL, report.Players[0].NetId);
        Assert.Equal(run.Ascension, report.Ascension);
        Assert.Equal(run.FloorReached, report.FloorReached);
        Assert.Equal(2, run.Players.Count);
        Assert.Equal(2, run.MapPointHistory[0][0].PlayerStats.Count);
        Assert.Equal(20242UL, run.Players[1].NetId);
        Assert.Throws<InvalidOperationException>(() => SakuraTelemetryPlayerReport.Project(run, 999));
    }

    [Fact]
    public void LocalIdentitySurvivesReloadAndDoesNotMergePlayersOrRuns()
    {
        var local = new SakuraTelemetryLocalRun();
        var identity = local.Resolve("team-a", 20242);
        identity.Usage = [new SakuraTelemetryUsageRow("OWN_CARD", 0, "Common", "Skill", "1",
            "clow", "SakuraMod", "deck_owned", 0, 8, 1)];
        var restored = JsonSerializer.Deserialize<SakuraTelemetryLocalRun>(JsonSerializer.Serialize(local))!;
        var resumed = restored.Resolve("team-a", 20242);
        Assert.Equal(identity.RunKey, resumed.RunKey);
        Assert.Equal(8, Assert.Single(resumed.Usage).DrawCount);
        Assert.NotEqual(identity.RunKey, restored.Resolve("team-a", 10142).RunKey);
        Assert.NotEqual(identity.RunKey, restored.Resolve("team-b", 20242).RunKey);
    }

    [Fact]
    public void AutomaticTeamReportsAreRejectedAndOnlyScopedPlayerReportsAreAdmitted()
    {
        var teamCapture = new TelemetryCaptureContext(SakuraTelemetry.RunHistoryEventName,
            SakuraTelemetry.RunHistoryRequestId, TelemetryDataCategory.RunHistory, "run_ended",
            new RunEndedEvent(MixedRun(), true, false, DateTimeOffset.UnixEpoch));
        Assert.False(SakuraTelemetry.ShouldCaptureBalanceTelemetry(teamCapture));
        var playerCapture = new TelemetryCaptureContext(SakuraTelemetry.RunHistoryEventName,
            SakuraTelemetry.RunHistoryRequestId, TelemetryDataCategory.RunHistory, "manual");
        Assert.False(SakuraTelemetry.ShouldCaptureBalanceTelemetry(playerCapture));
        SakuraTelemetryTerminalCapture.WithTerminalContribution(BalanceRunIdentity.Create(),
            () => Assert.True(SakuraTelemetry.ShouldCaptureBalanceTelemetry(playerCapture)));
        Assert.False(SakuraTelemetry.ShouldCaptureBalanceTelemetry(playerCapture));
    }

    private static SerializableRun MixedRun() => new()
    {
        GameMode = GameMode.Standard, Ascension = 7,
        Players =
        [
            new SerializablePlayer { NetId = 10142, CharacterId = new ModelId("CHARACTER", "IRONCLAD"),
                Deck = [new SerializableCard { Id = new ModelId("CARD", "TEAM_CARD") }] },
            new SerializablePlayer { NetId = 20242, CharacterId = new ModelId("CHARACTER", SakuraTelemetry.SakuraCharacterEntry),
                Deck = [new SerializableCard { Id = new ModelId("CARD", "OWN_CARD") }] }
        ],
        MapPointHistory =
        [
            [new MapPointHistoryEntry
            {
                PlayerStats =
                [
                    new PlayerMapPointHistoryEntry { PlayerId = 20242,
                        CardsGained = [new SerializableCard { Id = new ModelId("CARD", "OWN_GAIN") }] },
                    new PlayerMapPointHistoryEntry { PlayerId = 10142,
                        CardsGained = [new SerializableCard { Id = new ModelId("CARD", "TEAM_GAIN") }] }
                ]
            }]
        ]
    };
}
