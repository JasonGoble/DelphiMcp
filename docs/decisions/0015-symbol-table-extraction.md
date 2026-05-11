# ADR 0015: Symbol Table Extraction from Parsed Delphi Structure (v2.0 #59)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

`parse_delphi_structure` includes `SymbolTable` output mode in its schema contracts (ADR 0011). With lexer/directive handling (ADR 0012), unit section parsing (ADR 0013), and normalized source rendering (ADR 0014), the next step is converting parsed structure into symbol descriptors.

## Decision

Implement `DelphiSymbolExtractor` as a dedicated extraction layer that transforms `DelphiAstSummary` nodes into `DelphiSymbolDescriptor` entries.

Initial extraction scope:

1. unit symbol
2. class declarations
3. record declarations
4. interface declarations
5. generic type declaration fallback (`TypeDeclaration`)
6. helper/method/property node mapping support for future parser passes

Each symbol includes stable fields from contracts where available:

- `Name`
- `Kind`
- `UnitName`
- `Signature`
- `DeclaringType` (resolved via parent-node walk)
- `Visibility` (inferred from node modifiers when present)
- `Span`

## Rationale

1. **Separation of concerns**: keeps extraction logic decoupled from section parser implementation.
2. **Incremental enrichment**: parser can add deeper members later while extractor remains stable.
3. **Contract alignment**: directly populates fields defined in ADR 0011 schemas.
4. **Phase-2 readiness**: provides base symbol inventory for upcoming `resolve_symbol` and references work.

## Consequences

### Positive

- Establishes explicit symbol pipeline from AST to tool-facing output.
- Simplifies test coverage for symbol shape and deterministic signatures.
- Supports extension without breaking existing contracts.

### Negative

- Current extraction does not yet parse full method/property member bodies.
- Visibility and inheritance details depend on upcoming parser depth.

## Implementation Notes

- Added: `DelphiMcp/Parsing/DelphiSymbolExtractor.cs`
- Added tests: `DelphiMcp.Tests/DelphiSymbolExtractorTests.cs`
- Covered behaviors:
  - unit + type symbol extraction
  - unit name propagation to symbols
  - stable signature generation for extracted types

## Related Decisions

- ADR 0011: parse_delphi_structure Contract Schemas
- ADR 0012: Delphi Lexer and Preprocessor Region Handling
- ADR 0013: Delphi Structural Parser for Unit Sections
- ADR 0014: Normalized Source Renderer for parse_delphi_structure

## Related Work

- Issue #52: v2.0 Phase 1
- Issue #59: Add symbol_table extraction
