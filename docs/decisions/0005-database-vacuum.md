# ADR 0005: Database Vacuum Command

**Status:** Accepted

**Date:** 2026-05-09

## Context

The SQLite database used by DelphiMcp can accumulate unused space over time, particularly after bulk operations like `--reset` that delete large numbers of chunks. SQLite's `VACUUM` command rebuilds the database file, reclaiming this unused space and potentially improving query performance.

Currently, manual vacuuming requires running SQLite CLI commands outside the application. This is inconvenient for users who want to compact the database as part of their maintenance workflow.

## Decision

Add a `--vacuum` command-line switch to DelphiMcp that executes SQLite's `VACUUM` operation on the active database. The command runs independently without requiring `--library` or other library-specific parameters.

### Implementation Details

- **Command**: `dotnet run -- --vacuum` (or `DelphiMcp.exe --vacuum`)
- **No parameters required**: The command operates on the entire database file
- **Async execution**: Uses `async/await` with `ExecuteNonQueryAsync()` to match the application's async patterns
- **Output**: Confirms success with "Database vacuumed successfully."

## Rationale

1. **Convenience**: Users can compact the database without leaving the application context or using external tools
2. **Workflow integration**: Enables building efficient maintenance scripts (reset → vacuum → reindex)
3. **Performance**: Reclaimed space can improve query performance on large datasets
4. **Simplicity**: Single, focused responsibility—no side effects or complex logic
5. **Consistency**: Follows the same CLI pattern as `--reset`, `--index`, and `--bench-search`

## Alternatives Considered

1. **Automatic vacuuming during indexing**: Could run VACUUM after each reindex
   - Rejected: VACUUM can be slow; better to let users decide when to run it
2. **Periodic background vacuuming**: Scheduled task in MCP server mode
   - Rejected: Out of scope for this release; can be added later if needed
3. **Configuration-driven option**: Allow users to enable/disable VACUUM on reset
   - Rejected: Overcomplicates the reset operation; separate command is clearer

## Consequences

- Users gain explicit control over database compaction timing
- Slightly larger code footprint (1 new method in SqliteVectorStore, 1 CLI handler)
- No performance impact when not in use
- Future enhancements (e.g., automatic VACUUM after large deletes) are easy to add

## Notes

- VACUUM locks the database briefly; should not run during concurrent searches
- In MCP-over-HTTP scenarios, the server process is the only writer, so blocking is acceptable
- Large databases may take several seconds to vacuum; consider running during off-hours
