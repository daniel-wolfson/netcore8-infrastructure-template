@echo off
REM Stop Script for Custom.Framework Monitoring Stack
REM Stops Grafana and Prometheus containers

echo ========================================
echo Stopping Custom.Framework Monitoring
echo ========================================
echo.

cd /d "%~dp0"

echo Stopping containers...
docker-compose -f Monitoring.yaml down

if errorlevel 1 (
    echo.
    echo ERROR: Failed to stop services!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Services Stopped Successfully!
echo ========================================
echo.
echo To remove all data (volumes):
echo   docker-compose -f Monitoring.yaml down -v
echo.
echo To start again:
echo   start-monitoring.bat
echo.
pause
