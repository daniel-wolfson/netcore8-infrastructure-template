@echo off
REM ================================================
REM Amazon SQS - Stop Script
REM ================================================

echo.
echo ================================================
echo Stopping Amazon SQS (LocalStack)
echo ================================================
echo.

cd /d "%~dp0"

docker-compose stop localstack

echo.
echo [INFO] Amazon SQS stopped successfully
echo.
pause
