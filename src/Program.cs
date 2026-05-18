using dotenv.net;
using GraphDbSkillAutomation;

#region Parameter Checks
// ------------------------
// --- Check parameters ---
// ------------------------

if (args.Length != 3)
{
    Console.WriteLine($"Usage: {Environment.ProcessPath} <repo path> <.env path> <working directory>");
    return 1;
}

var repoPath = FileHelper.GetAbsolutePath(args[0]);
if (!Directory.Exists(repoPath))
{
    Console.WriteLine($"Path '{repoPath}' does not exist");
    return 1;
}

var envPath = FileHelper.GetAbsolutePath(args[1]);
if (!File.Exists(envPath))
{
    Console.WriteLine($"File '{envPath}' does not exist");
    return 1;
}
DotEnv.Fluent()
    .WithEnvFiles(envPath)
    .Load();


var workingDirectory = FileHelper.GetAbsolutePath(args[2]);
if (!Directory.Exists(workingDirectory))
{
    Console.WriteLine($"Path '{workingDirectory}' does not exist");
    return 1;
}
#endregion

#region Dependency Check
// --------------------------
// --- Check dependencies ---
// --------------------------
if (!ShellHelper.IsCommandAvailable("docker"))
{
    Console.WriteLine("Please install Docker: https://docs.docker.com/engine/install/");
    return 1;
}

if (!ShellHelper.RunCommand("docker", "ps").Success)
{
    Console.WriteLine("Please add your user to the docker group: 'sudo usermod -aG docker $USER'");
    Console.WriteLine("Then, log back into the shell.");
    return 1;
}
#endregion

#region Environment Check
// -------------------------
// --- Check environment ---
// -------------------------

// TODO this should probably be independent of the repo path
var graphDbRepoPath = Path.Combine(workingDirectory, "graphdb");
if (!Directory.Exists(graphDbRepoPath))
{
    Console.WriteLine("Pulling GraphDB repo from GitHub...");
    ShellHelper.RunCommand("git", $"clone -q https://github.com/jjdelorme/graphdb-skill.git {graphDbRepoPath}");
}

var geminiSkillsFolder = Path.Combine(graphDbRepoPath, ".gemini", "skills");
var graphDbBinaryFolderPath = Path.Combine(geminiSkillsFolder, "graphdb", "scripts");

// TODO the GraphState node containing the git commit comes from whatever directory the binary is in
var graphDbBinaryFilePath = Path.Combine(graphDbBinaryFolderPath, "graphdb");
if (!File.Exists(graphDbBinaryFilePath))
{
    Console.WriteLine($"Downloading latest graphdb release to '{graphDbBinaryFilePath}'...");
    Directory.CreateDirectory(graphDbBinaryFolderPath);
    FileHelper.DownloadFile(graphDbBinaryFilePath, "https://github.com/jjdelorme/graphdb-skill/releases/latest/download/graphdb");
    ShellHelper.RunCommand("chmod", $"+x {graphDbBinaryFilePath}");
}

var neo4jScriptsFolderPath = Path.Combine(geminiSkillsFolder, "neo4j-manager", "scripts");
var neo4jEnvPath = Path.Combine(neo4jScriptsFolderPath, ".env");
File.Copy(envPath, neo4jEnvPath, true);
if (!ShellHelper.DoesNeo4jContainerExist())
{
    Console.WriteLine("Starting Neo4j container...");
                    
    var neo4jStartupScriptPath = Path.Combine(neo4jScriptsFolderPath, "start_neo4j_container.sh");
    var scriptContent = File.ReadAllText(neo4jStartupScriptPath);
    File.WriteAllText(neo4jStartupScriptPath, scriptContent.Replace("podman", "docker"));

    var currentDir = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(neo4jScriptsFolderPath);
    ShellHelper.RunCommand("bash", neo4jStartupScriptPath);
    Directory.SetCurrentDirectory(currentDir);
}
#endregion

var graphDb = new GraphDb(new GraphDbOptions
{
    WorkingDirectory = workingDirectory,
    RepoPath = repoPath,
    BinaryPath = graphDbBinaryFilePath
});

var jsonlOutputPath = Path.Combine(workingDirectory, "graph.jsonl");
try
{
    graphDb.Ingest(jsonlOutputPath);
}
catch (Exception e)
{
    Console.WriteLine($"An error occurred while ingesting the codebase: {repoPath}");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.Import(jsonlOutputPath);
}
catch (Exception e)
{
    Console.WriteLine($"An error occurred while importing the JSONL file to neo4j: {repoPath}");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichFeaturesAndEmbed();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching features and embedding.");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichContamination();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching contamination.");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichHistory();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching history.");
    Console.WriteLine(e);
    return 1;
}

try
{
    if (File.Exists(jsonlOutputPath))
    {
        File.Delete(jsonlOutputPath);
    }

    File.Delete(graphDbBinaryFilePath);
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while cleaning up the environment.");
    Console.WriteLine(e);
    Console.WriteLine("The graphdb process completed successfully.");
    return 1;
}

Console.WriteLine("The graphdb process completed successfully.");
return 0;