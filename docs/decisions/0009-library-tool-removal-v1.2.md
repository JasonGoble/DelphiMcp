# ADR 0009: Library-Specific Tool Removal in v1.2

**Status**: Accepted (v1.2 planned)  
**Date**: 2026-05-10  
**Deciders**: Jason Goble

## Context

In v1.0, MCP tools were organized by source library:
- `search_rtl`, `lookup_rtl_class` — Delphi RTL/VCL/FMX
- `search_devexpress`, `lookup_devexpress_class` — DevExpress components

In v1.1, we introduced unified tools:
- `search_delphi_source` — search any library (with optional `library` parameter)
- `lookup_delphi_class` — lookup class in any library (with optional `library` parameter)

The unified tools:
1. Provide a simpler, more consistent API (2 tools instead of 4)
2. Support automatic scope resolution via user profiles
3. Enable multi-version class comparison in a single query
4. Centralize policy enforcement (MaxTopK, MaxVersionsPerLibraryPerQuery)

Library-specific tools are now redundant and complicate maintenance.

## Decision

**Remove library-specific tools in v1.2** and enforce exclusive use of unified tools.

### Rationale

1. **Reduced cognitive load**: Clients learn one tool pattern instead of four
2. **Consistent policy enforcement**: All queries flow through unified resolver
3. **Maintenance**: One code path to test, document, and evolve
4. **Migration window**: v1.1 provided a full release cycle for clients to migrate

### Migration Strategy

- **v1.1** (released): Unified tools available; library-specific tools functional but deprecated ([Obsolete] attributes added)
- **v1.2** (now): Library-specific tools emit deprecation warnings; removal code committed but not executed
- **v1.3** (future): Library-specific tool classes removed entirely (breaking change, semver major bump)

This gives v1.1 users three release cycles to migrate before removal:
1. v1.1 → v1.2: Warnings visible in tooling/logs
2. v1.2 → v1.3: Hard removal (clients must have migrated)

## Implementation

### v1.2 Changes
1. Add `[Obsolete(...)]` attributes to library-specific tool methods with migration guidance
2. Update method descriptions to recommend unified tools
3. Update README with v1.2 Breaking Changes section
4. Document that library-specific tools still function (for backward compatibility)
5. Provide clear v1.1 → v1.2 migration examples in README

### v1.3 Changes (Future)
1. Delete `DelphiMcp/Tools/RtlTools.cs` entirely
2. Delete `DelphiMcp/Tools/DevExpressTools.cs` entirely
3. Update tests to remove v1.0-specific test suite for these tools
4. Update README to remove v1.1 → v1.2 migration guidance

## Consequences

### Positive
- Simpler, more maintainable codebase (fewer tool implementations)
- Clearer upgrade path for clients
- Consistent policy enforcement across all queries
- Easier to add new libraries in the future (only add to unified tools)

### Negative
- Breaking change for v1.1 clients who haven't migrated (they must upgrade to v1.3+ or stay on v1.1)
- Deprecation warnings in v1.1 and v1.2 may cause alarm (mitigated by clear migration guide)

## Migration Examples

### v1.0 → v1.1 → v1.2 Path (Current)

**v1.0 Code** (library-specific):
```csharp
// Search RTL
var result = await mcp.CallTool("search_rtl", new { query = "TStringList", topK = 10 });

// Search DevExpress
var result = await mcp.CallTool("search_devexpress", new { query = "cxGrid", topK = 5 });
```

**v1.1+ Code** (unified with explicit library):
```csharp
// Search RTL
var result = await mcp.CallTool("search_delphi_source", new { 
    query = "TStringList", 
    library = "RTL", 
    topK = 10 
});

// Search DevExpress
var result = await mcp.CallTool("search_delphi_source", new { 
    query = "cxGrid", 
    library = "DevExpress", 
    topK = 5 
});
```

**v1.1+ Code** (unified with profile defaults):
```csharp
// If profile.DefaultScopes = ["RTL", "DevExpress"], 
// omit library to search both:
var result = await mcp.CallTool("search_delphi_source", new { 
    query = "TStringList", 
    topK = 10  // searches RTL + DevExpress per profile
});
```

**v1.2 Code** (no change for v1.1 code):
```csharp
// v1.1 code still works; library-specific tools emit warnings
// v1.1 users should migrate to v1.1+ code patterns above before v1.3
```

## Related Decisions

- **ADR 0007**: ClientAccess Subsystem (profile-based policy)
- **ADR 0008**: Unified Delphi Source Tools (scope resolution algorithm)

## References

- Issue #31: v1.2 library-specific tool removal
- Issue #22 (Epic): v1.1 Unified Source Tools + Machine Profile Policy (now closed)
