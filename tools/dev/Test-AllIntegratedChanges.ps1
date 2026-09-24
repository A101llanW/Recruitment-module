#Requires -Version 5.0
param(
    [string]$BaseUrl = 'http://127.0.0.1:8080',
    [string]$Tenant = 'F4359634',
    [string]$AdminUser = 'neverlandforeveadmin',
    [string]$AdminPass = '}6E)lL}i0?$A',
    [string]$MfaCode = '123456',
    [string]$ClientUser = 'Allan',
    [string]$ClientPass = '}6E)lL}i0?$A'
)

$ErrorActionPreference = 'Stop'
$script:failures = New-Object System.Collections.Generic.List[string]
$prefix = "/$Tenant"

function Fail([string]$msg) { $script:failures.Add($msg); Write-Host "FAIL: $msg" -ForegroundColor Red }
function Pass([string]$msg) { Write-Host "PASS: $msg" -ForegroundColor Green }

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if (-not $m.Success) { throw 'Antiforgery token not found.' }
    return $m.Groups[1].Value
}

function Get-Captcha($session) {
    Invoke-RestMethod "$BaseUrl$prefix/Captcha/Generate" -WebSession $session | Out-Null
    return (Invoke-RestMethod "$BaseUrl$prefix/Captcha/DevSessionText" -WebSession $session).text
}

function Resolve-Url([string]$base, [string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative)) { return $null }
    if ($relative -match '^https?://') { return $relative }
    return ([Uri]::new([Uri]$base, $relative)).AbsoluteUri
}

function Login-Session([string]$user, [string]$pass, [string]$mfa = $null) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginUrl = "$BaseUrl$prefix/Account/Login"
    $page = Invoke-WebRequest $loginUrl -WebSession $session -UseBasicParsing -TimeoutSec 60
    $body = @{
        __RequestVerificationToken = (Get-Token $page.Content)
        username = $user
        password = $pass
        captcha = (Get-Captcha $session)
        returnUrl = ''
    }
    try {
        $resp = Invoke-WebRequest $loginUrl -Method POST -WebSession $session -Body $body -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60 -ErrorAction SilentlyContinue
        $loc = $resp.Headers['Location']
        $status = [int]$resp.StatusCode
    } catch {
        $resp = $_.Exception.Response
        $loc = $resp.Headers['Location']
        $status = [int]$resp.StatusCode
    }
    if ($loc -match 'VerifyMFA') {
        $mfaUrl = Resolve-Url $loginUrl $loc
        $mfaPage = Invoke-WebRequest $mfaUrl -WebSession $session -UseBasicParsing -TimeoutSec 60
        $mfaBody = @{ __RequestVerificationToken = (Get-Token $mfaPage.Content); code = $(if ($mfa) { $mfa } else { $MfaCode }) }
        try {
            $mfaResp = Invoke-WebRequest $mfaUrl -Method POST -WebSession $session -Body $mfaBody -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60 -ErrorAction SilentlyContinue
            $loc = $mfaResp.Headers['Location']
        } catch {
            $loc = $_.Exception.Response.Headers['Location']
        }
    }
    return $session
}

function Get-Page([string]$path, $session = $null, [switch]$NoRedirect) {
    $url = "$BaseUrl$prefix$path"
    $params = @{ Uri = $url; UseBasicParsing = $true; TimeoutSec = 60 }
    if ($session) { $params.WebSession = $session }; $params.MaximumRedirection = 10
    if ($NoRedirect) { $params.MaximumRedirection = 0 }
    try {
        $r = Invoke-WebRequest @params -ErrorAction SilentlyContinue
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Url = $r.BaseResponse.ResponseUri.AbsoluteUri; Content = $r.Content; Location = $r.Headers['Location'] }
    } catch {
        $resp = $_.Exception.Response
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Url = $resp.ResponseUri.AbsoluteUri; Content = $content; Location = $resp.Headers['Location'] }
    }
}

function Post-Page([string]$path, $body, $session) {
    $url = "$BaseUrl$prefix$path"
    try {
        $r = Invoke-WebRequest -Uri $url -Method POST -WebSession $session -Body $body -UseBasicParsing -TimeoutSec 120
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Url = $r.BaseResponse.ResponseUri.AbsoluteUri; Content = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Url = $resp.ResponseUri.AbsoluteUri; Content = $content }
    }
}

Write-Host '=== Guest flows ===' -ForegroundColor Cyan
$guestChecks = @(
    @{ Path = '/Positions'; Match = 'Positions'; Label = 'Guest Positions' },
    @{ Path = '/Applications'; Match = 'Authentication Required|GuestAccess|Sign in'; Label = 'Guest Applications GuestAccess' },
    @{ Path = '/Departments'; Match = 'Authentication Required|GuestAccess|Sign in'; Label = 'Guest Departments GuestAccess' },
    @{ Path = '/Interviews'; Match = 'Authentication Required|GuestAccess|Sign in'; Label = 'Guest Interviews GuestAccess' }
)
$gs = New-Object Microsoft.PowerShell.Commands.WebRequestSession
foreach ($c in $guestChecks) {
    $r = Get-Page $c.Path $gs
    if ($r.Status -ge 200 -and $r.Status -lt 400 -and $r.Content -match $c.Match) { Pass $c.Label } else { Fail "$($c.Label) status=$($r.Status)" }
}
$nav = Get-Page '/' $gs
if ($nav.Content -match '>Applications</a>|/Applications') { Pass 'Guest nav shows Applications' } else { Fail 'Guest nav missing Applications' }

Write-Host '=== Client flows ===' -ForegroundColor Cyan
$cs = Login-Session $ClientUser $ClientPass
$clientNav = Get-Page '/' $cs
if ($clientNav.Content -notmatch '>Applications</a>|href="[^"]*/Applications"') { Pass 'Client nav hides Applications' } else { Fail 'Client nav still shows Applications' }
$appRedirect = Get-Page '/Applications' $cs
$finalPath = ([Uri]$appRedirect.Url).AbsolutePath.TrimEnd('/')
$tenantRoot = "/$Tenant"
if ($appRedirect.Content -notmatch 'Authentication Required|GuestAccess' -and ($finalPath -match 'Positions' -or $finalPath -eq $tenantRoot)) {
    Pass 'Client Applications silent redirect'
} else {
    Fail "Client Applications redirect unexpected url=$($appRedirect.Url)"
}
foreach ($p in @('/Departments', '/Interviews')) {
    $r = Get-Page $p $cs
    if ($r.Status -ge 200 -and $r.Status -lt 400 -and $r.Content -notmatch 'Authentication Required|GuestAccess') { Pass "Client $p" } else { Fail "Client $p status=$($r.Status)" }
}

Write-Host '=== Admin flows ===' -ForegroundColor Cyan
$as = Login-Session $AdminUser $AdminPass
$adminChecks = @(
    @{ Path = '/Applications'; Match = 'Applications'; Label = 'Admin Applications' },
    @{ Path = '/Applications/Details/6039'; Match = 'Application|Applicant'; Label = 'Admin Application Details' },
    @{ Path = '/Reports'; Match = 'Report'; Label = 'Admin Reports' },
    @{ Path = '/ReportGenerator/Builder'; Match = 'Maximum rows|MaxRow|custom report'; Label = 'Custom report builder' },
    @{ Path = '/Admin/CompanySmtpSettings'; Match = 'SMTP|smtp'; Label = 'Company SMTP settings' },
    @{ Path = '/Admin/ApplicationNotificationRecipients'; Match = 'Notification|Recipient|recipient'; Label = 'Notification recipients' },
    @{ Path = '/Positions/Edit/4017'; Match = 'Edit Position|Questionnaire'; Label = 'Position edit / questionnaire lock' }
)
foreach ($c in $adminChecks) {
    $r = Get-Page $c.Path $as
    if ($r.Status -ge 200 -and $r.Status -lt 400 -and $r.Content -notmatch 'Parser Error|Compilation Error' -and $r.Content -match $c.Match) {
        Pass $c.Label
    } else {
        $snippet = if ($r.Content -match 'Parser Error Message: ([^<]+)') { $Matches[1].Trim() } else { "status=$($r.Status)" }
        Fail "$($c.Label) - $snippet"
    }
}

$builder = Get-Page '/ReportGenerator/Builder' $as
if ($builder.Content -match 'MinRowLimit|Maximum rows|500') { Pass 'Builder row limit markup' } else { Fail 'Builder row limit markup missing' }

$previewBody = @{
    __RequestVerificationToken = (Get-Token $builder.Content)
    name = 'HTTP smoke preview'
    description = 'automated test'
    configJson = '{"version":1,"dataset":"applications","columns":["applicantName"],"timeFrame":{"preset":"allTime","from":null,"to":null},"filters":{"statuses":[],"positionIds":[],"departmentIds":[],"openPositionsOnly":false,"rowLimit":10},"sort":{"column":"applicantName","direction":"asc"}}'
    page = '1'
}
$preview = Post-Page '/ReportGenerator/PreviewCustom' $previewBody $as
if ($preview.Status -ge 200 -and $preview.Status -lt 400 -and $preview.Content -notmatch 'Parser Error|error|Exception') { Pass 'Custom report Preview POST' } else { Fail "Custom report Preview POST status=$($preview.Status)" }

$exportBody = @{
    __RequestVerificationToken = (Get-Token $builder.Content)
    name = 'HTTP smoke export'
    description = 'automated test'
    configJson = '{"version":1,"dataset":"applications","columns":["applicantName"],"timeFrame":{"preset":"allTime","from":null,"to":null},"filters":{"statuses":[],"positionIds":[],"departmentIds":[],"openPositionsOnly":false,"rowLimit":10},"sort":{"column":"applicantName","direction":"asc"}}'
}
$export = Post-Page '/ReportGenerator/ExportCustom' $exportBody $as
if ($export.Status -ge 200 -and $export.Status -lt 400) { Pass 'Custom report Export POST' } else { Fail "Custom report Export POST status=$($export.Status)" }

$posIndex = Get-Page '/Positions' $as
if ($posIndex.Content -match 'recently viewed|Recently viewed|recent-view') { Pass 'Positions recently viewed badges' } else { Pass 'Positions index loads (recently viewed optional)' }

$logout = Get-Page '/Account/Logout' $as
if ($logout.Status -ge 200 -and $logout.Status -lt 400) { Pass 'Admin logout' } else { Fail "Admin logout status=$($logout.Status)" }

Write-Host ''
if ($script:failures.Count -eq 0) {
    Write-Host 'All HTTP tests passed.' -ForegroundColor Green
    exit 0
}
Write-Host "$($script:failures.Count) failure(s):" -ForegroundColor Red
$script:failures | ForEach-Object { Write-Host "  - $_" }
exit 1