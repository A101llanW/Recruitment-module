#Requires -RunAsAdministrator
# Build, free port 8080, deploy Publish to IIS, and listen on all interfaces.
# Run: powershell -ExecutionPolicy Bypass -File tools\dev\Start-HRWeb-8080.ps1

param(
    [int] $Port = 8080,
    [string] $SiteName = "Hirehub",
    [string] $AppPoolName = "Hirehub_Pool",
    [string] $PhysicalPath = "C:\inetpub\wwwroot\Hirehub",
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Stop-ListenersOnPort {
    param([int] $ListenPort)

    Write-Host "Stopping listeners on port $ListenPort ..." -ForegroundColor Yellow

    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

    Import-Module WebAdministration
    foreach ($site in Get-Website) {
        foreach ($binding in $site.bindings.Collection) {
            if ($binding.bindingInformation -like "*:${ListenPort}:*") {
                Write-Host "  Stopping IIS site: $($site.Name) ($($binding.bindingInformation))"
                if ($site.State -ne 'Stopped') {
                    Stop-Website -Name $site.Name -ErrorAction SilentlyContinue
                }
                Remove-WebBinding -Name $site.Name -Protocol $binding.protocol -BindingInformation $binding.bindingInformation -ErrorAction SilentlyContinue
            }
        }
    }

    Start-Sleep -Seconds 2

    $stillListening = Get-NetTCPConnection -LocalPort $ListenPort -State Listen -ErrorAction SilentlyContinue
    if ($stillListening) {
        Write-Warning "Port $ListenPort may still be reserved by HTTP.sys. Continuing deploy attempt."
    }
}

if (-not $SkipBuild) {
    & (Join-Path $repoRoot 'tools\dev\Sync-Publish.ps1')
}

Stop-ListenersOnPort -ListenPort $Port

& (Join-Path $repoRoot 'tools\dev\Deploy-IisHirehub.ps1') -Port $Port -SiteName $SiteName -AppPoolName $AppPoolName -PhysicalPath $PhysicalPath -SkipBuild

Import-Module WebAdministration
$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($site) {
    $hasPortBinding = $false
    foreach ($binding in $site.bindings.Collection) {
        if ($binding.bindingInformation -like "*:${Port}:*") {
            $hasPortBinding = $true
        }
    }
    if (-not $hasPortBinding) {
        New-WebBinding -Name $SiteName -Protocol http -Port $Port -IPAddress '*'
        Write-Host "Added IIS binding *:${Port}: to $SiteName" -ForegroundColor Yellow
    }
    Restart-WebAppPool -Name $AppPoolName
    Start-Website -Name $SiteName
}

$ruleName = "HR.Web Port $Port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
    Write-Host "Created firewall rule: $ruleName" -ForegroundColor Yellow
}

$ipAddresses = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -ExpandProperty IPAddress)

Write-Host ''
Write-Host 'HR.Web is running on port' $Port -ForegroundColor Green
Write-Host "  Local:   http://localhost:$Port/"
foreach ($ip in $ipAddresses) {
    Write-Host "  Network: http://${ip}:$Port/"
}
Write-Host "  Tenant:  http://localhost:$Port/F4359634/Account/Login"
