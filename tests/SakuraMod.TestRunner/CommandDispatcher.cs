namespace SakuraMod.TestRunner;

public static class CommandDispatcher
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp(Console.Error);
            return 2;
        }

        if (IsHelp(args[0]))
        {
            PrintHelp(Console.Out);
            return 0;
        }

        var command = args[0];
        var commandArgs = args[1..];
        if (!IsKnownCommand(command))
            return UnknownCommand(command);

        if (commandArgs.Length == 1 && IsHelp(commandArgs[0]))
            return PrintCommandHelp(command, Console.Out);

        if (command is not ("combat" or "multiplayer") && commandArgs.Length > 0)
            return UnexpectedArguments(command);

        var repoRoot = RepoPaths.FindRoot();
        return command switch
        {
            "fast" => await ProcessRunner.RunInteractiveAsync(
                "dotnet",
                ["test", Path.Combine(repoRoot, "SakuraMod.sln"), "--nologo"],
                repoRoot),
            "package" => await PackageCommand.RunAsync(repoRoot),
            "runtime" => await RuntimeCommand.RunAsync(repoRoot),
            "combat" => await CombatCommand.RunAsync(repoRoot, commandArgs),
            "multiplayer" => await MultiplayerCommand.RunAsync(repoRoot, commandArgs),
            "preflight" => await PreflightCommand.RunAsync(repoRoot),
            "protocol-self-test" => await ProtocolSelfTestCommand.RunAsync(repoRoot),
            _ => UnknownCommand(command)
        };
    }

    private static bool IsHelp(string arg) => arg is "--help" or "-h" or "help";

    private static bool IsKnownCommand(string command) =>
        command is "fast" or "package" or "runtime" or "combat" or "multiplayer" or "preflight" or "protocol-self-test";

    private static int PrintCommandHelp(string command, TextWriter writer)
    {
        switch (command)
        {
            case "fast":
                writer.WriteLine("Usage: scripts/test-mod fast");
                return 0;
            case "package":
                writer.WriteLine("Usage: scripts/test-mod package");
                return 0;
            case "runtime":
                writer.WriteLine("Usage: scripts/test-mod runtime");
                writer.WriteLine("Runs the runtime smoke test. Use 'combat --scenario <id>' for a specific combat scenario.");
                return 0;
            case "combat":
                CombatCommand.PrintHelp(writer);
                return 0;
            case "multiplayer":
                MultiplayerCommand.PrintHelp(writer);
                return 0;
            case "preflight":
                writer.WriteLine("Usage: scripts/test-mod preflight");
                return 0;
            case "protocol-self-test":
                writer.WriteLine("Usage: scripts/test-mod protocol-self-test");
                return 0;
            default:
                return UnknownCommand(command);
        }
    }

    private static int UnexpectedArguments(string command)
    {
        Console.Error.WriteLine($"Unexpected arguments for test layer '{command}'.");
        PrintCommandHelp(command, Console.Error);
        return 2;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown test layer: {command}");
        PrintHelp(Console.Error);
        return 2;
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  scripts/test-mod fast");
        writer.WriteLine("  scripts/test-mod package");
        writer.WriteLine("  scripts/test-mod runtime");
        writer.WriteLine("  scripts/test-mod combat [--scenario <id>]");
        writer.WriteLine("  scripts/test-mod multiplayer --scenario <id>");
        writer.WriteLine("  scripts/test-mod preflight");
        writer.WriteLine("  scripts/test-mod protocol-self-test");
        writer.WriteLine();
        writer.WriteLine("Run 'scripts/test-mod <layer> --help' for layer-specific help.");
    }
}
