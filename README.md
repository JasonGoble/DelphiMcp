# DelphiMcp

[![CI](https://github.com/JasonGoble/DelphiMcp/actions/workflows/ci.yml/badge.svg)](https://github.com/JasonGoble/DelphiMcp/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/JasonGoble/DelphiMcp?display_name=tag)](https://github.com/JasonGoble/DelphiMcp/releases)

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

## CLI Quick Reference

- `--index` (requires `--library`, `--version`, `--path`)
  - Builds or resumes embeddings/chunks for a source tree.
  - Optional: `--max-chunks <N>` to cap indexing for pilot runs.
- `--reset` (requires `--library`; optional `--version`)
  - Deletes indexed chunks for a library (or a specific version).
- `--vacuum`
  - Compacts the SQLite database after large deletes.
- `--bench-search` (requires `--library`; optional `--version`)
  - Uses stored embeddings to benchmark retrieval latency.
  - Optional: `--iterations <N>` (default `20`), `--top-k <N>` (default `10`).
- `--compare-embedders` (requires `--library`, `--version1`, `--version2`)
  - Compares retrieval overlap and timing across two indexed versions.
  - Optional: `--provider1`, `--provider2`, `--top-k <N>`.
- `--compare-detailed` (requires `--library`, `--version1`, `--version2`)
  - Prints detailed top-K result lists for manual quality review.
  - Optional: `--provider1`, `--provider2`, `--top-k <N>`.
- No CLI args
  - Starts MCP stdio server mode.

## Indexing

```
DelphiMcp --index --library rtl        --version 12.0 --path "C:\Program Files (x86)\Embarcadero\Studio\37.0\source"
DelphiMcp --index --library devexpress --version 12.0 --path "C:\Path\To\DevExpress\Sources"
```

Re-running `--index` resumes from the last persisted chunk count (skip-by-count).

Pilot indexing with a safety cap:

```
DelphiMcp --index --library rtl --version 13.1 --path "C:\Program Files (x86)\Embarcadero\Studio\37.0\source" --max-chunks 5000
```

## Reset

```
DelphiMcp --reset --library rtl                       # delete all RTL chunks
DelphiMcp --reset --library rtl --version 12.0        # delete only RTL 12.0 chunks
```


## Vacuum Database

```
DelphiMcp --vacuum
```

Compacts the SQLite database file by reclaiming unused space. Run this after bulk --reset operations to recover storage and potentially improve query performance.

**When to use:**
- After resetting one or more library versions
- As part of a maintenance workflow (reset → vacuum → reindex)
- Periodically on large databases to recover fragmented space

**Note:** VACUUM locks the database briefly; do not run during concurrent searches. See ADR 0005 for details.

## Benchmark Search

```
DelphiMcp --bench-search --library rtl --version 12.0 --iterations 50 --top-k 10
```

Uses a stored sample embedding (no embed API call required) and reports average query latency.

## MCP Tool Behavior

- `search_rtl` / `search_devexpress`
  - Semantic search over indexed chunks for the selected library.
  - Optional `version` narrows results to one indexed version.
  - Returns ranked results with metadata (for example unit, section, and visibility when available).
- `lookup_rtl_class` / `lookup_devexpress_class`
  - Class-oriented lookup for known type names.
  - Optional `version` narrows lookup scope.
  - Intended for quick symbol access when class name is already known.

## End-to-End Workflow

Typical maintenance and indexing flow:

```
# 1) Reset stale data for a version
DelphiMcp --reset --library rtl --version 12.3

# 2) Optionally compact DB after large deletes
DelphiMcp --vacuum

# 3) Run a pilot index first (safe cap)
DelphiMcp --index --library rtl --version 12.3 --path "C:\Program Files (x86)\Embarcadero\Studio\23.0\source" --max-chunks 3000

# 4) Run full indexing
DelphiMcp --index --library rtl --version 12.3 --path "C:\Program Files (x86)\Embarcadero\Studio\23.0\source"

# 5) Validate retrieval latency
DelphiMcp --bench-search --library rtl --version 12.3 --iterations 50 --top-k 10
```

## Running as MCP server

```
DelphiMcp                # stdio transport — point your Claude config at the executable
DelphiMcp --http         # hosted HTTP transport (requires Hosted:ApiKey)
```

In hosted mode, MCP endpoint defaults to `/mcp` and requires an API key sent via one of:

- `Authorization: Bearer <api-key>`
- `X-API-Key: <api-key>`

Use [docs/setup/iis-and-claude-code.md](docs/setup/iis-and-claude-code.md) for IIS deployment and Claude Code setup.

## Configuration

`appsettings.json` (or environment variables):

- `Embedder:Provider` — `OpenAI` (default) or `Ollama`
- `OpenAI:ApiKey` — required when using OpenAI
- `Ollama:BaseUrl`, `Ollama:Model`
- `Storage:DbPath` — path to the SQLite file (default: alongside the executable)
- `Storage:FaissIndexDir` — optional path for Faiss index files (default: `faiss-indexes` next to DB)
- `Server:Mode` — `stdio` (default) or `http`
- `Hosted:Path` — hosted MCP path (default: `/mcp`)
- `Hosted:ApiKey` — required when `Server:Mode=http`
- `Hosted:RequireHttps` — when `true`, redirects hosted mode traffic to HTTPS
- `Search:PrioritizedNamespaces` — namespace prefixes to favor during candidate selection and final ranking
- `Search:NamespaceBoostFactor` — multiplier applied to distances for prioritized namespaces (default: `0.95`)
- `Search:NamespaceOversampleFactor` — how many extra candidates to retrieve before reranking (default: `5`)

## Performance Characteristics

- **Search latency (Faiss CPU index)**:
  - Ollama (nomic-embed-text, 768-dim): ~52ms avg per query
  - OpenAI (text-embedding-3-small, 1536-dim): ~97ms avg per query
- **Index scale**: Successfully indexed 194,921 chunks (RTL 13.1) and 185,624 chunks (RTL 12.3)
- **Index type**: Exact (`IndexFlatIP` with ID mapping); approximate indexes (HNSW, IVF) deferred for future evaluation
- **Namespace prioritization**: Search retrieves an oversampled candidate pool, boosts core Delphi namespaces (System, Vcl, FMX, FireDAC), then returns the final top K. This improves recall for canonical library results as well as final ordering. See ADR 0003.
- **Visibility metadata**: Chunks now carry inferred `published` / `public` / `protected` / `private` visibility, and search re-ranks close matches toward more accessible declarations. Existing databases migrate in place, but older rows remain visibility-null until reindexed. See ADR 0004.

## Quality & Testing

Embedder quality comparison available in `ManualTesting/` folder:
- **comparison-results.md**: Top-5 ranked search results for 14 test queries (Ollama vs OpenAI)
- **Indexing logs**: Full execution traces for Ollama and OpenAI production runs
- **Automated tests**: `dotnet test DelphiMcp.Tests/DelphiMcp.Tests.csproj` covers visibility extraction, schema migration, and visibility-aware ranking order
- **CI automation**: Build and test status are automatically reported by GitHub Actions (see CI badge above)

## Architecture Decisions

See [docs/decisions/README.md](docs/decisions/README.md) for a list of all Architecture Decision Records (ADRs) and their purpose.

## Hosted Smoke Test

Use [scripts/hosted-smoke-test.ps1](scripts/hosted-smoke-test.ps1) to quickly validate hosted deployment behavior:

- `/healthz` returns 200
- unauthenticated `/mcp` returns 401
- authenticated `/mcp` is accepted

## Release Process

Keep releases lightweight and consistent:

1. Ensure CI is green on `main`.
2. Create and push an annotated version tag (for example `v1.1.0`).
3. Publish a GitHub Release from that tag with summary notes.
4. Verify release badge updates and link to notes in release PR/issue comments.

## Status

Local-first scaffold with dual mode support:

- stdio mode for minimal local Claude Code setup
- hosted HTTP mode for centralized IIS deployment with API-key authentication

