# 0003: Namespace Prioritization in Search

## Status
Accepted

## Context

Search results for Delphi RTL/VCL/FMX/FireDAC code often include both core library and third-party/application code. Users expect core library results (e.g., System.*, Vcl.*, FMX.*, FireDAC.*) to be prioritized when relevant, as these are most likely to be useful and canonical.

## Decision

- Retrieve an oversampled candidate pool in `SqliteVectorStore` before the final top K cutoff.
- Core namespaces (System, Vcl, FMX, FireDAC) are prioritized by multiplying their distance by a configurable boost factor (default: 0.95).
- Apply the namespace-aware rerank to the oversampled pool, then trim back to the requested top K.
- The prioritized namespace list, boost factor, and oversample factor are configurable in `appsettings.json`.
- This logic is applied consistently in both Faiss and brute-force search paths.

## Consequences

- Core library results can survive the candidate cutoff more often, improving recall for canonical results.
- The prioritization remains transparent and tunable via configuration.
- Query-time work increases slightly because search retrieves more than the final top K when prioritization is enabled.
- No impact on indexing or storage; only affects ranking at query time.

## Related Issues
- #11 feat: Prioritize System/VCL/FMX/FireDAC namespaces in search results
