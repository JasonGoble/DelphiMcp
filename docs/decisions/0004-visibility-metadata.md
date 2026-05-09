# 0004: Visibility Metadata in Search Results

## Status
Accepted

## Context

Search results can surface private or protected declarations ahead of more usable public APIs when semantic similarity is close. In a library reference workflow, users usually need the most accessible declaration first.

The chunking pipeline previously stored no visibility metadata, so the search layer could not distinguish between published, public, protected, or private declarations.

## Decision

- Capture visibility metadata during chunking and store it with each chunk.
- Infer routine visibility from the unit section: interface declarations default to public, implementation declarations default to private.
- Infer type visibility by scanning the declaration block and selecting the most accessible visibility label present, normalizing `strict private` to `private` and `strict protected` to `protected`.
- Add a nullable `visibility` column to the SQLite `chunks` table and migrate existing databases by adding the column when missing.
- Re-rank search results using visibility-aware distance adjustments so accessible declarations are favored when semantic scores are close.
- Expose visibility in rendered search and lookup output.

## Consequences

- Search results become more usable for API discovery because accessible declarations are preferred over otherwise similar private ones.
- Existing databases remain readable after migration; rows indexed before this feature have null visibility and are treated like public results until they are reindexed.
- The chunker now uses a more accurate stopping rule for interface routine declarations, reducing over-capture into adjacent declarations.
- Ranking behavior becomes a combination of semantic similarity, namespace prioritization, and visibility accessibility.

## Related Issues
- #12 feat: Capture and expose visibility (published/public/protected/private) metadata
