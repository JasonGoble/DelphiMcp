using System.Buffers.Binary;
using DelphiMcp.Chunker;
using Microsoft.Data.Sqlite;

namespace DelphiMcp.VectorStore;

/// <summary>
/// SQLite-backed chunk store with in-memory cosine KNN.
///
/// Layout: one row per chunk in `chunks` (library, version, metadata, content, embedding BLOB).
/// Query path: load all rows matching (library, optional version) into a cached float[][]
/// matrix on first query, then brute-force cosine. For ~100k chunks × 1536 dims this is
/// well under 100ms per query and avoids any native vector-extension dependency.
/// </summary>
public class SqliteVectorStore : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly Dictionary<string, CachedMatrix> _cache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public SqliteVectorStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS chunks (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                library       TEXT NOT NULL,
                version       TEXT NOT NULL,
                unit_name     TEXT NOT NULL,
                file_path     TEXT NOT NULL,
                section       TEXT NOT NULL,
                chunk_type    TEXT NOT NULL,
                identifier    TEXT NOT NULL,
                start_line    INTEGER NOT NULL,
                content       TEXT NOT NULL,
                embedding     BLOB NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_chunks_lib_ver
                ON chunks (library, version);
            CREATE INDEX IF NOT EXISTS idx_chunks_lib_ident
                ON chunks (library, identifier);
            """;
        cmd.ExecuteNonQuery();
    }

    public int CountChunks(string library, string version)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunks WHERE library = $lib AND version = $ver";
        cmd.Parameters.AddWithValue("$lib", library);
        cmd.Parameters.AddWithValue("$ver", version);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int DeleteLibrary(string library, string? version = null)
    {
        InvalidateCacheFor(library);
        using var cmd = _conn.CreateCommand();
        if (version is null)
        {
            cmd.CommandText = "DELETE FROM chunks WHERE library = $lib";
            cmd.Parameters.AddWithValue("$lib", library);
        }
        else
        {
            cmd.CommandText = "DELETE FROM chunks WHERE library = $lib AND version = $ver";
            cmd.Parameters.AddWithValue("$lib", library);
            cmd.Parameters.AddWithValue("$ver", version);
        }
        return cmd.ExecuteNonQuery();
    }

    public void AddChunks(IList<DelphiChunk> chunks, IList<float[]> embeddings)
    {
        if (chunks.Count != embeddings.Count)
            throw new ArgumentException("chunks and embeddings count mismatch");
        if (chunks.Count == 0) return;

        InvalidateCacheFor(chunks[0].Library);

        using var tx = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO chunks
                (library, version, unit_name, file_path, section, chunk_type, identifier, start_line, content, embedding)
            VALUES
                ($library, $version, $unit, $file, $section, $type, $ident, $line, $content, $emb)
            """;
        var pLib = cmd.Parameters.Add("$library", SqliteType.Text);
        var pVer = cmd.Parameters.Add("$version", SqliteType.Text);
        var pUnit = cmd.Parameters.Add("$unit", SqliteType.Text);
        var pFile = cmd.Parameters.Add("$file", SqliteType.Text);
        var pSec = cmd.Parameters.Add("$section", SqliteType.Text);
        var pType = cmd.Parameters.Add("$type", SqliteType.Text);
        var pId = cmd.Parameters.Add("$ident", SqliteType.Text);
        var pLine = cmd.Parameters.Add("$line", SqliteType.Integer);
        var pContent = cmd.Parameters.Add("$content", SqliteType.Text);
        var pEmb = cmd.Parameters.Add("$emb", SqliteType.Blob);

        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            pLib.Value = c.Library;
            pVer.Value = c.Version;
            pUnit.Value = c.UnitName;
            pFile.Value = c.FilePath;
            pSec.Value = c.Section;
            pType.Value = c.ChunkType;
            pId.Value = c.Identifier;
            pLine.Value = c.StartLine;
            pContent.Value = c.Content;
            pEmb.Value = EncodeEmbedding(embeddings[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public record SearchHit(
        long Id,
        string Library,
        string Version,
        string UnitName,
        string FilePath,
        string Section,
        string ChunkType,
        string Identifier,
        int StartLine,
        string Content,
        float Distance
    );

    public async Task<List<SearchHit>> SearchAsync(
        string library, string? version, float[] queryVec, int topK)
    {
        var matrix = await LoadMatrixAsync(library, version);
        if (matrix.RowIds.Length == 0) return [];

        // Normalize once (cosine) — matrix is pre-normalized, so dot product == cosine similarity.
        float[] q = Normalize(queryVec);

        // Score every row, keep top-K via partial selection.
        var scores = new float[matrix.RowIds.Length];
        var emb = matrix.Embeddings;
        int dim = q.Length;
        for (int r = 0; r < scores.Length; r++)
        {
            var row = emb[r];
            float dot = 0f;
            for (int d = 0; d < dim; d++) dot += row[d] * q[d];
            scores[r] = dot;
        }

        // Top-K by descending similarity → ascending distance (1 - sim).
        var idx = Enumerable.Range(0, scores.Length)
            .OrderByDescending(i => scores[i])
            .Take(topK)
            .ToArray();

        var hits = new List<SearchHit>(idx.Length);
        foreach (var i in idx)
            hits.Add(matrix.Metadata[i] with { Distance = 1f - scores[i] });
        return hits;
    }

    public List<SearchHit> FindByIdentifier(string library, string? version, string identifier, string chunkType)
    {
        using var cmd = _conn.CreateCommand();
        var sql = """
            SELECT id, library, version, unit_name, file_path, section, chunk_type, identifier, start_line, content
            FROM chunks
            WHERE library = $lib AND identifier = $ident AND chunk_type = $type
            """;
        if (version is not null) sql += " AND version = $ver";

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$lib", library);
        cmd.Parameters.AddWithValue("$ident", identifier);
        cmd.Parameters.AddWithValue("$type", chunkType);
        if (version is not null) cmd.Parameters.AddWithValue("$ver", version);

        var hits = new List<SearchHit>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            hits.Add(new SearchHit(
                Id: rdr.GetInt64(0),
                Library: rdr.GetString(1),
                Version: rdr.GetString(2),
                UnitName: rdr.GetString(3),
                FilePath: rdr.GetString(4),
                Section: rdr.GetString(5),
                ChunkType: rdr.GetString(6),
                Identifier: rdr.GetString(7),
                StartLine: rdr.GetInt32(8),
                Content: rdr.GetString(9),
                Distance: 0f
            ));
        }
        return hits;
    }

    private async Task<CachedMatrix> LoadMatrixAsync(string library, string? version)
    {
        string key = $"{library}|{version ?? "*"}";
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            using var cmd = _conn.CreateCommand();
            var sql = """
                SELECT id, library, version, unit_name, file_path, section, chunk_type, identifier, start_line, content, embedding
                FROM chunks
                WHERE library = $lib
                """;
            if (version is not null) sql += " AND version = $ver";
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$lib", library);
            if (version is not null) cmd.Parameters.AddWithValue("$ver", version);

            var ids = new List<long>();
            var embs = new List<float[]>();
            var metas = new List<SearchHit>();

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                ids.Add(rdr.GetInt64(0));
                metas.Add(new SearchHit(
                    Id: rdr.GetInt64(0),
                    Library: rdr.GetString(1),
                    Version: rdr.GetString(2),
                    UnitName: rdr.GetString(3),
                    FilePath: rdr.GetString(4),
                    Section: rdr.GetString(5),
                    ChunkType: rdr.GetString(6),
                    Identifier: rdr.GetString(7),
                    StartLine: rdr.GetInt32(8),
                    Content: rdr.GetString(9),
                    Distance: 0f
                ));

                var blob = (byte[])rdr.GetValue(10);
                embs.Add(Normalize(DecodeEmbedding(blob)));
            }

            cached = new CachedMatrix(ids.ToArray(), embs.ToArray(), metas.ToArray());
            _cache[key] = cached;
            return cached;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void InvalidateCacheFor(string library)
    {
        _cacheLock.Wait();
        try
        {
            var stale = _cache.Keys.Where(k => k.StartsWith(library + "|")).ToList();
            foreach (var k in stale) _cache.Remove(k);
        }
        finally { _cacheLock.Release(); }
    }

    private static byte[] EncodeEmbedding(float[] vec)
    {
        var bytes = new byte[vec.Length * 4];
        for (int i = 0; i < vec.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), vec[i]);
        return bytes;
    }

    private static float[] DecodeEmbedding(byte[] bytes)
    {
        if (bytes.Length % 4 != 0)
            throw new InvalidDataException($"Embedding blob length {bytes.Length} not multiple of 4");
        var vec = new float[bytes.Length / 4];
        for (int i = 0; i < vec.Length; i++)
            vec[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
        return vec;
    }

    private static float[] Normalize(float[] v)
    {
        double sum = 0;
        for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
        float norm = (float)Math.Sqrt(sum);
        if (norm == 0f) return v;
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = v[i] / norm;
        return r;
    }

    private record CachedMatrix(long[] RowIds, float[][] Embeddings, SearchHit[] Metadata);

    public void Dispose()
    {
        _conn.Dispose();
        _cacheLock.Dispose();
    }
}
