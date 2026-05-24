param(
    [Parameter(Mandatory = $true)]
    [string]$Token
)

$ErrorActionPreference = 'Stop'

$token = $Token.Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Token cannot be empty.'
}

[Environment]::SetEnvironmentVariable('CODACY_ACCOUNT_TOKEN', $token, 'User')

$config = @{
    mcpServers = @{
        codacy = @{
            command = 'npx'
            args    = @('-y', '@codacy/codacy-mcp@0.6.21')
            env     = @{
                CODACY_ACCOUNT_TOKEN = $token
            }
        }
    }
}

$globalConfigPath = Join-Path $env:USERPROFILE '.cursor\mcp.json'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$projectConfigPath = Join-Path $repoRoot '.cursor\mcp.json'

New-Item -ItemType Directory -Force -Path (Split-Path $globalConfigPath -Parent) | Out-Null
$config | ConvertTo-Json -Depth 10 | Set-Content $globalConfigPath -Encoding UTF8

$projectDir = Split-Path $projectConfigPath -Parent
if (-not (Test-Path $projectDir)) {
    New-Item -ItemType Directory -Force -Path $projectDir | Out-Null
}
$config | ConvertTo-Json -Depth 10 | Set-Content $projectConfigPath -Encoding UTF8

Write-Host 'Codacy MCP token saved to user environment and MCP config files.'
Write-Host 'Restart Cursor, then verify Settings > MCP > codacy is connected.'
