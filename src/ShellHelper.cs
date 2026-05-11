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
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CommandOutput
        {
            ExitCode = process.ExitCode,
            StandardOutput = output,
            StandardError = error
        };
    }

    public static bool IsNeo4jRunning()
    {
        var output = RunCommand("docker", "ps");
        return output.StandardOutput.Contains("neo4j");
    }
}

public record CommandOutput
{
    public required int ExitCode { get; init; }
    public bool Success => ExitCode == 0;
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}