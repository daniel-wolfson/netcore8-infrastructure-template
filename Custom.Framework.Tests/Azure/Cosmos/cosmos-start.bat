@echo off
REM Cosmos DB Emulator - Start Script
REM This script starts the Cosmos DB Emulator using Docker Compose

echo ================================================
echo Azure Cosmos DB Emulator - Start
echo ================================================
echo.

REM Check if Docker is running
docker info > nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running!
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo Starting Cosmos DB Emulator...
echo.

REM Navigate to script directory
cd /d "%~dp0"

REM Start Cosmos DB Emulator
docker-compose -f docker-compose.cosmos.yml up -d cosmos-emulator

if errorlevel 1 (
    echo.
    echo ERROR: Failed to start Cosmos DB Emulator
    pause
    exit /b 1
)

echo.
echo ? Cosmos DB Emulator is starting...
echo.
echo ? Please wait 1-2 minutes for the emulator to fully initialize
echo    The emulator is resource-intensive and takes time to start
echo.
echo ?? Connection Details:
echo    Endpoint: https://localhost:8081
echo    Key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
echo.
echo ?? Data Explorer: https://localhost:8081/_explorer/index.html
echo.
echo ?? To initialize database/container, run: cosmos-init.bat
echo ?? To stop: cosmos-stop.bat
echo ?? To view logs: docker logs -f cosmos-emulator
echo.

pause
