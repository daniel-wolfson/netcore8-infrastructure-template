# Amazon SQS Integration Tests - Execution Report

## Test: SendMessage_SingleMessage_Success

**Source:** `AmazonSqsTests.cs` line 118  
**Duration:** 11.3 seconds  
**Status:** ✅ Passed

---

## Test Summary

This test validates the ability to send a single message to an Amazon SQS queue using LocalStack.

### Test Steps

1. **Container Initialization** (3 seconds)
   - LocalStack container started on port 4566
   - SQS service validated and ready
   
2. **Queue Creation** (5 seconds)
   - Created 5 test queues with specific attributes
   - Configured Dead Letter Queue for `test-orders-queue`
   - Purged all queues for clean state

3. **Message Send** (< 1 second)
   - Sent single order message
   - Message ID: `838f768c-38a9-4561-9544-0a97e3f29abc`
   - Successfully delivered to `test-orders-queue`

---

## Infrastructure Details

### Docker Environment
- **Host:** `npipe://./pipe/docker_engine`
- **Docker Version:** 28.5.1
- **Operating System:** Docker Desktop (WSL2)
- **API Version:** 1.51
- **Total Memory:** 31.26 GB

### LocalStack Container
- **Container ID:** `4857c720e987`
- **SQS Endpoint:** `http://localhost:4566`
- **Initialization Time:** ~3 seconds

---

## Queues Created

| Queue Name | Type | Purpose | Status |
|------------|------|---------|--------|
| `test-orders-queue` | Standard | Order processing | ✅ Created |
| `test-orders-queue-dlq` | DLQ | Failed orders | ✅ Created |
| `test-notifications-queue` | Standard | Notifications | ✅ Created |
| `test-jobs-queue` | Standard | Background jobs | ✅ Created |
| `test-orders-queue.fifo` | FIFO | Ordered messages | ✅ Created |

---

## Execution Logs (Summary)

```
[09:52:06] ================================================
[09:52:06] Waiting for LocalStack to be ready...
[09:52:06] ================================================
[09:52:09] LocalStack SQS is ready!
[09:52:09] ================================================
[09:52:09] LocalStack initialization complete!
[09:52:09] ================================================
[09:52:09] SQS init done

[Custom.Framework.Tests] Creating test queues...
[Custom.Framework.Tests] Queue test-orders-queue created
[Custom.Framework.Tests] Queue test-orders-queue-dlq created
[Custom.Framework.Tests] Queue test-notifications-queue created
[Custom.Framework.Tests] Queue test-jobs-queue created
[Custom.Framework.Tests] Queue test-orders-queue.fifo created
[Custom.Framework.Tests] All test queues created successfully

[Custom.Framework.Tests] Configured DLQ for test-orders-queue

[Custom.Framework.SqsClient] Purged queue test-orders-queue
[Custom.Framework.SqsClient] Purged queue test-orders-queue-dlq
[Custom.Framework.SqsClient] Purged queue test-notifications-queue
[Custom.Framework.SqsClient] Purged queue test-jobs-queue

[Custom.Framework.SqsClient] Sent message 838f768c-38a9-4561-9544-0a97e3f29abc to queue test-orders-queue
[Custom.Framework.Tests] Successfully sent message 838f768c-38a9-4561-9544-0a97e3f29abc
```

---

## Result

✅ **Test Passed** - Message successfully sent to SQS queue via LocalStack

**Total Duration:** 11.3 seconds  
**Message ID:** `838f768c-38a9-4561-9544-0a97e3f29abc`  
**Queue URL:** `http://localhost.localstack.cloud:4566/queue/us-east-1/000000000000/test-orders-queue`

---

**Generated:** 2025-10-22  
**Test Framework:** xUnit  
**.NET Version:** 8.0  
**LocalStack:** latest
