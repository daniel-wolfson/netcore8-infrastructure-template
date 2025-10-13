# Amazon SQS - Quick Start Guide

## ?? Getting Started in 5 Minutes

### 1. Install Dependencies (Already Done)
The AWSSDK.SQS package has been added to the Custom.Framework project.

### 2. Local Development Setup

#### Option A: Using LocalStack (Recommended for Development)
```bash
# Start LocalStack with SQS support
docker run -d -p 4566:4566 -p 4571:4571 \
  -e SERVICES=sqs \
  -e DEBUG=1 \
  localstack/localstack

# Verify LocalStack is running
curl http://localhost:4566/_localstack/health
```

#### Option B: Using AWS CLI to Create Queues
```bash
# Set LocalStack endpoint
export AWS_ENDPOINT=http://localhost:4566

# Create standard queue
aws --endpoint-url=$AWS_ENDPOINT sqs create-queue --queue-name test-orders-queue

# Create dead letter queue
aws --endpoint-url=$AWS_ENDPOINT sqs create-queue --queue-name test-orders-queue-dlq

# List queues
aws --endpoint-url=$AWS_ENDPOINT sqs list-queues
```

### 3. Configure Your Application

Add to `appsettings.json` (or `appsettings.Development.json`):

```json
{
  "AmazonSQS": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566",
    "AccessKey": "test",
    "SecretKey": "test",
    "DefaultQueueName": "orders-queue",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "EnableBatchProcessing": true,
    "EnableDeadLetterQueue": true
  }
}
```

For **Production** (using IAM roles):
```json
{
  "AmazonSQS": {
    "Region": "us-east-1",
    "DefaultQueueName": "prod-orders-queue",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20
  }
}
```

### 4. Register Services in Program.cs

```csharp
using Custom.Framework.Aws.AmazonSQS;

var builder = WebApplication.CreateBuilder(args);

// Register Amazon SQS services
builder.Services.AddAmazonSqs(builder.Configuration);

var app = builder.Build();
app.Run();
```

### 5. Use in Your Code

#### Example 1: Send a Message
```csharp
using Custom.Framework.Aws.AmazonSQS;
using Custom.Framework.Aws.AmazonSQS.Models;

public class OrderService
{
    private readonly ISqsClient _sqsClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ISqsClient sqsClient, ILogger<OrderService> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    public async Task CreateOrderAsync(OrderMessage order)
    {
        // Send single message
        var response = await _sqsClient.SendMessageAsync("orders-queue", order);
        _logger.LogInformation("Sent order {OrderId} with message ID {MessageId}", 
            order.OrderId, response.MessageId);
    }

    public async Task CreateOrdersAsync(List<OrderMessage> orders)
    {
        // Send batch of messages (up to 10 at a time)
        var response = await _sqsClient.SendMessageBatchAsync("orders-queue", orders);
        _logger.LogInformation("Sent {Count} orders successfully", 
            response.Successful.Count);
    }
}
```

#### Example 2: Receive and Process Messages
```csharp
public class OrderProcessor : BackgroundService
{
    private readonly ISqsClient _sqsClient;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(ISqsClient sqsClient, ILogger<OrderProcessor> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Receive messages with long polling
            var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
                "orders-queue",
                maxNumberOfMessages: 10,
                waitTimeSeconds: 20
            );

            foreach (var message in messages)
            {
                try
                {
                    // Process the order
                    await ProcessOrderAsync(message.Body);

                    // Delete message after successful processing
                    await _sqsClient.DeleteMessageAsync(
                        "orders-queue", 
                        message.ReceiptHandle
                    );

                    _logger.LogInformation("Processed order {OrderId}", 
                        message.Body?.OrderId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process order {OrderId}", 
                        message.Body?.OrderId);

                    // Extend visibility timeout for retry
                    await _sqsClient.ChangeMessageVisibilityAsync(
                        "orders-queue",
                        message.ReceiptHandle,
                        visibilityTimeoutSeconds: 60
                    );
                }
            }
        }
    }

    private async Task ProcessOrderAsync(OrderMessage? order)
    {
        // Your business logic here
        await Task.Delay(100); // Simulate processing
    }
}
```

#### Example 3: Dead Letter Queue Handling
```csharp
public async Task ProcessFailedMessagesAsync()
{
    var dlqMessages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
        "orders-queue-dlq",
        maxNumberOfMessages: 10
    );

    foreach (var message in dlqMessages)
    {
        _logger.LogWarning(
            "Processing failed message from DLQ: {OrderId}, Receive Count: {Count}",
            message.Body?.OrderId,
            message.ApproximateReceiveCount
        );

        // Implement recovery logic
        // - Retry processing
        // - Send to another queue
        // - Store in database for manual review
        // - Send alert to operations team

        await _sqsClient.DeleteMessageAsync("orders-queue-dlq", message.ReceiptHandle);
    }
}
```

### 6. Run Integration Tests

```bash
# Make sure LocalStack is running first!
docker run -d -p 4566:4566 localstack/localstack

# Run all SQS tests
dotnet test --filter "FullyQualifiedName~AmazonSqsTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~AmazonSqsTests.SendMessage_SingleMessage_Success"
```

## ?? Common Patterns

### Pattern 1: High-Throughput Producer
```csharp
public async Task SendBulkOrdersAsync(IEnumerable<OrderMessage> orders)
{
    var batches = orders.Chunk(10); // SQS limit is 10 per batch
    
    var tasks = batches.Select(batch => 
        _sqsClient.SendMessageBatchAsync("orders-queue", batch)
    );
    
    await Task.WhenAll(tasks);
}
```

### Pattern 2: Concurrent Consumer Workers
```csharp
public async Task StartMultipleWorkersAsync(int workerCount, CancellationToken ct)
{
    var workers = Enumerable.Range(0, workerCount)
        .Select(workerId => ProcessMessagesAsync(workerId, ct));
    
    await Task.WhenAll(workers);
}

private async Task ProcessMessagesAsync(int workerId, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
            "orders-queue",
            maxNumberOfMessages: 10,
            waitTimeSeconds: 20
        );
        
        // Process messages...
    }
}
```

### Pattern 3: Graceful Error Handling
```csharp
private async Task<bool> ProcessWithRetryAsync(
    SqsMessage<OrderMessage> message, 
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await ProcessOrderAsync(message.Body);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Attempt {Attempt} failed for order {OrderId}", 
                attempt, message.Body?.OrderId);
            
            if (attempt == maxRetries)
            {
                // Send to DLQ after max retries
                await _sqsClient.SendToDeadLetterQueueAsync(
                    "orders-queue",
                    message.Body!,
                    errorMessage: ex.Message
                );
                return false;
            }
            
            // Wait before retry (exponential backoff)
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
    return false;
}
```

## ?? Monitoring

### Check Queue Metrics
```csharp
var messageCount = await _sqsClient.GetApproximateMessageCountAsync("orders-queue");
_logger.LogInformation("Queue has approximately {Count} messages", messageCount);

var attributes = await _sqsClient.GetQueueAttributesAsync("orders-queue");
foreach (var attr in attributes)
{
    _logger.LogInformation("{Key}: {Value}", attr.Key, attr.Value);
}
```

### List All Queues
```csharp
var queues = await _sqsClient.ListQueuesAsync();
foreach (var queueUrl in queues)
{
    _logger.LogInformation("Found queue: {QueueUrl}", queueUrl);
}
```

## ?? Troubleshooting

### Issue: Can't connect to LocalStack
```bash
# Check if LocalStack is running
docker ps | grep localstack

# Check LocalStack logs
docker logs <container-id>

# Restart LocalStack
docker restart <container-id>
```

### Issue: Messages not being received
1. Check visibility timeout - messages may still be in flight
2. Wait for long polling timeout
3. Verify queue name is correct
4. Check if queue is purged or empty

### Issue: Messages going to DLQ immediately
1. Check `MaxReceiveCount` in redrive policy
2. Verify processing logic isn't throwing exceptions
3. Check visibility timeout is long enough

## ?? Additional Resources

- **Full Documentation**: See `README.md` in the AmazonSQS folder
- **Configuration Guide**: See `AmazonSqs.appsettings.json`
- **Integration Tests**: See `Custom.Framework.Tests\AWS\AmazonSqsTests.cs`
- **AWS SQS Docs**: https://docs.aws.amazon.com/sqs/
- **LocalStack Docs**: https://docs.localstack.cloud/user-guide/aws/sqs/

## ? Checklist

- [ ] LocalStack running (for local development)
- [ ] Configuration added to appsettings.json
- [ ] Services registered in Program.cs
- [ ] Queues created (automatically or manually)
- [ ] ISqsClient injected into your services
- [ ] Error handling implemented
- [ ] Dead letter queue configured
- [ ] Monitoring in place

---

**You're ready to start using Amazon SQS!** ??

For production deployment, remember to:
- Remove hardcoded credentials
- Use IAM roles instead
- Enable CloudWatch metrics
- Set appropriate timeouts
- Configure auto-scaling for workers
