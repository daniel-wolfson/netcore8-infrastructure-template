@echo off
REM Cosmos DB Emulator - View Logs
REM This script displays the live logs from the Cosmos DB Emulator

echo ================================================
echo Azure Cosmos DB Emulator - Logs
echo ================================================
echo.

REM Check if Cosmos emulator is running
docker ps | findstr "cosmos-emulator" > nul
if errorlevel 1 (
    echo ERROR: Cosmos DB Emulator is not running!
    echo Please run cosmos-start.bat first.
    pause
    exit /b 1
)

echo Showing live logs (Ctrl+C to exit)...
echo.

docker logs -f cosmos-emulator
