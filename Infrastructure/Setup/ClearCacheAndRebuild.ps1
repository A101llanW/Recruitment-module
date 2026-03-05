# Clear ASP.NET Temporary Files and Rebuild
# This script clears cached assemblies and rebuilds the application

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   CLEARING ASP.NET CACHE AND REBUILDING" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop IIS Express
Write-Host "1. Stopping IIS Express..." -ForegroundColor Yellow
Get-Process -Name "iisexpress" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "   Done: IIS Express stopped" -ForegroundColor Green

# Step 2: Clear Temporary ASP.NET Files
Write-Host ""
Write-Host "2. Clearing ASP.NET temporary files..." -ForegroundColor Yellow
$tempPaths = @(
    "$env:LOCALAPPDATA\Temp\Temporary ASP.NET Files",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files"
)

foreach ($path in $tempPaths) {
    if (Test-Path $path) {
        try {
            Remove-Item -Path "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "   Cleared: $path" -ForegroundColor Green
        }
        catch {
            Write-Host "   Could not clear: $path (may be in use)" -ForegroundColor DarkYellow
        }
    }
}

# Step 3: Clean Solution
Write-Host ""
Write-Host "3. Cleaning solution..." -ForegroundColor Yellow
$msbuild = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
$solution = "c:\Users\allan\Documents\Examples\HR\HR.sln"

& $msbuild $solution /t:Clean /p:Configuration=Debug /v:minimal
Write-Host "   Done: Solution cleaned" -ForegroundColor Green

# Step 4: Rebuild Solution
Write-Host ""
Write-Host "4. Rebuilding solution..." -ForegroundColor Yellow
& $msbuild $solution /t:Build /p:Configuration=Debug /v:minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "   Done: Build successful!" -ForegroundColor Green
}
else {
    Write-Host "   Error: Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "              CACHE CLEARED AND REBUILD COMPLETE" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Press F5 in Visual Studio to start the application" -ForegroundColor White
Write-Host "2. The application should now recognize the new database columns" -ForegroundColor White
Write-Host ""
