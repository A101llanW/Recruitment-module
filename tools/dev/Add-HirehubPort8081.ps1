#Requires -RunAsAdministrator
param(
    [int] $Port = 8081,
    [string] $SiteName = 'Hirehub',
    [string] $AppPoolName = 'Hirehub_Pool',
    [string] $PhysicalPath = 'C:\inetpub\wwwroot\Hirehub',
    [string] $PublishPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'Publish')
)

$ErrorActionPreference = 'Stop'
$log = Join-Path $PSScriptRoot 'start-8081-last-run.txt'

function Write-Log {
    param([string] $Message)
    $line = "$(Get-Date -Format o) $Message"
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $Message
}

"ELEVATED_START $(Get-Date -Format o)" | Set-Content -Path $log -Encoding UTF8

Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path (Join-Path $PublishPath 'bin\HR.Web.dll')) {
    Write-Log "Syncing Publish -> $PhysicalPath"
    New-Item -ItemType Directory -Force -Path $PhysicalPath | Out-Null
    robocopy $PublishPath $PhysicalPath /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    Write-Log 'Publish sync complete.'
}

Import-Module WebAdministration

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if (-not $site) { throw "IIS site '$SiteName' not found." }

$hasPortBinding = $false
foreach ($binding in $site.bindings.Collection) {
    if ($binding.bindingInformation -like "*:${Port}:*") { $hasPortBinding = $true }
}
if (-not $hasPortBinding) {
    New-WebBinding -Name $SiteName -Protocol http -Port $Port -IPAddress '*'
    Write-Log "Added IIS binding *:${Port}:"
}

# Avoid *:8080 conflict with AssetManagement site
$assetMgmt = Get-Website -Name 'AssetManagement' -ErrorAction SilentlyContinue
if ($assetMgmt -and $assetMgmt.State -eq 'Started') {
    $dup8080 = Get-WebBinding -Name $SiteName -Protocol http | Where-Object { $_.bindingInformation -eq '*:8080:' }
    if ($dup8080) {
        Remove-WebBinding -Name $SiteName -BindingInformation '*:8080:' -Protocol http
        Write-Log 'Removed duplicate *:8080 binding from Hirehub (AssetManagement owns 8080).'
    }
}

$ruleName = "HR.Web Port $Port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
    Write-Log "Created firewall rule: $ruleName"
}

Restart-WebAppPool -Name $AppPoolName
if ((Get-Website -Name $SiteName).State -ne 'Started') {
    Start-Website -Name $SiteName
}

$ipAddresses = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -ExpandProperty IPAddress)

Write-Log "HR.Web is running on port $Port"
Write-Log "  Local:   http://localhost:$Port/"
foreach ($ip in $ipAddresses) {
    Write-Log "  Network: http://${ip}:$Port/"
}
'START_OK' | Add-Content -Path $log -Encoding UTF8
