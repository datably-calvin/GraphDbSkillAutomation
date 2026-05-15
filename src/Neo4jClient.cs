using Neo4j.Driver;

namespace GraphDbSkillAutomation;

public class Neo4jClient
{
    public static Neo4jClient CreateFromEnvironment => new(new Neo4jCredentials
    {
        BoltUrl = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "",
        Username = Environment.GetEnvironmentVariable("NEO4J_USER") ?? "",
        Password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? ""
    });
    
    private readonly IDriver _client;
    
    public Neo4jClient(Neo4jCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.BoltUrl)
            || string.IsNullOrWhiteSpace(credentials.Username)
            || string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new Exception($"Environment variables are not set:\n" +
                                $"NEO4J_URI: {credentials.BoltUrl}\n" +
                                $"NEO4J_USER: {credentials.Username}\n" +
                                $"NEO4J_PASSWORD: {credentials.Password}\n");
        }
        
        _client = GraphDatabase.Driver(
            credentials.BoltUrl,
            AuthTokens.Basic(credentials.Username, credentials.Password));
    }

    public bool IsHealthy(int healthCheckCount, int waitTimeSeconds)
    {
        for (int i = 0; i <= healthCheckCount; i++)
        {
            Console.WriteLine("Waiting for neo4j...");
            try
            {
                QueryInt("RETURN 1");
                break;
            }
            catch (Exception e)
            {
                if (i < healthCheckCount)
                {
                    Console.WriteLine($"Could not connect to neo4j. Trying again in {waitTimeSeconds} seconds...");
                    Thread.Sleep(waitTimeSeconds * 1000);
                    continue;
                }
        
                Console.WriteLine("Could not connect to neo4j.");
                Console.WriteLine($"Exception: {e}");
                return false;
            }
        }

        return true;
    }

    public string? GetCurrentCommit()
    {
        var session = _client.AsyncSession();
        var cursor = session.RunAsync("MATCH (s:GraphState) RETURN s.commit AS commit LIMIT 1").Result;
        var record = cursor.SingleAsync().Result;
        return record?[0].As<string>();
    }
    
    public int QueryInt(string query)
    {
        var session = _client.AsyncSession();
        var cursor = session.RunAsync(query).Result;
        var record = cursor.SingleAsync().Result;
        return record[0].As<int>();
    }
}

public record Neo4jCredentials
{
    public required string BoltUrl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}