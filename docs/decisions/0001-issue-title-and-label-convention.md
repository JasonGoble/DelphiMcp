# ADR 0001: Issue Title Prefix and Label Convention

## Status
Accepted

## Date
2026-05-08

## Context
The project needs a consistent issue workflow so planning and execution are easy to scan in GitHub.
We want:
- predictable issue title prefixes
- matching type labels
- assignment at the start of implementation
- optional component labeling when useful

## Decision
Adopt the following issue convention for DelphiMcp:

1. Use a title prefix that reflects issue intent.
2. Preferred prefixes are examples, not an exclusive list.
3. Default examples:
   - `feat:` for feature work
   - `bug:` for defects
   - `test:` for test-focused work
   - `docs:` for documentation work
4. Other prefixes are allowed when they better match intent (for example `chore:` or `refactor:`).
5. Add a matching type label for the chosen prefix using existing repository labels:
   - `feat:` -> `enhancement`
   - `bug:` -> `bug`
   - `test:` -> `testing`
   - `docs:` -> `documentation`
6. Keep existing phase and component labels (`phase-*`, `indexing`, `quality`, etc.).
7. Assign the issue to Jason when implementation begins.
8. Record notable technical decisions as decision documents during implementation.

## Consequences
- Improves triage and dashboard readability.
- Makes issue type visible from title and labels.
- Preserves project phase/component context without replacing it.
- Creates a repeatable preflight checklist for issue updates.

## Preflight Checklist
Before creating or updating an issue:
1. Title has an appropriate prefix.
2. Matching type label is present.
3. Assignee is set when work starts.
4. Component/phase labels are present when obvious.
