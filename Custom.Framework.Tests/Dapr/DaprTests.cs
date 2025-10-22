using Dapr.Client;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Dapr;

/// <summary>
/// Integration tests for Dapr functionality
/// Tests state management, pub/sub, and service invocation
/// </summary>
public class DaprTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private DaprTestContainer _daprContainer = default!;
    private DaprClient _daprClient = default!;

    public DaprTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _daprContainer = new DaprTestContainer(_output, "integration-test-app");
            await _daprContainer.InitializeAsync();

            // Create Dapr client
            _daprClient = new DaprClientBuilder()
                .UseHttpEndpoint(_daprContainer.DaprHttpEndpoint)
                .UseGrpcEndpoint(_daprContainer.DaprGrpcEndpoint)
                .Build();
        }
        catch (Exception ex)
        {
            throw;
        }

        _output.WriteLine("✅ Dapr client initialized");
    }

    #region State Management Tests

    [Fact]
    public async Task StateStore_SaveAndRetrieve_ShouldWork()
    {
        // Arrange
        const string storeName = "statestore";
        const string key = "test-key";
        var testData = new TestData
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            Value = 42,
            CreatedAt = DateTime.UtcNow
        };

        // Act - Save state
        await _daprClient.SaveStateAsync(storeName, key, testData);
        _output.WriteLine($"✅ Saved state with key: {key}");

        // Act - Retrieve state
        var retrieved = await _daprClient.GetStateAsync<TestData>(storeName, key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(testData.Id, retrieved.Id);
        Assert.Equal(testData.Name, retrieved.Name);
        Assert.Equal(testData.Value, retrieved.Value);
        _output.WriteLine($"✅ Retrieved state: {retrieved.Name}");
    }

    [Fact]
    public async Task StateStore_DeleteState_ShouldRemoveData()
    {
        // Arrange
        const string storeName = "statestore";
        const string key = "test-key-delete";
        var testData = new TestData { Id = Guid.NewGuid(), Name = "To Delete" };

        await _daprClient.SaveStateAsync(storeName, key, testData);

        // Act - Delete state
        await _daprClient.DeleteStateAsync(storeName, key);
        _output.WriteLine($"✅ Deleted state with key: {key}");

        // Assert
        var retrieved = await _daprClient.GetStateAsync<TestData>(storeName, key);
        Assert.Null(retrieved);
        _output.WriteLine("✅ Verified state was deleted");
    }

    [Fact]
    public async Task StateStore_BulkOperations_ShouldWork()
    {
        // Arrange
        const string storeName = "statestore";
        var items = Enumerable.Range(1, 10)
            .Select(i => new TestData
            {
                Id = Guid.NewGuid(),
                Name = $"Item {i}",
                Value = i * 10
            })
            .ToList();

        // Act - Save multiple states
        var stateItems = items.Select((item, index) => new StateTransactionRequest(
            $"bulk-key-{index}",
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item),
            StateOperationType.Upsert
        )).ToList();

        await _daprClient.ExecuteStateTransactionAsync(storeName, stateItems);
        _output.WriteLine($"✅ Saved {items.Count} items in bulk");

        // Assert - Retrieve and verify
        for (int i = 0; i < items.Count; i++)
        {
            var retrieved = await _daprClient.GetStateAsync<TestData>(storeName, $"bulk-key-{i}");
            Assert.NotNull(retrieved);
            Assert.Equal(items[i].Name, retrieved.Name);
        }
        _output.WriteLine("✅ All bulk items verified");
    }

    [Fact]
    public async Task StateStore_ETags_ShouldPreventConcurrentUpdates()
    {
        // Arrange
        const string storeName = "statestore";
        const string key = "etag-test-key";
        var initialData = new TestData { Id = Guid.NewGuid(), Name = "Initial", Value = 1 };

        // Save initial state and get ETag
        await _daprClient.SaveStateAsync(storeName, key, initialData);
        var (data, etag) = await _daprClient.GetStateAndETagAsync<TestData>(storeName, key);

        _output.WriteLine($"✅ Initial ETag: {etag}");

        // Act - Update with correct ETag (should succeed)
        var updatedData = new TestData { Id = data!.Id, Name = "Updated", Value = 2 };
        var success = await _daprClient.TrySaveStateAsync(storeName, key, updatedData, etag);

        // Assert
        Assert.True(success);
        _output.WriteLine("✅ Update with valid ETag succeeded");

        // Act - Try to update with old ETag (should fail)
        var invalidUpdate = new TestData { Id = data.Id, Name = "Should Fail", Value = 3 };
        var failedUpdate = await _daprClient.TrySaveStateAsync(storeName, key, invalidUpdate, etag);

        // Assert
        Assert.False(failedUpdate);
        _output.WriteLine("✅ Update with invalid ETag correctly rejected");
    }

    #endregion

    #region Pub/Sub Tests

    [Fact]
    public async Task PubSub_PublishMessage_ShouldSucceed()
    {
        // Arrange
        const string pubsubName = "pubsub";
        const string topic = "test-topic";
        var message = new TestMessage
        {
            Id = Guid.NewGuid(),
            Content = "Hello from Dapr!",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _daprClient.PublishEventAsync(pubsubName, topic, message);

        // Assert
        _output.WriteLine($"✅ Published message to topic '{topic}'");
        _output.WriteLine($"   Message ID: {message.Id}");
        _output.WriteLine($"   Content: {message.Content}");
    }

    [Fact]
    public async Task PubSub_PublishMultipleMessages_ShouldSucceed()
    {
        // Arrange
        const string pubsubName = "pubsub";
        const string topic = "bulk-test-topic";
        var messages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage
            {
                Id = Guid.NewGuid(),
                Content = $"Message {i}",
                Timestamp = DateTime.UtcNow
            })
            .ToList();

        // Act
        foreach (var message in messages)
        {
            await _daprClient.PublishEventAsync(pubsubName, topic, message);
        }

        // Assert
        _output.WriteLine($"✅ Published {messages.Count} messages to topic '{topic}'");
    }

    #endregion

    #region Service Invocation Tests

    [Fact]
    public async Task ServiceInvocation_InvokeMethod_ShouldHandleErrors()
    {
        // Arrange
        const string appId = "non-existent-service";
        const string methodName = "test-method";

        // Act & Assert
        await Assert.ThrowsAsync<global::Dapr.DaprException>(async () =>
        {
            await _daprClient.InvokeMethodAsync<object>(appId, methodName);
        });

        _output.WriteLine("✅ Service invocation correctly handled non-existent service");
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task Dapr_GetMetadata_ShouldReturnComponentInfo()
    {
        // Act
        var metadata = await _daprClient.GetMetadataAsync();

        // Assert
        Assert.NotNull(metadata);
        Assert.NotEmpty(metadata.Components);

        _output.WriteLine("✅ Dapr metadata retrieved:");
        _output.WriteLine($"   App ID: {metadata.Id}");
        _output.WriteLine($"   Components: {metadata.Components.Count}");

        foreach (var component in metadata.Components)
        {
            _output.WriteLine($"     - {component.Name} ({component.Type})");
        }
    }

    #endregion

    #region Helper Models

    private class TestData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private class TestMessage
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    #endregion

    public async Task DisposeAsync()
    {
        _daprClient?.Dispose();

        if (_daprContainer != null)
        {
            await _daprContainer.DisposeAsync();
        }
    }
}
