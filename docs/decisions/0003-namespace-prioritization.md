# 0003: Namespace Prioritization in Search Results

## Status
Accepted

## Context

Search results for Delphi RTL/VCL/FMX/FireDAC code often include both core library and third-party/application code. Users expect core library results (e.g., System.*, Vcl.*, FMX.*, FireDAC.*) to be prioritized when relevant, as these are most likely to be useful and canonical.

## Decision

- Add a post-query re-ranking step to search results in `SqliteVectorStore`.
- Core namespaces (System, Vcl, FMX, FireDAC) are prioritized by multiplying their distance by a configurable boost factor (default: 0.95).
- The prioritized namespace list and boost factor are configurable in `appsettings.json`.
- This logic is applied after both Faiss and brute-force search paths.
- Overhead is negligible (<1ms for typical result sets).

## Consequences

- Core library results will appear higher in search results when present.
- The prioritization is transparent and can be tuned via configuration.
- No impact on indexing or storage; only affects ranking at query time.

## Related Issues
- #11 feat: Prioritize System/VCL/FMX/FireDAC namespaces in search results
