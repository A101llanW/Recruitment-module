#requires -Version 4.0
<#
.SYNOPSIS
  Applies all SQL migration scripts in this folder (no Entity Framework).

.DESCRIPTION
  Runs scripts in a fixed order: MultiTenantMigration.sql, then every 202*.sql file sorted by name.
  Uses sqlcmd only. Matches the connection defaults in Web.config (HrContext) unless you override.

.PARAMETER Server
  SQL Server instance (default: .)

.PARAMETER Database
  Database name (default: HR_Local)

.PARAMETER Username
  SQL login user. If omitted, uses Windows authentication (-E).

.PARAMETER Password
  SQL login password (used with -Username).

.PARAMETER SqlCmdPath
  Full path to sqlcmd.exe (optional). If omitted, searches common install locations.

.PARAMETER TrustServerCertificate
  Passes sqlcmd -C (needed with some ODBC 18 / encrypted connections).

.EXAMPLE
  .\Apply-SqlMigrations.ps1

.EXAMPLE
  .\Apply-SqlMigrations.ps1 -Server "localhost" -Database "HR_Local" -TrustServerCertificate

.EXAMPLE
  .\Apply-SqlMigrations.ps1 -Server "." -Database "HR_Prod" -Username "sa" -Password "secret"
#>
[CmdletBinding()]
param(
    [string] $Server = ".",
    [string] $Database = "HR_Local",
    [string] $Username = "",
    [string] $Password = "",
    [string] $SqlCmdPath = "",
    [switch] $TrustServerCertificate
)

$ErrorActionPreference = "Stop"

$migrationsRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-SqlCmdPath {
    param([string] $Explicit)
    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) {
        return (Resolve-Path -LiteralPath $Explicit).Path
    }
    $candidates = @(
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE",
        "C:\Program Files\SqlCmd\sqlcmd.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) {
            return $c
        }
    }
    $fromPath = Get-Command sqlcmd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
    if ($fromPath) {
        return $fromPath
    }
    throw "sqlcmd.exe not found. Install SQL Server Command Line Utilities or pass -SqlCmdPath."
}

$sqlcmd = Resolve-SqlCmdPath -Explicit $SqlCmdPath

$authArgs = @()
if ([string]::IsNullOrWhiteSpace($Username)) {
    $authArgs += "-E"
}
else {
    $authArgs += "-U", $Username
    if (-not [string]::IsNullOrEmpty($Password)) {
        $authArgs += "-P", $Password
    }
}

$extra = @()
if ($TrustServerCertificate) {
    $extra += "-C"
}

$ordered = New-Object System.Collections.Generic.List[string]
$multi = Join-Path $migrationsRoot "MultiTenantMigration.sql"
if (Test-Path -LiteralPath $multi) {
    $ordered.Add($multi)
}
Get-ChildItem -Path $migrationsRoot -Filter "202*.sql" -File | Sort-Object Name | ForEach-Object {
    $ordered.Add($_.FullName)
}

Write-Host "sqlcmd: $sqlcmd"
Write-Host "Target:  $Server / $Database"
Write-Host "Scripts: $($ordered.Count) file(s)"
Write-Host ""

foreach ($f in $ordered) {
    $name = Split-Path $f -Leaf
    Write-Host "======== $name ========"
    $args = @(
        "-S", $Server,
        "-d", $Database
    ) + $authArgs + $extra + @("-b", "-V", "16", "-i", $f)

    & $sqlcmd @args
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed (exit $LASTEXITCODE) on: $f"
    }
}

Write-Host ""
Write-Host "All SQL migrations applied successfully." -ForegroundColor Green
