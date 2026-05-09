using DelphiMcp;
using DelphiMcp.Embedder;
using DelphiMcp.Indexer;
using DelphiMcp.Search;
using DelphiMcp.VectorStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// CLI commands run synchronously then exit; default mode runs the MCP stdio server.
if (args.Length > 0 && (args[0] == "--index" || args[0] == "--reset" || args[0] == "--bench-search" || args[0] == "--compare-embedders" || args[0] == "--compare-detailed"))
{
    return await RunCliAsync(args);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(lb =>
    lb.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));

RegisterEmbedder(builder.Services, builder.Configuration);
builder.Services.AddSingleton(sp =>
{
    var dbPath = ResolveDbPath(builder.Configuration);
    var faissIndexDir = ResolveFaissIndexDir(builder.Configuration, dbPath);
    return new SqliteVectorStore(dbPath, faissIndexDir);
});
builder.Services.AddSingleton<DelphiIndexer>();
builder.Services.AddSingleton<DelphiSearcher>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

LogRegisteredTools();

await builder.Build().RunAsync();
return 0;

static async Task<int> RunCliAsync(string[] args)
{
    var cfg = BuildConfiguration();
    var services = new ServiceCollection();
    RegisterEmbedder(services, cfg);
    services.AddSingleton(sp =>
    {
        var dbPath = ResolveDbPath(cfg);
        var faissIndexDir = ResolveFaissIndexDir(cfg, dbPath);
        return new SqliteVectorStore(dbPath, faissIndexDir);
    });
    services.AddSingleton<DelphiIndexer>();
    var sp = services.BuildServiceProvider();

    string mode = args[0];
    string? library = GetArg(args, "--library");
    string? version = GetArg(args, "--version");
    string? path = GetArg(args, "--path");
    string? maxChunksStr = GetArg(args, "--max-chunks");
    int? maxChunks = maxChunksStr != null && int.TryParse(maxChunksStr, out int mc) ? mc : null;
    string? iterationsStr = GetArg(args, "--iterations");
    int iterations = iterationsStr != null && int.TryParse(iterationsStr, out int it) ? it : 20;
    string? topKStr = GetArg(args, "--top-k");
    int topK = topKStr != null && int.TryParse(topKStr, out int tk) ? tk : 10;

    if (string.IsNullOrEmpty(library))
    {
        Console.Error.WriteLine("Missing --library <rtl|devexpress>");
        return 1;
    }

    var store = sp.GetRequiredService<SqliteVectorStore>();

    if (mode == "--reset")
    {
        int n = store.DeleteLibrary(library, version);
        Console.Error.WriteLine(version is null
            ? $"Deleted {n} chunks for library '{library}' (all versions)."
            : $"Deleted {n} chunks for library '{library}' version '{version}'.");
        return 0;
    }

    if (mode == "--bench-search")
    {
        if (iterations < 1)
        {
            Console.Error.WriteLine("--iterations must be >= 1");
            return 1;
        }
        if (topK < 1)
        {
            Console.Error.WriteLine("--top-k must be >= 1");
            return 1;
        }

        var sample = store.GetSampleEmbedding(library, version);
        if (sample is null)
        {
            Console.Error.WriteLine(version is null
                ? $"No chunks found for library '{library}'."
                : $"No chunks found for library '{library}' version '{version}'.");
            return 0;
        }

        // Warmup
        await store.SearchAsync(library, version, sample, topK);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int totalHits = 0;
        for (int i = 0; i < iterations; i++)
        {
            var hits = await store.SearchAsync(library, version, sample, topK);
            totalHits += hits.Count;
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        Console.Error.WriteLine($"Benchmark complete: library={library} version={version ?? "*"} iterations={iterations} topK={topK} avgMs={avgMs:F2} totalMs={sw.Elapsed.TotalMilliseconds:F2} totalHits={totalHits}");
        return 0;
    }

    if (mode == "--compare-embedders")
    {
        string? version1 = GetArg(args, "--version1");
        string? version2 = GetArg(args, "--version2");
        string? provider1 = GetArg(args, "--provider1");
        string? provider2 = GetArg(args, "--provider2");
        if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
        {
            Console.Error.WriteLine("--compare-embedders requires --version1 and --version2");
            return 1;
        }
        if (topK < 1)
        {
            Console.Error.WriteLine("--top-k must be >= 1");
            return 1;
        }

        var sample1 = store.GetSampleEmbedding(library, version1);
        var sample2 = store.GetSampleEmbedding(library, version2);
        if (sample1 is null || sample2 is null)
        {
            Console.Error.WriteLine($"Missing indexed data for one or both versions: {version1}, {version2}");
            return 1;
        }

        var embedder1 = await ResolveComparisonEmbedderAsync(cfg, version1, sample1.Length, provider1);
        var embedder2 = await ResolveComparisonEmbedderAsync(cfg, version2, sample2.Length, provider2);

        var comparisonQueries = ComparisonQueries.GetTestQueries();
        Console.Error.WriteLine($"Comparing {library} {version1} vs {version2} using {comparisonQueries.Count} test queries (topK={topK})");
        Console.Error.WriteLine("---");

        int totalOverlap = 0;
        double totalTimeV1 = 0, totalTimeV2 = 0;
        int executedQueries = 0;

        foreach (var query in comparisonQueries)
        {
            var embeddings1 = await embedder1.EmbedBatchAsync(new List<string> { query });
            var embeddings2 = await embedder2.EmbedBatchAsync(new List<string> { query });
            if (embeddings1.Count == 0 || embeddings2.Count == 0)
                continue;
            var embedding1 = embeddings1[0];
            var embedding2 = embeddings2[0];

            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var hits1 = await store.SearchAsync(library, version1, embedding1, topK);
            sw1.Stop();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var hits2 = await store.SearchAsync(library, version2, embedding2, topK);
            sw2.Stop();

            totalTimeV1 += sw1.Elapsed.TotalMilliseconds;
            totalTimeV2 += sw2.Elapsed.TotalMilliseconds;
            executedQueries++;

            var ids1 = new HashSet<long>(hits1.Select(h => h.Id));
            var ids2 = new HashSet<long>(hits2.Select(h => h.Id));
            var overlap = ids1.Intersect(ids2).Count();
            totalOverlap += overlap;

            Console.Error.WriteLine($"Query: {query}");
            Console.Error.WriteLine($"  {version1}: {sw1.Elapsed.TotalMilliseconds:F1}ms, {version2}: {sw2.Elapsed.TotalMilliseconds:F1}ms, overlap: {overlap}/{topK}");
        }

        if (executedQueries == 0)
        {
            Console.Error.WriteLine("No comparison queries executed successfully.");
            return 1;
        }

        Console.Error.WriteLine("---");
        double avgTimeV1 = totalTimeV1 / executedQueries;
        double avgTimeV2 = totalTimeV2 / executedQueries;
        double avgOverlap = (double)totalOverlap / (executedQueries * topK) * 100;
        Console.Error.WriteLine($"Comparison summary:");
        Console.Error.WriteLine($"  {version1} avg query time: {avgTimeV1:F2}ms");
        Console.Error.WriteLine($"  {version2} avg query time: {avgTimeV2:F2}ms");
        Console.Error.WriteLine($"  Executed queries: {executedQueries}/{comparisonQueries.Count}");
        Console.Error.WriteLine($"  Avg top-K overlap: {avgOverlap:F1}%");
        return 0;
    }

    if (mode == "--compare-detailed")
    {
        string? version1 = GetArg(args, "--version1");
        string? version2 = GetArg(args, "--version2");
        string? provider1 = GetArg(args, "--provider1");
        string? provider2 = GetArg(args, "--provider2");
        if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
        {
            Console.Error.WriteLine("--compare-detailed requires --version1 and --version2");
            return 1;
        }
        if (topK < 1)
        {
            Console.Error.WriteLine("--top-k must be >= 1");
            return 1;
        }

        var sample1 = store.GetSampleEmbedding(library, version1);
        var sample2 = store.GetSampleEmbedding(library, version2);
        if (sample1 is null || sample2 is null)
        {
            Console.Error.WriteLine($"Missing indexed data for one or both versions: {version1}, {version2}");
            return 1;
        }

        var embedder1 = await ResolveComparisonEmbedderAsync(cfg, version1, sample1.Length, provider1);
        var embedder2 = await ResolveComparisonEmbedderAsync(cfg, version2, sample2.Length, provider2);

        var comparisonQueries = ComparisonQueries.GetTestQueries();
        Console.WriteLine($"# Embedder Comparison: {library} {version1} vs {version2}\n");
        Console.WriteLine($"Comparing {comparisonQueries.Count} test queries (topK={topK})\n");

        foreach (var query in comparisonQueries)
        {
            var embeddings1 = await embedder1.EmbedBatchAsync(new List<string> { query });
            var embeddings2 = await embedder2.EmbedBatchAsync(new List<string> { query });
            if (embeddings1.Count == 0 || embeddings2.Count == 0)
                continue;

            var hits1 = await store.SearchAsync(library, version1, embeddings1[0], topK);
            var hits2 = await store.SearchAsync(library, version2, embeddings2[0], topK);

            Console.WriteLine($"## Query: {query}\n");

            Console.WriteLine($"### {version1} Results:");
            for (int i = 0; i < hits1.Count; i++)
            {
                var h = hits1[i];
                Console.WriteLine($"**Rank {i + 1}** (dist={h.Distance:F4})");
                Console.WriteLine($"- Unit: `{h.UnitName}`");
                Console.WriteLine($"- Section: {h.Section}");
                Console.WriteLine($"- Identifier: `{h.Identifier}`");
                Console.WriteLine($"- Line: {h.StartLine}");
                Console.WriteLine($"- Content: {(h.Content.Length > 120 ? h.Content[..120] + "..." : h.Content)}");
                Console.WriteLine();
            }

            Console.WriteLine($"### {version2} Results:");
            for (int i = 0; i < hits2.Count; i++)
            {
                var h = hits2[i];
                Console.WriteLine($"**Rank {i + 1}** (dist={h.Distance:F4})");
                Console.WriteLine($"- Unit: `{h.UnitName}`");
                Console.WriteLine($"- Section: {h.Section}");
                Console.WriteLine($"- Identifier: `{h.Identifier}`");
                Console.WriteLine($"- Line: {h.StartLine}");
                Console.WriteLine($"- Content: {(h.Content.Length > 120 ? h.Content[..120] + "..." : h.Content)}");
                Console.WriteLine();
            }

            Console.WriteLine("---\n");
        }

        return 0;
    }

    // --index
    if (string.IsNullOrEmpty(version))
    {
        Console.Error.WriteLine("Missing --version <ver> (e.g. 12.0)");
        return 1;
    }
    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
    {
        Console.Error.WriteLine($"Missing or invalid --path; got '{path}'");
        return 1;
    }

    var indexer = sp.GetRequiredService<DelphiIndexer>();
    var embedder = sp.GetRequiredService<IEmbeddingService>();
    await indexer.IndexAsync(library, version, path, embedder, maxChunks);
    return 0;
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static IConfiguration BuildConfiguration() =>
    new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

static string ResolveDbPath(IConfiguration cfg)
{
    var configured = cfg["Storage:DbPath"];
    return string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(AppContext.BaseDirectory, "delphi-mcp.db")
        : configured;
}

static string ResolveFaissIndexDir(IConfiguration cfg, string dbPath)
{
    var configured = cfg["Storage:FaissIndexDir"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    var baseDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? AppContext.BaseDirectory;
    return Path.Combine(baseDir, "faiss-indexes");
}

static void RegisterEmbedder(IServiceCollection services, IConfiguration cfg)
{
    var provider = (cfg["Embedder:Provider"] ?? "OpenAI").Trim();

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = cfg["Ollama:Model"] ?? "nomic-embed-text";
        services.AddHttpClient(nameof(OllamaEmbeddingService), http =>
        {
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddSingleton<IEmbeddingService>(sp =>
            new OllamaEmbeddingService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OllamaEmbeddingService)),
                model));
        Console.Error.WriteLine($"Embedder: Ollama ({model}) at {baseUrl}");
    }
    else
    {
        var apiKey = cfg["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("WARNING: OpenAI API key not found, embedding will fail");
            apiKey = "MISSING_KEY";
        }
        services.AddSingleton<IEmbeddingService>(new OpenAiEmbeddingService(apiKey));
        Console.Error.WriteLine("Embedder: OpenAI (text-embedding-3-small)");
    }
}

static async Task<IEmbeddingService> ResolveComparisonEmbedderAsync(
    IConfiguration cfg,
    string version,
    int expectedDim,
    string? providerOverride)
{
    if (!string.IsNullOrWhiteSpace(providerOverride))
    {
        var forced = CreateEmbedder(providerOverride, cfg, $"comparison:{version}");
        await ValidateEmbedderDimensionAsync(forced, expectedDim, providerOverride, version);
        return forced;
    }

    var configured = (cfg["Embedder:Provider"] ?? "OpenAI").Trim();
    var candidates = new[] { configured, "Ollama", "OpenAI" }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    foreach (var candidate in candidates)
    {
        try
        {
            var embedder = CreateEmbedder(candidate, cfg, $"comparison:{version}");
            await ValidateEmbedderDimensionAsync(embedder, expectedDim, candidate, version);
            return embedder;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[compare] provider '{candidate}' rejected for version {version}: {ex.Message}");
        }
    }

    throw new InvalidOperationException(
        $"Unable to resolve embedder for version {version} with expected dimension {expectedDim}. " +
        "Use --provider1/--provider2 to force providers.");
}

static async Task ValidateEmbedderDimensionAsync(
    IEmbeddingService embedder,
    int expectedDim,
    string provider,
    string version)
{
    var probe = await embedder.EmbedBatchAsync(new List<string> { "dimension probe" });
    if (probe.Count == 0)
        throw new InvalidOperationException($"Provider '{provider}' returned no vectors for version {version}");

    int actualDim = probe[0].Length;
    if (actualDim != expectedDim)
    {
        throw new InvalidOperationException(
            $"Provider '{provider}' vector dimension {actualDim} does not match expected {expectedDim} for version {version}");
    }

    Console.Error.WriteLine($"[compare] provider '{provider}' selected for version {version} (dim={actualDim})");
}

static IEmbeddingService CreateEmbedder(string provider, IConfiguration cfg, string context)
{
    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = cfg["Ollama:Model"] ?? "nomic-embed-text";
        var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };
        Console.Error.WriteLine($"[compare] embedder ({context}): Ollama ({model}) at {baseUrl}");
        return new OllamaEmbeddingService(http, model);
    }

    if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        var apiKey = cfg["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key not found in configuration/environment");
        Console.Error.WriteLine($"[compare] embedder ({context}): OpenAI (text-embedding-3-small)");
        return new OpenAiEmbeddingService(apiKey);
    }

    throw new InvalidOperationException($"Unknown provider '{provider}'. Expected 'Ollama' or 'OpenAI'.");
}

static void LogRegisteredTools()
{
    var toolTypes = System.Reflection.Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .ToList();

    Console.Error.WriteLine($"Found {toolTypes.Count} tool type(s):");
    foreach (var tt in toolTypes)
    {
        var methods = tt.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0);
        foreach (var m in methods)
            Console.Error.WriteLine($"  Tool: {tt.Name}.{m.Name}");
    }
}
