using Amazon.SQS;
using Amazon.SQS.Model;
using Custom.Framework.Aws.AmazonSQS;
using Custom.Framework.Aws.AmazonSQS.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Serilog.Data;
using System.Diagnostics;
using Testcontainers.LocalStack;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.AWS;

/// <summary>
/// Integration tests for Amazon SQS client using local SQS endpoint (LocalStack).
/// These tests require either LocalStack running, or AWS credentials with access to SQS.
/// </summary>
public class AmazonSqsTests(ITestOutputHelper output) : IAsyncLifetime, IAsyncDisposable
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

    private INetwork _network = default!;
    private IContainer _localStackContainer = default!;
    private IContainer _sqsInitContainer = default!;
    private PostgreSqlContainer _auroraPostgresContainer = default!;

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

        var assemblyLocation = typeof(AmazonSqsTests).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
        var projectRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
        var volumePath = Path.Combine(projectRoot, "AWS\\volume");

        _network = new NetworkBuilder().WithName($"aws-test-network-{Guid.NewGuid():N}").Build();

        _localStackContainer = GetLocalStackContainer(_network, volumePath).Build();
        _sqsInitContainer = GetSqsInitContainer(_network, _localStackContainer, 
            (Microsoft.Extensions.Logging.ILogger)_logger, volumePath).Build();

        _auroraPostgresContainer = GetAuroraPostgresContainer(_network, volumePath).Build();

        await StartLocalStackContainersAsync();

        // Create all queues with attributes
        await CreateAllTestQueuesAsync();

        // Configure DLQ
        await ConfigureDeadLetterQueueAsync(TestQueueName, TestDlqName);

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
        await Task.Delay(10000).WaitAsync(CancellationToken.None); // Wait for messages to be available
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

    #region Containers

    private async Task StartLocalStackContainersAsync()
    {
        try
        {
            await _localStackContainer.StartAsync();

            // Start and wait for init container to complete
            await _sqsInitContainer.StartAsync();

            var (stdout, stderr) = await _sqsInitContainer.GetLogsAsync();
            _output.WriteLine(stdout);

            // Wait for the init container to finish its job and exit
            // The container will stop automatically after initialization
            var timeout = TimeSpan.FromSeconds(60);
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                if (stdout.Contains("SQS init done") || stdout.Contains("SQS init failed"))
                {
                    break;
                }
                await Task.Delay(1000);
            }

            await _auroraPostgresContainer.StartAsync();
        }
        catch (Exception ex )
        {
            _output.WriteLine($"❌ INITIALIZATION FAILED: {ex.Message}");
            _output.WriteLine($"Stack Trace: {ex.StackTrace}");

            // Dump all container logs on failure
            await DumpContainerLogsAsync();

            throw;
        }
    }

    private static LocalStackBuilder GetLocalStackContainer(INetwork network, string volumePath)
    {
        return new LocalStackBuilder()
                .WithImage("localstack/localstack:latest")
                .WithName(Environment.GetEnvironmentVariable("LOCALSTACK_DOCKER_NAME") ?? "localstack-main")
                .WithNetwork(network)
                .WithPortBinding(4566, 4566) // Maps host 4566 -> container 4566
                .WithPortBinding(4510, 4510) // Maps host 4510 -> container 4510
                .WithPortBinding(4559, 4559) // Maps host 4559 -> container 4559
                .WithEnvironment("LOCALSTACK_AUTH_TOKEN", Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN") ?? string.Empty)
                .WithEnvironment("SERVICES", "sqs,rds,dynamodb,s3")
                .WithEnvironment("DEBUG", Environment.GetEnvironmentVariable("DEBUG") ?? "1")
                .WithEnvironment("PERSISTENCE", Environment.GetEnvironmentVariable("PERSISTENCE") ?? "0")
                .WithEnvironment("LAMBDA_EXECUTOR", Environment.GetEnvironmentVariable("LAMBDA_EXECUTOR") ?? "local")
                .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
                .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
                .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
                .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
                .WithEnvironment("EAGER_SERVICE_LOADING", "1")
                .WithEnvironment("SQS_ENDPOINT_STRATEGY", "path")
                .WithEnvironment("RDS_PG_CUSTOM_VERSIONS", "16.1")
                .WithBindMount(Environment.GetEnvironmentVariable("LOCALSTACK_VOLUME_DIR")
                    ?? $"{volumePath}/volume", "/var/lib/localstack")
                .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock") // Docker-in-Docker support
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPath("/_localstack/health")
                        .ForPort(4566)
                        .ForStatusCode(System.Net.HttpStatusCode.OK)));
    }

    private static ContainerBuilder GetSqsInitContainer(INetwork network, IContainer localStackContainer,
        Microsoft.Extensions.Logging.ILogger logger, string volumePath)
    {
        var rawScript = @"
        set -e
        aws_local() { aws --endpoint-url=""$LOCALSTACK_ENDPOINT"" ""$@""; }
        echo '================================================'
        echo 'Waiting for LocalStack to be ready...'
        echo '================================================'
        for i in 1 2 3 4 5; do
          if aws_local sqs list-queues >/dev/null 2>&1; then
            echo 'LocalStack SQS is ready!'
            break
          fi
          sleep 2
          if [ ""$i"" -eq 5 ]; then
            echo 'ERROR: LocalStack not ready after 10 seconds' >&2
            exit 1
          fi
        done
        echo '================================================'
        echo 'LocalStack initialization complete!'
        echo '================================================'
        echo 'SQS init done'
        ";

        var cleanScript = rawScript.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

        return new ContainerBuilder()
            .WithImage("amazon/aws-cli:latest")
            .WithName("localstack-test-sqs-init")
            .WithNetwork(network)
            .DependsOn(localStackContainer)
            .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
            .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
            .WithEnvironment("LOCALSTACK_ENDPOINT", "http://localstack-main:4566")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand(cleanScript)
            .WithLogger(logger)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQS init done"));
    }

    private static PostgreSqlBuilder GetAuroraPostgresContainer(INetwork network, string volumePath)
    {
        return new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithName("aurora-postgres-local")
                .WithNetwork(network)
                .WithPortBinding(5432, 5432) //"127.0.0.1", 
                .WithEnvironment("POSTGRES_USER", "admin")
                .WithEnvironment("POSTGRES_PASSWORD", "localpassword")
                .WithEnvironment("POSTGRES_DB", "auroradb")
                .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8 --locale=en_US.utf8")
                .WithBindMount($"{volumePath}/Aurora/Postgres-data", "/var/lib/postgresql/data")
                .WithBindMount($"{volumePath}/Aurora/init-aurora.sql", "/docker-entrypoint-initdb.d/init-aurora.sql");
    }

    private async Task ConfigureDeadLetterQueueAsync(string queueName, string dlqName)
    {
        try
        {
            var dlqUrl = await _sqsClient.GetQueueUrlAsync(dlqName);
            var dlqArn = (await _awsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = dlqUrl,
                AttributeNames = new List<string> { "QueueArn" }
            })).Attributes["QueueArn"];

            var queueUrl = await _sqsClient.GetQueueUrlAsync(queueName);

            var redrivePolicy = $"{{\"deadLetterTargetArn\":\"{dlqArn}\",\"maxReceiveCount\":\"3\"}}";

            await _awsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                Attributes = new Dictionary<string, string>
                {
                    ["RedrivePolicy"] = redrivePolicy
                }
            });

            _logger.LogInformation("Configured DLQ for {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure DLQ for {QueueName}", queueName);
        }
    }

    /// <summary>
    /// Creates all test queues with their respective configurations
    /// </summary>
    private async Task CreateAllTestQueuesAsync()
    {
        try
        {
            _logger.LogInformation("Creating test queues...");

            // 1. Create main orders queue
            await CreateQueueWithAttributesAsync(
                TestQueueName,
                new Dictionary<string, string>
                {
                    ["VisibilityTimeout"] = "30",
                    ["MessageRetentionPeriod"] = "345600", // 4 days
                    ["ReceiveMessageWaitTimeSeconds"] = "20" // Long polling
                });

            // 2. Create Dead Letter Queue
            await CreateQueueWithAttributesAsync(
                TestDlqName,
                new Dictionary<string, string>
                {
                    ["MessageRetentionPeriod"] = "1209600" // 14 days
                });

            // 3. Create notifications queue
            await CreateQueueWithAttributesAsync(
                TestNotificationQueueName,
                new Dictionary<string, string>
                {
                    ["VisibilityTimeout"] = "30",
                    ["MessageRetentionPeriod"] = "345600" // 4 days
                });

            // 4. Create jobs queue
            await CreateQueueWithAttributesAsync(
                TestJobQueueName,
                new Dictionary<string, string>
                {
                    ["VisibilityTimeout"] = "60",
                    ["DelaySeconds"] = "0"
                });

            // 5. Create FIFO queue
            await CreateQueueWithAttributesAsync(
                TestFifoQueueName,
                new Dictionary<string, string>
                {
                    ["FifoQueue"] = "true",
                    ["ContentBasedDeduplication"] = "true"
                },
                isFifo: true);

            _logger.LogInformation("All test queues created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create test queues");
            throw;
        }
    }

    /// <summary>
    /// Creates a queue with specified attributes, or returns existing queue URL
    /// </summary>
    private async Task<string> CreateQueueWithAttributesAsync(
        string queueName,
        Dictionary<string, string> attributes,
        bool isFifo = false)
    {
        try
        {
            // Try to get existing queue first
            var queueUrl = await _sqsClient.GetQueueUrlAsync(queueName);
            _logger.LogInformation("Queue {QueueName} already exists", queueName);
            return queueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            // Queue doesn't exist, create it
            _logger.LogInformation("Creating queue {QueueName} with attributes: {Attributes}",
                queueName,
                string.Join(", ", attributes.Select(kvp => $"{kvp.Key}={kvp.Value}")));

            var createRequest = new CreateQueueRequest
            {
                QueueName = queueName,
                Attributes = attributes
            };

            var response = await _awsClient.CreateQueueAsync(createRequest);
            _logger.LogInformation("Queue {QueueName} created with URL: {QueueUrl}", queueName, response.QueueUrl);
            return response.QueueUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create queue {QueueName}, continuing anyway", queueName);
            return string.Empty;
        }
    }

    private async Task DumpContainerLogsAsync()
    {
        try
        {
            if (_localStackContainer != null)
            {
                _output.WriteLine("\n=== LOCALSTACK LOGS ===");
                var (stdout, stderr) = await _localStackContainer.GetLogsAsync();
                _output.WriteLine(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    _output.WriteLine("STDERR:");
                    _output.WriteLine(stderr);
                }
            }

            if (_sqsInitContainer != null)
            {
                _output.WriteLine("\n=== SQS INIT LOGS ===");
                var (stdout, stderr) = await _sqsInitContainer.GetLogsAsync();
                _output.WriteLine(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    _output.WriteLine("STDERR:");
                    _output.WriteLine(stderr);
                }
            }

            if (_auroraPostgresContainer != null)
            {
                _output.WriteLine("\n=== POSTGRES LOGS ===");
                var (stdout, stderr) = await _auroraPostgresContainer.GetLogsAsync();
                _output.WriteLine(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    _output.WriteLine("STDERR:");
                    _output.WriteLine(stderr);
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to dump container logs: {ex.Message}");
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        try
        {
            _provider?.Dispose();

            // Only clean up containers if tests completed successfully
            if (_sqsInitContainer != null)
            {
                _output.WriteLine("🧹 Cleaning up SQS init container...");
                await _sqsInitContainer.DisposeAsync();
            }

            if (_localStackContainer != null)
            {
                _output.WriteLine("🧹 Cleaning up LocalStack container...");
                await _localStackContainer.DisposeAsync();
            }

            if (_auroraPostgresContainer != null)
            {
                _output.WriteLine("🧹 Cleaning up Aurora Postgres container...");
                await _auroraPostgresContainer.DisposeAsync();
            }

            if (_network != null)
            {
                _output.WriteLine("🧹 Cleaning up network...");
                await _network.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Cleanup failed: {ex.Message}");
        }
    }

    #endregion
}
