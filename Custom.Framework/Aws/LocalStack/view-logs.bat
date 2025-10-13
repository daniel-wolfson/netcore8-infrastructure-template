@echo off
REM ================================================
REM LocalStack - View Logs
REM ================================================

echo.
echo ================================================
echo LocalStack Logs (Press Ctrl+C to exit)
echo ================================================
echo.

cd /d "%~dp0"

docker-compose logs -f
