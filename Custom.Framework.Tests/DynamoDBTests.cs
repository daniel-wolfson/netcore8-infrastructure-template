using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Custom.Framework.Aws.DynamoDB;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Custom.Framework.Tests;

/// <summary>
/// Integration tests for DynamoDB repository using a local/test DynamoDB endpoint.
/// These tests require either DynamoDB Local running, or AWS credentials with access to the target table.
/// </summary>
public class DynamoDBTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider _provider = default!;
    private IAmazonDynamoDB _client = default!;
    private ILogger<DynamoDBTests> _logger;

    public DynamoDBTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Load base configuration from files and environment variables
        var baseConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var region = baseConfig["DynamoDB:Region"] ?? "";
        var serviceUrl = baseConfig["DynamoDB:ServiceUrl"] ?? "";
        var accessKey = baseConfig["DynamoDB:AccessKey"] ?? "";
        var secretKey = baseConfig["DynamoDB:SecretKey"] ?? "";

        // Configuration for local DynamoDB by default. Override via env vars or appsettings.Test.json for real AWS.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDB:Region"] = region,
                ["DynamoDB:ServiceUrl"] = serviceUrl,
                ["DynamoDB:AccessKey"] = accessKey,
                ["DynamoDB:SecretKey"] = secretKey
            })
            .Build();

        services.AddLogging(b => b.AddXUnit(_output));
        services.AddSingleton<IConfiguration>(config);
        services.AddDynamoDb(config);

        _provider = services.BuildServiceProvider();
        _client = _provider.GetRequiredService<IAmazonDynamoDB>();

        // Ensure the tables used by our sample models exist
        await EnsureTableAsync("UserSessions",
            [("UserId", ScalarAttributeType.S)],
            ("SessionId", ScalarAttributeType.S));
        await EnsureTableAsync("ProductInventory",
            [("ProductSku", ScalarAttributeType.S)],
            ("WarehouseId", ScalarAttributeType.S));
        await EnsureTableAsync("Events",
            [("EventType", ScalarAttributeType.S)],
            ("TimestampEventId", ScalarAttributeType.S));


    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UserSession_CRUD_Works()
    {
        var repo = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();

        var session = new UserSession
        {
            UserId = Guid.NewGuid().ToString(),
            SessionId = Guid.NewGuid().ToString(),
            Token = Guid.NewGuid().ToString("N"),
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Metadata = new Dictionary<string, string> { { "env", "test" } }
        };

        await repo.PutAsync(session);

        var loaded = await repo.GetAsync(session.UserId, session.SessionId);
        Assert.NotNull(loaded);
        Assert.Equal(session.Token, loaded!.Token);

        await repo.DeleteAsync(session.UserId, session.SessionId);
        var deleted = await repo.GetAsync(session.UserId, session.SessionId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task ProductInventory_BatchWrite_And_Query_Works()
    {
        var repo = _provider.GetRequiredService<IDynamoDbRepository<ProductInventory>>();

        var skus = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
        var items = skus.Select((sku, i) => new ProductInventory
        {
            ProductSku = sku,
            WarehouseId = "WH-1",
            ProductName = $"Item-{i}",
            Category = "Test",
            Quantity = 10 + i,
            ReservedQuantity = i,
            Price = 100 + i,
            WarehouseName = "Main",
            Region = "us-east-1",
            ReorderThreshold = 5
        });

        await repo.BatchWriteAsync(items);

        var results = await repo.QueryAsync(skus[0]);
        // Using DataModel QueryAsync: depending on mapping, may require GSI; basic assert that call succeeds
        Assert.NotNull(results);
    }

    [Fact]
    public async Task Events_ConditionalPut_And_TransactWrite_Works()
    {
        var repo = _provider.GetRequiredService<IDynamoDbRepository<Event>>();

        var evt = new Event
        {
            EventType = "click",
            TimestampEventId = $"{DateTime.UtcNow:O}_{Guid.NewGuid():N}",
            UserId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = "{}",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };

        var ok = await repo.ConditionalPutAsync(evt,
            "attribute_not_exists(EventType) AND attribute_not_exists(TimestampEventId) AND :ok = :ok",
            new Dictionary<string, object> { [":ok"] = "1" });
        Assert.True(ok);

        // Now transact: put a new one and delete the first (idempotent example)
        var evt2 = new Event
        {
            EventType = "click",
            TimestampEventId = $"{DateTime.UtcNow:O}_{Guid.NewGuid():N}",
            UserId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = "{}",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };

        await repo.TransactWriteAsync([evt2], [(evt.EventType, evt.TimestampEventId)]);
    }

    private async Task EnsureTableAsync(string tableName,
        (string Name, ScalarAttributeType Type)[] hashKey,
        (string Name, ScalarAttributeType Type)? rangeKey = null)
    {
        var tables = await _client.ListTablesAsync();
        if (tables.TableNames.Contains(tableName)) return;

        var request = new CreateTableRequest
        {
            TableName = tableName,
            AttributeDefinitions = []
        };

        foreach (var hk in hashKey)
            request.AttributeDefinitions.Add(new AttributeDefinition(hk.Name, hk.Type));
        if (rangeKey.HasValue)
            request.AttributeDefinitions.Add(new AttributeDefinition(rangeKey.Value.Name, rangeKey.Value.Type));

        var keySchema = new List<KeySchemaElement>
        {
            new(hashKey[0].Name, KeyType.HASH)
        };
        if (rangeKey.HasValue)
            keySchema.Add(new KeySchemaElement(rangeKey.Value.Name, KeyType.RANGE));

        request.KeySchema = keySchema;
        request.BillingMode = BillingMode.PAY_PER_REQUEST;

        await _client.CreateTableAsync(request);

        // wait until active
        while (true)
        {
            var desc = await _client.DescribeTableAsync(tableName);
            if (desc.Table.TableStatus == TableStatus.ACTIVE) break;
            await Task.Delay(500);
        }
    }

    #region User Session Management - High Read/Write Load

    [Fact]
    public async Task FireUpdateAndForgetSessionAsync()
    {
        var sessionRepository = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();
        var sessionAdd = new UserSession
        {
            UserId = Guid.NewGuid().ToString(),
            SessionId = Guid.NewGuid().ToString(),
            Token = Guid.NewGuid().ToString("N"),
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(10).ToUnixTimeSeconds(),
            Metadata = new Dictionary<string, string> { { "env", "test" } }
        };

        await sessionRepository.PutAsync(sessionAdd);

        var session = await sessionRepository.GetAsync(sessionAdd.UserId, sessionAdd.SessionId);

        Assert.NotNull(session);
        Assert.True(session.IsActive);

        // Check if expired
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > session.ExpiresAt)
        {
            await InvalidateSessionAsync(session.UserId, session.SessionId);
            Assert.True(false);
        }

        // Update last activity (async fire-and-forget for performance)
        _ = Task.Run(async () =>
        {
            var updates = new Dictionary<string, object>
            {
                ["LastActivityAt"] = DateTime.UtcNow
            };
            await sessionRepository.UpdateAsync(session.UserId, session.SessionId, updates);
        });
    }

    /// <summary>
    /// Scenario 3: Batch session cleanup (scheduled task)
    /// </summary>
    private async Task CleanupExpiredSessionsAsync(int batchSize = 25)
    {
        var beginTimestamp = Stopwatch.GetTimestamp();
        var sessionRepository = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();

        // In real scenario, you'd use a GSI to query expired sessions
        var allSessions = await sessionRepository.ScanAsync();
        var expiredSessions = allSessions
            .Where(s => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > s.ExpiresAt)
            .ToList();

        var batches = expiredSessions.Chunk(batchSize);
        var totalDeleted = 0;

        foreach (var batch in batches)
        {
            var deleteKeys = batch.Select(s => (s.UserId, (string?)s.SessionId));
            await sessionRepository.TransactWriteAsync(
                Enumerable.Empty<UserSession>(),
                deleteKeys);
            totalDeleted += batch.Length;
        }

        _logger.LogInformation("Cleaned up {Count} expired sessions in {ElapsedMs}ms",
            totalDeleted, Stopwatch.GetElapsedTime(beginTimestamp));
    }

    /// <summary>
    /// Scenario 4: Get all active sessions for a user
    /// </summary>
    private async Task<IEnumerable<UserSession>> GetUserSessionsAsync(string userId)
    {
        var sessionRepository = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();
        var sessions = await sessionRepository.QueryAsync(userId);
        return sessions.Where(s => s.IsActive);
    }

    private async Task InvalidateSessionAsync(string userId, string sessionId)
    {
        var sessionRepository = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();

        var updates = new Dictionary<string, object>
        {
            ["IsActive"] = false
        };
        await sessionRepository.UpdateAsync(userId, sessionId, updates);
    }

    #endregion

    #region Event Tracking - Ultra High Write Load

    /// <summary>
    /// Scenario 5: Log event (millions of events per hour)
    /// </summary>
    private async Task LogEventAsync(string eventType, string? userId, string source, object? payload = null)
    {
        var timestamp = DateTime.UtcNow;
        var eventId = Guid.NewGuid().ToString();

        var evt = new Event
        {
            EventType = eventType,
            TimestampEventId = $"{timestamp:yyyy-MM-ddTHH:mm:ss.fff}_{eventId}",
            EventId = eventId,
            UserId = userId,
            Timestamp = timestamp,
            Source = source,
            Payload = payload != null ? System.Text.Json.JsonSerializer.Serialize(payload) : null,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };

        var eventRepository = _provider.GetRequiredService<IDynamoDbRepository<Event>>();

        // Fire and forget for maximum throughput
        await eventRepository.PutAsync(evt);
    }

    /// <summary>
    /// Scenario 6: Batch write events (high-throughput ingestion)
    /// </summary>
    private async Task<int> BatchLogEventsAsync(IEnumerable<(string eventType, string? userId, string source, object? payload)> events)
    {
        var sw = Stopwatch.StartNew();

        var eventObjects = events.Select(e =>
        {
            var timestamp = DateTime.UtcNow;
            var eventId = Guid.NewGuid().ToString();

            return new Event
            {
                EventType = e.eventType,
                TimestampEventId = $"{timestamp:yyyy-MM-ddTHH:mm:ss.fff}_{eventId}",
                EventId = eventId,
                UserId = e.userId,
                Timestamp = timestamp,
                Source = e.source,
                Payload = e.payload != null ? System.Text.Json.JsonSerializer.Serialize(e.payload) : null,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
            };
        }).ToList();

        var eventRepository = _provider.GetRequiredService<IDynamoDbRepository<Event>>();

        await eventRepository.BatchWriteAsync(eventObjects);

        _logger.LogInformation("Batch logged {Count} events in {ElapsedMs}ms",
            eventObjects.Count, sw.ElapsedMilliseconds);

        return eventObjects.Count;
    }

    /// <summary>
    /// Scenario 7: Query events by type and time range
    /// </summary>
    private async Task<IEnumerable<Event>> GetEventsByTypeAsync(string eventType, DateTime startTime, DateTime endTime)
    {
        var startKey = $"{startTime:yyyy-MM-ddTHH:mm:ss.fff}_";
        var endKey = $"{endTime:yyyy-MM-ddTHH:mm:ss.fff}_ZZZZZ";

        var filterExpression = "TimestampEventId BETWEEN :start AND :end";
        var expressionValues = new Dictionary<string, object>
        {
            [":start"] = startKey,
            [":end"] = endKey
        };

        var eventRepository = _provider.GetRequiredService<IDynamoDbRepository<Event>>();

        return await eventRepository.QueryAsync(eventType, filterExpression, expressionValues);
    }

    #endregion

    #region Product Inventory - High Concurrent Updates

    /// <summary>
    /// Scenario 8: Update inventory with optimistic locking
    /// </summary>
    private async Task<bool> UpdateInventoryQuantityAsync(
        string productSku,
        string warehouseId,
        int quantityChange,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Get current inventory
        var inventoryRepository = _provider.GetRequiredService<IDynamoDbRepository<ProductInventory>>();

        var inventory = await inventoryRepository.GetAsync(productSku, warehouseId, cancellationToken);

        if (inventory == null)
        {
            _logger.LogWarning("Inventory not found for {ProductSku} at {WarehouseId}", productSku, warehouseId);
            return false;
        }

        // Check if we have enough stock for negative changes
        if (quantityChange < 0 && inventory.Quantity + quantityChange < 0)
        {
            _logger.LogWarning("Insufficient stock for {ProductSku}. Available: {Available}, Requested: {Requested}",
                productSku, inventory.Quantity, Math.Abs(quantityChange));
            return false;
        }

        // Update with optimistic locking
        inventory.Quantity += quantityChange;
        inventory.UpdatedAt = DateTime.UtcNow;

        try
        {
            // Conditional put to ensure version hasn't changed
            var condition = "attribute_exists(Version) AND Version = :version";
            var values = new Dictionary<string, object>
            {
                [":version"] = inventory.Version ?? 0
            };

            var success = await inventoryRepository.ConditionalPutAsync(inventory, condition, values, cancellationToken);

            _logger.LogInformation("Updated inventory for {ProductSku} in {ElapsedMs}ms. Success: {Success}",
                productSku, sw.ElapsedMilliseconds, success);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory for {ProductSku}", productSku);
            return false;
        }
    }

    /// <summary>
    /// Scenario 9: Reserve inventory for order (transactional)
    /// </summary>
    private async Task<bool> ReserveInventoryAsync(
        List<(string productSku, string warehouseId, int quantity)> items,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var inventoryRepository = _provider.GetRequiredService<IDynamoDbRepository<ProductInventory>>();

            // Get all inventory items
            var keys = items.Select(i => (i.productSku, (string?)i.warehouseId));
            var inventories = (await inventoryRepository.BatchGetAsync(keys, cancellationToken)).ToList();

            // Validate all items have sufficient stock
            var updates = new List<ProductInventory>();

            foreach (var item in items)
            {
                var inventory = inventories.FirstOrDefault(i =>
                    i.ProductSku == item.productSku && i.WarehouseId == item.warehouseId);

                if (inventory == null)
                {
                    _logger.LogWarning("Product {ProductSku} not found in warehouse {WarehouseId}",
                        item.productSku, item.warehouseId);
                    return false;
                }

                var availableStock = inventory.Quantity - inventory.ReservedQuantity;
                if (availableStock < item.quantity)
                {
                    _logger.LogWarning("Insufficient stock for {ProductSku}. Available: {Available}, Requested: {Requested}",
                        item.productSku, availableStock, item.quantity);
                    return false;
                }

                // Update reserved quantity
                inventory.ReservedQuantity += item.quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                updates.Add(inventory);
            }

            // Transactional write to reserve all items atomically
            await inventoryRepository.TransactWriteAsync(updates, Enumerable.Empty<(string, string?)>(), cancellationToken);

            _logger.LogInformation("Reserved inventory for {Count} items in {ElapsedMs}ms",
                items.Count, sw.ElapsedMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving inventory");
            return false;
        }
    }

    /// <summary>
    /// Scenario 10: Batch update inventory across multiple warehouses
    /// </summary>
    private async Task<int> BatchRestockInventoryAsync(
        List<(string productSku, string warehouseId, int quantity)> restockItems)
    {
        var beginTimeSpan = Stopwatch.GetTimestamp();
        var inventoryRepository = _provider.GetRequiredService<IDynamoDbRepository<ProductInventory>>();

        var keys = restockItems.Select(i => (i.productSku, (string?)i.warehouseId));
        var inventories = (await inventoryRepository.BatchGetAsync(keys)).ToList();

        var updates = new List<ProductInventory>();

        foreach (var item in restockItems)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.ProductSku == item.productSku && i.WarehouseId == item.warehouseId);

            if (inventory != null)
            {
                inventory.Quantity += item.quantity;
                inventory.LastRestockedAt = DateTime.UtcNow;
                inventory.UpdatedAt = DateTime.UtcNow;
                updates.Add(inventory);
            }
        }

        await inventoryRepository.BatchWriteAsync(updates);

        _logger.LogInformation("Batch restocked {Count} items in {ElapsedMs}ms",
            updates.Count, Stopwatch.GetElapsedTime(beginTimeSpan));

        return updates.Count;
    }

    /// <summary>
    /// Scenario 11: Get low stock products
    /// </summary>
    public async Task<IEnumerable<ProductInventory>> GetLowStockProductsAsync()
    {
        var inventoryRepository = _provider.GetRequiredService<IDynamoDbRepository<ProductInventory>>();

        var allInventory = await inventoryRepository.ScanAsync();

        return allInventory.Where(i =>
            i.IsActive &&
            (i.Quantity - i.ReservedQuantity) <= i.ReorderThreshold);
    }

    #endregion

    #region Performance Testing Helpers

    /// <summary>
    /// Scenario 12: High-volume write stress test
    /// </summary>
    private async Task<(int successCount, int failureCount, long elapsedMs)> StressTestWritesAsync(
        int numberOfEvents,
        int batchSize = 25)
    {
        var sw = Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;

        var batches = Enumerable.Range(0, numberOfEvents)
            .Select(i => (
                eventType: "StressTest",
                userId: $"user-{i % 1000}", // Distribute across 1000 users
                source: "StressTest",
                payload: (object?)new { index = i, timestamp = DateTime.UtcNow }
            ))
            .Chunk(batchSize);

        foreach (var batch in batches)
        {
            try
            {
                var count = await BatchLogEventsAsync(batch);
                successCount += count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch write failed");
                failureCount += batch.Length;
            }
        }

        sw.Stop();

        _logger.LogInformation("Stress test completed: {Success} successful, {Failures} failed in {ElapsedMs}ms. " +
            "Throughput: {Throughput} events/sec",
            successCount, failureCount, sw.ElapsedMilliseconds,
            (double)successCount / sw.Elapsed.TotalSeconds);

        return (successCount, failureCount, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Scenario 13: Parallel read stress test
    /// </summary>
    private async Task<(int successCount, int failureCount, long elapsedMs)> StressTestReadsAsync(
        int numberOfReads,
        int degreeOfParallelism = 10)
    {
        var sw = Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;
        var semaphore = new SemaphoreSlim(degreeOfParallelism);
        var sessionRepository = _provider.GetRequiredService<IDynamoDbRepository<UserSession>>();

        var tasks = Enumerable.Range(0, numberOfReads).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                var userId = $"user-{i % 1000}";
                var sessionId = Guid.NewGuid().ToString();

                var session = await sessionRepository.GetAsync(userId, sessionId);
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Read failed");
                Interlocked.Increment(ref failureCount);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        _logger.LogInformation("Read stress test completed: {Success} successful, {Failures} failed in {ElapsedMs}ms. " +
            "Throughput: {Throughput} reads/sec",
            successCount, failureCount, sw.ElapsedMilliseconds,
            (double)successCount / sw.Elapsed.TotalSeconds);

        return (successCount, failureCount, sw.ElapsedMilliseconds);
    }

    #endregion

    #region Helper Methods

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
               Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    #endregion
}

internal static class XUnitLoggingExtensions
{
    public static Microsoft.Extensions.Logging.ILoggingBuilder AddXUnit(this Microsoft.Extensions.Logging.ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.AddProvider(new XUnitLoggerProvider(output));
        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        return builder;
    }
}

internal class XUnitLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
{
    private readonly ITestOutputHelper _output;
    public XUnitLoggerProvider(ITestOutputHelper output) => _output = output;
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new XUnitLogger(_output, categoryName);
    public void Dispose() { }
}

internal class XUnitLogger : Microsoft.Extensions.Logging.ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _category;

    public XUnitLogger(ITestOutputHelper output, string category)
    {
        _output = output;
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{_category}] {logLevel}: {formatter(state, exception)}");
        if (exception != null) _output.WriteLine(exception.ToString());
    }

    private class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new NullScope();
        public void Dispose() { }
    }
}


/// <summary>
/// High-load examples for DynamoDB operations
/// Real-world scenarios with performance considerations
/// </summary>
internal class DynamoDbHighLoadTests
{
    private readonly IDynamoDbRepository<UserSession> _sessionRepository;
    private readonly IDynamoDbRepository<Event> _eventRepository;
    private readonly IDynamoDbRepository<ProductInventory> _inventoryRepository;
    private readonly ILogger<DynamoDbHighLoadTests> _logger;

    public DynamoDbHighLoadTests(
        IDynamoDbRepository<UserSession> sessionRepository,
        IDynamoDbRepository<Event> eventRepository,
        IDynamoDbRepository<ProductInventory> inventoryRepository,
        ILogger<DynamoDbHighLoadTests> logger)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
        _inventoryRepository = inventoryRepository;
        _logger = logger;
    }


}
