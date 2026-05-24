# Start HR.Web with the same IIS Express site Visual Studio uses.
# Run from repository root: .\tools\dev\Start-HRWeb.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$config = Join-Path $repoRoot ".vs\HR\config\applicationhost.config"
$iisExpress = Join-Path ${env:ProgramFiles} "IIS Express\iisexpress.exe"

if (-not (Test-Path $config)) {
    Write-Error "Missing $config. Open the solution in Visual Studio once (F5) so it generates IIS Express config, then retry."
}

if (-not (Test-Path $iisExpress)) {
    Write-Error "IIS Express not found at $iisExpress"
}

$existing = Get-Process iisexpress -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing IIS Express (PID $($existing.Id))..."
    $existing | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "Starting HR.Web at http://localhost:5002 ..."
Write-Host "Press Ctrl+C to stop."

& $iisExpress /config:"$config" /site:HR.Web /systray:false
