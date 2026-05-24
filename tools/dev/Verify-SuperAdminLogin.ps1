#requires -Version 5.1
<#
.SYNOPSIS
  Verifies global (SuperAdmin) credentials against the HR database using the same PBKDF2 logic as the web app.

.DESCRIPTION
  Local development tool only — not part of the web deploy. Loads HR.Web.dll from HR.Web\bin and
  calls HR.Web.Helpers.PasswordHelper.VerifyPassword. The live /Account/Login page still requires CAPTCHA and MFA.

.PARAMETER Username
  Default: admin

.PARAMETER Password
  Required. Never commit or script a default value.

.PARAMETER ConnectionString
  Optional. If omitted, reads HrContext from HR.Web\Web.config.

.EXAMPLE
  $p = Read-Host -AsSecureString; $b = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($p))
  .\tools\dev\Verify-SuperAdminLogin.ps1 -Password $b
#>
param(
    [string]$Username = "admin",
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [string]$ConnectionString = $null
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$hrWebRoot = Join-Path $repoRoot "HR.Web"
$binPath = Join-Path $hrWebRoot "bin"
$webConfigPath = Join-Path $hrWebRoot "Web.config"
$hrWebDll = Join-Path $binPath "HR.Web.dll"

if (-not (Test-Path $hrWebDll)) {
    Write-Error "HR.Web.dll not found at '$hrWebDll'. Build HR.Web first."
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    if (-not (Test-Path $webConfigPath)) {
        Write-Error "Web.config not found at '$webConfigPath'."
    }
    [xml]$cfg = Get-Content -LiteralPath $webConfigPath
    $csNode = $cfg.configuration.connectionStrings.add | Where-Object { $_.name -eq "HrContext" } | Select-Object -First 1
    if ($null -eq $csNode -or [string]::IsNullOrWhiteSpace($csNode.connectionString)) {
        Write-Error "HrContext connection string not found in Web.config."
    }
    $ConnectionString = $csNode.connectionString
}

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT TOP (1)
    UserName,
    Role,
    CompanyId,
    PasswordHash,
    IsTwoFactorEnabled,
    IsEmailVerified
FROM dbo.Users
WHERE LOWER(LTRIM(RTRIM(UserName))) = LOWER(LTRIM(RTRIM(@username)))
  AND CompanyId IS NULL
ORDER BY CASE WHEN LOWER(Role) = N'superadmin' THEN 0 WHEN LOWER(Role) = N'admin' THEN 1 ELSE 2 END;
"@
$null = $cmd.Parameters.AddWithValue("@username", $Username)

try {
    $conn.Open()
    $reader = $cmd.ExecuteReader()
    if (-not $reader.Read()) {
        Write-Host "FAIL: No global user (CompanyId IS NULL) found with username '$Username'." -ForegroundColor Red
        exit 2
    }

    $dbUser = $reader["UserName"]
    $dbRole = $reader["Role"]
    $hash = [string]$reader["PasswordHash"]
    $mfa = [bool]$reader["IsTwoFactorEnabled"]
    $emailOk = [bool]$reader["IsEmailVerified"]
    $reader.Close()
}
finally {
    $conn.Dispose()
}

Write-Host "User: $dbUser | Role: $dbRole | CompanyId: (null) | EmailVerified: $emailOk | MFA enabled: $mfa"

if ([string]::IsNullOrWhiteSpace($hash)) {
    Write-Host "FAIL: PasswordHash is empty." -ForegroundColor Red
    exit 3
}

[System.Reflection.Assembly]::LoadFrom($hrWebDll) | Out-Null
$verified = [HR.Web.Helpers.PasswordHelper]::VerifyPassword($hash, $Password)

if ($verified) {
    Write-Host "PASS: Password matches the stored hash for '$Username'." -ForegroundColor Green
    if ($mfa) {
        Write-Host "Note: After password check, the site will still require MFA on /Account/VerifyMFA." -ForegroundColor Yellow
    }
    Write-Host "Note: /Account/Login also requires CAPTCHA; this script does not POST to the site." -ForegroundColor DarkGray
    exit 0
}

Write-Host "FAIL: Password does not match the stored hash." -ForegroundColor Red
exit 1
