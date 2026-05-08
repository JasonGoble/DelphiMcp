namespace DelphiMcp.Embedder;

public interface IEmbeddingService
{
    Task<List<float[]>> EmbedBatchAsync(List<string> texts);
}
