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
if (args.Length > 0 && (args[0] == "--index" || args[0] == "--reset"))
{
    return await RunCliAsync(args);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(lb =>
    lb.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));

RegisterEmbedder(builder.Services, builder.Configuration);
builder.Services.AddSingleton(sp => new SqliteVectorStore(ResolveDbPath(builder.Configuration)));
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
    services.AddSingleton(sp => new SqliteVectorStore(ResolveDbPath(cfg)));
    services.AddSingleton<DelphiIndexer>();
    var sp = services.BuildServiceProvider();

    string mode = args[0];
    string? library = GetArg(args, "--library");
    string? version = GetArg(args, "--version");
    string? path = GetArg(args, "--path");

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
    await indexer.IndexAsync(library, version, path, embedder);
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
