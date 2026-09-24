param([switch]$Elevated)
$repoRoot = 'c:\Users\allan\Documents\Examples\Recruitment'
$logPath = Join-Path $repoRoot 'tools\dev\deploy-8080-last-run.txt'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $Elevated -and -not $isAdmin) {
    Write-Host 'Requesting elevated PowerShell (approve UAC)...' -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoProfile -File `"$PSCommandPath`" -Elevated" -WorkingDirectory $repoRoot
    exit 0
}
$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $repoRoot 'tools\dev\Start-HRWeb-8080.ps1') -SkipBuild *>> $logPath
    "DEPLOY_OK $(Get-Date -Format o)" | Add-Content -Path $logPath -Encoding UTF8
    Write-Host 'Deploy to port 8080 complete.' -ForegroundColor Green
    exit 0
} catch {
    "DEPLOY_FAILED: $($_.Exception.Message)" | Add-Content -Path $logPath -Encoding UTF8
    Write-Host "Deploy failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}