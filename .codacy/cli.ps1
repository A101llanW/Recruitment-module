[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArgs
)

$ErrorActionPreference = "Stop"

$binName = "codacy-cli-v2.exe"
$osName = "windows"
$arch = if ([Environment]::Is64BitOperatingSystem) { "amd64" } else { "386" }

if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64" -or $env:PROCESSOR_ARCHITEW6432 -eq "ARM64") {
    $arch = "arm64"
}

if ([string]::IsNullOrWhiteSpace($env:CODACY_CLI_V2_TMP_FOLDER)) {
    $env:CODACY_CLI_V2_TMP_FOLDER = Join-Path $env:LOCALAPPDATA "Codacy\codacy-cli-v2"
}

$tmpFolder = $env:CODACY_CLI_V2_TMP_FOLDER
$versionFile = Join-Path $tmpFolder "version.txt"

function Get-LatestVersion {
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        $headers["Authorization"] = "Bearer $($env:GH_TOKEN)"
    }

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/codacy/codacy-cli-v2/releases/latest" -Headers $headers
    if ([string]::IsNullOrWhiteSpace($release.tag_name)) {
        throw "Unable to resolve latest Codacy CLI v2 version from GitHub releases."
    }

    return $release.tag_name
}

function Ensure-VersionFile {
    if (-not (Test-Path -LiteralPath $versionFile)) {
        New-Item -ItemType Directory -Force -Path $tmpFolder | Out-Null
        $latestVersion = Get-LatestVersion
        Set-Content -LiteralPath $versionFile -Value $latestVersion -NoNewline
        return
    }
}

function Resolve-Version {
    if (-not [string]::IsNullOrWhiteSpace($env:CODACY_CLI_V2_VERSION)) {
        return $env:CODACY_CLI_V2_VERSION
    }

    $version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = Get-LatestVersion
        Set-Content -LiteralPath $versionFile -Value $version -NoNewline
    }

    return $version
}

$isUpdate = $CliArgs.Count -gt 0 -and $CliArgs[0] -eq "update"
if ($isUpdate -or -not (Test-Path -LiteralPath $versionFile)) {
    New-Item -ItemType Directory -Force -Path $tmpFolder | Out-Null
    $latestVersion = Get-LatestVersion
    Set-Content -LiteralPath $versionFile -Value $latestVersion -NoNewline
}

Ensure-VersionFile
$version = Resolve-Version

$binFolder = Join-Path $tmpFolder $version
$binPath = Join-Path $binFolder $binName

if (-not (Test-Path -LiteralPath $binPath)) {
    New-Item -ItemType Directory -Force -Path $binFolder | Out-Null

    $zipName = "codacy-cli-v2_${version}_${osName}_${arch}.zip"
    $zipPath = Join-Path $binFolder $zipName
    $downloadUrl = "https://github.com/codacy/codacy-cli-v2/releases/download/$version/$zipName"

    Write-Host "Downloading Codacy CLI v2 from: $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $binFolder -Force
}

if ($CliArgs.Count -eq 1 -and $CliArgs[0] -eq "download") {
    Write-Host "Codacy CLI v2 download succeeded"
    exit 0
}

& $binPath @CliArgs
if ($LASTEXITCODE -ne $null) {
    exit $LASTEXITCODE
}
