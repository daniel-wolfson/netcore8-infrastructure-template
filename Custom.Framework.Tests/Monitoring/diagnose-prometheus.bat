@echo off
REM Diagnostic Script for Prometheus Issues
REM Helps identify why Prometheus won't start

echo ========================================
echo Prometheus Diagnostic Tool
echo ========================================
echo.

cd /d "%~dp0"

echo [1/6] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running!
    echo Please start Docker Desktop.
    pause
    exit /b 1
)
echo ? Docker is running

echo.
echo [2/6] Checking if prometheus.yml exists...
if not exist "Prometheus\prometheus.yml" (
    echo ERROR: Prometheus\prometheus.yml not found!
    echo Expected location: %CD%\Prometheus\prometheus.yml
    echo.
    echo Searching for prometheus.yml...
    dir /s prometheus.yml
    pause
    exit /b 1
)
echo ? prometheus.yml found at: Prometheus\prometheus.yml

echo.
echo [3/6] Validating prometheus.yml configuration...
docker run --rm -v "%CD%\Prometheus\prometheus.yml:/prometheus.yml" prom/prometheus:latest promtool check config /prometheus.yml
if errorlevel 1 (
    echo ERROR: Configuration validation failed!
    echo Check the error messages above.
    pause
    exit /b 1
)
echo ? Configuration is valid

echo.
echo [4/6] Checking port 9090...
netstat -ano | findstr :9090 >nul 2>&1
if not errorlevel 1 (
    echo WARNING: Port 9090 is already in use!
    echo.
    netstat -ano | findstr :9090
    echo.
    echo You may need to:
    echo   1. Stop the service using port 9090, OR
    echo   2. Change the port in Monitoring.yaml
    pause
) else (
    echo ? Port 9090 is available
)

echo.
echo [5/6] Checking existing Prometheus container...
docker ps -a --filter "name=prometheus" --format "{{.Names}}" | findstr prometheus >nul 2>&1
if not errorlevel 1 (
    echo Found existing Prometheus container. Status:
    docker ps -a --filter "name=prometheus" --format "table {{.Names}}\t{{.Status}}"
    echo.
    set /p cleanup="Remove existing container? (y/n): "
    if /i "%cleanup%"=="y" (
        echo Removing old container...
        docker rm -f prometheus
        echo ? Old container removed
    )
)

echo.
echo [6/6] Attempting to start Prometheus...
echo.
docker-compose -f Monitoring.yaml up -d prometheus

if errorlevel 1 (
    echo.
    echo ERROR: Failed to start Prometheus!
    echo.
    echo Showing logs:
    docker-compose -f Monitoring.yaml logs prometheus
    echo.
    pause
    exit /b 1
)

echo.
echo Waiting for Prometheus to start...
timeout /t 5 /nobreak >nul

echo.
echo Checking Prometheus health...
curl -s http://localhost:9090/-/healthy >nul 2>&1
if errorlevel 1 (
    echo WARNING: Prometheus health check failed
    echo.
    echo Container status:
    docker ps -a --filter "name=prometheus" --format "table {{.Names}}\t{{.Status}}"
    echo.
    echo Recent logs:
    docker logs prometheus --tail 20
) else (
    echo ? Prometheus is healthy!
    echo.
    echo Prometheus UI: http://localhost:9090
    echo Targets:       http://localhost:9090/targets
)

echo.
echo ========================================
echo Diagnostic Complete
echo ========================================
echo.
echo Full logs: docker-compose -f Monitoring.yaml logs prometheus
echo.
pause
