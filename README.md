# DelphiMcp

MCP server providing Claude with grounded access to Delphi RTL/VCL/FMX and DevExpress
component source code via embedding-based search.

## Architecture

- **Embedder**: OpenAI `text-embedding-3-small` (default) or Ollama `nomic-embed-text`.
- **Vector store**: SQLite, single file. Embeddings stored as BLOB; KNN uses Faiss (Flat IP + ID map)
  with on-disk index persistence per `(library, version)`. Falls back to in-memory brute-force cosine
  if Faiss cannot load/build.
- **Chunker**: Pascal-aware. Emits one chunk per type declaration, routine, and unit header.
- **MCP tools**: `search_rtl` / `lookup_rtl_class` and `search_devexpress` / `lookup_devexpress_class`.
  All accept an optional `version` filter.

## Indexing

```
DelphiMcp --index --library rtl        --version 12.0 --path "C:\Program Files (x86)\Embarcadero\Studio\37.0\source"
DelphiMcp --index --library devexpress --version 12.0 --path "C:\Path\To\DevExpress\Sources"
```

Re-running `--index` resumes from the last persisted chunk count (skip-by-count).

## Reset

```
DelphiMcp --reset --library rtl                       # delete all RTL chunks
DelphiMcp --reset --library rtl --version 12.0        # delete only RTL 12.0 chunks
```

## Benchmark Search

```
DelphiMcp --bench-search --library rtl --version 12.0 --iterations 50 --top-k 10
```

Uses a stored sample embedding (no embed API call required) and reports average query latency.

## Running as MCP server

```
DelphiMcp                # stdio transport — point your Claude config at the executable
```

## Configuration

`appsettings.json` (or environment variables):

- `Embedder:Provider` — `OpenAI` (default) or `Ollama`
- `OpenAI:ApiKey` — required when using OpenAI
- `Ollama:BaseUrl`, `Ollama:Model`
- `Storage:DbPath` — path to the SQLite file (default: alongside the executable)
- `Storage:FaissIndexDir` — optional path for Faiss index files (default: `faiss-indexes` next to DB)

## Performance Characteristics

- **Search latency (Faiss CPU index)**:
  - Ollama (nomic-embed-text, 768-dim): ~52ms avg per query
  - OpenAI (text-embedding-3-small, 1536-dim): ~97ms avg per query
- **Index scale**: Successfully indexed 194,921 chunks (RTL 13.1) and 185,624 chunks (RTL 12.3)
- **Index type**: Exact (`IndexFlatIP` with ID mapping); approximate indexes (HNSW, IVF) deferred for future evaluation

## Quality & Testing

Embedder quality comparison available in `ManualTesting/` folder:
- **comparison-results.md**: Top-5 ranked search results for 14 test queries (Ollama vs OpenAI)
- **Indexing logs**: Full execution traces for Ollama and OpenAI production runs

## Architecture Decisions

Key design decisions are documented in `docs/decisions/`:
- [ADR 0001: Issue Title Prefix and Label Convention](docs/decisions/0001-issue-title-and-label-convention.md)
- [ADR 0002: Faiss Native Vector Search Integration](docs/decisions/0002-faiss-native-vector-search.md)

## Status

Local-first scaffold. Hosted ASP.NET Core variant with API-key auth and per-library
licensing gating is planned for a follow-up pass.
