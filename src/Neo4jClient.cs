using Neo4j.Driver;

namespace GraphDbSkillAutomation;

public class Neo4jClient
{
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