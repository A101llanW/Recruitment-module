#Requires -Version 5.0
# Scan HR.Web Razor views for encoding and parser-risk patterns.
# Usage: powershell -ExecutionPolicy Bypass -File tools\dev\Scan-ViewEncodingIssues.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$viewsRoot = Join-Path $repoRoot 'HR.Web\Views'
$issues = New-Object System.Collections.Generic.List[string]

Get-ChildItem -Path $viewsRoot -Filter '*.cshtml' -Recurse | ForEach-Object {
    $rel = $_.FullName.Substring($repoRoot.Length + 1)
    $text = [System.IO.File]::ReadAllText($_.FullName, [System.Text.UTF8Encoding]::new($false))

    if ($text -match '@Html\.ValidationSummary[^\r\n]*\r?\n\s*@if\s*\(') {
        $issues.Add("${rel} ValidationSummary followed by @if (Razor parser risk)")
    }

    $lineNum = 0
    foreach ($line in ($text -split "`r?`n")) {
        $lineNum++
        $trim = $line.Trim()

        if ($trim -match 'â|Ã.|ï¿½') {
            $issues.Add("${rel}:${lineNum} mojibake")
            continue
        }

        if ($trim -match '[\u2026]' -and $trim -match "\.text\(|\.html\(") {
            $issues.Add("${rel}:${lineNum} unicode ellipsis in JS string")
        }

        if ($line -match '@\([^)]*[\u2013\u2014][^)]*\)' -or $line -match '@Custom[A-Za-z.]+[\u2013\u2014]') {
            $issues.Add("${rel}:${lineNum} unicode dash in Razor expression")
        }
    }
}

Write-Host "Scanned: $viewsRoot" -ForegroundColor Cyan
if ($issues.Count -eq 0) {
    Write-Host 'No issues found.' -ForegroundColor Green
    exit 0
}

Write-Host "$($issues.Count) issue(s):" -ForegroundColor Yellow
$issues | ForEach-Object { Write-Host "  $_" }
exit 1