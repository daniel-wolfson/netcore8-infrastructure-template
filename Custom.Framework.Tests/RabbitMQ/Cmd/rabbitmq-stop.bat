@echo off
REM Stop RabbitMQ test container

echo ================================================
echo RabbitMQ Test Container - Stop
echo ================================================
echo.

docker-compose -f docker-compose.rabbitmq.yml down

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ RabbitMQ container stopped successfully
    echo.
) else (
    echo.
    echo ❌ Failed to stop RabbitMQ container
    echo.
)

pause
