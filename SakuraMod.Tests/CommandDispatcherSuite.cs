using SakuraMod.TestRunner;

public sealed class CommandDispatcherSuite
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public async Task GeneralHelpReturnsSuccess(string helpArgument)
    {
        Assert.Equal(0, await CommandDispatcher.RunAsync([helpArgument]));
    }

    [Theory]
    [InlineData("fast")]
    [InlineData("package")]
    [InlineData("runtime")]
    [InlineData("combat")]
    [InlineData("multiplayer")]
    [InlineData("preflight")]
    [InlineData("protocol-self-test")]
    public async Task LayerHelpReturnsSuccessWithoutRunningTheLayer(string layer)
    {
        Assert.Equal(0, await CommandDispatcher.RunAsync([layer, "--help"]));
    }

    [Fact]
    public async Task RuntimeRejectsCombatScenarioArguments()
    {
        Assert.Equal(
            2,
            await CommandDispatcher.RunAsync(
                ["runtime", "--scenario", "generated-pile-memory"]));
    }

    [Fact]
    public void HelpDocumentsTheSpecificCombatScenarioEntryPoint()
    {
        using var writer = new StringWriter();

        CommandDispatcher.PrintHelp(writer);
        CombatCommand.PrintHelp(writer);

        var help = writer.ToString();
        Assert.Contains("scripts/test-mod combat [--scenario <id>]", help, StringComparison.Ordinal);
        Assert.Contains("affliction-visual-layout", help, StringComparison.Ordinal);
        Assert.Contains("dark-selection-combat-reentry", help, StringComparison.Ordinal);
        Assert.Contains("exchange-four-pile-selection", help, StringComparison.Ordinal);
        Assert.Contains("generated-pile-memory", help, StringComparison.Ordinal);
        Assert.Contains("windy-bind-draw", help, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("multiplayer")]
    [InlineData("multiplayer", "--scenario")]
    [InlineData("multiplayer", "--scenario", "unknown")]
    [InlineData("multiplayer", "--scenario", "clow-defensive-powers", "extra")]
    public async Task MultiplayerRejectsMissingUnknownAndExtraArguments(params string[] args)
    {
        Assert.Equal(2, await CommandDispatcher.RunAsync(args));
    }

    [Fact]
    public void HelpDocumentsFocusedMultiplayerEntryPoint()
    {
        using var writer = new StringWriter();
        CommandDispatcher.PrintHelp(writer);
        MultiplayerCommand.PrintHelp(writer);
        var help = writer.ToString();
        Assert.Contains("scripts/test-mod multiplayer --scenario <id>", help, StringComparison.Ordinal);
        Assert.Contains("clow-defensive-powers", help, StringComparison.Ordinal);
        Assert.Contains("clow-silent-client", help, StringComparison.Ordinal);
        Assert.Contains("clow-shield-client", help, StringComparison.Ordinal);
        Assert.Contains("clow-shield-ward", help, StringComparison.Ordinal);
        Assert.Contains("sealed-wand-charge", help, StringComparison.Ordinal);
        Assert.Contains("turn-end-damage-sync", help, StringComparison.Ordinal);
        Assert.Contains("three-player-defensive-powers (3 peers)", help, StringComparison.Ordinal);
        Assert.Contains("three-player-repair-jump (3 peers)", help, StringComparison.Ordinal);
        Assert.Contains("three-player-repair-jump-load (3 peers)", help, StringComparison.Ordinal);
        Assert.Contains("three-player-mirror-copy (3 peers)", help, StringComparison.Ordinal);
    }
}
