# Manual Testing Artifacts

This folder contains testing results from the Faiss vector search implementation (issue #9).

## Contents

### Comparison Results
- **comparison-results.md** — Detailed side-by-side ranked search results for 14 test queries comparing:
  - Ollama embedder (nomic-embed-text, 768-dim) indexed on RTL 13.1
  - OpenAI embedder (text-embedding-3-small, 1536-dim) indexed on RTL 12.3

Each query includes top-5 ranked results per embedder with distance scores and manual rating sections.

### Indexing Logs

#### RTL 13.1 - Ollama (nomic-embed-text)
- `reindex-rtl-13.1-20260508-232117.log` — Initial indexing run
- `reindex-rtl-13.1-20260508-232131.log` — Production indexing (194,921 chunks, ~8h)

#### RTL 13.1 - OpenAI (text-embedding-3-small)
- `reindex-rtl-13.1-openai-20260509-080947.log` — Test indexing
- `reindex-rtl-13.1-openai-20260509-080954.log` — Production indexing

#### RTL 12.3 - OpenAI (text-embedding-3-small)
- `reindex-rtl-12.3-openai-20260509-081215.log` — Test indexing
- `reindex-rtl-12.3-openai-20260509-081250.log` — Production indexing (185,624 chunks, ~1h)

## Key Findings

- **Performance**: Ollama ~52.69ms average vs OpenAI ~96.59ms average per query
- **Semantic Space**: 0% result overlap expected (different embedders encode different semantic spaces)
- **Quality Assessment**: See comparison-results.md for manual evaluation

## Testing Methodology

1. **Indexing**: Full RTL library versions indexed with respective embedders
2. **Benchmarking**: 14 standardized Delphi RTL queries tested 100 iterations each
3. **Comparison**: Top-5 results compared side-by-side for manual quality assessment

## Usage

Review `comparison-results.md` for detailed rankings and analysis.
