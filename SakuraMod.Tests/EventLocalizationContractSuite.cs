using SakuraMod.SakuraModCode.Events;
using System.Text.Json;

public sealed class EventLocalizationContractSuite
{
    private static readonly IReadOnlyDictionary<Type, IReadOnlyList<string>> RequiredKeySuffixes =
        new Dictionary<Type, IReadOnlyList<string>>
        {
            [typeof(ClassicMonsterEvent)] =
            [
                ".title",
                ".pages.INITIAL.description",
                ".pages.INITIAL.options.ACCEPT.title",
                ".pages.INITIAL.options.ACCEPT.description",
                ".pages.INITIAL.options.REJECT.title",
                ".pages.INITIAL.options.REJECT.description",
                ".pages.ACCEPT.description",
                ".pages.REJECT.description"
            ],
            [typeof(ClassicXiaoLangsFeelingsEvent)] =
            [
                ".title",
                ".pages.INITIAL.description",
                ".pages.INITIAL.options.ACCEPT.title",
                ".pages.INITIAL.options.ACCEPT.description",
                ".pages.INITIAL.options.REJECT.title",
                ".pages.INITIAL.options.REJECT.description",
                ".pages.ACCEPT.description",
                ".pages.REJECT.description"
            ],
            [typeof(ClassicTheSealedCardEvent)] =
            [
                ".title",
                ".pages.INITIAL.description",
                ".pages.INITIAL.options.FIGHT_WITHOUT_LOVE.title",
                ".pages.INITIAL.options.FIGHT_WITHOUT_LOVE.description",
                ".pages.INITIAL.options.FIGHT_WITHOUT_LOVE_LOCKED.title",
                ".pages.INITIAL.options.FIGHT_WITHOUT_LOVE_LOCKED.description",
                ".pages.INITIAL.options.FIGHT_WITH_LOVE.title",
                ".pages.INITIAL.options.FIGHT_WITH_LOVE.description",
                ".pages.INITIAL.options.FIGHT_WITH_LOVE_LOCKED.title",
                ".pages.INITIAL.options.FIGHT_WITH_LOVE_LOCKED.description",
                ".pages.INITIAL.options.ESCAPE.title",
                ".pages.INITIAL.options.ESCAPE.description",
                ".pages.ESCAPE.description",
                ".pages.DONE.description"
            ]
        };

    [Theory]
    [InlineData("eng")]
    [InlineData("zhs")]
    public void ClassicEventsUseRegisteredLocalizationEntries(string locale)
    {
        var relativePath = $"SakuraMod/localization/{locale}/events.json";
        var path = RegressionTestHarness.FindRepoFile(relativePath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var (eventType, suffixes) in RequiredKeySuffixes)
        {
            var entry = RegressionTestHarness.RegisteredModelEntry(eventType);
            foreach (var suffix in suffixes)
            {
                RegressionTestHarness.Require(
                    entries.ContainsKey(entry + suffix),
                    $"Expected {relativePath} to localize {entry + suffix}.");
            }

            const string registeredPrefix = "SAKURA_MOD_EVENT_";
            RegressionTestHarness.Require(
                entry.StartsWith(registeredPrefix, StringComparison.Ordinal),
                $"Expected {eventType.Name} to use the registered event entry convention.");
            var legacyPrefix = $"SAKURAMOD-{entry[registeredPrefix.Length..]}.";
            RegressionTestHarness.Require(
                !entries.Keys.Any(key => key.StartsWith(legacyPrefix, StringComparison.Ordinal)),
                $"Expected {relativePath} to avoid legacy event key {legacyPrefix}*.");
        }
    }
}
