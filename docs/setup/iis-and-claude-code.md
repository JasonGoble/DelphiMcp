# IIS + Claude Code Setup

This guide describes the hosted deployment path for DelphiMcp when you want to build the large index once and serve it centrally.

**Updated for v1.1+**: This guide uses the ClientAccess subsystem with machine profiles for per-API-key isolation and policy enforcement. See [ADR 0007](../decisions/0007-machine-profile-client-access-policy.md) for design rationale.

## Overview

- Local default mode remains stdio (`dotnet run`) for minimal setup.
- Hosted mode uses HTTP MCP (`dotnet run -- --http`) and requires client authentication.
- **v1.1+ model**: Machine profiles + API key to profile mapping.
- Authentication via HTTP header:
  - `Authorization: Bearer <api-key>` (recommended)
  - `X-API-Key: <api-key>` (alternative)

## 1. Build and Prepare Artifacts

1. Build and publish the app:

```powershell
cd E:\code\DelphiMcp\DelphiMcp
dotnet publish -c Release -o E:\deploy\DelphiMcp
```

2. Ensure your centralized index assets are in stable locations:
- SQLite DB file (for example `E:\data\delphi-mcp\delphi-mcp.db`)
- Faiss index folder (for example `E:\data\delphi-mcp\faiss-indexes`)

3. Copy publish output to the IIS server deployment folder.

## 2. IIS Prerequisites

1. Install IIS.
2. Install the .NET ASP.NET Core Hosting Bundle matching your runtime.
3. Create an IIS App Pool:
- `.NET CLR version`: No Managed Code
- `Managed pipeline`: Integrated
4. Create an IIS Site (or Application) pointing to the published app folder.
5. Add HTTPS binding and certificate.

## 3. Configure ClientAccess & Environment (IIS Site/App)

### Environment Variables

Set these environment variables for the IIS process:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Storage__DbPath=E:\data\delphi-mcp\delphi-mcp.db`
- `Storage__FaissIndexDir=E:\data\delphi-mcp\faiss-indexes`
- `Embedder__Provider=OpenAI` or `Ollama`
- `OpenAI__ApiKey=<openai-key>` (if OpenAI)
- `Ollama__BaseUrl` and `Ollama__Model` (if Ollama)

Recommended: Keep API keys for embedders out of source control via user secrets or secure environment variables.

### Machine Profiles Configuration

Configure machine profiles and API key mapping via `appsettings.json` or `appsettings.Production.json`:

```json
{
  "ClientAccess": {
    "GlobalPolicy": {
      "MaxTopK": 50,
      "MaxVersionsPerLibraryPerQuery": 3,
      "MaxTargetScopesPerQuery": 4,
      "AllowUnversionedQueries": true
    },
    "Profiles": {
      "default": {
        "Enabled": true,
        "DisplayName": "Default Machine",
        "ApiKeyRef": "DEFAULT_API_KEY",
        "DefaultScopes": [
          "rtl",
          "devexpress"
        ]
      },
      "team-machine": {
        "Enabled": true,
        "DisplayName": "Team Machine",
        "ApiKeyRef": "TEAM_MACHINE_API_KEY",
        "DefaultScopes": [
          "rtl",
          "devexpress"
        ],
        "Policy": {
          "MaxTopK": 20,
          "MaxVersionsPerLibraryPerQuery": 2
        }
      }
    },
    "ApiKeyToProfile": {
      "key_abc123": "default",
      "key_team456": "team-machine"
    }
  }
}
```

Then set the API key environment variables:

- `DEFAULT_API_KEY=<strong-random-key-1>`
- `TEAM_MACHINE_API_KEY=<strong-random-key-2>`

### What This Configuration Means

- **Global policy**: Applied to all profiles by default
- **Per-profile overrides**: `MaxTopK`, `MaxVersionsPerLibraryPerQuery` can be overridden per profile
- **DefaultScopes**: Automatic multi-library searches (e.g., RTL + DevExpress with omitted `library` parameter)
- **ApiKeyToProfile mapping**: Maps incoming API key to profile for policy enforcement

### Security Best Practices

1. Keep `appsettings.json` with profile structure but NO keys in source control
2. Use environment variables or secure configuration for `ApiKeyRef` values
3. Use strong random keys (min 32 characters recommended)
4. Rotate API keys periodically
5. Restrict file ACLs so only the app pool identity can read config

## 4. Permissions

Grant the IIS app pool identity read/write access to:
- App deployment folder
- Database folder
- Faiss index folder

Without correct ACLs, hosted mode may start but fail at runtime when loading the vector store.

## 5. Run and Verify

1. Start/restart the IIS site.
2. Verify health endpoint:

```powershell
curl https://<host>/healthz
```

3. Verify MCP endpoint requires auth:

```powershell
curl -i https://<host>/mcp
```

Expected: `401 Unauthorized`.

4. Verify authenticated request (header accepted):

```powershell
curl -i https://<host>/mcp -H "Authorization: Bearer <api-key>"
```

5. Run smoke test script:

```powershell
cd E:\code\DelphiMcp
./scripts/hosted-smoke-test.ps1 -BaseUrl "https://<host>" -ApiKey "<api-key>" -McpPath "/mcp"
```

## 6. Claude Code Setup

Configure Claude Code to use one of these paths:

### Local Minimal Setup (Recommended for Local Dev)

Configure Claude Code to launch the local executable via stdio:

- Command: `DelphiMcp` (or full path to the exe, e.g., `E:\code\DelphiMcp\DelphiMcp\bin\Release\net10.0\DelphiMcp.exe`)
- No API key required
- Tools available: `search_delphi_source`, `lookup_delphi_class` (unified tools v1.1+)
- Scope resolution: Falls back to common libraries (RTL + DevExpress) in stdio mode
- **Lowest friction path for local development**

### Hosted Centralized Setup

Configure Claude Code MCP server URL and authentication:

- **Base URL**: `https://<host>/mcp`
- **Authentication header** (recommended): `Authorization: Bearer <api-key>`
- **Alternative header**: `X-API-Key: <api-key>`
- **Tools available**: `search_delphi_source`, `lookup_delphi_class` (unified tools v1.1+)
- **Scope resolution**: Uses resolved profile's `DefaultScopes` (e.g., RTL + DevExpress from your machine profile)
- **Query policies**: MaxTopK, MaxVersionsPerLibraryPerQuery enforced per profile
- **Ideal for**: Team deployments with centralized index and policy control

## 7. Operations & Maintenance

### API Key & Profile Management

- **Rotate API keys**: Periodically update keys in environment variables or secure config storage
- **Add new profiles**: Update `appsettings.json` with new profile + add entry to `ApiKeyToProfile` + create environment variable for the key
- **Revoke access**: Remove entry from `ApiKeyToProfile` to block API key (existing profile remains available for updates)
- **Policy tuning**: Adjust `MaxTopK`, `MaxVersionsPerLibraryPerQuery` per profile without restart (requires config reload or restart depending on framework)

### Database & Index Management

- Back up DB and Faiss directories together
- After replacing DB/index files, restart the IIS site
- Run `dotnet run --reset --library <lib>` on a local copy to clean up before re-indexing
- Run `dotnet run --vacuum` after large resets to reclaim storage

### Troubleshooting

- Keep stdio mode available for local fallback and troubleshooting
- Check IIS logs for authentication errors (bad API key, profile not found)
- Enable verbose logging in `appsettings.Production.json` if needed
- Verify ClientAccess configuration is valid JSON and referenced profiles exist

### Monitoring

Monitor for:
- Failed authentication attempts (401 Unauthorized)
- Policy violations (TopK or version limits exceeded)
- Search latency degradation (check Faiss index health, consider re-indexing)
- Vector store load errors (verify DB path, Faiss index dir permissions)

---

## 8. References & Further Reading

### Architecture & Design Decisions

- **[ADR 0007: Machine-Profile Client Access Policy](../decisions/0007-machine-profile-client-access-policy.md)**: Detailed rationale for the ClientAccess subsystem, profile-based policy enforcement, and deployment patterns
- **[ADR 0008: Unified Delphi Source Tools](../decisions/0008-unified-delphi-source-tools.md)**: Design of unified tools (`search_delphi_source`, `lookup_delphi_class`) and scope resolution

### Tools & Features

- **Unified Tools (v1.1+)**: `search_delphi_source`, `lookup_delphi_class` — See README for tool behavior and examples
- **Client Access**: Per-API-key machine profiles, default scopes, and per-profile policies
- **Multi-Version Comparison**: Lookup classes across multiple versions in a single query
- **Policy Enforcement**: MaxTopK, MaxVersionsPerLibraryPerQuery limits enforced server-side

### Migration Guides

- **[README v1.0 → v1.1 Migration Guide](../../README.md#v10--v11-migration-guide)**: If upgrading from v1.0 library-specific tools
- **[README v1.2 Breaking Changes](../../README.md#v12-breaking-changes--deprecation)**: v1.2 deprecation notices
- **[README v1.3 Breaking Changes](../../README.md#v13-breaking-changes-library-tools-removed)**: v1.3 tool removal (if applicable)
