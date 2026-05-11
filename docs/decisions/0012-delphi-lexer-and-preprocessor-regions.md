# ADR 0012: Delphi Lexer and Preprocessor Region Handling (v2.0 #56)

**Status**: Accepted (v2.0 Phase 1)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

v2.0 requires parser-backed Delphi understanding. After introducing contract schemas in ADR 0011, the next step is lexical analysis with conditional compilation awareness.

Delphi code frequently depends on directives such as `{$IFDEF ...}` and `{$ENDIF}` (including `(*$ ... *)` forms). Without explicit region tracking, downstream AST/symbol parsing can produce incorrect structure and unreliable AI responses.

## Decision

Implement a first-pass lexer (`DelphiLexer`) that produces:

1. token stream (`DelphiToken`)
2. conditional directive regions (`DelphiDirectiveRegion`)
3. diagnostics (`DelphiParserDiagnostic`)

The lexer supports:

- identifiers, numbers, string literals, symbols, comments, whitespace
- directive comment forms: `{$...}` and `(*$...*)`
- conditional region start directives: `IFDEF`, `IFNDEF`, `IF`, `IFOPT`
- conditional region end directives: `ENDIF`, `IFEND`
- nested conditional region handling via stack-based matching

Diagnostics:

- `DP2001`: unmatched `ENDIF/IFEND`
- `DP2002`: unclosed conditional start directive

## Rationale

1. **Parser foundation**: establishes deterministic lexical boundaries before structural parsing (#57).
2. **Directive awareness**: captures one of the most common Delphi-specific sources of parsing ambiguity.
3. **Resilience**: diagnostics allow partial-success parsing in malformed or mixed-source inputs.
4. **Compatibility**: supports both common directive comment syntaxes used in Delphi codebases.

## Consequences

### Positive

- Enables downstream parser passes to reason about active/inactive regions.
- Improves reliability for symbol extraction and structural analysis.
- Creates explicit testable behavior for directive matching.

### Negative

- First-pass lexer does not yet evaluate full expression semantics inside directives.
- Some advanced macro/include workflows remain for later phases.

## Implementation Notes

- Added: `DelphiMcp/Parsing/DelphiLexer.cs`
- Added tests: `DelphiMcp.Tests/DelphiLexerTests.cs`
- Covered behaviors:
  - basic tokenization
  - nested directive region resolution
  - unmatched end directive diagnostics
  - unclosed start directive diagnostics
  - paren-style directive comments

## Related Decisions

- ADR 0011: parse_delphi_structure Contract Schemas

## Related Work

- Issue #52: v2.0 Phase 1
- Issue #56: Implement Delphi lexer + preprocessor region handling
