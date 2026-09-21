$log = Join-Path $PSScriptRoot 'deploy-iis-last-run.txt'
"ELEVATED_START $(Get-Date -Format o)" | Set-Content -Path $log -Encoding UTF8
try {
    & (Join-Path $PSScriptRoot 'Deploy-IisHirehub.ps1') -SkipBuild -Port 8080 *>&1 | Tee-Object -FilePath $log -Append
    "DEPLOY_OK $(Get-Date -Format o)" | Add-Content -Path $log -Encoding UTF8
    exit 0
} catch {
    "DEPLOY_FAILED: $($_.Exception.Message)" | Add-Content -Path $log -Encoding UTF8
    exit 1
}
