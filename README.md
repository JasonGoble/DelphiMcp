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
- **MCP tools (v1.1)**: `search_delphi_source` and `lookup_delphi_class` (unified).
  - When `library` is specified, searches that library only.
  - When omitted, uses resolved client profile's `DefaultScopes` (for multi-library searches).
  - Support optional `version` filtering and multi-version comparison.
- **Client Access (v1.1)**: Machine profiles with API keys, default scopes, and per-profile query policy enforcement.
  See [ADR 0007](docs/decisions/0007-machine-profile-client-access-policy.md) and [ADR 0008](docs/decisions/0008-unified-delphi-source-tools.md).
- **Parser Foundation (v2.0 Phase 1)**: Contract models for `parse_delphi_structure` are in place (`AstSummary`, `NormalizedSource`, `SymbolTable`, `Diagnostics`) as the base for parser-backed Delphi intelligence.
  See [ADR 0011](docs/decisions/0011-parse-delphi-structure-contracts.md).

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

## MCP Tool Behavior (v1.1)

v1.1 provides **unified Delphi source tools** that work across all indexed libraries. The tools automatically resolve client profiles and enforce query policies.

### `search_delphi_source`
- **Query parameter**: Natural language search query or symbol name, e.g., `"TStringList thread safety"` or `"cxGrid focused row"`.
- **Library parameter** (optional):
  - If specified (e.g., `"rtl"`, `"devexpress"`): Searches that library only.
  - If omitted: Searches using the resolved client profile's `DefaultScopes` (v1.1 hosted mode) or falls back to common libraries (stdio mode).
- **Version parameter** (optional): Filters results to a single library version (e.g., `"12.0"`).
- **topK parameter** (optional, default 5): Number of results to return. Capped by policy limit `MaxTopK`.
- **Returns**: Ranked search results with metadata (unit, identifier, chunk type, visibility, library, version, line number).

### `lookup_delphi_class`
- **className parameter**: Class or type name to look up, e.g., `"TStringList"`, `"TcxGrid"`.
- **Library parameter** (optional):
  - If specified: Searches that library only.
  - If omitted: Searches using the resolved client profile's `DefaultScopes` (hosted) or fallback libraries (stdio).
- **Version parameter** (optional): Filters to a single version.
- **Versions parameter** (optional): Comma-separated list for multi-version comparison, e.g., `"12.0,11.0"`.
  - Validates count against policy limit `MaxVersionsPerLibraryPerQuery`.
  - Returns formatted side-by-side declarations for each version.
- **Returns**: Class declaration(s) including properties and method signatures.

### Scope Resolution

When `library` is omitted (unscoped query):
1. **Hosted HTTP mode**: Resolves client profile from `Authorization` header via ClientProfileResolver.
   - If profile found: Uses its `DefaultScopes`.
   - If not found: Returns error.
2. **Stdio mode**: Falls back to searching common libraries (RTL + DevExpress).

### Policy Enforcement

All queries are subject to global and profile-specific policy limits:
- `MaxTopK`: Maximum results per query (default 10).
- `MaxVersionsPerLibraryPerQuery`: Maximum versions for multi-version comparison (default 2).
- `MaxTargetScopesPerQuery`: Maximum library scopes to search when using `DefaultScopes` (default 4).
- `AllowUnversionedQueries`: Whether to allow queries without a version specified (default true).

See [ADR 0007](docs/decisions/0007-machine-profile-client-access-policy.md) for details.

### v1.0 to v1.1 Migration

**v1.0 tools** (library-specific) are deprecated but remain functional in v1.1:
- `search_rtl`, `lookup_rtl_class`
- `search_devexpress`, `lookup_devexpress_class`

These tools will be removed in v1.2. Existing users should migrate to unified tools:
- Replace separate searches with `search_delphi_source` (omit library to use profile scopes).
- Replace separate lookups with `lookup_delphi_class` (omit library to use profile scopes).
- For multi-version comparison, use `lookup_delphi_class` with comma-separated `versions` parameter.

See [v1.0 → v1.1 Migration Guide](#v10--v11-migration-guide) below.

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
DelphiMcp --http         # hosted HTTP transport
```

In hosted mode, MCP endpoint defaults to `/mcp` and requires an API key sent via one of:

- `Authorization: Bearer <api-key>`
- `X-API-Key: <api-key>`

Hosted authentication prefers profile-based client access (`ClientAccess`).
Legacy `Hosted:ApiKey` is still supported as a migration fallback.

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
- `Hosted:ApiKey` — legacy fallback API key for hosted mode
- `Hosted:RequireHttps` — when `true`, redirects hosted mode traffic to HTTPS
- `ClientAccess:GlobalPolicy:*` — hosted query policy defaults (`MaxTopK`, scope limits, version behavior)
- `ClientAccess:Profiles:<id>:ApiKeyRef` — secret reference for a machine profile API key
- `ClientAccess:Profiles:<id>:DefaultScopes` — default `(library, version)` targets for unscoped queries
- `ClientAccess:Profiles:<id>:Options:*` — optional per-profile policy overrides
- `Search:PrioritizedNamespaces` — namespace prefixes to favor during candidate selection and final ranking
- `Search:NamespaceBoostFactor` — multiplier applied to distances for prioritized namespaces (default: `0.95`)
- `Search:NamespaceOversampleFactor` — how many extra candidates to retrieve before reranking (default: `5`)

### ClientAccess Example

```json
"ClientAccess": {
  "GlobalPolicy": {
    "MaxTopK": 10,
    "MaxVersionsPerLibraryPerQuery": 2,
    "MaxTargetScopesPerQuery": 4,
    "AllowUnversionedQueries": true,
    "RequireVersionWhenLibrarySpecified": false,
    "DefaultSearchBehavior": "UseProfileDefaults"
  },
  "Profiles": {
    "machine-a": {
      "Enabled": true,
      "DisplayName": "Computer A",
      "ApiKeyRef": "MCP__KEY__MACHINE_A",
      "DefaultScopes": [
        { "Library": "rtl", "Version": "13.1" },
        { "Library": "devexpress", "Version": "25.2.6" }
      ],
      "Options": {
        "MaxTopK": 8
      }
    }
  }
}
```

Set `MCP__KEY__MACHINE_A` (or your chosen key reference names) in environment variables or user-secrets.

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
Client access policy for machine profiles is documented in [ADR 0007](docs/decisions/0007-machine-profile-client-access-policy.md).
Unified tool design is documented in [ADR 0008](docs/decisions/0008-unified-delphi-source-tools.md).

## v1.0 → v1.1 Migration Guide

### Overview

v1.1 introduces **unified MCP tools** and **machine profile client access** for v1.0 users upgrading to the latest version.

**Good news:** v1.0 library-specific tools remain functional in v1.1, so you can upgrade at your own pace.

### What Changed

| Feature | v1.0 | v1.1 |
|---------|------|------|
| **Tools** | `search_rtl`, `lookup_rtl_class`, `search_devexpress`, `lookup_devexpress_class` | `search_delphi_source`, `lookup_delphi_class` (unified) |
| **Multi-library search** | Multiple tool calls | One call with `DefaultScopes` |
| **Version comparison** | Not supported | Supported via `versions` parameter |
| **Client authentication** | `Hosted:ApiKey` only | `ClientAccess` profiles + legacy fallback |
| **Query policy** | No limits | Global + per-profile policy enforcement |

### Tool Migration Examples

#### Example 1: Single Library Search (No Change in Behavior)

**v1.0:**
```
Tool: search_rtl(query="TStringList", topK=5)
```

**v1.1 (same):**
```
Tool: search_delphi_source(query="TStringList", library="rtl", topK=5)
```

#### Example 2: Multi-Library Search (Simplified)

**v1.0 (manual combination):**
```
Tool 1: search_rtl(query="TStringList", topK=5)
Tool 2: search_devexpress(query="TStringList", topK=5)
Combine results manually
```

**v1.1 (unified, with profile scopes):**
```
Tool: search_delphi_source(query="TStringList", topK=5)
# Automatically searches DefaultScopes from client profile (e.g., RTL 12.0 + DevExpress 25.2.6)
```

#### Example 3: Class Lookup (Same Signature, Enhanced)

**v1.0:**
```
Tool: lookup_rtl_class(className="TStringList", version="12.0")
```

**v1.1 (backward compatible):**
```
Tool: lookup_delphi_class(className="TStringList", library="rtl", version="12.0")
```

**v1.1 (new: multi-version comparison):**
```
Tool: lookup_delphi_class(className="TStringList", library="rtl", versions="12.0,11.0")
# Returns side-by-side declarations for easy comparison
```

### Configuration Migration

#### v1.0 (Hosted Mode)

```json
{
  "Hosted": {
    "ApiKey": "your-secret-key"
  }
}
```

#### v1.1 (Option A: Keep Legacy)

No changes needed. `Hosted:ApiKey` is still supported as fallback.

#### v1.1 (Option B: Adopt Profiles)

```json
{
  "ClientAccess": {
    "GlobalPolicy": {
      "MaxTopK": 10,
      "MaxVersionsPerLibraryPerQuery": 2,
      "MaxTargetScopesPerQuery": 4
    },
    "Profiles": {
      "my-machine": {
        "Enabled": true,
        "DisplayName": "My Machine",
        "ApiKeyRef": "MACHINE_API_KEY",
        "DefaultScopes": [
          { "Library": "rtl", "Version": "13.1" },
          { "Library": "devexpress", "Version": "25.2.6" }
        ]
      }
    }
  }
}
```

Set `MACHINE_API_KEY` environment variable or via user secrets.

### What You Need to Do

**Minimum (No-Action Upgrade):**
- Upgrade to v1.1.
- Old tools (`search_rtl`, etc.) continue working.
- No code changes required.

**Recommended (Full Migration):**
1. Update tool calls to use `search_delphi_source` and `lookup_delphi_class`.
2. Configure `ClientAccess` profiles with your machine's default libraries and versions.
3. Remove explicit `library` parameters from queries where you can use `DefaultScopes`.
4. Use `versions` parameter for version comparisons (instead of multiple tool calls).

**Deprecation Timeline:**
- v1.1: Old tools functional, deprecated in docs.
- v1.2: Old tools emit deprecation warnings; profiles required for scoped queries.
- v1.3: Old tools removed (breaking change).

---

## v1.2 Breaking Changes & Deprecation

### Overview

v1.2 deprecates library-specific tools in preparation for removal in v1.3. This gives v1.1 users time to migrate.

### What's Changing

**Library-specific tools are now deprecated:**
- `search_rtl` → Migrate to `search_delphi_source` with `library="rtl"`
- `lookup_rtl_class` → Migrate to `lookup_delphi_class` with `library="rtl"`
- `search_devexpress` → Migrate to `search_delphi_source` with `library="devexpress"`
- `lookup_devexpress_class` → Migrate to `lookup_delphi_class` with `library="devexpress"`

**What happens:**
- v1.2: Old tools still work but emit `[Obsolete]` compiler warnings and deprecation notices in logs
- v1.3 (next major): Old tools completely removed; profiles will be required

### Migration from v1.1 to v1.2

**Good news:** If you've already migrated to v1.1 unified tools, no action is required.

**If you're still using v1.0 tools:**

1. Update your code to use unified tools:
   ```csharp
   // Old (v1.0):
   var result = await mcp.CallTool("search_rtl", new { query = "TStringList", topK = 5 });
   
   // New (v1.1+):
   var result = await mcp.CallTool("search_delphi_source", new { 
       query = "TStringList", 
       library = "rtl", 
       topK = 5 
   });
   ```

2. Configure ClientAccess profiles (optional but recommended):
   - Set `DefaultScopes` in your profile
   - Omit the `library` parameter for multi-library searches

3. Test your integration against v1.2 to ensure old tool calls produce deprecation warnings
4. Plan to upgrade to v1.3+ once ready (removes old tools entirely)

### Rationale

See [ADR 0009: Library-Specific Tool Removal in v1.2](docs/decisions/0009-library-tool-removal-v1.2.md) for detailed rationale.

---

## v1.3 Breaking Changes: Library Tools Removed

### Overview

v1.3 removes library-specific tools entirely. This is a **breaking change requiring semver major version bump**.

v1.2 users have had two release cycles (v1.1 → v1.2 → v1.3) to migrate. The time for deprecation is over.

### What's Removed

The following tools are **no longer available**:
- `search_rtl` — permanently removed
- `lookup_rtl_class` — permanently removed
- `search_devexpress` — permanently removed
- `lookup_devexpress_class` — permanently removed

### What Changed

| Component | v1.2 | v1.3 |
|-----------|------|------|
| Library-specific tools | `[Obsolete]` warnings | **Removed entirely** |
| Unified tools | Available | Available (unchanged) |
| Profiles | Optional | Optional (unchanged) |
| File count | +2 tool files | -2 tool files |

### Migration Required

All v1.2 code must be updated before upgrading to v1.3:

**Before (v1.2)**:
```csharp
// These no longer work:
var rtl = await mcp.CallTool("search_rtl", new { query = "TStringList", topK = 5 });
var cls = await mcp.CallTool("lookup_rtl_class", new { className = "TStringList" });
```

**After (v1.3)**:
```csharp
// Use unified tools instead:
var rtl = await mcp.CallTool("search_delphi_source", new { 
    query = "TStringList", 
    library = "rtl", 
    topK = 5 
});
var cls = await mcp.CallTool("lookup_delphi_class", new { 
    className = "TStringList", 
    library = "rtl" 
});
```

### Deprecation Complete

See [ADR 0010: Library-Specific Tool Removal Completion in v1.3](docs/decisions/0010-library-tool-removal-completion-v1.3.md) for full rationale.

---

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

## Governance Pattern

For all `feat:`, `fix:`, and `docs:` changes, this project requires:

1. A linked GitHub issue
2. An ADR add/update under `docs/decisions/`
3. A `README.md` update reflecting user/operator impact
4. Tests where applicable and local validation

Enforcement is provided by:

- PR checklist template (`.github/pull_request_template.md`)
- CI governance gate in `.github/workflows/ci.yml` for feature/fix/docs PRs
- Copilot instructions in `.github/copilot-instructions.md`

## Status

Local-first scaffold with dual mode support:

- stdio mode for minimal local Claude Code setup
- hosted HTTP mode for centralized IIS deployment with API-key authentication

