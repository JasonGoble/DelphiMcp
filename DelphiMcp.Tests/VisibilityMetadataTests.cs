using DelphiMcp.Chunker;
using DelphiMcp.VectorStore;
using Microsoft.Data.Sqlite;

namespace DelphiMcp.Tests;

public class VisibilityMetadataTests
{
    [Fact]
    public void ChunkFile_CapturesVisibilityMetadata()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "SampleUnit.pas");
        File.WriteAllText(filePath, """
            unit SampleUnit;

            interface

            type
              TSample = class
              published
                property Name: string read FName write FName;
              public
                procedure DoPublicThing;
              protected
                procedure DoProtectedThing;
              private
                FName: string;
              end;

            procedure PublicApi;

            implementation

            procedure UnitPrivateHelper;
            begin
            end;

            end.
            """);

        var chunks = DelphiChunker.ChunkFile(filePath, "rtl", "test").ToList();

        var typeChunk = Assert.Single(chunks, c => c.ChunkType == "type");
        Assert.Equal("published", typeChunk.Visibility);

        var interfaceRoutine = Assert.Single(chunks, c => c.Identifier == "PublicApi");
        Assert.Equal("public", interfaceRoutine.Visibility);

        var implementationRoutine = Assert.Single(chunks, c => c.Identifier == "UnitPrivateHelper");
        Assert.Equal("private", implementationRoutine.Visibility);
    }

    [Fact]
    public void SqliteVectorStore_AddsVisibilityColumnToExistingSchema()
    {
        var tempDir = CreateTempDirectory();
        var dbPath = Path.Combine(tempDir, "chunks.db");

        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE chunks (
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
                """;
            cmd.ExecuteNonQuery();
        }

        using (var store = new SqliteVectorStore(dbPath, faissIndexDir: null))
        using (var verify = new SqliteConnection($"Data Source={dbPath}"))
        {
            verify.Open();
            using var pragma = verify.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(chunks)";
            using var rdr = pragma.ExecuteReader();

            var columns = new List<string>();
            while (rdr.Read())
                columns.Add(rdr.GetString(1));

            Assert.Contains("visibility", columns);
        }
    }

    [Fact]
    public async Task SearchAsync_PrefersAccessibleVisibilityWhenScoresAreClose()
    {
        var tempDir = CreateTempDirectory();
        var dbPath = Path.Combine(tempDir, "chunks.db");

        using (var store = new SqliteVectorStore(dbPath, faissIndexDir: null))
        {
            store.AddChunks(
                [
                    CreateChunk("PublishedMember", "published"),
                    CreateChunk("PublicMember", "public"),
                    CreateChunk("ProtectedMember", "protected"),
                    CreateChunk("PrivateMember", "private")
                ],
                [
                    CreateEmbedding(0.9898f),
                    CreateEmbedding(0.9899f),
                    CreateEmbedding(0.98995f),
                    CreateEmbedding(0.99f)
                ]);

            var hits = await store.SearchAsync("rtl", "test", [1f, 0f], 4);

            Assert.Equal(
                ["PublishedMember", "PublicMember", "ProtectedMember", "PrivateMember"],
                hits.Select(h => h.Identifier).ToArray());
        }
    }

    private static DelphiChunk CreateChunk(string identifier, string visibility) => new(
        Library: "rtl",
        Version: "test",
        UnitName: "System.TestUnit",
        FilePath: "System.TestUnit.pas",
        Section: "interface",
        ChunkType: "routine",
        Identifier: identifier,
        Content: $"procedure {identifier};",
        StartLine: 1,
        Visibility: visibility);

    private static float[] CreateEmbedding(float similarity)
    {
        var orthogonal = MathF.Sqrt(Math.Max(0f, 1f - (similarity * similarity)));
        return [similarity, orthogonal];
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DelphiMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
