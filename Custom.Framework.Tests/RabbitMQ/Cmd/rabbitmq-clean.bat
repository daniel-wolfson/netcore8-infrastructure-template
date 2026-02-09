@echo off
REM Clean RabbitMQ test container and volumes

echo ================================================
echo RabbitMQ Test Container - Clean
echo ================================================
echo.
echo ⚠️  This will remove the container and all data!
echo.
pause

docker-compose -f docker-compose.rabbitmq.yml down -v

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ RabbitMQ container and volumes cleaned successfully
    echo.
) else (
    echo.
    echo ❌ Failed to clean RabbitMQ container
    echo.
)

pause
