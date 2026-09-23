# Applies schema changes introduced after live publish package Publish-2026-09-08.zip.
# Runs Infrastructure\Database\Apply-Updates-Since-Publish-2026-09-08.sql (idempotent).
# Does not call Entity Framework or DbMigrator — no EF migration history updates.
#
# Usage (from repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\dev\Apply-Updates-Since-Publish-2026-09-08.ps1
# Optional:
#   -WebConfigPath "D:\apps\HR.Web\Web.config"
#   -WhatIf          (print target and script path only)

param(
    [string] $WebConfigPath = "",
    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'

function Resolve-WebConfigPath {
    param([string] $Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit) -and (Test-Path -LiteralPath $Explicit)) {
        return (Resolve-Path -LiteralPath $Explicit).Path
    }
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $candidate = Join-Path $repoRoot 'HR.Web\Web.config'
    if (Test-Path -LiteralPath $candidate) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
    throw "Web.config not found. Pass -WebConfigPath."
}

function Get-SqlCmdConnectionArgs {
    param([string] $ConnectionString)
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
    $server = $b.DataSource
    $database = $b.InitialCatalog
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
        throw "Connection string must include Data Source and Initial Catalog (or Server/Database)."
    }
    $args = @('-S', $server, '-d', $database)
    if ($b.IntegratedSecurity) {
        $args += @('-E')
    }
    else {
        if ([string]::IsNullOrWhiteSpace($b.UserID)) {
            throw "Non-integrated security requires User ID in the connection string."
        }
        $args += @('-U', $b.UserID, '-P', $b.Password)
    }
    return $args
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sqlPath = Join-Path $repoRoot 'Infrastructure\Database\Apply-Updates-Since-Publish-2026-09-08.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) {
    throw "SQL script not found: $sqlPath"
}

$webConfig = Resolve-WebConfigPath -Explicit $WebConfigPath
[xml] $cfg = Get-Content -LiteralPath $webConfig -Raw
$csNode = $cfg.configuration.connectionStrings.add | Where-Object { $_.name -eq 'HrContext' } | Select-Object -First 1
if ($null -eq $csNode) {
    throw "HrContext connection string not found in $webConfig"
}
$conn = $csNode.connectionString
if ([string]::IsNullOrWhiteSpace($conn)) {
    throw "HrContext connectionString is empty."
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($null -eq $sqlcmd) {
    throw "sqlcmd not found on PATH. Install SQL Server Command Line Utilities or use Developer PowerShell."
}

$connArgs = Get-SqlCmdConnectionArgs -ConnectionString $conn
$catalog = (New-Object System.Data.SqlClient.SqlConnectionStringBuilder $conn).InitialCatalog

Write-Host "Web.config: $webConfig"
Write-Host "Database: $catalog"
Write-Host "Script: $sqlPath"
Write-Host "Scope: schema changes after Publish-2026-09-08.zip"
Write-Host ""

if ($WhatIf) {
    Write-Host "WhatIf: would run sqlcmd against $catalog."
    exit 0
}

$arguments = @($connArgs + @('-b', '-i', $sqlPath))
& sqlcmd @arguments
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed (exit $LASTEXITCODE) on Apply-Updates-Since-Publish-2026-09-08.sql"
}

Write-Host ""
Write-Host "Update script applied successfully."
