using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DelphiMcp.Embedder;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;

    // nomic-embed-text caps at 2048 tokens. Worst-case 1 char ≈ 1 token, so 1800 is unconditionally safe.
    private const int InitialMaxChars = 5000;
    private const int FallbackMaxChars = 1800;

    public OllamaEmbeddingService(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
    {
        var safeTexts = texts.Select(t => Truncate(t, InitialMaxChars)).ToList();

        try
        {
            return await CallEmbedAsync(safeTexts);
        }
        catch (InvalidOperationException ex) when (IsContextLengthError(ex))
        {
            Console.Error.WriteLine($"[ollama] batch hit context limit; falling back to per-item with {FallbackMaxChars}-char cap");
            return await EmbedPerItemAsync(texts);
        }
    }

    private async Task<List<float[]>> EmbedPerItemAsync(List<string> texts)
    {
        var results = new List<float[]>(texts.Count);
        foreach (var t in texts)
        {
            var input = Truncate(t, FallbackMaxChars);
            try
            {
                var single = await CallEmbedAsync(new List<string> { input });
                results.Add(single[0]);
            }
            catch (InvalidOperationException ex) when (IsContextLengthError(ex))
            {
                int chars = FallbackMaxChars / 2;
                while (true)
                {
                    try
                    {
                        var single = await CallEmbedAsync(new List<string> { Truncate(t, chars) });
                        Console.Error.WriteLine($"[ollama] embedded item after truncating to {chars} chars");
                        results.Add(single[0]);
                        break;
                    }
                    catch (InvalidOperationException ex2) when (IsContextLengthError(ex2) && chars > 200)
                    {
                        chars /= 2;
                    }
                }
            }
        }
        return results;
    }

    private async Task<List<float[]>> CallEmbedAsync(List<string> input)
    {
        const int maxAttempts = 4;
        int delayMs = 1000;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("/api/embed", new
                {
                    model = _model,
                    input,
                    truncate = true
                });
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"Ollama embed failed ({(int)resp.StatusCode} {resp.StatusCode}): {err}");
                }
                var body = await resp.Content.ReadFromJsonAsync<OllamaEmbedResponse>()
                    ?? throw new InvalidOperationException("Ollama embed returned empty body");
                return body.Embeddings;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                Console.Error.WriteLine($"[retry] Ollama embed attempt {attempt} failed ({ex.GetType().Name}: {ex.Message}); retrying in {delayMs}ms");
                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }
    }

    private static string Truncate(string s, int maxChars) => s.Length > maxChars ? s[..maxChars] : s;

    private static bool IsContextLengthError(Exception ex) =>
        ex.Message.Contains("context length", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransient(Exception ex)
    {
        if (ex is TaskCanceledException) return true;
        if (ex is HttpRequestException) return true;
        if (ex is InvalidOperationException ioe)
        {
            if (IsContextLengthError(ioe)) return false;
            return ioe.Message.Contains("(429 ") || ioe.Message.Contains("(408 ") ||
                   ioe.Message.Contains("(500 ") || ioe.Message.Contains("(502 ") ||
                   ioe.Message.Contains("(503 ") || ioe.Message.Contains("(504 ");
        }
        return false;
    }

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] List<float[]> Embeddings);
}
