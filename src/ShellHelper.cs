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
        Console.WriteLine($"Running command: {command} {string.Join(" ", args)}");
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.EnableRaisingEvents = false;
        process.StartInfo = processStartInfo;

        StdOut.Clear();
        StdErr.Clear();
        process.OutputDataReceived += LogStdOut;
        process.ErrorDataReceived += LogStdErr;
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new CommandOutput
        {
            ExitCode = process.ExitCode,
            StandardOutput = string.Join('\n', StdOut),
            StandardError = string.Join('\n', StdErr)
        };
    }

    public static bool IsNeo4jRunning()
    {
        var output = RunCommand("docker", "ps");
        return output.StandardOutput.Contains("neo4j");
    }

    private static List<string> StdOut = [];
    private static DataReceivedEventHandler LogStdOut = (_, e) =>
    {
        if (e.Data is null) return;
        
        Console.WriteLine($"stdout: {e.Data}");
        StdOut.Add(e.Data);
    };
    
    private static List<string> StdErr = [];
    private static DataReceivedEventHandler LogStdErr = (_, e) =>
    {
        if (e.Data is null) return;
        
        Console.WriteLine($"stderr: {e.Data}");
        StdErr.Add(e.Data);
    };
}

public record CommandOutput
{
    public required int ExitCode { get; init; }
    public bool Success => ExitCode == 0;
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}