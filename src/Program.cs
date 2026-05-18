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
    Console.WriteLine($"Downloading graphdb release v1.8.0 to '{graphDbBinaryFilePath}'...");
    Directory.CreateDirectory(graphDbBinaryFolderPath);
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
}
catch (Exception e)
{
    Console.WriteLine("An error occurred while starting up the neo4j container:");
    Console.WriteLine(e);
    return 1;
}

try
{
    Environment.SetEnvironmentVariable("GRAPHDB_DIR", repoPath);
    var buildAll = ShellHelper.RunCommand(graphDbBinaryFilePath, "build-all");
    if (!buildAll.Success)
    {
        throw new Exception(buildAll.StdErr);
    }
}
catch (Exception e)
{
    Console.WriteLine("The graphdb build-all process failed:");
    Console.WriteLine(e);
    return 1;
}

Console.WriteLine("The graphdb build-all process completed successfully.");
return 0;