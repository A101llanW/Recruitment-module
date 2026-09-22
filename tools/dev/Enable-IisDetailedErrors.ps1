$ErrorActionPreference = "Stop"
$webConfigPath = "C:\inetpub\wwwroot\Hirehub\Web.config"
$log = Join-Path $PSScriptRoot "enable-detailed-errors-last-run.txt"
$appcmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"

function Write-Log([string]$Message) {
    $line = "$(Get-Date -Format o) $Message"
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $Message
}

try {
    "$(Get-Date -Format o) ELEVATED_START (PID $PID)" | Set-Content -Path $log -Encoding UTF8
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Write-Log "Running as admin: $isAdmin"
    if (-not $isAdmin) { throw "Administrator privileges required" }
    if (-not (Test-Path $webConfigPath)) { throw "Web.config not found at $webConfigPath" }

    Start-Process -FilePath $appcmd -ArgumentList "stop","apppool","/apppool.name:Hirehub_Pool" -Wait -NoNewWindow
    Start-Sleep -Seconds 2
    Write-Log "Stopped app pool Hirehub_Pool"

    [xml]$doc = Get-Content -Path $webConfigPath
    foreach ($add in $doc.configuration.appSettings.add) {
        if ($add.key -eq "AppEnvironment") {
            $add.SetAttribute("value", "Remote/Dev")
        }
    }
    Write-Log "Set AppEnvironment=Remote/Dev"

    $customErrors = $doc.CreateElement("customErrors")
    $customErrors.SetAttribute("mode", "Off")
    $systemWeb = $doc.configuration."system.web"
    $null = $systemWeb.ReplaceChild($customErrors, $systemWeb.customErrors)
    $systemWeb.compilation.SetAttribute("debug", "true")
    Write-Log "Set customErrors mode=Off and compilation debug=true"

    $httpErrors = $doc.CreateElement("httpErrors")
    $httpErrors.SetAttribute("existingResponse", "PassThrough")
    $httpErrors.SetAttribute("errorMode", "Detailed")
    $webServer = $doc.configuration."system.webServer"
    $null = $webServer.ReplaceChild($httpErrors, $webServer.httpErrors)
    Write-Log "Set httpErrors existingResponse=PassThrough errorMode=Detailed"

    $tempPath = "$webConfigPath.detailed-errors.tmp"
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = "`n"
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($tempPath, $settings)
    try { $doc.Save($writer) } finally { $writer.Close() }
    Move-Item -Path $tempPath -Destination $webConfigPath -Force
    Write-Log "Saved $webConfigPath"

    Start-Process -FilePath $appcmd -ArgumentList "start","apppool","/apppool.name:Hirehub_Pool" -Wait -NoNewWindow
    Start-Process -FilePath $appcmd -ArgumentList "recycle","apppool","/apppool.name:Hirehub_Pool" -Wait -NoNewWindow
    Write-Log "Started and recycled app pool Hirehub_Pool"
    "$(Get-Date -Format o) DETAILED_ERRORS_OK" | Add-Content -Path $log -Encoding UTF8
    exit 0
}
catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    "$(Get-Date -Format o) DETAILED_ERRORS_FAILED" | Add-Content -Path $log -Encoding UTF8
    exit 1
}
