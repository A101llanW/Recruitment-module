#Requires -Version 5.0
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:8080'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Get-AntiforgeryToken {
    param([string]$Html)
    return [regex]::Match($Html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
}

function Get-CaptchaText {
    param($WebSession)
    Invoke-RestMethod -Uri "$base/Captcha/Generate" -WebSession $WebSession | Out-Null
    return (Invoke-RestMethod -Uri "$base/Captcha/DevSessionText" -WebSession $WebSession).text
}

function Test-Login {
    param(
        [string]$TenantSlug,
        [string]$Username,
        [string]$Password
    )

    $prefix = if ([string]::IsNullOrEmpty($TenantSlug)) { '' } else { "/$TenantSlug" }
    $loginPath = "$prefix/Account/Login"
    Invoke-WebRequest -Uri "$base$loginPath" -WebSession $session -UseBasicParsing | Out-Null
    $loginPage = Invoke-WebRequest -Uri "$base$loginPath" -WebSession $session -UseBasicParsing
    $token = Get-AntiforgeryToken -Html $loginPage.Content
    $captcha = Get-CaptchaText -WebSession $session
    $body = @{
        __RequestVerificationToken = $token
        username = $Username
        password = $Password
        captcha = $captcha
        returnUrl = ''
    }
    try {
        $resp = Invoke-WebRequest -Uri "$base$loginPath" -Method POST -WebSession $session -Body $body -MaximumRedirection 0 -UseBasicParsing -ErrorAction SilentlyContinue
    } catch {
        $resp = $_.Exception.Response
    }
    $url = if ($resp.Headers['Location']) { $resp.Headers['Location'] } else { '(no redirect)' }
    $status = if ($resp.StatusCode) { [int]$resp.StatusCode } else { 0 }
    $content = if ($resp.Content) { $resp.Content } else { '' }
    $hasError = $content -match 'Invalid username|Invalid security|alert-danger'
    $hasInfo = $content -match 'password was accepted|legalConsentForm'
    $hasMfa = $url -match 'VerifyMFA'
    Write-Host "Tenant=$TenantSlug User=$Username => status=$status url=$url error=$hasError info=$hasInfo mfa=$hasMfa"
}

Test-Login -TenantSlug 'F4359634' -Username 'neverlandforeveadmin' -Password '}6E)lL}i0?$A'
Test-Login -TenantSlug 'H73485888' -Username 'amwamw2134@gmail.com' -Password '}6E)lL}i0?$A'
Test-Login -TenantSlug 'H73485888' -Username 'AMW' -Password 'wrongpassword'
