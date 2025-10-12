@echo off
REM Emergency cleanup script for Docker mount issues
REM Fixes "not a directory" errors

echo ========================================
echo Docker Cleanup and Restart
echo ========================================
echo.

cd /d "%~dp0"

echo [1/5] Stopping all monitoring containers...
docker-compose -f Monitoring.yaml down -v 2>nul
docker stop prometheus grafana 2>nul
docker rm -f prometheus grafana 2>nul
echo ? Containers stopped and removed

echo.
echo [2/5] Removing volumes...
docker volume rm prometheus-data 2>nul
docker volume rm grafana-data 2>nul
echo ? Volumes removed

echo.
echo [3/5] Verifying prometheus.yml exists...
if not exist "Prometheus\prometheus.yml" (
    echo ERROR: Prometheus\prometheus.yml not found!
    echo Expected location: %CD%\Prometheus\prometheus.yml
    echo.
    dir /s prometheus.yml
    pause
    exit /b 1
)
echo ? Found: Prometheus\prometheus.yml

echo.
echo [4/5] Pruning Docker system (optional - removes unused data)...
set /p prune="Run docker system prune? This removes unused containers/images. (y/n): "
if /i "%prune%"=="y" (
    docker system prune -f
    echo ? Docker system pruned
) else (
    echo Skipped system prune
)

echo.
echo [5/5] Starting fresh containers...
docker-compose -f Monitoring.yaml up -d

if errorlevel 1 (
    echo.
    echo ERROR: Failed to start containers!
    echo.
    echo Checking logs...
    docker-compose -f Monitoring.yaml logs
    pause
    exit /b 1
)

echo.
echo ========================================
echo Cleanup Complete - Containers Starting
echo ========================================
echo.
echo Waiting for services to start...
timeout /t 10 /nobreak >nul

echo.
echo Checking status...
docker-compose -f Monitoring.yaml ps

echo.
echo Prometheus: http://localhost:9090
echo Grafana:    http://localhost:3001
echo.
echo To view logs: docker-compose -f Monitoring.yaml logs -f
echo.
pause
