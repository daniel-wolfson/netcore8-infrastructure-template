@echo off
REM ================================================
REM Amazon SQS - First Time Setup & Verification
REM ================================================

echo.
echo ================================================
echo   Amazon SQS - First Time Setup
echo ================================================
echo.

REM Check Docker
echo [1/5] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo [X] Docker is not running! Please start Docker Desktop.
    pause
    exit /b 1
)
echo [OK] Docker is running

REM Check AWS CLI (optional)
echo.
echo [2/5] Checking AWS CLI (optional)...
aws --version >nul 2>&1
if errorlevel 1 (
    echo [!] AWS CLI not found (optional, but recommended)
    echo     Install from: https://aws.amazon.com/cli/
) else (
    echo [OK] AWS CLI is installed
)

REM Check .NET SDK
echo.
echo [3/5] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [X] .NET SDK not found! Please install .NET 8 SDK.
    pause
    exit /b 1
)
echo [OK] .NET SDK is installed

REM Start LocalStack for SQS
echo.
echo [4/5] Starting LocalStack for SQS...
docker-compose up -d localstack

if errorlevel 1 (
    echo [X] Failed to start LocalStack
    pause
    exit /b 1
)

echo [OK] LocalStack container started
echo [INFO] Waiting 15 seconds for initialization...
timeout /t 15 /nobreak >nul

REM Verify Health
echo.
echo [5/5] Verifying LocalStack health...
curl -s http://localhost:4566/_localstack/health >nul 2>&1
if errorlevel 1 (
    echo [!] Health check failed (LocalStack may still be starting)
    echo [INFO] View logs: docker logs localstack-main -f
) else (
    echo [OK] LocalStack is healthy
)

echo.
echo ================================================
echo   SQS Setup Complete!
echo ================================================
echo.
echo Next steps:
echo   1. Open SQS_README.md
echo   2. Send your first message
echo   3. Run tests: dotnet test --filter AmazonSqsTests
echo.
echo Quick commands:
echo   - Interactive CLI: sqs-cli.bat
echo   - View status: .\sqs-learning.ps1 -TestSqs
echo   - View logs: docker logs localstack-main -f
echo   - Stop: docker-compose stop localstack
echo.

REM List queues
echo Available queues:
echo.
aws --endpoint-url=http://localhost:4566 sqs list-queues 2>nul

echo.
echo ================================================
echo Ready to learn Amazon SQS!
echo ================================================
echo.

pause
