@echo off
echo Starting HR Web Application...
echo.

REM Kill existing IIS Express
taskkill /F /IM iisexpress.exe 2>nul

REM Wait
timeout /t 2 >nul

REM Start IIS Express with path-based configuration
"C:\Program Files (x86)\IIS Express\iisexpress.exe" /port:5002 /clr:v4.0 /path:"C:\Users\allan\Documents\Examples\Recruitment\HR.Web"

echo.
echo Application should now be running on:
echo Local: http://localhost:5002
echo Network: http://192.168.30.122:5002
echo VPN: http://10.203.99.38:5002
echo.
pause
