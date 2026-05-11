using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CodebaseIngestion
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // -------------------------------
                // --- Check script parameters ---
                // -------------------------------

                if (args.Length != 2)
                {
                    Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <repo path> <.env path>");
                    return 1;
                }

                string repoPath = Path.GetFullPath(args[0]);
                if (!Directory.Exists(repoPath))
                {
                    Console.WriteLine($"Path '{repoPath}' does not exist");
                    return 1;
                }

                string envFilePath = Path.GetFullPath(args[1]);
                if (!File.Exists(envFilePath))
                {
                    Console.WriteLine($"File '{envFilePath}' does not exist");
                    return 1;
                }

                // ---------------------------------
                // --- Check script dependencies ---
                // ---------------------------------

                if (!IsCommandAvailable("docker"))
                {
                    Console.WriteLine("Please install Docker: https://docs.docker.com/engine/install/");
                    return 1;
                }

                if (!CanRunDocker())
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Console.WriteLine("Please ensure Docker Desktop is running and you have permission to use it.");
                    }
                    else
                    {
                        Console.WriteLine("Please add your user to the docker group: 'sudo usermod -aG docker $USER'");
                        Console.WriteLine("Then, log back into the shell.");
                    }
                    return 1;
                }

                // -------------------------
                // --- Check environment ---
                // -------------------------

                string graphDbRepoPath = Path.GetFullPath("./graphdb");
                if (!Directory.Exists(graphDbRepoPath))
                {
                    Console.WriteLine("GraphDB repo not found.");
                    RunCommand("git", $"clone -q https://github.com/jjdelorme/graphdb-skill.git {graphDbRepoPath}");
                }

                string graphDbBinaryFolderPath = Path.Combine(graphDbRepoPath, ".gemini/skills/graphdb/scripts");
                string graphDbBinaryFilePath = Path.Combine(graphDbBinaryFolderPath, "graphdb");
                
                if (!File.Exists(graphDbBinaryFilePath))
                {
                    Console.WriteLine($"Downloading latest graphdb release to '{graphDbBinaryFilePath}'...");
                    Directory.CreateDirectory(graphDbBinaryFolderPath);
                    
                    DownloadFile(
                        "https://github.com/jjdelorme/graphdb-skill/releases/latest/download/graphdb",
                        graphDbBinaryFilePath
                    );
                    
                    // Make executable on Unix systems
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        RunCommand("chmod", $"+x {graphDbBinaryFilePath}");
                    }
                }

                // Neo4j container
                string neo4jScriptsFolderPath = Path.Combine(graphDbRepoPath, ".gemini/skills/neo4j-manager/scripts");
                string neo4jStartupScriptPath = Path.Combine(neo4jScriptsFolderPath, "start_neo4j_container.sh");
                string neo4jEnvPath = Path.Combine(neo4jScriptsFolderPath, ".env");

                File.Copy(envFilePath, neo4jEnvPath, overwrite: true);

                if (!IsNeo4jRunning())
                {
                    Console.WriteLine("Starting Neo4j container...");
                    
                    // Replace podman with docker in startup script
                    string scriptContent = File.ReadAllText(neo4jStartupScriptPath);
                    scriptContent = scriptContent.Replace("podman", "docker");
                    File.WriteAllText(neo4jStartupScriptPath, scriptContent);

                    string currentDir = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(neo4jScriptsFolderPath);
                    RunCommand("bash", neo4jStartupScriptPath);
                    Directory.SetCurrentDirectory(currentDir);
                }

                // --------------------------
                // --- Phase 1: Ingestion ---
                // --------------------------
                // TODO: Check if ingestion is done by checking existence of graph.jsonl file.
                // TODO: If it doesn't exist, check the graph for the existence of the GraphState node

                Console.WriteLine($"Ingesting codebase '{repoPath}'...");
                RunCommand(graphDbBinaryFilePath, $"ingest -dir {repoPath}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static bool IsCommandAvailable(string command)
        {
            try
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

                using var process = Process.Start(processStartInfo);
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        static bool CanRunDocker()
        {
            try
            {
                var result = RunCommand("docker", "ps", throwOnError: false);
                return result.exitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        static bool IsNeo4jRunning()
        {
            try
            {
                var result = RunCommand("docker", "ps", throwOnError: false);
                return result.output.Contains("neo4j");
            }
            catch
            {
                return false;
            }
        }

        static (int exitCode, string output) RunCommand(string command, string arguments, bool throwOnError = true)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (throwOnError && process.ExitCode != 0)
            {
                throw new Exception($"Command '{command} {arguments}' failed with exit code {process.ExitCode}: {error}");
            }

            return (process.ExitCode, output + error);
        }

        static void DownloadFile(string url, string destinationPath)
        {
            using var client = new System.Net.Http.HttpClient();
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            
            using var fileStream = File.Create(destinationPath);
            response.Content.CopyToAsync(fileStream).Wait();
        }
    }
}