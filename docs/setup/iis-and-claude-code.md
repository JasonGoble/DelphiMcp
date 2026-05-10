# IIS + Claude Code Setup

This guide describes the hosted deployment path for DelphiMcp when you want to build the large index once and serve it centrally.

## Overview

- Local default mode remains stdio (`dotnet run`) for minimal setup.
- Hosted mode uses HTTP MCP (`dotnet run -- --http`) and requires an API key.
- API key headers accepted:
  - `Authorization: Bearer <api-key>`
  - `X-API-Key: <api-key>`

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

## 3. Configure Environment (IIS Site/App)

Set these environment variables for the process (IIS or system-level):

- `Server__Mode=http`
- `Hosted__ApiKey=<strong-random-key>`
- `Hosted__Path=/mcp`
- `Storage__DbPath=E:\data\delphi-mcp\delphi-mcp.db`
- `Storage__FaissIndexDir=E:\data\delphi-mcp\faiss-indexes`
- `Embedder__Provider=OpenAI` or `Ollama`
- `OpenAI__ApiKey=<openai-key>` (if OpenAI)
- `Ollama__BaseUrl` and `Ollama__Model` (if Ollama)

Recommended:
- Keep `Hosted__ApiKey` and provider keys out of source control.
- Restrict file ACLs so only the app pool identity can read keys/config and write needed paths.

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

## 6. Claude Code Setup

Use one of these two paths:

### Local minimal setup (recommended for local dev)

Configure Claude Code to launch the local executable via stdio.

- Command: `DelphiMcp` (or full path to the exe)
- No API key header required
- This is the lowest-friction path

### Hosted setup (central server)

Configure Claude Code MCP server URL and auth header:

- URL: `https://<host>/mcp`
- Header (recommended): `Authorization: Bearer <api-key>`
- Optional header alternative: `X-API-Key: <api-key>`

If your client only supports one header style, use whichever style is available.

## 7. Operations

- Rotate `Hosted__ApiKey` periodically.
- Back up DB and Faiss directories together.
- After replacing DB/index files, restart the IIS site.
- Keep stdio mode available for troubleshooting and local fallback.
