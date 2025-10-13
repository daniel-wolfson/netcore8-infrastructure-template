# ?? Amazon SQS - Quick Reference Card

Complete command reference for Amazon SQS with LocalStack.

---

## ?? Getting Started

### Start SQS Service
```bash
# Windows
sqs-on-start.bat

# PowerShell  
.\sqs-learning.ps1 -Start

# Linux/Mac
docker-compose up -d
```

### Check SQS Status
```bash
# PowerShell
.\sqs-learning.ps1 -Status

# Health check
curl http://localhost:4566/_localstack/health

# List queues
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

---

## ?? AWS CLI Commands

#### List Queues
```bash
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

#### Send Message
```bash
aws --endpoint-url=http://localhost:4566 sqs send-message \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --message-body "Your message"
```

#### Send with Delay
```bash
aws --endpoint-url=http://localhost:4566 sqs send-message \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --message-body "Delayed message" \
    --delay-seconds 10
```

#### Send Batch
```bash
aws --endpoint-url=http://localhost:4566 sqs send-message-batch \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --entries file://sample-batch.json
```

#### Receive Messages
```bash
aws --endpoint-url=http://localhost:4566 sqs receive-message \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --max-number-of-messages 10 \
    --wait-time-seconds 20
```

#### Delete Message
```bash
aws --endpoint-url=http://localhost:4566 sqs delete-message \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --receipt-handle "RECEIPT_HANDLE"
```

#### Get Queue Attributes
```bash
aws --endpoint-url=http://localhost:4566 sqs get-queue-attributes \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME \
    --attribute-names All
```

#### Purge Queue
```bash
aws --endpoint-url=http://localhost:4566 sqs purge-queue \
    --queue-url http://localhost:4566/000000000000/QUEUE_NAME
```

---

## ?? .NET API Commands

#### Send Message
```csharp
await _sqsClient.SendMessageAsync("queue-name", message);
```

#### Send Batch
```csharp
await _sqsClient.SendMessageBatchAsync("queue-name", messages);
```

#### Receive Messages
```csharp
var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
    "queue-name", maxNumberOfMessages: 10, waitTimeSeconds: 20);
```

#### Delete Message
```csharp
await _sqsClient.DeleteMessageAsync("queue-name", receiptHandle);
```

#### Delete Batch
```csharp
await _sqsClient.DeleteMessageBatchAsync("queue-name", receiptHandles);
```

#### Change Visibility
```csharp
await _sqsClient.ChangeMessageVisibilityAsync(
    "queue-name", receiptHandle, visibilityTimeoutSeconds: 60);
```

#### Get Message Count
```csharp
var count = await _sqsClient.GetApproximateMessageCountAsync("queue-name");
```

---

## ?? Available Queues

| Queue Name | Type | Purpose |
|------------|------|---------|
| test-orders-queue | Standard | Order processing |
| test-orders-queue-dlq | Standard | Dead letter queue |
| test-notifications-queue | Standard | Notifications |
| test-jobs-queue | Standard | Background jobs |
| test-orders-queue.fifo | FIFO | Ordered messages |

**Queue URLs:**
```
http://localhost:4566/000000000000/QUEUE_NAME
```

---

## ?? Connection Details

```
Endpoint: http://localhost:4566
Region: us-east-1
AccessKey: test
SecretKey: test
```

**.NET Configuration:**
```json
{
  "AmazonSQS": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566",
    "AccessKey": "test",
    "SecretKey": "test",
    "DefaultQueueName": "test-orders-queue"
  }
}
```

---

## ?? Testing

```bash
# All SQS tests
dotnet test --filter "FullyQualifiedName~AmazonSqsTests"

# PowerShell
.\sqs-learning.ps1 -TestSqs
```

---

## ?? Common Patterns

### Producer
```csharp
await _sqsClient.SendMessageAsync(queueName, message);
return Accepted();
```

### Consumer
```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var messages = await _sqsClient.ReceiveMessagesAsync<T>(queueName, 10, 20);
    foreach (var msg in messages)
    {
        await ProcessAsync(msg.Body);
        await _sqsClient.DeleteMessageAsync(queueName, msg.ReceiptHandle);
    }
}
```

---

## ?? Troubleshooting

```bash
# Check health
curl http://localhost:4566/_localstack/health

# View logs
docker logs localstack-main -f

# Reset
.\sqs-learning.ps1 -Clean
.\sqs-learning.ps1 -Start
```

---

## ?? Performance Tips

- ? Batch operations (10 messages/call)
- ? Long polling (waitTimeSeconds: 20)
- ? Visibility timeout > processing time
- ? Always delete processed messages

---

**For detailed guide, see:** `SQS_README.md`
**For AuroraDB commands, see:** `ADB_QUICK_REFERENCE.md`
