namespace GraphDbSkillAutomation;

public class GraphDb
{
    private readonly GraphDbOptions _options;
    
    public GraphDb(GraphDbOptions options)
    {
        _options = options;
    }
    
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
    public void Ingest(string jsonlOutputPath)
    {
        Console.WriteLine($"Step 1: Ingesting the codebase into a JSONL file -> '{jsonlOutputPath}'");
        // TODO check if this step already ran using the graph database
        // TODO if the file already exists, maybe use the -since-commit flag?
        if (File.Exists(jsonlOutputPath))
        {
            Console.WriteLine($"The JSONL file '{jsonlOutputPath}' already exists. Skipping ingestion.");
            return;
        }
        
        var result = ShellHelper.RunCommand(
            _options.BinaryPath,
            "ingest -dir", _options.RepoPath,
            "-output", jsonlOutputPath);
        
        if (!result.Success)
        {
            throw new Exception(result.StdErr);
        }
        
        Console.WriteLine("Ingestion complete.");
    }

    // Usage of import:
    // -batch-size int
    //     Batch size for insertion (default 500)
    // -edges string
    //     Path to edges JSONL file
    // -input string
    //     Path to combined JSONL file (nodes + edges)
    // -nodes string
    //     Path to nodes JSONL file
    public void Import(string jsonlOutputPath)
    {
        Console.WriteLine("Step 2: Importing graph JSONL data into the database...");
        if (!File.Exists(jsonlOutputPath))
        {
            throw new Exception($"Unable to find graph data file at '{jsonlOutputPath}'.");
        }

        var neo4jClient = Neo4jClient.CreateFromEnvironment;
        if (!neo4jClient.IsHealthy(6, 5))
        {
            throw new Exception("Could not connect to neo4j database.");
        }

        var databaseCommit = neo4jClient.GetCurrentCommit();
        if (databaseCommit is not null)
        {
            Console.WriteLine($"Database already has a node for commit {databaseCommit}.");
            Console.WriteLine("Incremental updates are not implemented yet. Skipping import.");
            return;
        }

        var result = ShellHelper.RunCommand(
            _options.BinaryPath,
            "import -input", jsonlOutputPath);
        
        if (!result.Success)
        {
            throw new Exception(result.StdErr);
        }
    }

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
    // TODO idempotence?
    public void EnrichFeaturesAndEmbed()
    {
        Console.WriteLine("Step 3: Feature enrichment and embedding...");
        GoogleCloudHelper.AssertValidADC();

        Directory.SetCurrentDirectory(_options.RepoPath);
        var result = ShellHelper.RunCommand(
            _options.BinaryPath, "enrich-features -dir", _options.RepoPath);
        
        if (!result.Success)
        {
            Directory.SetCurrentDirectory(_options.WorkingDirectory);
            throw new Exception(result.StdErr);
        }
        
        Directory.SetCurrentDirectory(_options.WorkingDirectory);
        Console.WriteLine("Feature enrichment and embedding complete.");
    }

    // This command takes no parameters
    // TODO idempotence?
    public void EnrichContamination()
    {
        Console.WriteLine("Step 4: Contamination enrichment...");
        
        var result = ShellHelper.RunCommand(_options.BinaryPath, "enrich-contamination");
        if (!result.Success)
        {
            throw new Exception(result.StdErr);
        }
        
        Console.WriteLine("Contamination enrichment complete.");
    }
    
    // Usage of enrich-history:
    // -dir string
    //     Directory to analyze (must be a git repository) (default ".")
    // -since string
    //     How far back to analyze history (default "1 year ago")
    // TODO idempotence
    public void EnrichHistory()
    {
        Console.WriteLine("Step 5: History enrichment...");

        var result = ShellHelper.RunCommand(
            _options.BinaryPath,
            "enrich-history -dir", _options.RepoPath);
        if (!result.Success)
        {
            throw new Exception(result.StdErr);
        }
        
        Console.WriteLine("History enrichment complete.");
    }
}

public record GraphDbOptions
{
    public required string WorkingDirectory { get; init; }
    public required string RepoPath { get; init; }
    public required string BinaryPath { get; init; }
}