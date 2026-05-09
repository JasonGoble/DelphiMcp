using DelphiMcp.Chunker;
using DelphiMcp.Embedder;
using DelphiMcp.VectorStore;
using System.Diagnostics;

namespace DelphiMcp.Indexer;

public class DelphiIndexer(SqliteVectorStore store)
{
    private readonly SqliteVectorStore _store = store;
    private const int BatchSize = 25;

    public async Task IndexAsync(string library, string version, string rootPath, IEmbeddingService embedder, int? maxChunks = null)
    {
        var totalSw = Stopwatch.StartNew();
        int existing = _store.CountChunks(library, version);
        Console.Error.WriteLine($"[{DateTime.UtcNow:O}] Resuming from {existing} existing chunks for {library} {version}...");
        if (maxChunks.HasValue)
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] Limiting to {maxChunks} chunks (test mode)");

        int total = existing;
        int skipped = 0;
        var batch = new List<DelphiChunk>();

        foreach (var chunk in DelphiChunker.ChunkDirectory(rootPath, library, version))
        {
            if (skipped < existing) { skipped++; continue; }

            if (maxChunks.HasValue && total >= maxChunks)
                break;

            batch.Add(chunk);
            if (batch.Count >= BatchSize)
            {
                await FlushAsync(batch, embedder);
                total += batch.Count;
                Console.Error.WriteLine($"[{DateTime.UtcNow:O}] Indexed {total} chunks...");
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, embedder);
            total += batch.Count;
        }

        totalSw.Stop();
        Console.Error.WriteLine($"[{DateTime.UtcNow:O}] Indexing complete. Total chunks for {library} {version}: {total} (elapsed {totalSw.Elapsed})");
    }

    private async Task FlushAsync(List<DelphiChunk> batch, IEmbeddingService embedder)
    {
        var texts = batch.Select(c => c.Content).ToList();
        var embeddings = await embedder.EmbedBatchAsync(texts);
        _store.AddChunks(batch, embeddings);
    }
}
