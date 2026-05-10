using DelphiMcp.Chunker;
using DelphiMcp.Embedder;
using DelphiMcp.Search;
using DelphiMcp.Tools;
using DelphiMcp.VectorStore;
namespace DelphiMcp.Tests;

public class DevExpressToolsTests
{
    private const string Library = "devexpress";
    private const string Version = "25.2.6";

    [Fact]
    public async Task LookupDevExpressClass_ReturnsDeclarationForKnownClass()
    {
        using var store = CreateSeededStore();
        var searcher = new DelphiSearcher(store, new ThrowingEmbeddingService());

        var result = await DevExpressTools.LookupDevExpressClass("TcxGrid", searcher, Version);

        Assert.Contains("TcxGrid", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type declaration", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No relevant devexpress source found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchDevExpress_ReturnsResultsForIndexedDataset()
    {
        using var store = CreateSeededStore();
        var sample = store.GetSampleEmbedding(Library, Version)
            ?? throw new InvalidOperationException("Sample embedding not found in seeded store.");

        var searcher = new DelphiSearcher(store, new FixedEmbeddingService(sample));

        var result = await DevExpressTools.SearchDevExpress("grid focused row", searcher, topK: 3, version: Version);

        Assert.Contains("Found", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("devexpress", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TcxGrid", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No relevant devexpress source found", result, StringComparison.OrdinalIgnoreCase);
    }

    private static SqliteVectorStore CreateSeededStore()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"delphi-mcp-tests-{Guid.NewGuid():N}.db");
        var store = new SqliteVectorStore(dbPath, faissIndexDir: null);

        var chunks = new List<DelphiChunk>
        {
            new(
                Library: Library,
                Version: Version,
                UnitName: "cxGrid",
                FilePath: "cxGrid.pas",
                Section: "interface",
                ChunkType: "type",
                Identifier: "TcxGrid",
                Content: "TcxGrid = class(TcxCustomGrid)\npublic\n  property FocusedRow: Integer read GetFocusedRow write SetFocusedRow;\nend;",
                StartLine: 120,
                Visibility: "public"
            ),
            new(
                Library: Library,
                Version: Version,
                UnitName: "cxGridTableView",
                FilePath: "cxGridTableView.pas",
                Section: "implementation",
                ChunkType: "routine",
                Identifier: "TcxGridTableView.ApplyFocusedRow",
                Content: "procedure TcxGridTableView.ApplyFocusedRow;\nbegin\n  // focuses selected row in the table view\nend;",
                StartLine: 410,
                Visibility: "public"
            )
        };

        var embeddings = new List<float[]>
        {
            new float[] { 1f, 0f, 0f },
            new float[] { 0.8f, 0.2f, 0f }
        };

        store.AddChunks(chunks, embeddings);

        return store;
    }

    private sealed class FixedEmbeddingService(float[] embedding) : IEmbeddingService
    {
        public Task<List<float[]>> EmbedBatchAsync(List<string> texts)
            => Task.FromResult(new List<float[]> { embedding });
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public Task<List<float[]>> EmbedBatchAsync(List<string> texts)
            => throw new InvalidOperationException("Embedder should not be called for exact class lookup tests.");
    }
}
