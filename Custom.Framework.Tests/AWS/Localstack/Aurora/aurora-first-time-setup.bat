@echo off
REM ================================================
REM Aurora PostgreSQL - First Time Setup & Verification
REM ================================================

echo.
echo ================================================
echo   Aurora PostgreSQL - First Time Setup
echo ================================================
echo.

REM Check Docker
echo [1/4] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo [X] Docker is not running! Please start Docker Desktop.
    pause
    exit /b 1
)
echo [OK] Docker is running

REM Check .NET SDK
echo.
echo [2/4] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [X] .NET SDK not found! Please install .NET 8 SDK.
    pause
    exit /b 1
)
echo [OK] .NET SDK is installed

REM Start PostgreSQL
echo.
echo [3/4] Starting Aurora PostgreSQL...
docker-compose up -d aurora-postgres

if errorlevel 1 (
    echo [X] Failed to start PostgreSQL
    pause
    exit /b 1
)

echo [OK] PostgreSQL container started
echo [INFO] Waiting 10 seconds for initialization...
timeout /t 10 /nobreak >nul

REM Verify Health
echo.
echo [4/4] Verifying PostgreSQL health...
docker exec aurora-postgres-local pg_isready -U admin -d auroradb >nul 2>&1
if errorlevel 1 (
    echo [!] Health check failed (PostgreSQL may still be starting)
    echo [INFO] View logs: docker logs aurora-postgres-local -f
) else (
    echo [OK] PostgreSQL is healthy
)

echo.
echo ================================================
echo   Aurora PostgreSQL Setup Complete!
echo ================================================
echo.
echo Next steps:
echo   1. Open ADB_README.md
echo   2. Query your first table
echo   3. Run tests: dotnet test --filter AuroraDBTests
echo.
echo Quick commands:
echo   - Connect: docker exec -it aurora-postgres-local psql -U admin -d auroradb
echo   - View status: .\sqs-learning.ps1 -TestAurora
echo   - View logs: docker logs aurora-postgres-local -f
echo   - Stop: docker-compose stop aurora-postgres
echo.

REM Show tables
echo Available tables:
echo.
docker exec aurora-postgres-local psql -U admin -d auroradb -c "\dt app.*" 2>nul

REM Show sample data
echo.
echo Sample data:
docker exec aurora-postgres-local psql -U admin -d auroradb -c "SELECT COUNT(*) as customers FROM app.customers;" 2>nul
docker exec aurora-postgres-local psql -U admin -d auroradb -c "SELECT COUNT(*) as products FROM app.products;" 2>nul

echo.
echo ================================================
echo Ready to learn Aurora PostgreSQL!
echo ================================================
echo.

pause
