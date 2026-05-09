# ADR 0002: Faiss Native Vector Search Integration

## Status
Accepted

## Date
2026-05-08

## Context
Issue 9 requires replacing brute-force in-memory KNN with a Faiss-based approach to improve scalability and keep search latency predictable as chunk counts grow.

The previous implementation loaded all embeddings into memory and computed cosine similarity with a full scan on every query.

## Decision
Use Faiss.NET native packages and a CPU index with custom ID mapping:

1. Package strategy:
   - `Faiss.Net.Native` (meta/runtime package)
   - `Faiss.Net.Native.Windows` (native binaries for Windows runtime)
2. Vector index type:
   - `IndexFlatIP` wrapped in `IndexIDMap<IndexFlatIP>`
   - vectors are normalized before insertion and query, so inner product equals cosine similarity
3. Persistence strategy:
   - serialize Faiss index to disk per `(library, version)` using `IndexSerializer.Write`
   - attempt to load existing index using `IndexDeserializer.Read<GenericIndex>`
4. Reliability strategy:
   - keep a brute-force fallback path if Faiss load/build/search fails
5. Configuration:
   - `Storage:FaissIndexDir` for explicit index location
   - default fallback path: sibling `faiss-indexes` folder next to configured DB file

## Consequences
- Query path shifts from full scan to Faiss index search.
- Search remains operational even if Faiss cannot load (fallback behavior).
- On-disk index files are created and reused for faster startup/query behavior.
- Current index choice is exact (Flat IP), prioritizing correctness before approximate indexes.

## Follow-ups
1. Add telemetry/logging around Faiss load/build fallback events.
2. Benchmark Flat IP vs HNSW/IVF variants on representative corpora.
3. Add integration tests covering index serialization/deserialization and ID mapping correctness.
