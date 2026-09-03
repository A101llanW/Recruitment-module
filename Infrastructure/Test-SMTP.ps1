param(
    [string]$ConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "HR.Web\secrets.config")
)

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    Write-Error "Secrets file not found: $ConfigPath"
    exit 1
}

[xml]$secrets = Get-Content -LiteralPath $ConfigPath

$smtpHost = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpHost" } | Select-Object -ExpandProperty value
$smtpPort = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpPort" } | Select-Object -ExpandProperty value
$smtpUser = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpUser" } | Select-Object -ExpandProperty value
$enableSsl = $secrets.appSettings.add | Where-Object { $_.key -eq "SmtpEnableSsl" } | Select-Object -ExpandProperty value

Write-Host "--- SMTP Configuration Test ---"
Write-Host "Host: $smtpHost"
Write-Host "Port: $smtpPort"
Write-Host "User: $smtpUser"
Write-Host "SSL:  $enableSsl"
Write-Host "-------------------------------"

if ([string]::IsNullOrWhiteSpace($smtpUser) -or $smtpUser -eq "your-email@gmail.com") {
    Write-Error "Action Required: Set SmtpUser in secrets.config before testing."
    exit 1
}

$credential = Get-Credential -UserName $smtpUser -Message "Enter the SMTP password for $smtpUser"
if ($null -eq $credential) {
    Write-Error "SMTP password is required to send a test email."
    exit 1
}

$to = Read-Host "Enter an email address to send the test MFA code to"

$subject = "TEST: MFA Verification Code"
$code = Get-Random -Minimum 100000 -Maximum 999999
$body = "Your test verification code is: $code. This is a system test for Nanosoft HR."

try {
    Send-MailMessage -To $to `
                     -From $smtpUser `
                     -Subject $subject `
                     -Body $body `
                     -SmtpServer $smtpHost `
                     -Port $smtpPort `
                     -UseSsl `
                     -Credential $credential `
                     -ErrorAction Stop

    Write-Host "`nSUCCESS! Test email sent to $to."
    Write-Host "Check your inbox for the code: $code"
} catch {
    Write-Host "`nFAILED!"
    Write-Error $_.Exception.Message
}
