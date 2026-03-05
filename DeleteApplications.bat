@echo off
echo Building HR Application Cleanup Utility...
cd /d "C:\Users\allan\Documents\Examples\Recruitment"

echo Building solution...
"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" HR.sln /p:Configuration=Debug

if %ERRORLEVEL% EQU 0 (
    echo Build successful!
    echo.
    echo Running application cleanup utility...
    echo.
    
    REM Create a simple runner using PowerShell
    powershell -ExecutionPolicy Bypass -Command ^
    "$assemblyPath = 'C:\Users\allan\Documents\Examples\Recruitment\HR.Web\bin\Debug\HR.Web.dll'; ^
     $utilityPath = 'C:\Users\allan\Documents\Examples\Recruitment\HR.Web\bin\Debug\HR.Web.dll'; ^
     Add-Type -Path $assemblyPath; ^
     Add-Type -Path 'C:\Users\allan\Documents\Examples\Recruitment\packages\EntityFramework.6.4.4\lib\net45\EntityFramework.dll'; ^
     Add-Type -Path 'C:\Users\allan\Documents\Examples\Recruitment\packages\EntityFramework.6.4.4\lib\net45\EntityFramework.SqlServer.dll'; ^
     [HR.Web.Utilities.ApplicationCleanup]::ListApplications(); ^
     Write-Host 'Do you want to delete all applications? (y/N): '; ^
     $response = Read-Host; ^
     if ($response -eq 'y' -or $response -eq 'yes') { ^
         [HR.Web.Utilities.ApplicationCleanup]::DeleteAllApplications(); ^
         [HR.Web.Utilities.ApplicationCleanup]::ListApplications(); ^
     } else { ^
         Write-Host 'No applications deleted.'; ^
     }"
) else (
    echo Build failed!
)

pause
