# ADR 0010: Library-Specific Tool Removal Completion in v1.3

**Status**: Accepted (v1.3 implementation)  
**Date**: 2026-05-11  
**Deciders**: Jason Goble

## Context

In v1.2, we added `[Obsolete]` attributes and deprecation warnings to library-specific tools:
- `search_rtl`, `lookup_rtl_class`
- `search_devexpress`, `lookup_devexpress_class`

These tools were replaced by unified tools in v1.1:
- `search_delphi_source` — search any library (replaces search_rtl and search_devexpress)
- `lookup_delphi_class` — lookup class in any library (replaces lookup_rtl_class and lookup_devexpress_class)

v1.2 provided users with one full release cycle to migrate before removal. Users have now had:
- v1.1: Unified tools available alongside old tools (functional)
- v1.2: Old tools emit compiler warnings (time to migrate)
- v1.3: Old tools removed (breaking change, requires migration)

## Decision

**Remove library-specific tools entirely in v1.3** as a semver major version bump (breaking change).

### What Was Removed

1. **DelphiMcp/Tools/RtlTools.cs** — Deleted entirely
   - `search_rtl` method removed
   - `lookup_rtl_class` method removed

2. **DelphiMcp/Tools/DevExpressTools.cs** — Deleted entirely
   - `search_devexpress` method removed
   - `lookup_devexpress_class` method removed

3. **DelphiMcp.Tests/DevExpressToolsTests.cs** — Deleted entirely
   - All tests for library-specific tools removed
   - Unified tools coverage remains in UnifiedDelphiToolsTests.cs

## Implementation

### Files Removed
- `DelphiMcp/Tools/RtlTools.cs` (48 lines)
- `DelphiMcp/Tools/DevExpressTools.cs` (45 lines)
- `DelphiMcp.Tests/DevExpressToolsTests.cs` (~130 lines)

### Files Modified
- `README.md`: Add v1.3 Breaking Changes section
- `docs/decisions/README.md`: Index this ADR (0010)

### Test Results
- Build: ✅ Succeeded (`net10.0` Release, 4 warnings in Program.cs — pre-existing)
- Tests: ✅ 19/19 passing (11 existing + 8 unified tools tests; old tool tests removed)
- Functionality: ✅ All unified tools operational

## Consequences

### Positive
- **Simplified codebase**: Two fewer files, fewer methods to maintain
- **Clear API surface**: Only unified tools visible in MCP tool list
- **Reduced technical debt**: No deprecated code paths
- **Cleaner upgrade messaging**: v1.3 clearly marks breaking change

### Negative
- **Mandatory migration**: v1.2 users must migrate before upgrading to v1.3
- **No backward compatibility**: v1.2 code calling old tools will fail on v1.3
- **Breaking change**: Requires semver major version bump

## Migration Path for v1.3 Adopters

All users must complete migration to unified tools before upgrading to v1.3.

**v1.2 Code → v1.3 Code**:

```csharp
// v1.2 (old, still works):
await mcp.CallTool("search_rtl", new { query = "TStringList", topK = 5 });

// v1.3 (required):
await mcp.CallTool("search_delphi_source", new { 
    query = "TStringList", 
    library = "rtl",   // explicit library
    topK = 5 
});
```

Or with profile defaults (recommended):
```csharp
// Configure profile with DefaultScopes = [RTL, DevExpress]
await mcp.CallTool("search_delphi_source", new { 
    query = "TStringList",
    topK = 5  // searches profile's DefaultScopes
});
```

## Related Decisions

- **ADR 0007**: ClientAccess Subsystem (profile-based policy)
- **ADR 0008**: Unified Delphi Source Tools (scope resolution)
- **ADR 0009**: Library-Specific Tool Removal in v1.2 (deprecation strategy)

## References

- Issue #33: v1.3 Remove library-specific tools (breaking change)
- Issue #31 (closed): v1.2 Deprecation of library-specific tools
- v1.2.0 Release: [GitHub Release](https://github.com/JasonGoble/DelphiMcp/releases/tag/v1.2.0)

## Release Notes

**v1.3.0 Breaking Change Notice**:
```
BREAKING CHANGE: Library-specific tools removed in v1.3

The following tools are no longer available:
- search_rtl → Use: search_delphi_source(library="rtl")
- lookup_rtl_class → Use: lookup_delphi_class(library="rtl")
- search_devexpress → Use: search_delphi_source(library="devexpress")
- lookup_devexpress_class → Use: lookup_delphi_class(library="devexpress")

Users must complete migration from v1.2 before upgrading to v1.3.
See README v1.3 Breaking Changes for migration examples.
```
