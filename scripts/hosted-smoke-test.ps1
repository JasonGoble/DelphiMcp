param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [string]$McpPath = "/mcp"
)

$ErrorActionPreference = "Stop"

if ($BaseUrl.EndsWith('/'))
{
    $BaseUrl = $BaseUrl.TrimEnd('/')
}

if (-not $McpPath.StartsWith('/'))
{
    $McpPath = "/$McpPath"
}

$healthUrl = "$BaseUrl/healthz"
$mcpUrl = "$BaseUrl$McpPath"

Write-Host "[1/3] Checking health endpoint: $healthUrl"
$health = Invoke-WebRequest -Uri $healthUrl -Method GET
if ($health.StatusCode -ne 200)
{
    throw "Health check failed with status $($health.StatusCode)"
}

Write-Host "[2/3] Verifying unauthenticated MCP request is rejected: $mcpUrl"
try
{
    Invoke-WebRequest -Uri $mcpUrl -Method POST -ContentType "application/json" -Body "{}" | Out-Null
    throw "Expected 401 for unauthenticated MCP request, but request succeeded."
}
catch
{
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -ne 401)
    {
        throw "Expected 401 for unauthenticated request, got $status"
    }
}

Write-Host "[3/3] Verifying authenticated MCP request is accepted (Bearer header)"
$authHeaders = @{ Authorization = "Bearer $ApiKey" }
$authResponse = Invoke-WebRequest -Uri $mcpUrl -Method POST -Headers $authHeaders -ContentType "application/json" -Body "{}"
if ($authResponse.StatusCode -lt 200 -or $authResponse.StatusCode -ge 500)
{
    throw "Authenticated request returned unexpected status $($authResponse.StatusCode)"
}

Write-Host "Hosted MCP smoke test passed."
