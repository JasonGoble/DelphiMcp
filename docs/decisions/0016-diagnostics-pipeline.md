# ADR 0016: Diagnostics Pipeline for Parser Output (v2.0 #60)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

The parser pipeline now has multiple diagnostics sources:

1. lexer diagnostics (ADR 0012)
2. structural parser diagnostics (ADR 0013)
3. future extractor and endpoint validation diagnostics

Without a shared pipeline, diagnostics can become duplicated, unordered, and difficult to truncate consistently for tool responses.

## Decision

Introduce `DelphiDiagnosticsPipeline` as the shared aggregation layer for parser diagnostics.

Pipeline responsibilities:

1. deduplicate diagnostics using stable semantic keys
2. order diagnostics deterministically
   - severity: Error > Warning > Info
   - then source span
   - then code
3. compute summary metadata:
   - total count
   - returned count
   - severity counts
   - truncation flag
4. enforce configurable maximum returned diagnostics

The structural parser now returns:

- filtered `Diagnostics`
- `DiagnosticsReport` containing counts and truncation metadata

## Rationale

1. **Consistency**: all parser stages can flow through one diagnostic policy.
2. **Determinism**: stable ordering improves tests and client expectations.
3. **Response safety**: truncation can be enforced before endpoint wiring (#62).
4. **Observability**: summary counts expose useful status without requiring clients to inspect every entry.

## Consequences

### Positive

- Stable, deduplicated diagnostics for future endpoint responses.
- Easier integration with policy limits from parser contracts.
- Better support for golden tests and parser regression triage.

### Negative

- Introduces an additional internal result object to maintain.
- Downstream components must opt into the pipeline to receive normalized diagnostics.

## Implementation Notes

- Added: `DelphiMcp/Parsing/DelphiDiagnosticsPipeline.cs`
- Updated: `DelphiMcp/Parsing/DelphiStructuralParser.cs`
- Added tests: `DelphiMcp.Tests/DelphiDiagnosticsPipelineTests.cs`
- Covered behaviors:
  - deduplication
  - deterministic ordering
  - truncation handling
  - structural parser integration

## Related Decisions

- ADR 0011: parse_delphi_structure Contract Schemas
- ADR 0012: Delphi Lexer and Preprocessor Region Handling
- ADR 0013: Delphi Structural Parser for Unit Sections
- ADR 0014: Normalized Source Renderer for parse_delphi_structure
- ADR 0015: Symbol Table Extraction from Parsed Delphi Structure

## Related Work

- Issue #52: v2.0 Phase 1
- Issue #60: Implement diagnostics pipeline
