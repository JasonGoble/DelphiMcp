# ADR 0011: parse_delphi_structure Contract Schemas (v2.0 Phase 1)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

DelphiMcp v2.0 introduces parser-backed source intelligence. The first implementation slice (#55) defines stable, tool-facing schemas for a future `parse_delphi_structure` endpoint before lexer/parser internals are implemented.

Delphi syntax and layout differ significantly from common C-like languages. To improve AI interpretation quality, the endpoint must expose explicit structural output rather than only raw snippets.

## Decision

Define and version parser contracts in a dedicated namespace (`DelphiMcp.Parsing`) with four output modes:

1. `AstSummary`
2. `NormalizedSource`
3. `SymbolTable`
4. `Diagnostics`

The contracts include:

- request context (`sourcePath`, `library`, `version`, selected output modes)
- limits/truncation metadata for policy-aware responses
- AST summary node/section/directive-region models
- symbol descriptor model (kind, signature, visibility, directives, relationships)
- diagnostics model (severity, code, message, span, recovery hint)
- source span model for deterministic location references

Enum serialization is string-based to preserve stable JSON readability for clients and tests.

## Rationale

1. **Contract-first delivery**: downstream parser work (#56, #57) can be built against stable schemas.
2. **AI reliability**: explicit structure reduces dependence on formatting/layout heuristics.
3. **Policy readiness**: response limits/truncation fields are built in from the start.
4. **Testability**: schema defaults and serialization behavior are validated before parser complexity is introduced.

## Consequences

### Positive

- Unblocks parallel development of lexer/parser and endpoint wiring.
- Provides a stable foundation for phase-2 symbol/version endpoints.
- Improves future migration safety by pinning schema behavior in tests.

### Negative

- Contracts may evolve as parser internals mature.
- Early schema additions increase up-front model surface area.

## Implementation Notes

- Added models: `DelphiMcp/Parsing/DelphiParserContracts.cs`
- Added tests: `DelphiMcp.Tests/DelphiParserContractsTests.cs`
- Validation includes:
  - request default stability
  - response schema version default
  - enum JSON string serialization behavior

## Related Decisions

- ADR 0007: Machine-Profile Client Access Policy
- ADR 0008: Unified Delphi Source Tools

## Related Work

- Issue #51: v2.0 Epic
- Issue #52: v2.0 Phase 1
- Issue #55: Define parser contracts and output schemas
- PR #63: initial contract and test implementation
