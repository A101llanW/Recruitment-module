# PowerShell Script to Configure External Access for HR Application
# Run this script as Administrator for full functionality

Write-Host "=== HR Application External Access Setup ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get current IP addresses
Write-Host "Step 1: Detecting network configuration..." -ForegroundColor Yellow
$ipAddresses = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } | Select-Object IPAddress, InterfaceAlias
Write-Host "Found IP addresses:" -ForegroundColor Green
$ipAddresses | ForEach-Object { Write-Host "  - $($_.IPAddress) ($($_.InterfaceAlias))" -ForegroundColor Gray }

# Get primary IP (first non-loopback, non-link-local)
$primaryIP = ($ipAddresses | Select-Object -First 1).IPAddress
Write-Host "`nPrimary IP: $primaryIP" -ForegroundColor Cyan

# Step 2: Configure Firewall Rule
Write-Host "`nStep 2: Configuring Windows Firewall..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -DisplayName "HR App - Port 8080" -ErrorAction SilentlyContinue
if ($firewallRule) {
    Write-Host "Firewall rule already exists. Removing old rule..." -ForegroundColor Gray
    Remove-NetFirewallRule -DisplayName "HR App - Port 8080" -ErrorAction SilentlyContinue
}

try {
    New-NetFirewallRule -DisplayName "HR App - Port 8080" `
        -Direction Inbound `
        -LocalPort 8080 `
        -Protocol TCP `
        -Action Allow `
        -Description "Allow inbound traffic on port 8080 for HR Application" | Out-Null
    Write-Host "✓ Firewall rule created successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to create firewall rule: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Note: This requires Administrator privileges" -ForegroundColor Yellow
}

# Step 3: Configure URL ACL (requires Administrator)
Write-Host "`nStep 3: Configuring URL ACL for port 8080..." -ForegroundColor Yellow
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    # Remove existing reservation if it exists
    $existingReservation = netsh http show urlacl | Select-String "8080"
    if ($existingReservation) {
        Write-Host "Removing existing URL reservation..." -ForegroundColor Gray
        netsh http delete urlacl url=http://*:8080/ 2>&1 | Out-Null
    }
    
    # Add new reservation
    try {
        netsh http add urlacl url=http://*:8080/ user=Everyone 2>&1 | Out-Null
        Write-Host "✓ URL ACL configured successfully" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to configure URL ACL: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "⚠ Skipping URL ACL configuration (requires Administrator privileges)" -ForegroundColor Yellow
    Write-Host "  Run this command as Administrator:" -ForegroundColor Gray
    Write-Host "    netsh http add urlacl url=http://*:8080/ user=Everyone" -ForegroundColor White
}

# Step 4: Update Web.config with external URL
Write-Host "`nStep 4: Updating Web.config..." -ForegroundColor Yellow
$webConfigPath = "HR.Web\Web.config"
if (Test-Path $webConfigPath) {
    [xml]$webConfig = Get-Content $webConfigPath
    $externalUrl = "http://$primaryIP`:8080"
    
    $appSettings = $webConfig.configuration.appSettings
    $externalBaseUrl = $appSettings.add | Where-Object { $_.key -eq "ExternalBaseUrl" }
    
    if ($externalBaseUrl) {
        $oldValue = $externalBaseUrl.value
        $externalBaseUrl.value = $externalUrl
        Write-Host "  Updated ExternalBaseUrl from '$oldValue' to '$externalUrl'" -ForegroundColor Gray
    } else {
        $newSetting = $webConfig.CreateElement("add")
        $newSetting.SetAttribute("key", "ExternalBaseUrl")
        $newSetting.SetAttribute("value", $externalUrl)
        $appSettings.AppendChild($newSetting) | Out-Null
        Write-Host "  Added ExternalBaseUrl: $externalUrl" -ForegroundColor Gray
    }
    
    $webConfig.Save((Resolve-Path $webConfigPath).Path)
    Write-Host "✓ Web.config updated successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Web.config not found at: $webConfigPath" -ForegroundColor Red
}

# Step 5: Display Summary
Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Local:    http://localhost:8080" -ForegroundColor Cyan
Write-Host "  External: http://$primaryIP`:8080" -ForegroundColor Cyan
Write-Host ""
Write-Host "To start the application, run:" -ForegroundColor Yellow
Write-Host "  cd `"$PWD`"" -ForegroundColor White
Write-Host "  `$iisPath = `"C:\Program Files\IIS Express\iisexpress.exe`"" -ForegroundColor White
Write-Host "  `$configPath = (Resolve-Path `"applicationhost-custom.config`").Path" -ForegroundColor White
Write-Host "  Start-Process `$iisPath -ArgumentList `/config:`"`$configPath`"`,`"/site:WebSite1`" -NoNewWindow" -ForegroundColor White
Write-Host ""
Write-Host "Note: If you need to access from outside your local network," -ForegroundColor Yellow
Write-Host "      configure port forwarding on your router for port 8080." -ForegroundColor Yellow
