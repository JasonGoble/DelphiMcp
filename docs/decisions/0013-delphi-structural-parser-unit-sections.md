# ADR 0013: Delphi Structural Parser for Unit Sections (v2.0 #57)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

v2.0 phase 1 requires parser-backed understanding of Delphi unit structure. After contracts (ADR 0011) and lexer/directive handling (ADR 0012), the next step is structural extraction of unit sections and key declarations.

Delphi analysis quality depends on recognizing section boundaries (`interface`, `implementation`, `initialization`, `finalization`), scoped `uses` clauses, and type declarations in `type` blocks.

## Decision

Implement a first structural parser (`DelphiStructuralParser`) that consumes lexer output and produces `DelphiAstSummary` data using existing parser contracts.

Implemented extraction scope:

1. Unit name (`unit <Name>;`)
2. Section discovery:
   - interface
   - implementation
   - initialization
   - finalization
3. `uses` clauses per section (including `in 'path'` handling)
4. Type declaration nodes in `type` blocks:
   - class
   - record
   - interface

Diagnostics include missing unit-name detection (`DP3001`). Lexer diagnostics remain propagated.

## Rationale

1. **Foundation for deeper parsing**: section and type structure is required by later symbol and version-intelligence endpoints.
2. **Delphi-specific correctness**: section-scoped `uses` handling reduces context ambiguity compared to plain text search.
3. **Incremental delivery**: provides measurable structure now while leaving advanced constructs (helpers, generics, full members) for follow-up tasks.

## Consequences

### Positive

- `AstSummary` now includes concrete structural nodes and section metadata.
- Enables subsequent work for symbol extraction and semantic analysis.
- Improves AI interpretation of unit context boundaries.

### Negative

- First pass is intentionally conservative and does not fully parse all Delphi grammar constructs.
- Advanced `type` variants and nested declarations may require additional passes.

## Implementation Notes

- Added: `DelphiMcp/Parsing/DelphiStructuralParser.cs`
- Added tests: `DelphiMcp.Tests/DelphiStructuralParserTests.cs`
- Covered behaviors:
  - unit/section extraction
  - section-scoped uses extraction
  - class/record/interface declaration recognition in type blocks
  - missing unit diagnostics

## Related Decisions

- ADR 0011: parse_delphi_structure Contract Schemas
- ADR 0012: Delphi Lexer and Preprocessor Region Handling

## Related Work

- Issue #52: v2.0 Phase 1
- Issue #57: Structural parser for Delphi unit sections
