# ADR 0006: Dual-Mode MCP Hosting with API Key Authentication

- Status: Accepted
- Date: 2026-05-10

## Context

DelphiMcp serves large indexed source data (SQLite + optional Faiss indexes). Rebuilding this dataset on every developer machine is expensive in time and storage.

At the same time, local developer usage with Claude Code should remain low friction.

We need a deployment model that supports both:
- minimal local setup for development
- centralized hosted access for shared production-like usage

## Decision

Adopt a dual-mode hosting strategy:

1. `stdio` mode remains the default startup mode.
- This keeps Claude Code local setup minimal and backward compatible.

2. Add hosted HTTP MCP mode for centralized deployment.
- Enabled with `--http` or `Server:Mode=http`.
- Intended for IIS-compatible ASP.NET Core hosting.

3. Add global API key authentication for hosted mode.
- All authenticated users have full server access.
- No per-library authorization partitioning.
- Accepted headers:
  - `Authorization: Bearer <api-key>`
  - `X-API-Key: <api-key>`

4. Add hosted hardening options.
- Optional HTTPS redirect (`Hosted:RequireHttps`).
- Unauthorized request logging for hosted MCP paths.

## Rationale

- Preserves existing local workflows without additional setup burden.
- Enables centralized hosting so large databases are generated once and shared.
- API key auth is simpler to operate than external identity provider integration for this use case.
- Header duality improves client interoperability where only one auth header style may be configurable.

## Consequences

Positive:
- Faster onboarding for local Claude Code users (stdio unchanged).
- Central hosting reduces duplicate indexing/storage cost.
- Operational control is improved via API key rotation and centralized backups.

Trade-offs:
- Hosted mode introduces deployment and secret management overhead.
- API key auth is coarse-grained and does not provide user-level identity.

## Operational Notes

- Keep hosted API keys in environment variables, not source-controlled config.
- Use HTTPS in hosted environments.
- Back up DB and Faiss index artifacts together.
- Use the hosted smoke test script for deployment validation.
