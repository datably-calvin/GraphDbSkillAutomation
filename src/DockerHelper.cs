namespace GraphDbSkillAutomation;

public class DockerHelper
{
    public static bool IsContainerRunning(string name)
    {
        var output = ShellHelper.RunCommand("docker", "ps");
        return output.StdOut.Contains(name);
    }

    public static void StartNeo4jContainer(string workingDirectory)
    {
        var credentials = Neo4jCredentials.CreateFromEnvironment;
        
        var result = ShellHelper.RunCommand(
            "docker", "run -d",
                "--name neo4j",
                "-p 7474:7474",
                "-p 7687:7687",
                $"-v {Path.Combine(workingDirectory, "neo4j-data")}:/data",
                $"--env NEO4J_AUTH={credentials.Username}/{credentials.Password}",
                "neo4j:2026.04@sha256:17073aaf68a7a48332ebd8de2e9de9833ba58f8301542b8a0b7d27a861a0c2dc");

        if (!result.Success)
        {
            throw new Exception(result.StdErr);
        }

        if (!Neo4jClient.CreateFromEnvironment.IsHealthy(12, 5))
        {
            throw new Exception("Neo4j container did not start up.");
        }
    }
}