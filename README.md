# DelphiMcp

MCP server providing Claude with grounded access to Delphi RTL/VCL/FMX and DevExpress
component source code via embedding-based search.

## Architecture

- **Embedder**: OpenAI `text-embedding-3-small` (default) or Ollama `nomic-embed-text`.
- **Vector store**: SQLite, single file. Embeddings stored as BLOB; KNN done in-memory
  via brute-force cosine over a per-(library, version) cache. No native extensions.
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

## Status

Local-first scaffold. Hosted ASP.NET Core variant with API-key auth and per-library
licensing gating is planned for a follow-up pass.
