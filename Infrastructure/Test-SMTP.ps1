$configPath = "c:\Users\allan\Documents\Examples\HR\HR.Web\secrets.config"
[xml]$secrets = Get-Content $configPath

$smtpHost = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpHost" } | Select-Object -ExpandProperty value
$smtpPort = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpPort" } | Select-Object -ExpandProperty value
$smtpUser = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpUser" } | Select-Object -ExpandProperty value
$smtpPass = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpPassword" } | Select-Object -ExpandProperty value
$enableSsl = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpEnableSsl" } | Select-Object -ExpandProperty value

Write-Host "--- SMTP Configuration Test ---"
Write-Host "Host: $smtpHost"
Write-Host "Port: $smtpPort"
Write-Host "User: $smtpUser"
Write-Host "SSL:  $enableSsl"
Write-Host "-------------------------------"

if ($smtpUser -eq "your-email@gmail.com" -or $smtpPass -eq "your-app-password") {
    Write-Error "Action Required: Please update SmtpUser and SmtpPassword in secrets.config before testing."
    exit
}

$to = Read-Host "Enter an email address to send the test MFA code to"

$subject = "TEST: MFA Verification Code"
$code = Get-Random -Minimum 100000 -Maximum 999999
$body = "Your test verification code is: $code. This is a system test for Nanosoft HR."

try {
    $securePass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
    $creds = New-Object System.Management.Automation.PSCredential($smtpUser, $securePass)
    
    Send-MailMessage -To $to `
                     -From $smtpUser `
                     -Subject $subject `
                     -Body $body `
                     -SmtpServer $smtpHost `
                     -Port $smtpPort `
                     -UseSsl `
                     -Credential $creds `
                     -ErrorAction Stop
    
    Write-Host "`n✅ SUCCESS! Test email sent to $to."
    Write-Host "Check your inbox for the code: $code"
} catch {
    Write-Host "`n❌ FAILED!"
    Write-Error $_.Exception.Message
}
