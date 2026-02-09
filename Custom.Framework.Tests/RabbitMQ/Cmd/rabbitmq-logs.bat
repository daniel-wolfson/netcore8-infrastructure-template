@echo off
REM View RabbitMQ container logs

echo ================================================
echo RabbitMQ Test Container - Logs
echo ================================================
echo.

docker logs -f rabbitmq-test
