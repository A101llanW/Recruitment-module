<#
.SYNOPSIS
  Runs Audit-ApplicantProfiles.sql against SQL Server (defaults: localhost, HR_Local, Windows auth).

.PARAMETER ServerInstance
  SQL Server instance, e.g. localhost or .\SQLEXPRESS

.PARAMETER Database
  Database name (default HR_Local)

.EXAMPLE
  .\Run-ApplicantProfileAudit.ps1
  .\Run-ApplicantProfileAudit.ps1 -ServerInstance "(localdb)\MSSQLLocalDB" -Database HR_Local
#>
param(
    [string] $ServerInstance = "localhost",
    [string] $Database = "HR_Local"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFile = Join-Path $scriptDir "Audit-ApplicantProfiles.sql"

if (-not (Test-Path $sqlFile)) {
    throw "Missing $sqlFile"
}

$sqlcmd = @(
    "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE",
    "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
    "sqlcmd"
) | Where-Object { Test-Path $_ -ErrorAction SilentlyContinue } | Select-Object -First 1

if (-not $sqlcmd) {
    throw "sqlcmd not found. Install SQL Server Command Line Tools or run Audit-ApplicantProfiles.sql in SSMS."
}

Write-Host "Using: $sqlcmd"
Write-Host "Server: $ServerInstance  Database: $Database"
& $sqlcmd -S $ServerInstance -d $Database -E -C -b -i $sqlFile
