using System.Diagnostics;

namespace SakuraMod.TestRunner;

public sealed record ProcessResult(int ExitCode, TimeSpan Duration);

public static class ProcessRunner
{
    public static async Task<int> RunInteractiveAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        using var process = CreateProcess(executable, arguments, workingDirectory, redirectOutput: false);
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    public static async Task<ProcessResult> RunLoggedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string logPath,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default,
        bool mirrorOutput = true)
    {
        var started = Stopwatch.StartNew();
        await using var log = new StreamWriter(new FileStream(
            logPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true));
        using var process = CreateProcess(executable, arguments, workingDirectory, redirectOutput: true, environment);
        process.OutputDataReceived += (_, eventArgs) => WriteLine(eventArgs.Data, Console.Out, log, mirrorOutput);
        process.ErrorDataReceived += (_, eventArgs) => WriteLine(eventArgs.Data, Console.Error, log, mirrorOutput);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }

        await log.FlushAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, started.Elapsed);
    }

    private static Process CreateProcess(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool redirectOutput,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        return new Process { StartInfo = startInfo };
    }

    private static void WriteLine(string? value, TextWriter console, StreamWriter log, bool mirrorOutput)
    {
        if (value is null)
        {
            return;
        }

        if (mirrorOutput)
        {
            console.WriteLine(value);
        }
        lock (log)
        {
            log.WriteLine(value);
        }
    }
}
