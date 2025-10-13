# ?? Amazon SQS with LocalStack - Complete Guide

Complete setup and learning guide for Amazon SQS using LocalStack.

---

## ?? What is SQS?

**Amazon Simple Queue Service (SQS)** is a fully managed message queuing service for decoupling and scaling microservices, distributed systems, and serverless applications.

### Key Benefits
- ? **Decouple** application components
- ? **Scale** to handle millions of messages
- ? **Reliable** message delivery
- ? **Cost-effective** pay-per-use model

---

## ?? Quick Start (3 Steps)

### Step 1: Start LocalStack
```bash
cd Custom.Framework\Aws\LocalStack
sqs-on-start.bat
```

### Step 2: Verify SQS is Running
```bash
# Check health
curl http://localhost:4566/_localstack/health

# List queues
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

### Step 3: Send Your First Message
```bash
aws --endpoint-url=http://localhost:4566 sqs send-message \
    --queue-url http://localhost:4566/000000000000/test-orders-queue \
    --message-body "Hello from SQS!"
```

---

## ?? Pre-configured Queues

### Standard Queues
1. **test-orders-queue** - Main order processing
   - Visibility timeout: 30 seconds
   - Dead letter queue enabled (max 3 retries)
   - Long polling: 20 seconds

2. **test-notifications-queue** - User notifications
   - Visibility timeout: 30 seconds
   - High throughput

3. **test-jobs-queue** - Background jobs
   - Visibility timeout: 60 seconds
   - Delayed message support

### Dead Letter Queue
4. **test-orders-queue-dlq** - Failed messages
   - Captures messages after 3 failed attempts
   - Retention: 14 days

### FIFO Queue
5. **test-orders-queue.fifo** - Ordered messages
   - Exactly-once processing
   - Content-based deduplication
   - Strict FIFO ordering

---

## ?? .NET Integration

### Step 1: Configure
Add to `appsettings.json`:

```json
{
  "AmazonSQS": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566",
    "AccessKey": "test",
    "SecretKey": "test",
    "DefaultQueueName": "test-orders-queue",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "EnableDeadLetterQueue": true,
    "MaxReceiveCount": 3
  }
}
```

### Step 2: Register Services
In `Program.cs`:

```csharp
builder.Services.AddAmazonSqs(builder.Configuration);
```

### Step 3: Use in Your Code

#### Send Message
```csharp
public class OrderService
{
    private readonly ISqsClient _sqsClient;

    public OrderService(ISqsClient sqsClient)
    {
        _sqsClient = sqsClient;
    }

    public async Task PlaceOrderAsync(OrderMessage order)
    {
        await _sqsClient.SendMessageAsync("test-orders-queue", order);
    }
}
```

#### Receive and Process Messages
```csharp
public class OrderProcessor : BackgroundService
{
    private readonly ISqsClient _sqsClient;
    private readonly ILogger<OrderProcessor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
                "test-orders-queue",
                maxNumberOfMessages: 10,
                waitTimeSeconds: 20
            );

            foreach (var message in messages)
            {
                try
                {
                    await ProcessOrderAsync(message.Body);
                    await _sqsClient.DeleteMessageAsync(
                        "test-orders-queue",
                        message.ReceiptHandle
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process order");
                }
            }
        }
    }
}
```

---

## ?? Key Concepts

### Standard Queue
- **At-least-once delivery**: Message may be delivered multiple times
- **Best-effort ordering**: Messages usually arrive in order
- **Unlimited throughput**: Handle millions of messages
- **Use case**: When high throughput is needed and duplicates are acceptable

### FIFO Queue
- **Exactly-once processing**: No duplicates
- **Strict ordering**: First-In-First-Out guaranteed
- **Limited throughput**: 300 TPS (3000 with batching)
- **Use case**: When order matters (financial transactions, order processing)

### Visibility Timeout
```
0s ??????> 30s ??????> 60s
 Receive     Invisible    Visible again (if not deleted)
```
- Message becomes invisible after being received
- Prevents duplicate processing
- Set longer than your processing time
- Can be extended if needed

### Dead Letter Queue (DLQ)
```
Main Queue ? Retry 1 ? Retry 2 ? Retry 3 ? DLQ
```
- Captures messages that can't be processed
- Prevents poison messages from blocking queue
- Use for debugging and alerts
- Process separately from main queue

### Long Polling
- **Short polling** (0s): Returns immediately even if empty
- **Long polling** (20s): Waits for messages to arrive
- Reduces costs by eliminating empty responses
- More efficient use of resources

---

## ?? Common Operations

### Send Messages

#### Single Message
```csharp
await _sqsClient.SendMessageAsync("queue-name", message);
```

#### Batch (More Efficient)
```csharp
var messages = new List<OrderMessage> { /* 10 messages */ };
await _sqsClient.SendMessageBatchAsync("queue-name", messages);
```

#### With Delay
```csharp
await _sqsClient.SendMessageAsync("queue-name", message, delaySeconds: 10);
```

### Receive Messages

#### Basic
```csharp
var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>("queue-name");
```

#### With Long Polling
```csharp
var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
    "queue-name",
    maxNumberOfMessages: 10,
    waitTimeSeconds: 20  // Long polling
);
```

### Delete Messages

#### Single
```csharp
await _sqsClient.DeleteMessageAsync("queue-name", receiptHandle);
```

#### Batch
```csharp
var receiptHandles = messages.Select(m => m.ReceiptHandle).ToList();
await _sqsClient.DeleteMessageBatchAsync("queue-name", receiptHandles);
```

### Error Handling

#### Extend Visibility Timeout
```csharp
await _sqsClient.ChangeMessageVisibilityAsync(
    "queue-name",
    receiptHandle,
    visibilityTimeoutSeconds: 60
);
```

#### Send to DLQ
```csharp
await _sqsClient.SendToDeadLetterQueueAsync(
    "queue-name",
    message,
    errorMessage: "Processing failed"
);
```

---

## ?? Real-World Scenarios

### Scenario 1: E-Commerce Order Processing

**Flow:**
```
Web API ? SQS (orders) ? Worker ? Database ? SQS (notifications)
```

**Implementation:**
```csharp
// Web API: Place order
[HttpPost("orders")]
public async Task<IActionResult> PlaceOrder(OrderRequest request)
{
    var message = new OrderMessage
    {
        OrderId = Guid.NewGuid().ToString(),
        CustomerId = request.CustomerId,
        Items = request.Items,
        TotalAmount = request.Items.Sum(i => i.Subtotal)
    };
    
    await _sqsClient.SendMessageAsync("test-orders-queue", message);
    
    return Accepted(new { OrderId = message.OrderId });
}

// Worker: Process orders
while (!cancellationToken.IsCancellationRequested)
{
    var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
        "test-orders-queue", 10, 20);
    
    foreach (var msg in messages)
    {
        try
        {
            // Save to database
            await _orderRepository.CreateAsync(msg.Body);
            
            // Send notification
            await _sqsClient.SendMessageAsync(
                "test-notifications-queue",
                new NotificationMessage {
                    UserId = msg.Body.CustomerId,
                    Type = NotificationType.OrderConfirmation
                }
            );
            
            // Delete from queue
            await _sqsClient.DeleteMessageAsync(
                "test-orders-queue",
                msg.ReceiptHandle
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order processing failed");
        }
    }
}
```

### Scenario 2: High-Volume Notifications

**Flow:**
```
Event ? SQS ? Multiple Workers (parallel) ? Email/SMS/Push
```

**Implementation:**
```csharp
// Send 1000 notifications
var notifications = Enumerable.Range(1, 1000)
    .Select(i => new NotificationMessage { /* ... */ })
    .ToList();

// Batch send (100 messages at a time, 10 per batch)
var batches = notifications.Chunk(10);
foreach (var batch in batches)
{
    await _sqsClient.SendMessageBatchAsync(
        "test-notifications-queue",
        batch
    );
}

// Multiple workers process in parallel
for (int i = 0; i < 5; i++)
{
    _ = Task.Run(() => ProcessNotificationsAsync(i, cancellationToken));
}
```

### Scenario 3: Background Jobs with Retry

**Flow:**
```
Schedule ? SQS (delay) ? Worker ? Retry or DLQ
```

**Implementation:**
```csharp
// Schedule job with delay
var job = new JobMessage
{
    JobId = Guid.NewGuid().ToString(),
    JobType = "GenerateReport",
    Parameters = new Dictionary<string, object>
    {
        ["reportType"] = "sales",
        ["startDate"] = DateTime.UtcNow.AddMonths(-1)
    }
};

await _sqsClient.SendMessageAsync(
    "test-jobs-queue",
    job,
    delaySeconds: 300  // 5 minute delay
);

// Process with retry
var messages = await _sqsClient.ReceiveMessagesAsync<JobMessage>(
    "test-jobs-queue");

foreach (var msg in messages)
{
    try
    {
        await ExecuteJobAsync(msg.Body);
        await _sqsClient.DeleteMessageAsync("test-jobs-queue", msg.ReceiptHandle);
    }
    catch (Exception ex)
    {
        if (msg.ApproximateReceiveCount >= 3)
        {
            // Send to DLQ after 3 attempts
            await _sqsClient.SendToDeadLetterQueueAsync(
                "test-jobs-queue",
                msg.Body,
                errorMessage: ex.Message
            );
            await _sqsClient.DeleteMessageAsync("test-jobs-queue", msg.ReceiptHandle);
        }
        else
        {
            // Extend visibility for retry
            await _sqsClient.ChangeMessageVisibilityAsync(
                "test-jobs-queue",
                msg.ReceiptHandle,
                visibilityTimeoutSeconds: 60
            );
        }
    }
}
```

---

## ? Best Practices

### 1. Always Delete Processed Messages
```csharp
// ? Bad: Message will reappear
await ProcessAsync(message.Body);

// ? Good: Delete after processing
await ProcessAsync(message.Body);
await _sqsClient.DeleteMessageAsync(queueName, message.ReceiptHandle);
```

### 2. Use Batch Operations
```csharp
// ? Bad: 10 API calls
foreach (var msg in messages)
{
    await _sqsClient.SendMessageAsync(queueName, msg);
}

// ? Good: 1 API call
await _sqsClient.SendMessageBatchAsync(queueName, messages);
```

### 3. Implement Idempotency
```csharp
// ? Good: Handle duplicates
var orderId = message.Body.OrderId;
if (await _orderRepository.ExistsAsync(orderId))
{
    _logger.LogWarning("Order {OrderId} already processed", orderId);
    await _sqsClient.DeleteMessageAsync(queueName, message.ReceiptHandle);
    return;
}
```

### 4. Use Long Polling
```csharp
// ? Bad: Short polling wastes resources
var messages = await _sqsClient.ReceiveMessagesAsync<T>(queueName);

// ? Good: Long polling is efficient
var messages = await _sqsClient.ReceiveMessagesAsync<T>(
    queueName, maxNumberOfMessages: 10, waitTimeSeconds: 20);
```

### 5. Set Appropriate Visibility Timeout
```csharp
// If processing takes ~60 seconds, set timeout to 90 seconds
var messages = await _sqsClient.ReceiveMessagesAsync<T>(
    queueName, visibilityTimeoutSeconds: 90);
```

### 6. Monitor Queue Depth
```csharp
var count = await _sqsClient.GetApproximateMessageCountAsync(queueName);
if (count > 1000)
{
    _logger.LogWarning("Queue depth is high: {Count}", count);
    // Consider scaling workers
}
```

### 7. Handle Errors Gracefully
```csharp
try
{
    await ProcessAsync(message.Body);
    await _sqsClient.DeleteMessageAsync(queueName, message.ReceiptHandle);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Processing failed");
    // Don't delete - let it retry or go to DLQ
}
```

---

## ?? Testing

### Run All SQS Tests
```bash
dotnet test --filter "FullyQualifiedName~AmazonSqsTests"
```

### Test Categories
- Basic Operations (send, receive, delete)
- Batch Operations
- Queue Management
- Visibility Timeout
- High Volume Performance
- Real-World Scenarios
- Dead Letter Queue

### Using PowerShell
```powershell
.\sqs-learning.ps1 -TestSqs
```

---

## ?? Troubleshooting

### LocalStack Not Running
```bash
# Check status
docker ps | grep localstack

# Check health
curl http://localhost:4566/_localstack/health

# Restart
docker-compose restart localstack
```

### Messages Not Appearing
- Wait for visibility timeout (30 seconds)
- Check correct queue name
- Verify message was sent successfully

### Tests Failing
```bash
# Clean and restart
docker-compose down -v
docker-compose up -d

# Check queues
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

---

## ?? Additional Resources

- **Quick Reference**: [SQS_QUICK_REFERENCE.md](SQS_QUICK_REFERENCE.md)
- **AWS SQS Docs**: https://docs.aws.amazon.com/sqs/
- **LocalStack SQS**: https://docs.localstack.cloud/user-guide/aws/sqs/
- **Test Examples**: `Custom.Framework.Tests\AWS\AmazonSqsTests.cs`

---

**Ready to start?** Run `on-start.bat` and send your first message!

For AuroraDB guide, see: [ADB_README.md](ADB_README.md)
