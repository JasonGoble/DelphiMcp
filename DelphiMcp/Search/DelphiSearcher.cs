using System.Text;
using DelphiMcp.Embedder;
using DelphiMcp.VectorStore;

namespace DelphiMcp.Search;

public class DelphiSearcher(SqliteVectorStore store, IEmbeddingService embedder)
{
    private readonly SqliteVectorStore _store = store;
    private readonly IEmbeddingService _embedder = embedder;

    public async Task<string> SearchAsync(string library, string? version, string query, int topK)
    {
        var embeddings = await _embedder.EmbedBatchAsync([query]);
        var hits = await _store.SearchAsync(library, version, embeddings[0], topK);

        if (hits.Count == 0)
            return $"No relevant {library} source found for that query"
                + (version is null ? "." : $" (version {version}).");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {hits.Count} relevant {library} chunks for: \"{query}\"");
        sb.AppendLine();

        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            string visibility = h.Visibility ?? "public";
            sb.AppendLine($"--- [{i + 1}] {h.UnitName}.{h.Identifier} ({h.ChunkType}, {h.Section}, visibility {visibility}, {h.Library} {h.Version}, line {h.StartLine}) [distance: {h.Distance:F4}] ---");
            sb.AppendLine(h.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public async Task<string> LookupClassAsync(string library, string? version, string className)
    {
        // Exact-identifier hits first (cheap and authoritative).
        var exact = _store.FindByIdentifier(library, version, className, "type");
        if (exact.Count > 0)
        {
            // Forward declarations are short; the real class body is much larger.
            var best = exact.OrderByDescending(h => h.Content.Length).First();
            var visibility = best.Visibility ?? "public";
            var sb = new StringBuilder();
            sb.AppendLine($"--- {best.UnitName}.{className} ({best.Library} {best.Version}, type declaration, visibility {visibility}) ---");
            sb.AppendLine(best.Content);
            return sb.ToString();
        }

        // Fall back to vector search biased toward type declarations.
        return await SearchAsync(library, version, $"class declaration {className}", 3);
    }
}
