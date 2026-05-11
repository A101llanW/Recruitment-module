#Requires -Version 5.0
<#
.SYNOPSIS
  1) Reminds you to run EF6 Update-Database (primary path for schema).
  2) Optionally runs VerifyDatabaseSchema.sql and optional idempotent SQL via sqlcmd.

.EXAMPLE
  .\Apply-DatabaseUpdates.ps1
  (prints Package Manager Console instructions only)

.EXAMPLE
  .\Apply-DatabaseUpdates.ps1 -ServerInstance ".\SQLEXPRESS" -Database "HrRecruitment" -WindowsAuth
  (runs verify script; requires sqlcmd on PATH)

.EXAMPLE
  Same + -ApplyOptionalSql
  (also runs Migrations\202605050000013_AddCompanyHrCcEmails.sql if present)
#>
param(
    [string] $ServerInstance = "localhost",
    [string] $Database = "",
    [switch] $WindowsAuth,
    [string] $UserName = "",
    [string] $Password = "",
    [switch] $ApplyOptionalSql
)

$ErrorActionPreference = "Stop"
$hrWebRoot = Split-Path $PSScriptRoot -Parent
$migrations = Join-Path $hrWebRoot "Migrations"
$verifyScript = Join-Path $PSScriptRoot "VerifyDatabaseSchema.sql"
$hrCcSql = Join-Path $migrations "202605050000013_AddCompanyHrCcEmails.sql"

Write-Host "=== HR.Web database updates ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Primary (recommended): Package Manager Console in Visual Studio" -ForegroundColor Yellow
Write-Host "  Default project: HR.Web"
Write-Host "  Startup project: HR.Web"
Write-Host "  Command:        Update-Database"
Write-Host ""
Write-Host "This applies all compiled EF6 migrations (AutomaticMigrationsEnabled = false)."
Write-Host "Rebuild solution first so HR.Web.dll includes every migration class."
Write-Host ""

if ([string]::IsNullOrWhiteSpace($Database)) {
    Write-Host "No -Database supplied; skipping sqlcmd steps." -ForegroundColor DarkGray
    Write-Host "To verify columns with VerifyDatabaseSchema.sql, run:" -ForegroundColor Gray
    Write-Host "  .\Apply-DatabaseUpdates.ps1 -ServerInstance `"YOUR_SERVER`" -Database `"YOUR_DB`" -WindowsAuth"
    exit 0
}

$sqlcmd = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Warning "sqlcmd.exe not found on PATH. Install SQL Server Command Line Tools or run Scripts\VerifyDatabaseSchema.sql in SSMS manually."
    exit 1
}

if (-not (Test-Path $verifyScript)) {
    throw "Missing file: $verifyScript"
}

if ($WindowsAuth) {
    $authArgs = @("-E")
}
elseif ($UserName -and $Password) {
    $authArgs = @("-U", $UserName, "-P", $Password)
}
else {
    throw "Provide -WindowsAuth or both -UserName and -Password for SQL authentication."
}

function Invoke-HrSqlFile {
    param([Parameter(Mandatory)][string] $Path)
    Write-Host "Running: $Path" -ForegroundColor Green
    $args = @("-S", $ServerInstance, "-d", $Database) + $authArgs + @("-b", "-i", $Path)
    & sqlcmd.exe @args
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed ($LASTEXITCODE): $Path"
    }
}

Invoke-HrSqlFile -Path $verifyScript

if ($ApplyOptionalSql) {
    if (-not (Test-Path $hrCcSql)) {
        Write-Warning "Optional script not found: $hrCcSql"
    }
    else {
        Invoke-HrSqlFile -Path $hrCcSql
    }
}

Write-Host ""
Write-Host "Done. If verification reported missing columns, run Update-Database, then verify again." -ForegroundColor Cyan
