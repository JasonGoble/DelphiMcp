# ADR 0007: Machine-Profile Client Access Policy

- Status: Accepted
- Date: 2026-05-10
- Related Issue: #26

## Context

Hosted MCP mode originally authenticated with a single global API key (`Hosted:ApiKey`).

For v1.1 planning, we need machine-specific defaults so different client machines can query different default library/version scopes without changing server code. We also need server-enforced performance limits that apply consistently across clients.

## Decision

Adopt a machine-profile client access model for hosted authentication and default scope policy:

1. Use 1:1 API key to profile resolution.
- Each enabled profile defines `ApiKeyRef` (preferred) or `ApiKey`.
- The incoming API key resolves to exactly one profile.

2. Introduce `ClientAccess:GlobalPolicy` for shared defaults.
- `MaxTopK`
- `MaxVersionsPerLibraryPerQuery`
- `MaxTargetScopesPerQuery`
- `AllowUnversionedQueries`
- `RequireVersionWhenLibrarySpecified`

3. Allow per-profile overrides via `ClientAccess:Profiles:<id>:Options`.
- Profile overrides are merged over global policy.

4. Require `DefaultScopes` per enabled profile.
- Generic unscoped queries resolve to these defaults.
- Library-only queries can expand through defaults and policy logic.

5. Validate configuration at startup.
- At least one enabled profile is required in profile-auth mode.
- Every enabled profile must resolve to a non-empty API key.
- Resolved API keys must be unique.
- `DefaultScopes` must be non-empty and de-duplicated.

6. Keep legacy `Hosted:ApiKey` as fallback only.
- If profile config is missing/invalid but `Hosted:ApiKey` is set, hosted mode continues with legacy behavior.
- This reduces operational break risk during migration.

## Rationale

- Machine profiles make default query behavior explicit and maintainable.
- 1:1 key/profile mapping avoids hidden indirection.
- Global policy with profile overrides centralizes performance controls while supporting per-machine tuning.
- Startup validation prevents ambiguous or unsafe runtime behavior.

## Consequences

Positive:
- Hosted authentication now supports per-machine policy and defaults.
- Query scope policy is explicit and testable.
- Configuration errors surface at startup instead of during requests.

Trade-offs:
- More configuration complexity than a single global key.
- Operators must manage per-profile secrets.
- Legacy and profile auth modes now coexist during migration.

## Operational Notes

- Prefer `ApiKeyRef` resolved from environment variables or secret stores.
- Avoid storing plaintext API keys in source-controlled config.
- Use `DefaultScopes` to represent installed machine defaults.
- Keep policy limits conservative first, then tune based on observed latency.
