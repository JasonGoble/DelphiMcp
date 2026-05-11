# ADR 0008: Unified Delphi Source Tools

**Status:** Accepted (implemented in v1.1)
**Date:** May 2026

## Context

v1.0 provided library-specific MCP tools: `search_rtl`, `lookup_rtl_class`, `search_devexpress`, `lookup_devexpress_class`.

This approach worked well for single-library queries but became verbose when clients needed to:
- Search across multiple libraries without hand-coding library switches
- Enforce per-machine (profile-based) default scopes and query policies
- Perform version comparisons without API redesign

v1.1 introduces the **ClientAccess subsystem** (ADR 0007) for machine profiles and query policy enforcement.
Unified tools build on that foundation to provide a simpler, more consistent query API.

## Decision

Implement two unified tools that replace the four library-specific tools:

1. **`search_delphi_source`** — Searches Delphi source code across RTL, VCL, FMX, DevExpress, and other libraries.
   - When `library` is specified: searches that library only.
   - When `library` is omitted: searches using resolved client profile's `DefaultScopes`.
   - Optional `version` filter applies to the selected library scope(s).
   - Respects policy limit `MaxTopK`.

2. **`lookup_delphi_class`** — Looks up Delphi class/type declarations.
   - When `library` is specified: searches that library only.
   - When `library` is omitted: searches using resolved client profile's `DefaultScopes`.
   - Optional single `version` or multiple `versions` (comma-separated) for comparison.
   - Multi-version lookup returns formatted side-by-side declarations.
   - Respects policy limit `MaxVersionsPerLibraryPerQuery`.

## Rationale

### Unified vs. Library-Specific

| Aspect | Library-Specific (v1.0) | Unified (v1.1) |
|--------|---------|---------|
| **Search RTL + DevExpress** | Two tool calls | One tool call + profile `DefaultScopes` |
| **Version comparison** | Not supported | Supported via `versions` parameter |
| **Profile scopes** | N/A | Automatically used when no library specified |
| **Policy enforcement** | Per-tool limits | Centralized in ClientProfileResolver |
| **Migration surface** | v1.0 clients need rewrite | v1.0 clients can continue using old tools or migrate |

### Scope Resolution

When `library` is omitted:
1. Try to resolve client profile from HTTP context (requires `IHttpContextAccessor`).
2. If resolved, use profile's `DefaultScopes`.
3. If no profile (e.g., stdio mode), fall back to searching common libraries (RTL + DevExpress).

This preserves the familiar behavior for stdio users while enabling profile-based scoping for hosted clients.

### Multi-Version Comparison

`lookup_delphi_class` with comma-separated `versions` parameter:
- Parses versions and validates count against policy `MaxVersionsPerLibraryPerQuery`.
- Retrieves each version's class declaration.
- Formats results with per-version headers for easy side-by-side review.
- Enables spotting behavioral or API changes across versions without manual lookup repetition.

## Implementation

### Tool Parameters

#### `search_delphi_source`
- `query` (required): Natural language or symbol name, e.g., "TStringList thread safety"
- `library` (optional): Library name (e.g., "rtl", "devexpress"). Omit to use profile scopes.
- `version` (optional): Single version filter (e.g., "12.0", "11.0").
- `topK` (optional): Number of results (default 5, max enforced by policy).
- **Returns**: Ranked results with unit, identifier, chunk type, visibility, library, version, line number.

#### `lookup_delphi_class`
- `className` (required): Class name to look up, e.g., "TStringList".
- `library` (optional): Library name. Omit to use profile scopes.
- `version` (optional): Single version filter.
- `versions` (optional): Comma-separated versions for comparison, e.g., "12.0,11.0".
- **Returns**: Class declaration or multi-version formatted comparison.

### Profile Integration

- `search_delphi_source` and `lookup_delphi_class` receive `IHttpContextAccessor` for optional context.
- During tool invocation, the resolver retrieves the resolved profile from `HttpContext.Items[ClientProfileResolver.HttpContextItemKey]`.
- Policy limits applied: `topK` clamped to `MaxTopK`, version count validated against `MaxVersionsPerLibraryPerQuery`.
- If no HTTP context, tools operate in fallback mode (stdio).

### Backward Compatibility

- Old library-specific tools (`search_rtl`, etc.) remain available in v1.1 for migration grace period.
- They will be deprecated (marked as legacy) but functional.
- v1.1 clients should adopt unified tools; v1.0 clients can upgrade without immediate rewrite.

## Consequences

### Positive

- **Simpler API**: Two tools instead of four (easier to document, teach, test).
- **Scope automation**: Profile `DefaultScopes` handle multi-library queries without tool chaining.
- **Version comparison**: Clients can compare versions in a single tool invocation.
- **Policy consistency**: All queries flow through the same resolver and policy engine.
- **HTTP + Stdio**: Works in both modes with graceful fallback.

### Negative

- **Tool removal**: Old library-specific tools eventually deprecated (v1.2 or later).
- **Learning curve**: Existing Claude Code users need to learn new tool names/signatures.
- **Profile dependency**: Unscoped queries now depend on profile resolution (may be confusing if profile not configured).

## Migration (v1.0 → v1.1)

### For Existing v1.0 Users

1. **Option A (Minimal)**: Continue using old tools.
   - `search_rtl`, `lookup_rtl_class`, `search_devexpress`, `lookup_devexpress_class` still work.
   - No immediate action required.

2. **Option B (Recommended)**: Adopt unified tools.
   - Replace `search_rtl` and `search_devexpress` calls with `search_delphi_source`.
   - Replace `lookup_rtl_class` and `lookup_devexpress_class` calls with `lookup_delphi_class`.
   - For multi-library searches, omit `library` and configure profile `DefaultScopes`.

### Configuration Changes

- v1.0: No client configuration needed (anonymous stdio mode).
- v1.1 (hosted): Configure `ClientAccess` profiles with `ApiKeyRef`, `DefaultScopes`, and optional policy overrides.
- v1.1 (stdio): No changes; tools work with fallback scopes.

### Example Migration

**v1.0 code:**
```
Tool 1: search_rtl(query="TStringList", topK=5)
Tool 2: search_devexpress(query="TStringList", topK=5)
→ Combine results manually
```

**v1.1 code:**
```
Tool 1: search_delphi_source(query="TStringList", topK=5)
→ Automatically searches both RTL + DevExpress (if configured in profile DefaultScopes)
```

## Related

- **ADR 0007**: Machine-profile client access policy — provides profile resolver, policy enforcement, scope models
- **Issue #26**: ClientAccess subsystem implementation
- **Issue #27**: Unified Delphi tools implementation

## References

- [README.md](../README.md#mcp-tools-v11): Updated tool documentation with unified signatures
- [ClientAccess Configuration](../README.md#clientaccess-example): Profile setup examples
