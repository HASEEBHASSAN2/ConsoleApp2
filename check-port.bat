@echo off
echo ========================================
echo Scanner API Port Configuration
echo ========================================
echo.

echo Checking if port 5195 is in use...
netstat -ano | findstr :5195
echo.

echo Current URL reservations:
netsh http show urlacl | findstr 5195
echo.

echo To reserve the URL (run as Administrator):
echo netsh http add urlacl url=http://localhost:5195/ user=Everyone
echo.

echo To delete reservation:
echo netsh http delete urlacl url=http://localhost:5195/
echo.

pause
