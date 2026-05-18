using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GraphDbSkillAutomation;

public static class ShellHelper
{
    public static bool IsCommandAvailable(string command)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;
        
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public static CommandOutput RunCommand(string command, params string[] args)
    {
        return RunCommand(new CommandParams
        {
            Command = command,
            Args = string.Join(" ", args),
            OutputToConsole = true
        });
    }

    public static CommandOutput RunCommand(CommandParams commandParams)
    {
        Console.WriteLine($"Running command: {commandParams.Command} {commandParams.Args}");
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = commandParams.Command,
            Arguments = commandParams.Args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.EnableRaisingEvents = false;
        process.StartInfo = processStartInfo;

        StdOut.Clear();
        StdErr.Clear();
        process.OutputDataReceived += LogStdOut(commandParams.OutputToConsole);
        process.ErrorDataReceived += LogStdErr(commandParams.OutputToConsole);
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new CommandOutput
        {
            ExitCode = process.ExitCode,
            StdOut = string.Join('\n', StdOut),
            StdErr = string.Join('\n', StdErr)
        };
    }

    public static bool DoesNeo4jContainerExist()
    {
        var output = RunCommand("docker", "ps");
        return output.StdOut.Contains("neo4j");
    }

    private static List<string> StdOut = [];
    private static DataReceivedEventHandler LogStdOut(bool outputToConsole) => (_, e) =>
    {
        if (e.Data is null) return;
        if (outputToConsole) Console.WriteLine($"stdout: {e.Data}");
        StdOut.Add(e.Data);
    };
    
    private static List<string> StdErr = [];
    private static DataReceivedEventHandler LogStdErr(bool outputToConsole) => (_, e) =>
    {
        if (e.Data is null) return;
        if (outputToConsole) Console.WriteLine($"stderr: {e.Data}");
        StdErr.Add(e.Data);
    };
}

public record CommandParams
{
    public required string Command { get; init; }
    public required string Args { get; init; }
    public required bool OutputToConsole { get; init; }
}

public record CommandOutput
{
    public required int ExitCode { get; init; }
    public bool Success => ExitCode == 0;
    public required string StdOut { get; init; }
    public required string StdErr { get; init; }
}