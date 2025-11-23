@echo off
REM Consul Docker Setup Script for Windows
REM This script helps you start and manage Consul with Docker

setlocal enabledelayedexpansion

:menu
cls
echo ===================================
echo    Consul Docker Management
echo ===================================
echo 1. Start Consul only
echo 2. Start complete infrastructure
echo 3. Stop Consul
echo 4. Stop all services
echo 5. Show status
echo 6. Show logs
echo 7. Cleanup (remove volumes)
echo 8. Exit
echo ===================================
echo.

set /p choice="Enter choice [1-8]: "

if "%choice%"=="1" goto start_consul
if "%choice%"=="2" goto start_all
if "%choice%"=="3" goto stop_consul
if "%choice%"=="4" goto stop_all
if "%choice%"=="5" goto status
if "%choice%"=="6" goto logs
if "%choice%"=="7" goto cleanup
if "%choice%"=="8" goto exit
goto menu

:start_consul
echo Starting Consul server...
docker-compose -f docker-compose.consul.yml up -d
timeout /t 5 /nobreak >nul
echo.
echo [92m? Consul server started successfully[0m
echo [93m??  Consul UI: http://localhost:8500/ui[0m
echo [93m??  Consul API: http://localhost:8500[0m
pause
goto menu

:start_all
echo Starting complete infrastructure...
docker-compose up -d
timeout /t 10 /nobreak >nul
echo.
echo [92m? Infrastructure started successfully[0m
echo [93m??  Services:[0m
echo    - Consul UI: http://localhost:8500/ui
echo    - Your API: http://localhost:5000
echo    - PostgreSQL: localhost:5432
echo    - Redis: localhost:6379
pause
goto menu

:stop_consul
echo Stopping Consul server...
docker-compose -f docker-compose.consul.yml down
echo [92m? Consul stopped[0m
pause
goto menu

:stop_all
echo Stopping all services...
docker-compose down
echo [92m? All services stopped[0m
pause
goto menu

:status
echo Container Status:
docker-compose ps
echo.
echo Consul Members:
docker exec consul-server consul members 2>nul
echo.
echo Registered Services:
curl -s http://localhost:8500/v1/catalog/services
pause
goto menu

:logs
set /p service="Service name (default: consul): "
if "%service%"=="" set service=consul
echo Showing logs for: %service%
docker-compose logs --tail=100 %service%
pause
goto menu

:cleanup
echo [93m??  WARNING: This will remove all data![0m
set /p confirm="Are you sure? (y/n): "
if /i "%confirm%"=="y" (
    echo Cleaning up volumes...
    docker-compose down -v
    echo [92m? Cleanup complete[0m
)
pause
goto menu

:exit
echo Goodbye!
exit /b 0
