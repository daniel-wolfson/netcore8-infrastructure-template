@echo off
REM Cosmos DB Emulator - Stop Script
REM This script stops the Cosmos DB Emulator

echo ================================================
echo Azure Cosmos DB Emulator - Stop
echo ================================================
echo.

REM Navigate to script directory
cd /d "%~dp0"

echo Stopping Cosmos DB Emulator...
echo.

REM Stop Cosmos DB Emulator
docker-compose -f docker-compose.cosmos.yml down

if errorlevel 1 (
    echo.
    echo ERROR: Failed to stop Cosmos DB Emulator
    pause
    exit /b 1
)

echo.
echo ? Cosmos DB Emulator stopped
echo.
echo ?? To preserve data, volumes are retained
echo ?? To remove all data: cosmos-clean.bat
echo.

pause
