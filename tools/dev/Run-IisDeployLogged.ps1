$log = 'c:\Users\allan\Documents\Examples\Recruitment\tools\dev\deploy-iis-last-run.txt'
"STARTED $(Get-Date -Format o)" | Set-Content -Path $log -Encoding UTF8
try {
    & (Join-Path $PSScriptRoot 'Sync-Publish.ps1')
    & (Join-Path $PSScriptRoot 'elevate-deploy.bat')
    "DEPLOY_OK $(Get-Date -Format o)" | Add-Content -Path $log
    exit 0
} catch {
    "DEPLOY_FAILED: $($_.Exception.Message)" | Add-Content -Path $log
    exit 1
}
