# ADR 0014: Normalized Source Renderer for parse_delphi_structure (v2.0 #58)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

`parse_delphi_structure` contracts include a `NormalizedSource` output mode (ADR 0011). To make this mode useful for AI workflows, source formatting must be deterministic across equivalent input layouts.

Raw Delphi source often includes inconsistent whitespace, mixed line endings, and inline directive/comment constructs that make textual comparison and model prompting noisy.

## Decision

Implement a lexer-backed normalized source renderer (`DelphiSourceNormalizer`) with deterministic formatting rules:

1. Ignore original whitespace tokens and rebuild spacing from tokens.
2. Preserve semantics-bearing tokens (identifiers, strings, symbols, directives).
3. Emit directives on standalone lines.
4. Insert line breaks after semicolons.
5. Normalize line endings to `\n` and trim trailing blank lines.
6. Support optional comment inclusion (`includeComments` flag).

## Rationale

1. **Determinism**: equivalent source layouts normalize to identical output.
2. **Prompt quality**: cleaner source reduces token noise for AI consumption.
3. **Interoperability**: normalized output improves diffability and golden-test stability.
4. **Incremental scope**: token-based normalization avoids over-committing to full pretty-printer complexity in phase 1.

## Consequences

### Positive

- Stable normalized text for downstream parser/symbol workflows.
- Directive visibility is preserved explicitly.
- Optional comment suppression enables compact prompt payloads.

### Negative

- Formatting style is intentionally minimal and not a full Delphi formatter.
- Advanced layout preservation is deferred to later phases.

## Implementation Notes

- Added: `DelphiMcp/Parsing/DelphiSourceNormalizer.cs`
- Added tests: `DelphiMcp.Tests/DelphiSourceNormalizerTests.cs`
- Covered behaviors:
  - deterministic output across whitespace variants
  - standalone directive rendering
  - optional comment suppression
  - normalized line endings and trailing-blank-line trimming

## Related Decisions

- ADR 0011: parse_delphi_structure Contract Schemas
- ADR 0012: Delphi Lexer and Preprocessor Region Handling
- ADR 0013: Delphi Structural Parser for Unit Sections

## Related Work

- Issue #52: v2.0 Phase 1
- Issue #58: Add normalized_source renderer
