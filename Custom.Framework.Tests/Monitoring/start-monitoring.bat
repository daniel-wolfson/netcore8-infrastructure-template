@echo off
REM Quick Start Script for Custom.Framework Monitoring Stack
REM Starts Grafana and Prometheus using Docker Compose

echo ========================================
echo Custom.Framework Monitoring Stack
echo ========================================
echo.

cd /d "%~dp0"

echo Checking if Docker is running...
docker info >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running!
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo Starting Grafana and Prometheus...
docker-compose -f Monitoring.yaml up -d

if errorlevel 1 (
    echo.
    echo ERROR: Failed to start services!
    echo Check the error messages above.
    pause
    exit /b 1
)

echo.
echo ========================================
echo Services Started Successfully!
echo ========================================
echo.
echo Grafana:    http://localhost:3001
echo   Username: admin
echo   Password: Graf1939!
echo.
echo Prometheus: http://localhost:9090
echo.
echo ========================================
echo.

REM Wait for services to be healthy
echo Waiting for services to be ready...
timeout /t 10 /nobreak >nul

echo.
echo Checking service health...
curl -s http://localhost:3001/api/health >nul 2>&1
if errorlevel 1 (
    echo WARNING: Grafana is not ready yet. Please wait a few more seconds.
) else (
    echo Grafana is ready! ?
)

curl -s http://localhost:9090/-/healthy >nul 2>&1
if errorlevel 1 (
    echo WARNING: Prometheus is not ready yet. Please wait a few more seconds.
) else (
    echo Prometheus is ready! ?
)

echo.
echo To view logs:    docker-compose -f Monitoring.yaml logs -f
echo To stop:         docker-compose -f Monitoring.yaml down
echo To restart:      docker-compose -f Monitoring.yaml restart
echo.
pause
