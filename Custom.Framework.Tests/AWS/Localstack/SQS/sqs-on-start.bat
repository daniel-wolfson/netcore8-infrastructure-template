@echo off
REM ================================================
REM Amazon SQS - Start Script
REM ================================================

echo.
echo ================================================
echo Starting Amazon SQS with LocalStack
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

REM Start LocalStack
echo [INFO] Starting LocalStack for SQS...
echo.
docker-compose up -d localstack

if errorlevel 1 (
    echo [ERROR] Failed to start LocalStack
    pause
    exit /b 1
)

echo.
echo [INFO] Waiting for LocalStack to be ready...
timeout /t 10 /nobreak >nul

REM Check health
echo [INFO] Checking LocalStack health...
curl -s http://localhost:4566/_localstack/health

echo.
echo.
echo ================================================
echo Amazon SQS is running!
echo ================================================
echo.
echo ?? SQS Service:
echo   Endpoint: http://localhost:4566
echo   Health: http://localhost:4566/_localstack/health
echo.
echo   Available Queues:
echo     - test-orders-queue
echo     - test-orders-queue-dlq (Dead Letter Queue)
echo     - test-notifications-queue
echo     - test-jobs-queue
echo     - test-orders-queue.fifo (FIFO Queue)
echo.
echo ================================================
echo Quick Commands:
echo ================================================
echo.
echo # View logs:
echo   docker logs localstack-main -f
echo.
echo # Stop SQS:
echo   docker-compose stop localstack
echo.
echo # Restart SQS:
echo   docker-compose restart localstack
echo.
echo # Run SQS tests:
echo   dotnet test --filter "FullyQualifiedName~AmazonSqsTests"
echo.
echo # List queues:
echo   aws --endpoint-url=http://localhost:4566 sqs list-queues
echo.
echo # Interactive CLI:
echo   sqs-cli.bat
echo.
echo ================================================
echo.
pause
