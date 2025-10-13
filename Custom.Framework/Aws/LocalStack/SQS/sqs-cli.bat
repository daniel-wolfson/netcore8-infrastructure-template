@echo off
REM ================================================
REM LocalStack SQS - Interactive Learning CLI
REM ================================================

setlocal enabledelayedexpansion

set ENDPOINT=http://localhost:4566
set QUEUE_BASE_URL=http://localhost:4566/000000000000

:menu
cls
echo.
echo ================================================
echo    LocalStack SQS Learning Environment
echo ================================================
echo.
echo 1. List all queues
echo 2. Send a test message
echo 3. Receive messages
echo 4. Get queue attributes
echo 5. Purge queue (clear all messages)
echo 6. Check queue message count
echo 7. Send batch messages
echo 8. Create new queue
echo 9. Delete queue
echo 0. Exit
echo.
set /p choice="Enter your choice: "

if "%choice%"=="1" goto list_queues
if "%choice%"=="2" goto send_message
if "%choice%"=="3" goto receive_messages
if "%choice%"=="4" goto queue_attributes
if "%choice%"=="5" goto purge_queue
if "%choice%"=="6" goto message_count
if "%choice%"=="7" goto send_batch
if "%choice%"=="8" goto create_queue
if "%choice%"=="9" goto delete_queue
if "%choice%"=="0" goto end

goto menu

:list_queues
echo.
echo [INFO] Listing all queues...
echo.
aws --endpoint-url=%ENDPOINT% sqs list-queues
echo.
pause
goto menu

:send_message
echo.
echo Available queues:
echo 1. test-orders-queue
echo 2. test-notifications-queue
echo 3. test-jobs-queue
echo 4. test-orders-queue.fifo
echo.
set /p queue_choice="Select queue (1-4): "

if "%queue_choice%"=="1" set QUEUE_NAME=test-orders-queue
if "%queue_choice%"=="2" set QUEUE_NAME=test-notifications-queue
if "%queue_choice%"=="3" set QUEUE_NAME=test-jobs-queue
if "%queue_choice%"=="4" set QUEUE_NAME=test-orders-queue.fifo

echo.
set /p message="Enter message body: "

echo.
echo [INFO] Sending message to %QUEUE_NAME%...
echo.

if "%QUEUE_NAME%"=="test-orders-queue.fifo" (
    set /p group_id="Enter message group ID: "
    set /p dedup_id="Enter deduplication ID: "
    aws --endpoint-url=%ENDPOINT% sqs send-message ^
        --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME% ^
        --message-body "%message%" ^
        --message-group-id "!group_id!" ^
        --message-deduplication-id "!dedup_id!"
) else (
    aws --endpoint-url=%ENDPOINT% sqs send-message ^
        --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME% ^
        --message-body "%message%"
)

echo.
pause
goto menu

:receive_messages
echo.
echo Available queues:
echo 1. test-orders-queue
echo 2. test-notifications-queue
echo 3. test-jobs-queue
echo 4. test-orders-queue-dlq (Dead Letter Queue)
echo 5. test-orders-queue.fifo
echo.
set /p queue_choice="Select queue (1-5): "

if "%queue_choice%"=="1" set QUEUE_NAME=test-orders-queue
if "%queue_choice%"=="2" set QUEUE_NAME=test-notifications-queue
if "%queue_choice%"=="3" set QUEUE_NAME=test-jobs-queue
if "%queue_choice%"=="4" set QUEUE_NAME=test-orders-queue-dlq
if "%queue_choice%"=="5" set QUEUE_NAME=test-orders-queue.fifo

echo.
set /p max_msgs="Max messages to receive (1-10): "

echo.
echo [INFO] Receiving messages from %QUEUE_NAME%...
echo.
aws --endpoint-url=%ENDPOINT% sqs receive-message ^
    --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME% ^
    --max-number-of-messages %max_msgs% ^
    --wait-time-seconds 5 ^
    --attribute-names All ^
    --message-attribute-names All

echo.
pause
goto menu

:queue_attributes
echo.
echo Available queues:
echo 1. test-orders-queue
echo 2. test-notifications-queue
echo 3. test-jobs-queue
echo 4. test-orders-queue-dlq
echo.
set /p queue_choice="Select queue (1-4): "

if "%queue_choice%"=="1" set QUEUE_NAME=test-orders-queue
if "%queue_choice%"=="2" set QUEUE_NAME=test-notifications-queue
if "%queue_choice%"=="3" set QUEUE_NAME=test-jobs-queue
if "%queue_choice%"=="4" set QUEUE_NAME=test-orders-queue-dlq

echo.
echo [INFO] Getting attributes for %QUEUE_NAME%...
echo.
aws --endpoint-url=%ENDPOINT% sqs get-queue-attributes ^
    --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME% ^
    --attribute-names All

echo.
pause
goto menu

:purge_queue
echo.
echo Available queues:
echo 1. test-orders-queue
echo 2. test-notifications-queue
echo 3. test-jobs-queue
echo.
set /p queue_choice="Select queue (1-3): "

if "%queue_choice%"=="1" set QUEUE_NAME=test-orders-queue
if "%queue_choice%"=="2" set QUEUE_NAME=test-notifications-queue
if "%queue_choice%"=="3" set QUEUE_NAME=test-jobs-queue

echo.
echo [WARNING] This will delete ALL messages from %QUEUE_NAME%!
set /p confirm="Are you sure? (y/n): "

if /i "%confirm%"=="y" (
    echo.
    echo [INFO] Purging queue %QUEUE_NAME%...
    echo.
    aws --endpoint-url=%ENDPOINT% sqs purge-queue ^
        --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME%
    echo.
    echo [INFO] Queue purged successfully!
)

echo.
pause
goto menu

:message_count
echo.
echo Available queues:
echo 1. test-orders-queue
echo 2. test-notifications-queue
echo 3. test-jobs-queue
echo 4. test-orders-queue-dlq
echo.
set /p queue_choice="Select queue (1-4): "

if "%queue_choice%"=="1" set QUEUE_NAME=test-orders-queue
if "%queue_choice%"=="2" set QUEUE_NAME=test-notifications-queue
if "%queue_choice%"=="3" set QUEUE_NAME=test-jobs-queue
if "%queue_choice%"=="4" set QUEUE_NAME=test-orders-queue-dlq

echo.
echo [INFO] Getting message count for %QUEUE_NAME%...
echo.
aws --endpoint-url=%ENDPOINT% sqs get-queue-attributes ^
    --queue-url %QUEUE_BASE_URL%/%QUEUE_NAME% ^
    --attribute-names ApproximateNumberOfMessages,ApproximateNumberOfMessagesNotVisible,ApproximateNumberOfMessagesDelayed

echo.
pause
goto menu

:send_batch
echo.
echo [INFO] Sending batch of 3 sample messages to test-orders-queue...
echo.
aws --endpoint-url=%ENDPOINT% sqs send-message-batch ^
    --queue-url %QUEUE_BASE_URL%/test-orders-queue ^
    --entries file://sample-batch.json

echo.
pause
goto menu

:create_queue
echo.
set /p new_queue="Enter new queue name: "

echo.
echo [INFO] Creating queue %new_queue%...
echo.
aws --endpoint-url=%ENDPOINT% sqs create-queue ^
    --queue-name %new_queue%

echo.
pause
goto menu

:delete_queue
echo.
set /p del_queue="Enter queue name to delete: "

echo.
echo [WARNING] This will permanently delete the queue: %del_queue%
set /p confirm="Are you sure? (y/n): "

if /i "%confirm%"=="y" (
    echo.
    echo [INFO] Deleting queue %del_queue%...
    echo.
    aws --endpoint-url=%ENDPOINT% sqs delete-queue ^
        --queue-url %QUEUE_BASE_URL%/%del_queue%
    echo.
    echo [INFO] Queue deleted successfully!
)

echo.
pause
goto menu

:end
echo.
echo Goodbye!
echo.
endlocal
exit /b 0
