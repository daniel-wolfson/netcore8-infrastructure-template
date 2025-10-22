@echo off
REM ================================================
REM Aurora PostgreSQL - Stop Script
REM ================================================

echo.
echo ================================================
echo Stopping Aurora PostgreSQL
echo ================================================
echo.

cd /d "%~dp0"

docker-compose stop aurora-postgres

echo.
echo [INFO] Aurora PostgreSQL stopped successfully
echo.
pause
