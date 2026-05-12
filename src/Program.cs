using GraphDbSkillAutomation;

// ------------------------
// --- Check parameters ---
// ------------------------

if (args.Length != 2)
{
    Console.WriteLine($"Usage: {Environment.ProcessPath} <repo path> <.env path>");
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

// -------------------------
// --- Check environment ---
// -------------------------

// TODO this should probably be independent of the repo path
var graphDbRepoPath = Path.GetFullPath(Path.Combine(repoPath, "../graphdb"));
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
var neo4jStartupScriptPath = Path.Combine(neo4jScriptsFolderPath, "start_neo4j_container.sh");
var neo4jEnvPath = Path.Combine(neo4jScriptsFolderPath, ".env");
File.Copy(envPath, neo4jEnvPath, true);

if (!ShellHelper.IsNeo4jRunning())
{
    Console.WriteLine("Starting Neo4j container...");
                    
    var scriptContent = File.ReadAllText(neo4jStartupScriptPath);
    File.WriteAllText(neo4jStartupScriptPath, scriptContent.Replace("podman", "docker"));

    var currentDir = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(neo4jScriptsFolderPath);
    ShellHelper.RunCommand("bash", neo4jStartupScriptPath);
    Directory.SetCurrentDirectory(currentDir);
}

return 0;