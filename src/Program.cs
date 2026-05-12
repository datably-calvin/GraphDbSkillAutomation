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
if (!ShellHelper.IsNeo4jRunning())
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

#region Ingestion
// -------------------------
// --- Step 1: Ingestion ---
// -------------------------

var jsonlFilePath = Path.Combine(workingDirectory, "graph.jsonl");
Console.WriteLine($"Step 1: Ingesting the codebase into a JSONL file -> {jsonlFilePath}");
// TODO check if this step already ran using the graph database
// TODO if the file already exists, maybe use the -since-commit flag?
if (!File.Exists(jsonlFilePath))
{
    ShellHelper.RunCommand(graphDbBinaryFilePath, "ingest -dir", repoPath, "-output", jsonlFilePath);
}
Console.WriteLine("Ingestion complete.");

// Usage of ingest:
// -dir string
//     Directory to walk (ignored if -file-list is used) (default ".")
// -edges string
//     Output file path for edges
// -file-list string
//     Path to a file containing a list of files to process
// -nodes string
//     Output file path for nodes
// -output string
//     Output file path (combined) (default "graph.jsonl")
// -since-commit string
//     Commit hash for incremental ingestion (skips JSONL, writes to DB)
// -workers int
//     Number of workers (default 4)
#endregion

#region Import JSONL

Console.WriteLine("Step 2: Importing JSONL into the database...");
if (!File.Exists(jsonlFilePath))
{
    throw new Exception($"Unable to find graph data file: {jsonlFilePath}");
}

ShellHelper.RunCommand(graphDbBinaryFilePath, "import -input", jsonlFilePath);
Console.WriteLine("Import complete.");

// Usage of import:
// -batch-size int
//     Batch size for insertion (default 500)
// -edges string
//     Path to edges JSONL file
// -input string
//     Path to combined JSONL file (nodes + edges)
// -nodes string
//     Path to nodes JSONL file
#endregion

// #region Enrich Features
//
// Console.WriteLine("Step 3: Feature enrichment...");
// ShellHelper.RunCommand(graphDbBinaryFilePath, "enrich-features -dir", repoPath);
// Console.WriteLine("Feature enrichment complete.");
//
// Usage of enrich-features:
// -app-context string
//       Optional path to an OVERVIEW.md or context preamble file
// -batch-size int
//       Batch size for LLM feature extraction (default 20)
// -dir string
//       Directory to analyze (default ".")
// -embed-batch-size int
//       Batch size for embedding generation (default 100)
// -llm-concurrency int
//       Number of concurrent LLM requests during extraction/summarization (default 5)
// -seed int
//       Seed for deterministic K-Means clustering (default 42)
// #endregion

return 0;