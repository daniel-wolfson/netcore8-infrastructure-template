@echo off
REM Start RabbitMQ test container

echo ================================================
echo RabbitMQ Test Container - Start
echo ================================================
echo.

docker-compose -f docker-compose.rabbitmq.yml up -d

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ RabbitMQ container started successfully
    echo.
    echo ⏳ Waiting for RabbitMQ to be ready...
    timeout /t 10 /nobreak >nul
    echo.
    echo 📊 Connection Details:
    echo    AMQP: amqp://guest:guest@localhost:5672/
    echo    Management UI: http://localhost:15672
    echo    Username: guest
    echo    Password: guest
    echo.
    echo 💡 Management UI will be available shortly at: http://localhost:15672
    echo.
) else (
    echo.
    echo ❌ Failed to start RabbitMQ container
    echo.
)

pause
