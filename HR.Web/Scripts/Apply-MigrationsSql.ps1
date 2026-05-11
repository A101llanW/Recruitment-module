# Applies HR.Web\Migrations\*.sql in a safe order (idempotent scripts only).
# Does not call Entity Framework or DbMigrator — no EF migration history updates.
#
# Usage (from repo root or any path):
#   powershell -NoProfile -ExecutionPolicy Bypass -File HR.Web\scripts\Apply-MigrationsSql.ps1
# Optional:
#   -WebConfigPath "D:\apps\HR.Web\Web.config"
#   -WhatIf          (list files only)

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
    $here = $PSScriptRoot
    $candidate = Join-Path (Split-Path $here -Parent) 'Web.config'
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

$migrationsDir = Join-Path (Split-Path $webConfig -Parent) 'Migrations'
if (-not (Test-Path -LiteralPath $migrationsDir)) {
    throw "Migrations folder not found: $migrationsDir"
}

$multi = Join-Path $migrationsDir 'MultiTenantMigration.sql'
# Timestamp-prefixed migration scripts (e.g. 202604220000001_Name.sql)
$dated = Get-ChildItem -LiteralPath $migrationsDir -Filter '*.sql' -File |
    Where-Object { $_.Name -match '^\d{10,}_' } |
    Sort-Object Name

$ordered = New-Object System.Collections.Generic.List[string]
if (Test-Path -LiteralPath $multi) {
    $ordered.Add($multi)
}
foreach ($f in $dated) {
    if ($f.FullName -ieq $multi) {
        continue
    }
    $ordered.Add($f.FullName)
}

if ($ordered.Count -eq 0) {
    throw "No .sql migration files found under $migrationsDir"
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($null -eq $sqlcmd) {
    throw "sqlcmd not found on PATH. Install SQL Server Command Line Utilities or use Developer PowerShell."
}

$connArgs = Get-SqlCmdConnectionArgs -ConnectionString $conn

Write-Host "Web.config: $webConfig"
Write-Host "Database: $((New-Object System.Data.SqlClient.SqlConnectionStringBuilder $conn).InitialCatalog)"
Write-Host "Applying $($ordered.Count) script(s)."
Write-Host ""

if ($WhatIf) {
    $i = 1
    foreach ($p in $ordered) {
        Write-Host ("{0,3}. {1}" -f $i++, (Split-Path $p -Leaf))
    }
    exit 0
}

$i = 1
foreach ($p in $ordered) {
    $leaf = Split-Path $p -Leaf
    Write-Host "[$i/$($ordered.Count)] $leaf ..."
    $arguments = @($connArgs + @('-b', '-i', $p))
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed (exit $LASTEXITCODE) on: $leaf"
    }
    $i++
}

Write-Host ""
Write-Host "All migration SQL scripts applied successfully."
