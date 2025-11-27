@echo off
REM Cosmos DB Emulator - Clean/Reset
REM This script stops the emulator and removes all data

echo ================================================
echo Azure Cosmos DB Emulator - Clean/Reset
echo ================================================
echo.
echo WARNING: This will DELETE all Cosmos DB data!
echo.

set /p confirm="Are you sure you want to continue? (yes/no): "
if /i not "%confirm%"=="yes" (
    echo Operation cancelled.
    pause
    exit /b 0
)

echo.

REM Navigate to script directory
cd /d "%~dp0"

echo Stopping Cosmos DB Emulator...
docker-compose -f docker-compose.cosmos.yml down

echo.
echo Removing volumes and data...
docker volume rm cosmos-data 2>nul

echo.
echo ? Cosmos DB Emulator cleaned
echo.
echo ?? Run cosmos-start.bat to start fresh
echo.

pause
