@echo off
echo Starting HR Web Application for remote access...
echo.

REM Kill any existing IIS Express processes
taskkill /F /IM iisexpress.exe 2>nul

REM Wait a moment
timeout /t 2 >nul

REM Start IIS Express with external binding
"C:\Program Files (x86)\IIS Express\iisexpress.exe" /port:5002 /site:"HR Web App" /apppool:"Clr4IntegratedAppPool" /clr:v4.0 /path:"%CD%\HR.Web"

echo.
echo Application started!
echo Local Access: http://localhost:5002
echo Network Access: http://192.168.30.122:5002
echo VPN Access: http://10.203.99.38:5002
echo.
echo Press any key to stop...
pause > nul
