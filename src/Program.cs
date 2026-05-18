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
    Console.WriteLine("Please ensure 'gcloud' is in your PATH or install it: https://docs.cloud.google.com/sdk/docs/install-sdk#linux");
    return 1;
}

if (!ShellHelper.IsCommandAvailable("git"))
{
    Console.WriteLine("Please ensure 'git' is in your PATH or install it: https://git-scm.com/install/linux");
    return 1;
}
#endregion

#region graphdb-skill Repo Checks
var graphDbRepoPath = Path.Combine(workingDirectory, "graphdb");
if (!Directory.Exists(graphDbRepoPath))
{
    Console.WriteLine("Pulling GraphDB repo from GitHub...");
    ShellHelper.RunCommand("git", $"clone -q https://github.com/jjdelorme/graphdb-skill.git {graphDbRepoPath}");
}

var graphDbBinaryFolderPath = Path.Combine(graphDbRepoPath, ".gemini", "skills", "graphdb", "scripts");
var graphDbBinaryFilePath = Path.Combine(graphDbBinaryFolderPath, "graphdb");
if (!File.Exists(graphDbBinaryFilePath))
{
    Console.WriteLine($"Downloading latest graphdb release to '{graphDbBinaryFilePath}'...");
    Directory.CreateDirectory(graphDbBinaryFolderPath);
    FileHelper.DownloadFile(graphDbBinaryFilePath, "https://github.com/jjdelorme/graphdb-skill/releases/latest/download/graphdb");
    ShellHelper.RunCommand("chmod", $"+x {graphDbBinaryFilePath}");
}
#endregion

var graphDb = new GraphDb(new GraphDbOptions
{
    WorkingDirectory = workingDirectory,
    RepoPath = repoPath,
    BinaryPath = graphDbBinaryFilePath
});

File.Copy(envPath, Path.Combine(graphDbRepoPath, ".env"), true);
try
{
    graphDb.StartNeo4jContainer(graphDbRepoPath);
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while starting up the neo4j container:");
    Console.WriteLine(e);
    return 1;
}

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
    Console.WriteLine($"An error occurred while importing the JSONL file to neo4j: {jsonlOutputPath}");
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
    if (File.Exists(jsonlOutputPath))
    {
        File.Delete(jsonlOutputPath);
    }

    File.Delete(graphDbBinaryFilePath);
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