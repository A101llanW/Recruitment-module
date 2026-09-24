param([switch]$Elevated)
$repoRoot = 'c:\Users\allan\Documents\Examples\Recruitment'
$logPath = Join-Path $repoRoot 'tools\dev\deploy-iis-last-run.txt'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $Elevated -and -not $isAdmin) {
    Write-Host 'Requesting elevated PowerShell window (approve UAC)...' -ForegroundColor Yellow
    $argList = "-ExecutionPolicy Bypass -NoProfile -File `"$PSCommandPath`" -Elevated"
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -WorkingDirectory $repoRoot
    exit 0
}
$ErrorActionPreference = 'Stop'
$publish = Join-Path $repoRoot 'Publish'
$physicalPath = 'C:\inetpub\wwwroot\Hirehub'
$appPoolName = 'Hirehub_Pool'
$siteName = 'Hirehub'
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
function Write-Log([string]$Message) {
    $line = "$(Get-Date -Format o) $Message"
    Add-Content -Path $logPath -Value $line -Encoding UTF8
    Write-Host $Message
}
"ELEVATED_START $(Get-Date -Format o) (PID $PID)" | Set-Content -Path $logPath -Encoding UTF8
Write-Log "Running as admin: $isAdmin"
if (-not $isAdmin) { Write-Log 'DEPLOY_FAILED: Administrator privileges required'; exit 1 }
try {
    if (-not (Test-Path (Join-Path $publish 'bin\HR.Web.dll'))) { throw 'Missing Publish\bin\HR.Web.dll. Run Sync-Publish.ps1 -ShowDetailedErrors first.' }
    Write-Log "Robocopy $publish -> $physicalPath"
    New-Item -ItemType Directory -Force -Path $physicalPath | Out-Null
    robocopy $publish $physicalPath /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    Write-Log "Robocopy complete (exit $LASTEXITCODE)"
    $staleBinViews = Join-Path $physicalPath 'bin\Views'
    if (Test-Path $staleBinViews) { Remove-Item -Path $staleBinViews -Recurse -Force; Write-Log 'Removed stale bin\Views' }
    $webConfigPath = Join-Path $physicalPath 'Web.config'
    [xml]$doc = Get-Content -Path $webConfigPath
    foreach ($add in $doc.configuration.appSettings.add) {
        if ($add.key -eq 'AppEnvironment') { $add.SetAttribute('value', 'Remote/Dev') }
        elseif ($add.key -eq 'ShowDetailedErrors') { $add.SetAttribute('value', 'true') }
    }
    $customErrors = $doc.CreateElement('customErrors')
    $customErrors.SetAttribute('mode', 'Off')
    $systemWeb = $doc.configuration.'system.web'
    $null = $systemWeb.ReplaceChild($customErrors, $systemWeb.customErrors)
    $systemWeb.compilation.SetAttribute('debug', 'true')
    $httpErrors = $doc.CreateElement('httpErrors')
    $httpErrors.SetAttribute('existingResponse', 'PassThrough')
    $httpErrors.SetAttribute('errorMode', 'Detailed')
    $webServer = $doc.configuration.'system.webServer'
    $null = $webServer.ReplaceChild($httpErrors, $webServer.httpErrors)
    $tempPath = "$webConfigPath.detailed-errors.tmp"
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = '  '
    $settings.NewLineChars = "`n"
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($tempPath, $settings)
    try { $doc.Save($writer) } finally { $writer.Close() }
    Move-Item -Path $tempPath -Destination $webConfigPath -Force
    Write-Log 'Enabled YSOD: customErrors=Off, ShowDetailedErrors=true, httpErrors=Detailed'
    Start-Process -FilePath $appcmd -ArgumentList 'stop', 'apppool', "/apppool.name:$appPoolName" -Wait -NoNewWindow
    Start-Sleep -Seconds 2
    Import-Module WebAdministration -ErrorAction Stop
    Start-Website -Name $siteName -ErrorAction SilentlyContinue
    Start-Process -FilePath $appcmd -ArgumentList 'start', 'apppool', "/apppool.name:$appPoolName" -Wait -NoNewWindow
    Start-Process -FilePath $appcmd -ArgumentList 'recycle', 'apppool', "/apppool.name:$appPoolName" -Wait -NoNewWindow
    Write-Log "Recycled app pool $appPoolName"
    Write-Log 'DEPLOY_OK'
    Write-Host ''
    Write-Host 'Deploy complete. Detailed errors (YSOD) enabled on IIS.' -ForegroundColor Green
    Write-Host "Site: http://localhost:5002" -ForegroundColor Cyan
    exit 0
}
catch {
    Write-Log "DEPLOY_FAILED: $($_.Exception.Message)"
    Write-Host "Deploy failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}