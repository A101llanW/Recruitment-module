#Requires -RunAsAdministrator
# Deploy NanoHireHub to IIS at C:\inetpub\wwwroot\Hirehub
# Run: powershell -ExecutionPolicy Bypass -File tools\dev\Deploy-IisHirehub.ps1

param(
    [string] $SiteName = "Hirehub",
    [string] $AppPoolName = "Hirehub_Pool",
    [int] $Port = 5002,
    [string] $PhysicalPath = "C:\inetpub\wwwroot\Hirehub",
    [switch] $SkipBuild,
    [switch] $ShowDetailedErrors
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

Write-Host "=== Deploy to IIS: $PhysicalPath ===" -ForegroundColor Cyan

if (-not $SkipBuild) {
    if ($ShowDetailedErrors) {
        & (Join-Path $repoRoot "tools\dev\Sync-Publish.ps1") -ShowDetailedErrors
    }
    else {
        & (Join-Path $repoRoot "tools\dev\Sync-Publish.ps1")
    }
}

$publish = Join-Path $repoRoot "Publish"
if (-not (Test-Path (Join-Path $publish "bin\HR.Web.dll"))) {
    throw "Missing Publish\bin\HR.Web.dll. Run Sync-Publish.ps1 first."
}

New-Item -ItemType Directory -Force -Path $PhysicalPath | Out-Null
robocopy $publish $PhysicalPath /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}

$staleBinViews = Join-Path $PhysicalPath "bin\Views"
if (Test-Path $staleBinViews) {
    Remove-Item -Path $staleBinViews -Recurse -Force
    Write-Host "Removed stale bin\Views"
}

Import-Module WebAdministration

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "Created app pool: $AppPoolName"
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value "Integrated"

$existing = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($existing) {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    Write-Host "Updated site: $SiteName"
}
else {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port $Port | Out-Null
    Write-Host "Created site: $SiteName on port $Port"
}

$appPoolIdentity = "IIS AppPool\$AppPoolName"
foreach ($relative in @("Reports", "Content\company-logos")) {
    $folder = Join-Path $PhysicalPath $relative
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    & icacls.exe $folder /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Null
    Write-Host "Granted Modify: $folder"
}

Start-Website -Name $SiteName

Restart-WebAppPool -Name $AppPoolName
Write-Host "Recycled app pool: $AppPoolName"

if ($ShowDetailedErrors) {
    $enableErrorsScript = Join-Path $repoRoot "tools\dev\Enable-IisDetailedErrors.ps1"
    if (Test-Path $enableErrorsScript) {
        & $enableErrorsScript
    }
}

Write-Host ""
Write-Host "Deploy complete." -ForegroundColor Green
Write-Host "  Path: $PhysicalPath"
Write-Host "  URL:  http://localhost:$Port"
Get-Website -Name $SiteName | Format-List Name, State, PhysicalPath
