@echo off
echo Setting up remote access for HR Web Application...
echo.

REM Allow port 5002 through Windows Firewall
netsh advfirewall firewall add rule name="HR Web App Port 5002" dir=in action=allow protocol=TCP localport=5002

echo.
echo Firewall rule added for port 5002
echo.
echo Your application will be accessible from:
echo Local Network: http://192.168.30.122:5002
echo ZeroTier VPN:  http://10.203.99.38:5002
echo.
echo Press any key to continue...
pause > nul
