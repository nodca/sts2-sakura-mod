using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Lints player-facing rules text against the terminology glossary in
/// .trellis/spec/runtime/card-keyword-and-terminology-guidelines.md.
/// Flavour, dialogue, event prose, and titles are out of scope.
/// </summary>
public sealed class LocalizationTerminologySuite
{
    private static readonly string[] RulesTextFiles =
    [
        "afflictions.json",
        "card_keywords.json",
        "cards.json",
        "enchantments.json",
        "intents.json",
        "powers.json",
        "relics.json",
        "static_hover_tips.json"
    ];

    private static readonly string[] RulesTextSuffixes =
    [
        "description",
        "smartDescription",
        "extraDescription",
        "extraCardText"
    ];

    private static readonly (string Rule, Regex Pattern)[] EnglishBans =
    [
        ("E1 Firey -> Fiery", new Regex(@"\bFirey\b")),
        ("E3 Extra Effect: -> Extra:", new Regex(@"Extra Effect:")),
        ("E4 rounds -> turns", new Regex(@"\brounds?\b")),
        ("E6 all enemies -> ALL enemies", new Regex(@"\b[Aa]ll enemies\b")),
        ("E7 (s) -> {Var:plural:x|xs}", new Regex(@"\w\(s\)")),
        ("E10 non-secondary -> non-minion", new Regex(@"non-secondary")),
        ("E5b lowercase pile name -> [gold]X Pile[/gold]", new Regex(@"\b(?:[Dd]iscard|[Dd]raw|[Ee]xhaust) pile\b")),
        ("E11 colon timing -> At the start/end of your turn", new Regex(@"\b(?:Start of turn|End of turn|Enemy turn start):"))
    ];

    private static readonly (string Rule, Regex Pattern)[] ChineseBans =
    [
        ("Z1 能耗 -> 耗能", new Regex("能耗")),
        ("Z3 全体敌人 -> 所有敌人", new Regex("全体敌人")),
        ("Z5 回合开始： -> 在你的回合开始时，", new Regex("回合开始：")),
        ("Z6 回合结束： -> 在你的回合结束时，", new Regex("回合结束：")),
        ("Z7 消耗堆 -> 消耗牌堆", new Regex("消耗堆"))
    ];

    [Fact]
    public void EnglishRulesTextUsesGlossaryTerms() =>
        RequireNoBannedTerms("eng", EnglishBans);

    [Fact]
    public void ChineseRulesTextUsesGlossaryTerms() =>
        RequireNoBannedTerms("zhs", ChineseBans);

    [Fact]
    public void RulesTextScopeExcludesFlavourAndTitles()
    {
        RegressionTestHarness.Require(
            IsRulesTextKey("SAKURA_MOD_CARD_CLOW_SWORD.description")
            && IsRulesTextKey("SAKURA_MOD_POWER_CLASSIC_FLY_POWER.smartDescription")
            && IsRulesTextKey("SAKURA_MOD_CARD_CLOW_RAIN.extraDescription")
            && IsRulesTextKey("SAKURA_MOD_CARD_REMIND.selectionPrompt")
            && !IsRulesTextKey("SAKURA_MOD_CARD_CLOW_FIREY.title")
            && !IsRulesTextKey("SAKURA_MOD_RELIC_CLASSIC_RED_CAPE_RELIC.flavor"),
            "Expected the terminology lint to cover descriptions and prompts but skip titles and flavour.");
        RegressionTestHarness.Require(
            EnglishBans.Any(ban => ban.Pattern.IsMatch("Draw {Cards:diff()} card(s)."))
            && EnglishBans.Any(ban => ban.Pattern.IsMatch("Put it into your discard pile."))
            && !EnglishBans.Any(ban => ban.Pattern.IsMatch(
                "Deal {Damage:diff()} damage to ALL enemies.\nAdd 1 [gold]Void[/gold] into your [gold]Discard Pile[/gold].")),
            "Expected the English bans to catch legacy forms and accept glossary forms.");
    }

    private static void RequireNoBannedTerms(string locale, (string Rule, Regex Pattern)[] bans)
    {
        var violations = new List<string>();
        foreach (var file in RulesTextFiles)
        {
            var path = RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/{file}");
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"Expected {locale}/{file} to be a string map.");
            foreach (var (key, value) in values)
            {
                if (!IsRulesTextKey(key))
                    continue;

                foreach (var (rule, pattern) in bans)
                {
                    var match = pattern.Match(value);
                    if (match.Success)
                        violations.Add($"{locale}/{file}:{key}: [{rule}] \"{match.Value}\"");
                }
            }
        }

        RegressionTestHarness.Require(
            violations.Count == 0,
            "Localization rules text uses banned terminology (see card-keyword-and-terminology-guidelines.md):\n"
            + string.Join("\n", violations));
    }

    private static bool IsRulesTextKey(string key)
    {
        var suffix = key[(key.LastIndexOf('.') + 1)..];
        return RulesTextSuffixes.Contains(suffix, StringComparer.Ordinal)
               || suffix.EndsWith("Prompt", StringComparison.Ordinal);
    }
}
