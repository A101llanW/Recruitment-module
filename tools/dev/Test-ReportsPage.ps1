param(
    [string] $BaseUrl = 'http://localhost:8080/F4359634',
    [string] $Password = 'Neverland9!x'
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$login = Invoke-WebRequest -Uri "$base/Account/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($login.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
if (-not $token) { throw 'Missing antiforgery token on login page' }

Invoke-WebRequest -Uri "$base/Captcha/Generate" -WebSession $session -UseBasicParsing | Out-Null
$captchaJson = (Invoke-WebRequest -Uri "$base/Captcha/DevSessionText" -WebSession $session -UseBasicParsing).Content | ConvertFrom-Json
if (-not $captchaJson.success) { throw $captchaJson.message }

$body = @{
    __RequestVerificationToken = $token
    Username = 'neverlandforeveadmin'
    Password = $Password
    Captcha = $captchaJson.text
}
$loginPost = Invoke-WebRequest -Uri "$base/Account/Login" -Method POST -WebSession $session -Body $body -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
Write-Host "Login status: $($loginPost.StatusCode) location: $($loginPost.Headers.Location)"

sqlcmd -S 127.0.0.1 -U sa -P 'sa@123456' -d HR_Local -Q "UPDATE Users SET TwoFactorCode='123456', TwoFactorExpiry=DATEADD(minute,10,GETDATE()), MfaMethod='Email' WHERE UserName='neverlandforeveadmin'" | Out-Null

$mfaPage = Invoke-WebRequest -Uri "$base/Account/VerifyMFA" -WebSession $session -UseBasicParsing
$mfaToken = [regex]::Match($mfaPage.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
$mfaBody = @{
    __RequestVerificationToken = $mfaToken
    code = '123456'
}
$mfaPost = Invoke-WebRequest -Uri "$base/Account/VerifyMFA" -Method POST -WebSession $session -Body $mfaBody -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
Write-Host "MFA status: $($mfaPost.StatusCode) location: $($mfaPost.Headers.Location)"

$sw = [Diagnostics.Stopwatch]::StartNew()
$page = Invoke-WebRequest -Uri "$base/ReportGenerator/Index" -WebSession $session -UseBasicParsing -TimeoutSec 120
$sw.Stop()
Write-Host "Reports page: $($page.StatusCode) len=$($page.RawContentLength) time=$($sw.ElapsedMilliseconds)ms final=$($page.BaseResponse.ResponseUri)"

$checks = @(
    @{ Name = 'generate-reports'; Pattern = 'Generate Reports' },
    @{ Name = 'preview-fn'; Pattern = 'previewReport' },
    @{ Name = 'generate-fn'; Pattern = 'generateReport' },
    @{ Name = 'html2pdf'; Pattern = 'html2pdf' },
    @{ Name = 'download-now'; Pattern = 'DOWNLOAD NOW' },
    @{ Name = 'access-denied'; Pattern = 'Access Denied' }
)
foreach ($check in $checks) {
    $found = [regex]::IsMatch($page.Content, $check.Pattern)
    Write-Host ("{0}: {1}" -f $check.Name, $(if ($found) { 'yes' } else { 'no' }))
}

$reportsToken = [regex]::Match($page.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
Write-Host "Reports token found: $([bool]$reportsToken)"

Write-Host "Preview candidate report..."
$previewBody = @{
    __RequestVerificationToken = $reportsToken
    reportType = 'candidate'
}
$preview = Invoke-WebRequest -Uri "$base/ReportGenerator/Preview" -Method POST -WebSession $session -Body $previewBody -UseBasicParsing -TimeoutSec 120
$previewJson = $preview.Content | ConvertFrom-Json
Write-Host "Preview success: $($previewJson.success) htmlLen: $($previewJson.html.Length) message: $($previewJson.message)"

Write-Host "Generate CSV candidate..."
$genBody = @{
    __RequestVerificationToken = $reportsToken
    reportType = 'candidate'
    format = 'csv'
}
$gen = Invoke-WebRequest -Uri "$base/ReportGenerator/GenerateDirect" -Method POST -WebSession $session -Body $genBody -UseBasicParsing -TimeoutSec 120
$genJson = $gen.Content | ConvertFrom-Json
Write-Host "Generate CSV success: $($genJson.success) file: $($genJson.fileName) message: $($genJson.message)"

if ($genJson.success -and $genJson.fileName) {
    $dl = Invoke-WebRequest -Uri "$base/ReportGenerator/Download?fileName=$($genJson.fileName)" -WebSession $session -UseBasicParsing -TimeoutSec 60
    Write-Host "Download CSV: $($dl.StatusCode) bytes=$($dl.RawContentLength) type=$($dl.Headers['Content-Type'])"
}

Write-Host "Generate PDF application..."
$genPdfBody = @{
    __RequestVerificationToken = $reportsToken
    reportType = 'application'
    format = 'pdf'
}
$genPdf = Invoke-WebRequest -Uri "$base/ReportGenerator/GenerateDirect" -Method POST -WebSession $session -Body $genPdfBody -UseBasicParsing -TimeoutSec 120
$genPdfJson = $genPdf.Content | ConvertFrom-Json
Write-Host "Generate PDF success: $($genPdfJson.success) file: $($genPdfJson.fileName) message: $($genPdfJson.message)"

if ($genPdfJson.success -and $genPdfJson.fileName) {
    $dlPdf = Invoke-WebRequest -Uri "$base/ReportGenerator/Download?fileName=$($genPdfJson.fileName)" -WebSession $session -UseBasicParsing -TimeoutSec 60
    Write-Host "Download PDF: $($dlPdf.StatusCode) bytes=$($dlPdf.RawContentLength)"
}

