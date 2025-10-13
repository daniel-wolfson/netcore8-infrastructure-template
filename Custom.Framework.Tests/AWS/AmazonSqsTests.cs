using Amazon.SQS;
using Amazon.SQS.Model;
using Custom.Framework.Aws.AmazonSQS;
using Custom.Framework.Aws.AmazonSQS.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.AWS;

/// <summary>
/// Integration tests for Amazon SQS client using local SQS endpoint (LocalStack).
/// These tests require either LocalStack running, or AWS credentials with access to SQS.
/// </summary>
public class AmazonSqsTests(ITestOutputHelper output) : IAsyncLifetime
{
    private ServiceProvider _provider = default!;
    private ISqsClient _sqsClient = default!;
    private IAmazonSQS _awsClient = default!;
    private ILogger<AmazonSqsTests> _logger = default!;
    private readonly ITestOutputHelper _output = output;

    private const string TestQueueName = "test-orders-queue";
    private const string TestFifoQueueName = "test-orders-queue.fifo";
    private const string TestDlqName = "test-orders-queue-dlq";
    private const string TestNotificationQueueName = "test-notifications-queue";
    private const string TestJobQueueName = "test-jobs-queue";

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Load configuration
        var baseConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AmazonSQS:Region"] = baseConfig["AmazonSQS:Region"] ?? "us-east-1",
                ["AmazonSQS:ServiceUrl"] = baseConfig["AmazonSQS:ServiceUrl"] ?? "http://localhost:4566",
                ["AmazonSQS:AccessKey"] = baseConfig["AmazonSQS:AccessKey"] ?? "test",
                ["AmazonSQS:SecretKey"] = baseConfig["AmazonSQS:SecretKey"] ?? "test",
                ["AmazonSQS:DefaultQueueName"] = TestQueueName,
                ["AmazonSQS:MaxNumberOfMessages"] = "10",
                ["AmazonSQS:WaitTimeSeconds"] = "5",
                ["AmazonSQS:EnableDeadLetterQueue"] = "true",
                ["AmazonSQS:MaxReceiveCount"] = "3"
            })
            .Build();

        services.AddLogging(b => b.AddXUnit(_output));
        services.AddSingleton<IConfiguration>(config);
        services.AddAmazonSqs(config);

        _provider = services.BuildServiceProvider();
        _sqsClient = _provider.GetRequiredService<ISqsClient>();
        _awsClient = _provider.GetRequiredService<IAmazonSQS>();
        _logger = _provider.GetRequiredService<ILogger<AmazonSqsTests>>();

        // Create test queues
        await EnsureQueueExistsAsync(TestQueueName);
        await EnsureQueueExistsAsync(TestDlqName);
        await EnsureQueueExistsAsync(TestNotificationQueueName);
        await EnsureQueueExistsAsync(TestJobQueueName);

        // Purge queues to start fresh
        await PurgeQueueSafeAsync(TestQueueName);
        await PurgeQueueSafeAsync(TestDlqName);
        await PurgeQueueSafeAsync(TestNotificationQueueName);
        await PurgeQueueSafeAsync(TestJobQueueName);
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    #region Basic Operations Tests

    [Fact]
    public async Task SendMessage_SingleMessage_Success()
    {
        // Arrange
        var order = new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = "CUST-001",
            TotalAmount = 99.99m,
            Status = "Pending"
        };

        // Act
        var response = await _sqsClient.SendMessageAsync(TestQueueName, order);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.MessageId);
        _logger.LogInformation("Successfully sent message {MessageId}", response.MessageId);
    }

    [Fact]
    public async Task SendMessageBatch_MultipleMessages_Success()
    {
        // Arrange
        var orders = Enumerable.Range(1, 5).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = $"CUST-{i:D3}",
            TotalAmount = 100m + i,
            Status = "Pending"
        }).ToList();

        // Act
        var response = await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(5, response.Successful.Count);
        Assert.Empty(response.Failed);
        _logger.LogInformation("Successfully sent batch of {Count} messages", orders.Count);
    }

    [Fact]
    public async Task ReceiveMessages_WithLongPolling_Success()
    {
        // Arrange
        var order = new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = "CUST-001",
            TotalAmount = 149.99m,
            Status = "Pending"
        };
        await _sqsClient.SendMessageAsync(TestQueueName, order);
        await Task.Delay(1000); // Wait for message to be available

        // Act
        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
            TestQueueName,
            maxNumberOfMessages: 1,
            waitTimeSeconds: 5
        );

        // Assert
        Assert.NotNull(messages);
        Assert.NotEmpty(messages);
        Assert.Equal(order.OrderId, messages[0].Body?.OrderId);
        _logger.LogInformation("Successfully received {Count} messages", messages.Count);
    }

    [Fact]
    public async Task DeleteMessage_AfterReceive_Success()
    {
        // Arrange
        var order = new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = "CUST-001",
            TotalAmount = 199.99m,
            Status = "Pending"
        };
        await _sqsClient.SendMessageAsync(TestQueueName, order);
        await Task.Delay(1000);

        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(TestQueueName);
        Assert.NotEmpty(messages);

        // Act
        var response = await _sqsClient.DeleteMessageAsync(TestQueueName, messages[0].ReceiptHandle);

        // Assert
        Assert.NotNull(response);
        _logger.LogInformation("Successfully deleted message");
    }

    [Fact]
    public async Task DeleteMessageBatch_MultipleMessages_Success()
    {
        // Arrange
        var orders = Enumerable.Range(1, 5).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = $"CUST-{i:D3}",
            TotalAmount = 100m + i
        }).ToList();

        await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);
        await Task.Delay(1000);

        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
            TestQueueName,
            maxNumberOfMessages: 10
        );

        // Act
        var receiptHandles = messages.Select(m => m.ReceiptHandle).ToList();
        var response = await _sqsClient.DeleteMessageBatchAsync(TestQueueName, receiptHandles);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(messages.Count, response.Successful.Count);
        _logger.LogInformation("Successfully deleted batch of {Count} messages", messages.Count);
    }

    #endregion

    #region Queue Management Tests

    [Fact]
    public async Task CreateQueue_StandardQueue_Success()
    {
        // Arrange
        var queueName = $"test-create-queue-{Guid.NewGuid():N}";

        // Act
        var queueUrl = await _sqsClient.CreateQueueAsync(queueName);

        // Assert
        Assert.NotNull(queueUrl);
        Assert.Contains(queueName, queueUrl);
        _logger.LogInformation("Successfully created queue {QueueName}", queueName);

        // Cleanup
        await _sqsClient.DeleteQueueAsync(queueName);
    }

    [Fact]
    public async Task GetQueueAttributes_ReturnsAttributes_Success()
    {
        // Act
        var attributes = await _sqsClient.GetQueueAttributesAsync(TestQueueName);

        // Assert
        Assert.NotNull(attributes);
        Assert.True(attributes.Count > 0);
        _logger.LogInformation("Queue has {Count} attributes", attributes.Count);
    }

    [Fact]
    public async Task GetApproximateMessageCount_ReturnsCount_Success()
    {
        // Arrange
        var orders = Enumerable.Range(1, 3).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = $"CUST-{i:D3}",
            TotalAmount = 100m + i
        }).ToList();

        await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);
        await Task.Delay(2000); // Wait for messages to be counted

        // Act
        var count = await _sqsClient.GetApproximateMessageCountAsync(TestQueueName);

        // Assert
        Assert.True(count >= 3);
        _logger.LogInformation("Queue has approximately {Count} messages", count);
    }

    [Fact]
    public async Task PurgeQueue_RemovesAllMessages_Success()
    {
        // Arrange
        var orders = Enumerable.Range(1, 5).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = $"CUST-{i:D3}"
        }).ToList();

        await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);
        await Task.Delay(1000);

        // Act
        await _sqsClient.PurgeQueueAsync(TestQueueName);
        await Task.Delay(2000); // Purge takes time

        // Assert
        var count = await _sqsClient.GetApproximateMessageCountAsync(TestQueueName);
        Assert.Equal(0, count);
        _logger.LogInformation("Successfully purged queue");
    }

    [Fact]
    public async Task ListQueues_ReturnsQueues_Success()
    {
        // Act
        var queues = await _sqsClient.ListQueuesAsync("test-");

        // Assert
        Assert.NotNull(queues);
        Assert.True(queues.Count > 0);
        _logger.LogInformation("Found {Count} queues with prefix 'test-'", queues.Count);
    }

    #endregion

    #region Message Visibility Tests

    [Fact]
    public async Task ChangeMessageVisibility_ExtendsTimeout_Success()
    {
        // Arrange
        var order = new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = "CUST-001",
            TotalAmount = 99.99m
        };
        await _sqsClient.SendMessageAsync(TestQueueName, order);
        await Task.Delay(1000);

        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(TestQueueName);
        Assert.NotEmpty(messages);

        // Act
        var response = await _sqsClient.ChangeMessageVisibilityAsync(
            TestQueueName,
            messages[0].ReceiptHandle,
            visibilityTimeoutSeconds: 60
        );

        // Assert
        Assert.NotNull(response);
        _logger.LogInformation("Successfully changed message visibility");

        // Cleanup
        await _sqsClient.DeleteMessageAsync(TestQueueName, messages[0].ReceiptHandle);
    }

    #endregion

    #region High-Volume Performance Tests

    [Fact]
    public async Task HighVolume_SendBatch_1000Messages_Success()
    {
        // Arrange
        var totalMessages = 1000;
        var sw = Stopwatch.StartNew();

        var orders = Enumerable.Range(1, totalMessages).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{i:D6}",
            CustomerId = $"CUST-{i % 100:D3}",
            TotalAmount = 100m + (i % 100)
        }).ToList();

        // Act
        var batches = orders.Chunk(10);
        var successCount = 0;

        foreach (var batch in batches)
        {
            var response = await _sqsClient.SendMessageBatchAsync(TestQueueName, batch);
            successCount += response.Successful.Count;
        }

        sw.Stop();

        // Assert
        Assert.Equal(totalMessages, successCount);
        var throughput = totalMessages / sw.Elapsed.TotalSeconds;
        _logger.LogInformation(
            "Sent {Count} messages in {ElapsedMs}ms, Throughput: {Throughput:F0} msg/sec",
            totalMessages, sw.ElapsedMilliseconds, throughput);

        Assert.True(throughput > 100, $"Throughput {throughput:F0} msg/sec is too low");
    }

    [Fact]
    public async Task HighVolume_ReceiveAndDelete_Success()
    {
        // Arrange
        var messageCount = 50;
        var orders = Enumerable.Range(1, messageCount).Select(i => new OrderMessage
        {
            OrderId = $"ORD-{i:D6}",
            CustomerId = $"CUST-{i % 10:D3}"
        }).ToList();

        await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);
        await Task.Delay(2000);

        var sw = Stopwatch.StartNew();
        var receivedCount = 0;

        // Act
        while (receivedCount < messageCount)
        {
            var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
                TestQueueName,
                maxNumberOfMessages: 10,
                waitTimeSeconds: 5
            );

            if (!messages.Any()) break;

            var receiptHandles = messages.Select(m => m.ReceiptHandle).ToList();
            await _sqsClient.DeleteMessageBatchAsync(TestQueueName, receiptHandles);

            receivedCount += messages.Count;
        }

        sw.Stop();

        // Assert
        Assert.True(receivedCount >= messageCount);
        _logger.LogInformation(
            "Received and deleted {Count} messages in {ElapsedMs}ms",
            receivedCount, sw.ElapsedMilliseconds);
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public async Task Scenario_OrderProcessing_EndToEnd()
    {
        // Arrange - Create orders
        var orders = new List<OrderMessage>
        {
            new OrderMessage
            {
                OrderId = $"ORD-{Guid.NewGuid()}",
                CustomerId = "CUST-001",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = "PROD-001", ProductName = "Laptop", Quantity = 1, UnitPrice = 999.99m, Subtotal = 999.99m },
                    new OrderItem { ProductId = "PROD-002", ProductName = "Mouse", Quantity = 2, UnitPrice = 29.99m, Subtotal = 59.98m }
                },
                TotalAmount = 1059.97m,
                Status = "Pending",
                ShippingAddress = new Address
                {
                    Street = "123 Main St",
                    City = "New York",
                    State = "NY",
                    ZipCode = "10001",
                    Country = "USA"
                },
                PaymentMethod = "CreditCard"
            }
        };

        // Act - Send orders
        await _sqsClient.SendMessageBatchAsync(TestQueueName, orders);
        await Task.Delay(1000);

        // Receive and process
        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(TestQueueName);
        Assert.NotEmpty(messages);

        var processedOrders = new List<OrderMessage>();
        foreach (var message in messages)
        {
            // Simulate processing
            var order = message.Body;
            Assert.NotNull(order);
            processedOrders.Add(order);

            // Delete after processing
            await _sqsClient.DeleteMessageAsync(TestQueueName, message.ReceiptHandle);
        }

        // Assert
        Assert.Equal(orders.Count, processedOrders.Count);
        Assert.Equal(orders[0].TotalAmount, processedOrders[0].TotalAmount);
        _logger.LogInformation("Successfully processed {Count} orders", processedOrders.Count);
    }

    [Fact]
    public async Task Scenario_NotificationSystem_HighVolume()
    {
        // Arrange
        var notifications = Enumerable.Range(1, 20).Select(i => new NotificationMessage
        {
            UserId = $"user{i:D3}",
            Type = (NotificationType)(i % 4),
            Title = $"Notification {i}",
            Message = $"This is notification message {i}",
            Recipient = $"user{i:D3}@example.com",
            Priority = i % 3
        }).ToList();

        // Act - Send notifications
        var sw = Stopwatch.StartNew();
        await _sqsClient.SendMessageBatchAsync(TestNotificationQueueName, notifications);
        sw.Stop();

        _logger.LogInformation("Sent {Count} notifications in {ElapsedMs}ms", notifications.Count, sw.ElapsedMilliseconds);

        await Task.Delay(2000);

        // Receive notifications
        var receivedNotifications = new List<NotificationMessage>();
        var maxAttempts = 5;
        var attempt = 0;

        while (receivedNotifications.Count < notifications.Count && attempt < maxAttempts)
        {
            var messages = await _sqsClient.ReceiveMessagesAsync<NotificationMessage>(
                TestNotificationQueueName,
                maxNumberOfMessages: 10,
                waitTimeSeconds: 5
            );

            if (messages.Any())
            {
                receivedNotifications.AddRange(messages.Select(m => m.Body!));

                var receiptHandles = messages.Select(m => m.ReceiptHandle).ToList();
                await _sqsClient.DeleteMessageBatchAsync(TestNotificationQueueName, receiptHandles);
            }

            attempt++;
        }

        // Assert
        Assert.True(receivedNotifications.Count >= notifications.Count);
        _logger.LogInformation("Successfully received {Count} notifications", receivedNotifications.Count);
    }

    [Fact]
    public async Task Scenario_BackgroundJobs_WithRetry()
    {
        // Arrange
        var job = new JobMessage
        {
            JobId = Guid.NewGuid().ToString(),
            JobType = "GenerateReport",
            UserId = "user123",
            Parameters = new Dictionary<string, object>
            {
                ["reportType"] = "sales",
                ["startDate"] = DateTime.UtcNow.AddMonths(-1).ToString("O"),
                ["endDate"] = DateTime.UtcNow.ToString("O")
            },
            TimeoutSeconds = 300,
            MaxRetries = 3
        };

        // Act - Send job
        await _sqsClient.SendMessageAsync(TestJobQueueName, job, delaySeconds: 5);
        await Task.Delay(6000); // Wait for delay

        // Receive job
        var messages = await _sqsClient.ReceiveMessagesAsync<JobMessage>(
            TestJobQueueName,
            maxNumberOfMessages: 1,
            waitTimeSeconds: 5
        );

        // Assert
        Assert.NotEmpty(messages);
        var receivedJob = messages[0].Body;
        Assert.NotNull(receivedJob);
        Assert.Equal(job.JobId, receivedJob.JobId);
        Assert.Equal(job.JobType, receivedJob.JobType);
        _logger.LogInformation("Successfully received job {JobId} of type {JobType}", receivedJob.JobId, receivedJob.JobType);

        // Cleanup
        await _sqsClient.DeleteMessageAsync(TestJobQueueName, messages[0].ReceiptHandle);
    }

    #endregion

    #region Dead Letter Queue Tests

    [Fact]
    public async Task SendToDeadLetterQueue_WithError_Success()
    {
        // Arrange
        var order = new OrderMessage
        {
            OrderId = $"ORD-{Guid.NewGuid()}",
            CustomerId = "CUST-FAILED",
            TotalAmount = 999.99m,
            Status = "Failed"
        };

        // Act
        await _sqsClient.SendToDeadLetterQueueAsync(
            TestQueueName,
            order,
            errorMessage: "Payment processing failed"
        );

        await Task.Delay(1000);

        // Assert - Check DLQ
        var messages = await _sqsClient.ReceiveMessagesAsync<OrderMessage>(
            TestDlqName,
            maxNumberOfMessages: 1,
            waitTimeSeconds: 5
        );

        Assert.NotEmpty(messages);
        Assert.Equal(order.OrderId, messages[0].Body?.OrderId);
        _logger.LogInformation("Successfully sent message to DLQ");

        // Cleanup
        await _sqsClient.DeleteMessageAsync(TestDlqName, messages[0].ReceiptHandle);
    }

    #endregion

    #region Helper Methods

    private async Task EnsureQueueExistsAsync(string queueName)
    {
        try
        {
            await _sqsClient.GetQueueUrlAsync(queueName);
            _logger.LogInformation("Queue {QueueName} already exists", queueName);
        }
        catch (QueueDoesNotExistException)
        {
            _logger.LogInformation("Creating queue {QueueName}", queueName);
            await _sqsClient.CreateQueueAsync(queueName);
        }
    }

    private async Task PurgeQueueSafeAsync(string queueName)
    {
        try
        {
            await _sqsClient.PurgeQueueAsync(queueName);
            await Task.Delay(1000); // Wait for purge to complete
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to purge queue {QueueName}, continuing anyway", queueName);
        }
    }

    #endregion
}
