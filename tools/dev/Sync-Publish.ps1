# Sync HR.Web Release build to Publish\ for IIS deployment.
# Run from repository root: .\tools\dev\Sync-Publish.ps1

param(
    [switch]$SkipBuild,
    [switch]$UpdateSecrets,
    [switch]$ShowDetailedErrors
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Join-Path $repoRoot "HR.Web"
$dest = Join-Path $repoRoot "Publish"
$solution = Join-Path $repoRoot "HR.sln"

if (-not (Test-Path $source)) {
    Write-Error "HR.Web not found at $source"
}

Write-Host "=== Sync HR.Web -> Publish ===" -ForegroundColor Cyan
Write-Host "Source: $source"
Write-Host "Dest:   $dest"
Write-Host ""

if (-not $SkipBuild) {
    Write-Host "Building Release..." -ForegroundColor Yellow
    Push-Location $repoRoot
    try {
        $msbuild = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuild)) {
            $msbuild = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
        }
        if (-not (Test-Path $msbuild)) {
            $msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
        }
        if (-not (Test-Path $msbuild)) {
            $msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        }
        if (Test-Path $msbuild) {
            & $msbuild $solution /p:Configuration=Release /t:Rebuild /v:minimal
        }
        else {
            dotnet build $solution -c Release -t:Rebuild -v minimal
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
    Write-Host "Build OK." -ForegroundColor Green
}

$preserveSecretsPath = Join-Path $dest "secrets.config"
$secretsBackup = $null
if ((Test-Path $preserveSecretsPath) -and -not $UpdateSecrets) {
    $secretsBackup = Get-Content -Path $preserveSecretsPath -Raw -Encoding UTF8
    Write-Host "Preserving existing Publish\secrets.config" -ForegroundColor DarkGray
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null

function Set-ProductionWebConfig {
    param(
        [string]$WebConfigPath,
        [bool]$DetailedErrors = $false
    )

    if (-not (Test-Path $WebConfigPath)) {
        Write-Error "Web.config not found at $WebConfigPath"
    }

    [xml]$doc = Get-Content -Path $WebConfigPath
    $hasShowDetailedErrors = $false
    foreach ($add in $doc.configuration.appSettings.add) {
        if ($add.key -eq "AppEnvironment") {
            $add.SetAttribute("value", $(if ($DetailedErrors) { "Remote/Dev" } else { "Production" }))
        }
        elseif ($add.key -eq "ShowDetailedErrors") {
            $add.SetAttribute("value", $(if ($DetailedErrors) { "true" } else { "false" }))
            $hasShowDetailedErrors = $true
        }
        elseif ($add.key -eq "LastRestart") {
            $add.SetAttribute("value", (Get-Date -Format "yyyy-MM-dd") + "-prod")
        }
    }

    if (-not $hasShowDetailedErrors) {
        $appSettings = $doc.configuration.appSettings
        $showDetailedNode = $doc.CreateElement("add")
        $showDetailedNode.SetAttribute("key", "ShowDetailedErrors")
        $showDetailedNode.SetAttribute("value", $(if ($DetailedErrors) { "true" } else { "false" }))
        $null = $appSettings.AppendChild($showDetailedNode)
    }

    $systemWeb = $doc.configuration."system.web"
    $customErrors = $doc.CreateElement("customErrors")
    if ($DetailedErrors) {
        $customErrors.SetAttribute("mode", "Off")
    }
    else {
        $customErrors.SetAttribute("mode", "On")
        $customErrors.SetAttribute("redirectMode", "ResponseRewrite")
        $customErrors.SetAttribute("defaultRedirect", "~/Home/Error")
        foreach ($status in @(
                @{ code = "404"; redirect = "~/Home/NotFound" },
                @{ code = "500"; redirect = "~/Home/Error" }
            )) {
            $errorNode = $doc.CreateElement("error")
            $errorNode.SetAttribute("statusCode", $status.code)
            $errorNode.SetAttribute("redirect", $status.redirect)
            $null = $customErrors.AppendChild($errorNode)
        }
    }
    $null = $systemWeb.ReplaceChild($customErrors, $systemWeb.customErrors)
    $systemWeb.compilation.SetAttribute("debug", "true")

    $httpErrors = $doc.CreateElement("httpErrors")
    $httpErrors.SetAttribute("existingResponse", "PassThrough")
    if ($DetailedErrors) {
        $httpErrors.SetAttribute("errorMode", "Detailed")
    }
    else {
        $httpErrors.SetAttribute("errorMode", "Custom")
        $remove404 = $doc.CreateElement("remove")
        $remove404.SetAttribute("statusCode", "404")
        $null = $httpErrors.AppendChild($remove404)
        $error404 = $doc.CreateElement("error")
        $error404.SetAttribute("statusCode", "404")
        $error404.SetAttribute("path", "/Home/NotFound")
        $error404.SetAttribute("responseMode", "ExecuteURL")
        $null = $httpErrors.AppendChild($error404)
    }
    $webServer = $doc.configuration."system.webServer"
    $null = $webServer.ReplaceChild($httpErrors, $webServer.httpErrors)

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = "`n"
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($WebConfigPath, $settings)
    try {
        $doc.Save($writer)
    }
    finally {
        $writer.Close()
    }

    if ($DetailedErrors) {
        Write-Host "  Applied publish Web.config (AppEnvironment=Remote/Dev, customErrors=Off, httpErrors=Detailed, debug=true)" -ForegroundColor Green
    }
    else {
        Write-Host "  Applied publish Web.config (AppEnvironment=Production, customErrors=On+Rewrite, debug=true)" -ForegroundColor Green
    }
}

function Invoke-RoboMirror {
    param(
        [string]$RelativePath,
        [string]$DestinationRelativePath = $RelativePath,
        [string[]]$ExtraExcludeFiles = @()
    )
    $srcDir = Join-Path $source $RelativePath
    $dstDir = Join-Path $dest $DestinationRelativePath
    if (-not (Test-Path $srcDir)) {
        Write-Host "  Skip (missing): $RelativePath" -ForegroundColor DarkYellow
        return
    }
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    $exclude = @("*.md", "Test-*.ps1", "mfa_codes.txt", "verification_codes.txt", "email_errors.txt", "disk_files_utf8.txt") + $ExtraExcludeFiles
    $xf = ($exclude | ForEach-Object { "/XF"; $_ })
    $args = @(
        $srcDir, $dstDir,
        "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/NC", "/NS", "/NP"
    ) + $xf
    $null = & robocopy @args
    $code = $LASTEXITCODE
    if ($code -ge 8) {
        throw "robocopy failed for $RelativePath (exit $code)"
    }
    Write-Host "  Synced: $RelativePath" -ForegroundColor Green
}

$releaseBin = Join-Path $source "bin\Release"
if (-not (Test-Path (Join-Path $releaseBin "HR.Web.dll"))) {
    Write-Error "Missing $releaseBin\HR.Web.dll. Run without -SkipBuild or build Release first."
}

Write-Host "Mirroring deploy folders..." -ForegroundColor Yellow
Invoke-RoboMirror -RelativePath "bin\Release" -DestinationRelativePath "bin"
Invoke-RoboMirror -RelativePath "Content"
Invoke-RoboMirror -RelativePath "Views"
Invoke-RoboMirror -RelativePath "Scripts" -ExtraExcludeFiles @("*.ps1", "*.sql", "*.txt")

$rootFiles = @("Global.asax", "Web.config", "sw.js", "offline.html")
foreach ($file in $rootFiles) {
    $srcFile = Join-Path $source $file
    if (Test-Path $srcFile) {
        Copy-Item -Path $srcFile -Destination (Join-Path $dest $file) -Force
        Write-Host "  Copied: $file" -ForegroundColor Green
    }
}

$publishWebConfig = Join-Path $dest "Web.config"
if (Test-Path $publishWebConfig) {
    Set-ProductionWebConfig -WebConfigPath $publishWebConfig -DetailedErrors:$ShowDetailedErrors
}

if ($UpdateSecrets -and (Test-Path (Join-Path $source "secrets.config"))) {
    Copy-Item -Path (Join-Path $source "secrets.config") -Destination $preserveSecretsPath -Force
    Write-Host "  Updated: secrets.config from HR.Web" -ForegroundColor Green
}
elseif ($secretsBackup) {
    Set-Content -Path $preserveSecretsPath -Value $secretsBackup -Encoding UTF8 -NoNewline
}
elseif (Test-Path (Join-Path $source "secrets.config")) {
    if (-not (Test-Path $preserveSecretsPath)) {
        Copy-Item -Path (Join-Path $source "secrets.config") -Destination $preserveSecretsPath -Force
        Write-Host "  Copied: secrets.config (new)" -ForegroundColor Green
    }
}

$devViews = @(
    (Join-Path $dest "Views\Home\Debug.cshtml"),
    (Join-Path $dest "Views\Home\Index.cshtml")
)
foreach ($view in $devViews) {
    if (Test-Path $view) {
        Remove-Item -Path $view -Force
        Write-Host "  Removed dev view: $view" -ForegroundColor DarkGray
    }
}

Write-Host "Cleaning non-production artifacts..." -ForegroundColor Yellow
$binViews = Join-Path $dest "bin\Views"
if (Test-Path $binViews) {
    Remove-Item -Path $binViews -Recurse -Force
    Write-Host "  Removed stale bin\Views (not used at runtime)" -ForegroundColor DarkGray
}
$purgePatterns = @("*.md", "Test-*.ps1", "mfa_codes.txt", "verification_codes.txt", "email_errors.txt", "disk_files_utf8.txt")
foreach ($pattern in $purgePatterns) {
    Get-ChildItem -Path $dest -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

$dll = Get-Item (Join-Path $dest "bin\HR.Web.dll") -ErrorAction Stop
Write-Host ""
Write-Host "Publish package ready." -ForegroundColor Cyan
Write-Host "  HR.Web.dll: $($dll.LastWriteTime) ($([math]::Round($dll.Length / 1KB)) KB)"
Write-Host "  Path: $dest"
