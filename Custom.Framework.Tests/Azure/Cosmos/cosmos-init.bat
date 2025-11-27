@echo off
REM Cosmos DB Emulator - Initialize Database/Container
REM This script creates the database and container with proper configuration

echo ================================================
echo Azure Cosmos DB Emulator - Initialize
echo ================================================
echo.

REM Navigate to script directory
cd /d "%~dp0"

REM Check if Cosmos emulator is running
docker ps | findstr "cosmos-emulator" > nul
if errorlevel 1 (
    echo ERROR: Cosmos DB Emulator is not running!
    echo Please run cosmos-start.bat first.
    pause
    exit /b 1
)

echo Running initialization...
echo.

REM Run initialization using the init profile
docker-compose -f docker-compose.cosmos.yml --profile init up cosmos-init

echo.
echo ? Initialization complete
echo.
echo ?? Database: HospitalityOrders
echo ?? Container: Orders
echo ?? Partition Key: /hotelCode
echo.
echo ?? View in Data Explorer: https://localhost:8081/_explorer/index.html
echo.

pause
