using OpenAI.Embeddings;

namespace DelphiMcp.Embedder;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private const string Model = "text-embedding-3-small";

    public OpenAiEmbeddingService(string apiKey)
    {
        _client = new EmbeddingClient(Model, apiKey);
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
    {
        var safeTexts = texts
            .Select(t => t.Length > 6000 ? t[..6000] : t)
            .ToList();

        const int maxAttempts = 4;
        int delayMs = 1000;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _client.GenerateEmbeddingsAsync(safeTexts);
                return response.Value
                    .Select(e => e.ToFloats().ToArray())
                    .ToList();
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                Console.Error.WriteLine($"[retry] OpenAI embed attempt {attempt} failed ({ex.GetType().Name}: {ex.Message}); retrying in {delayMs}ms");
                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is TaskCanceledException) return true;
        if (ex is HttpRequestException) return true;
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("429") || msg.Contains("500") || msg.Contains("502") ||
               msg.Contains("503") || msg.Contains("504") || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }
}
