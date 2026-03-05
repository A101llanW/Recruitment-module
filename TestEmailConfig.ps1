# Email Configuration Test Script
param(
    [switch]$Test,
    [switch]$Status
)

# Load configuration
$configPath = "C:\Users\allan\Documents\Examples\Recruitment\HR.Web\secrets.config"
if (Test-Path $configPath) {
    [xml]$config = Get-Content $configPath
    $smtpHost = ($config.appSettings.add | Where-Object { $_.key -eq "SmtpHost" }).value
    $smtpPort = ($config.appSettings.add | Where-Object { $_.key -eq "SmtpPort" }).value
    $smtpUser = ($config.appSettings.add | Where-Object { $_.key -eq "SmtpUser" }).value
    $smtpPass = ($config.appSettings.add | Where-Object { $_.key -eq "SmtpPassword" }).value
    $smtpSsl = ($config.appSettings.add | Where-Object { $_.key -eq "SmtpEnableSsl" }).value
    $fromEmail = ($config.appSettings.add | Where-Object { $_.key -eq "FromEmail" }).value
    $fromName = ($config.appSettings.add | Where-Object { $_.key -eq "FromName" }).value
} else {
    Write-Host "❌ Configuration file not found: $configPath" -ForegroundColor Red
    exit 1
}

Write-Host "HR System Email Configuration Status" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green
Write-Host ""

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  SMTP Host: $smtpHost"
Write-Host "  SMTP Port: $smtpPort"
Write-Host "  SSL Enabled: $smtpSsl"
Write-Host "  From Email: $fromEmail"
Write-Host "  From Name: $fromName"
Write-Host "  SMTP User: $smtpUser"
Write-Host "  SMTP Password: $(if ([string]::IsNullOrEmpty($smtpPass)) { 'NOT SET' } else { 'SET' })"
Write-Host ""

# Check if configuration is complete
$configComplete = -not ([string]::IsNullOrEmpty($smtpHost) -or [string]::IsNullOrEmpty($smtpUser) -or [string]::IsNullOrEmpty($smtpPass))

if (-not $configComplete) {
    Write-Host "❌ Email configuration is INCOMPLETE" -ForegroundColor Red
    Write-Host "Missing settings will prevent emails from being sent." -ForegroundColor Red
    Write-Host ""
    Write-Host "Email features that will NOT work:" -ForegroundColor Red
    Write-Host "  • Forgot Password emails"
    Write-Host "  • MFA code delivery via email"
    Write-Host "  • User notifications"
} else {
    Write-Host "✅ Email configuration appears COMPLETE" -ForegroundColor Green
    Write-Host ""
    Write-Host "Email features that SHOULD work:" -ForegroundColor Green
    Write-Host "  • Forgot Password emails"
    Write-Host "  • MFA code delivery via email"
    Write-Host "  • User notifications"
}

Write-Host ""
Write-Host "Development Fallback:" -ForegroundColor Yellow
Write-Host "  MFA codes are logged to: mfa_codes.txt"
Write-Host "  Debug output contains email information"

# Check MFA codes file
$mfaCodesPath = "C:\Users\allan\Documents\Examples\Recruitment\HR.Web\mfa_codes.txt"
if (Test-Path $mfaCodesPath) {
    $recentCodes = Get-Content $mfaCodesPath | Select-Object -Last 3
    Write-Host ""
    Write-Host "Recent MFA codes (development fallback):" -ForegroundColor Yellow
    $recentCodes | ForEach-Object { Write-Host "  $_" }
}

if ($Test -and $configComplete) {
    Write-Host ""
    Write-Host "Sending test email..." -ForegroundColor Yellow
    
    try {
        $mail = New-Object System.Net.Mail.MailMessage
        $mail.From = New-Object System.Net.Mail.MailAddress($fromEmail, $fromName)
        $mail.To.Add($smtpUser)  # Send to self
        $mail.Subject = "HR System Email Test - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        $mail.IsBodyHtml = $true
        $mail.Body = @"
<h2>Email Configuration Test</h2>
<p>This is a test email from the Nanosoft HR System to verify email configuration.</p>
<p><strong>Sent:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
<p><strong>SMTP Server:</strong> ${smtpHost}:$smtpPort</p>
<p><strong>SSL:</strong> $smtpSsl</p>
<hr>
<p><em>If you receive this email, the email configuration is working correctly.</em></p>
"@
        
        $smtp = New-Object System.Net.Mail.SmtpClient($smtpHost, $smtpPort)
        $smtp.EnableSsl = [bool]::Parse($smtpSsl)
        $smtp.Credentials = New-Object System.Net.NetworkCredential($smtpUser, $smtpPass)
        
        $smtp.Send($mail)
        
        Write-Host "✅ Test email sent successfully to: $smtpUser" -ForegroundColor Green
    } catch {
        Write-Host "❌ Test email failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
if ($configComplete) {
    Write-Host "• Email system is configured and should work for production use" -ForegroundColor Green
    Write-Host "• Forgot Password emails will be sent to users" -ForegroundColor Green
    Write-Host "• MFA codes will be delivered via email" -ForegroundColor Green
    Write-Host "• Development fallback: MFA codes are also logged locally" -ForegroundColor Yellow
} else {
    Write-Host "• Email system is NOT properly configured" -ForegroundColor Red
    Write-Host "• Forgot Password will show success message but NO email will be sent" -ForegroundColor Red
    Write-Host "• MFA codes will only be available via local log file" -ForegroundColor Yellow
}
