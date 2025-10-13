@echo off
REM ================================================
REM Aurora PostgreSQL - Start Script
REM ================================================

echo.
echo ================================================
echo Starting Aurora PostgreSQL
echo ================================================
echo.

cd /d "%~dp0"

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not running. Please start Docker Desktop first.
    pause
    exit /b 1
)

echo [INFO] Docker is running
echo.

REM Start PostgreSQL
echo [INFO] Starting Aurora PostgreSQL container...
echo.
docker-compose up -d aurora-postgres

if errorlevel 1 (
    echo [ERROR] Failed to start PostgreSQL
    pause
    exit /b 1
)

echo.
echo [INFO] Waiting for PostgreSQL to be ready...
timeout /t 10 /nobreak >nul

REM Check health
echo [INFO] Checking PostgreSQL health...
docker exec -it aurora-postgres-local pg_isready -U admin -d auroradb

echo.
echo.
echo ================================================
echo Aurora PostgreSQL is running!
echo ================================================
echo.
echo ???  Aurora PostgreSQL:
echo   Host: localhost
echo   Port: 5432
echo   Database: auroradb
echo   Username: admin
echo   Password: localpassword
echo.
echo   Connection String:
echo   Host=localhost;Port=5432;Database=auroradb;Username=admin;Password=localpassword
echo.
echo   Tables:
echo     - app.customers (3 sample records)
echo     - app.products (5 sample products)
echo     - app.orders
echo     - app.order_items
echo.
echo ================================================
echo Quick Commands:
echo ================================================
echo.
echo # View logs:
echo   docker logs aurora-postgres-local -f
echo.
echo # Stop PostgreSQL:
echo   docker-compose stop aurora-postgres
echo.
echo # Restart PostgreSQL:
echo   docker-compose restart aurora-postgres
echo.
echo # Connect to database:
echo   docker exec -it aurora-postgres-local psql -U admin -d auroradb
echo.
echo # Run query:
echo   docker exec -it aurora-postgres-local psql -U admin -d auroradb -c "SELECT * FROM app.customers;"
echo.
echo # Run AuroraDB tests:
echo   dotnet test --filter "FullyQualifiedName~AuroraDBTests"
echo.
echo ================================================
echo.
pause
