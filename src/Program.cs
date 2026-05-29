using dotenv.net;
using GraphDbSkillAutomation;

#region Parameter Checks
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

DotEnv.Load(new DotEnvOptions(
    envFilePaths: [envPath]));

var workingDirectory = FileHelper.GetAbsolutePath(args[2]);
if (!Directory.Exists(workingDirectory))
{
    Directory.CreateDirectory(workingDirectory);
}
#endregion

#region Dependency Checks
if (!ShellHelper.IsCommandAvailable("docker"))
{
    Console.WriteLine("Please ensure 'docker' is in your PATH or install it: https://docs.docker.com/engine/install/");
    return 1;
}

if (!ShellHelper.RunCommand("docker", "ps").Success)
{
    Console.WriteLine("Please add your user to the docker group: 'sudo usermod -aG docker $USER'");
    Console.WriteLine("Then, log back into the shell.");
    return 1;
}

if (!ShellHelper.IsCommandAvailable("gcloud"))
{
    Console.WriteLine("Please ensure 'gcloud' is in your PATH or install it: https://docs.cloud.google.com/sdk/docs/install-sdk");
    return 1;
}

if (!ShellHelper.IsCommandAvailable("git"))
{
    Console.WriteLine("Please ensure 'git' is in your PATH or install it: https://git-scm.com/install/");
    return 1;
}

var graphDbBinaryFilePath = Path.Combine(workingDirectory, "graphdb");
if (!File.Exists(graphDbBinaryFilePath))
{
    Console.WriteLine($"Downloading graphdb release v1.8.0 to '{graphDbBinaryFilePath}'...");
    FileHelper.DownloadFile(graphDbBinaryFilePath, "https://github.com/jjdelorme/graphdb-skill/releases/download/v1.8.0/graphdb");
    ShellHelper.RunCommand("chmod", $"+x {graphDbBinaryFilePath}");
}
#endregion

try
{
    if (!DockerHelper.IsContainerRunning("neo4j"))
    {
        DockerHelper.StartNeo4jContainer(workingDirectory);
    }
    
    var (count, wait) = (12, 5);
    if (!Neo4jClient.CreateFromEnvironment.IsHealthy(count, wait))
    {
        throw new Exception($"Neo4j container did not become healthy within {count * wait} seconds");
    }
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while starting up the neo4j container:");
    Console.WriteLine(e);
    return 1;
}

var graphDb = new GraphDbClient(new GraphDbOptions
{
    WorkingDirectory = workingDirectory,
    RepoPath = repoPath,
    BinaryPath = graphDbBinaryFilePath
});

var nodesJsonl = Path.Combine(workingDirectory, "nodes.jsonl");
var edgesJsonl = Path.Combine(workingDirectory, "edges.jsonl");
try
{
    graphDb.Ingest(nodesJsonl, edgesJsonl);
}
catch (Exception e)
{
    Console.WriteLine($"An error occurred while ingesting the codebase: {repoPath}");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.Import(nodesJsonl, edgesJsonl);
}
catch (Exception e)
{
    Console.WriteLine($"An error occurred while importing JSONL files to neo4j. Nodes: {nodesJsonl} Edges: {edgesJsonl}");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichFeaturesAndEmbed();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching features and embedding:");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichHistory();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching history:");
    Console.WriteLine(e);
    return 1;
}

try
{
    graphDb.EnrichContamination();
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while enriching contamination:");
    Console.WriteLine(e);
    return 1;
}

try
{
    FileHelper.DeleteFileIfExists(nodesJsonl);
    FileHelper.DeleteFileIfExists(edgesJsonl);
    FileHelper.DeleteFileIfExists(graphDbBinaryFilePath);
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while cleaning up the file system:");
    Console.WriteLine(e);
    Console.WriteLine("The graphdb process completed successfully.");
    return 1;
}

Console.WriteLine("The graphdb process completed successfully.");
return 0;