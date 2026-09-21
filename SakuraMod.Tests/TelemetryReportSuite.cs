using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Telemetry;

public sealed class TelemetryReportSuite
{
    [Fact]
    public void FinalReportIncludesUnpickedShopEventCardsAndDeduplicatesDefinitions()
    {
        var history = JsonNode.Parse("""
        {"players":[{"deck":[{"id":"CARD.A","current_upgrade_level":1}]}],
         "map_point_history":[[{"map_point_type":"shop","player_stats":[{
           "card_choices":[{"card":{"id":"CARD.B"},"was_picked":false}],
           "cards_gained":[{"id":"CARD.A","current_upgrade_level":1}]}]},
           {"map_point_type":"event","player_stats":[{
           "card_choices":[{"card":{"id":{"entry":"C"},"current_upgrade_level":1},"was_picked":false}],
           "cards_transformed":[{"original_card":{"id":"CARD.A"},"final_card":{"id":"CARD.C","current_upgrade_level":1}}],
           "cards_removed":[{"id":"CARD.D"}],"upgraded_cards":["CARD.A"]}]}]]}
        """)!;
        var original = history.ToJsonString();
        var identity = Identity();
        var requested = new List<SakuraTelemetryCardReference>();
        var report = SakuraTelemetryReport.Build(identity, new JsonObject { ["run_history"] = history }, card =>
        {
            requested.Add(card);
            return new(card.CardId, card.UpgradeLevel, "Common", "Skill", "1", "clow", "SakuraMod");
        });
        Assert.Equal(5, requested.Count);
        Assert.Contains(new("B", 0), requested);
        Assert.Contains(new("C", 1), requested);
        Assert.Equal(5, report["card_definitions"]!.AsArray().Count);
        Assert.Equal(3, report["balance_contract_version"]!.GetValue<int>());
        Assert.Equal(identity.RunKey, report["context"]!["run_key"]!.GetValue<string>());
        Assert.Equal(original, history.ToJsonString());
        var restored = JsonSerializer.Deserialize<BalanceRunIdentity>(JsonSerializer.Serialize(identity))!;
        var resumed = SakuraTelemetryReport.Build(restored, new JsonObject { ["run_history"] = history.DeepClone() },
            card => new(card.CardId, card.UpgradeLevel, "Common", "Skill", "1", "clow", "SakuraMod"));
        Assert.True(JsonNode.DeepEquals(report, resumed));
    }

    [Fact]
    public void MissingDefinitionDoesNotInventMetadataOrLoseReportIdentity()
    {
        var identity = Identity();
        var report = SakuraTelemetryReport.Build(identity,
            JsonNode.Parse("""{"run_history":{"players":[{"deck":[{"id":"CARD.UNKNOWN"}]}]}}"""), _ => null);
        Assert.Empty(report["card_definitions"]!.AsArray());
        Assert.Equal(identity.ContextChecksum, report["context_checksum"]!.GetValue<string>());
        var old = JsonSerializer.SerializeToNode(identity)!.AsObject();
        old["last_offer_sequence"] = 12;
        var restored = old.Deserialize<BalanceRunIdentity>()!;
        Assert.True(restored.IsValid());
        Assert.Equal(identity.RunKey, restored.RunKey);
    }

    [Fact]
    public void CanonicalSakuraMetadataCanBeReadWithoutGameplayOwner()
    {
        var type = SakuraCardCatalog.Entries.Single(entry => entry.CardType.Name == "ClowPower").CardType;
        var card = (CardModel)Activator.CreateInstance(type)!;
        Assert.True(SakuraTelemetryCardClassifier.TryClassify(card, out var definition));
        Assert.Equal("clow", definition.Category);
        Assert.Equal("SakuraMod", definition.Owner);
    }

    private static BalanceRunIdentity Identity()
    {
        var identity = BalanceRunIdentity.Create();
        identity.Context = new(2, identity.RunKey, "v1.7.7", 0, 1, "Standard", []);
        identity.ContextChecksum = SakuraTelemetryContract.ContextChecksum(identity.Context);
        return identity;
    }
}
