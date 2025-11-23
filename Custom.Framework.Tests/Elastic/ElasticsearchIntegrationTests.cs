using Nest;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Elastic;

/// <summary>
/// Integration tests for Elasticsearch functionality
/// </summary>
public class ElasticsearchIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElasticTestContainer _container = default!;
    private IElasticClient _client = default!;

    public ElasticsearchIntegrationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _container = new ElasticTestContainer(_output);
        await _container.InitializeAsync();

        var settings = new ConnectionSettings(new Uri(_container.ElasticUrl))
            .DefaultIndex("test-logs");

        _client = new ElasticClient(settings);
    }

    [Fact]
    public async Task Elasticsearch_Cluster_ShouldBeHealthy()
    {
        // Act
        var healthResponse = await _client.Cluster.HealthAsync();

        // Assert
        Assert.True(healthResponse.IsValid);
        Assert.Equal("green", healthResponse.Status.ToString().ToLowerInvariant());

        _output.WriteLine($"✅ Cluster: {healthResponse.ClusterName}");
        _output.WriteLine($"✅ Nodes: {healthResponse.NumberOfNodes}");
    }

    [Fact]
    public async Task IndexDocument_ShouldSucceed()
    {
        // Arrange
        var logEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Information",
            Message = "Test log message",
            ServiceName = "TestService",
            Environment = "Test",
            TraceId = Guid.NewGuid().ToString()
        };

        // Act
        var indexResponse = await _client.IndexAsync(logEntry, idx => idx.Index("test-logs"));

        // Assert
        Assert.True(indexResponse.IsValid);
        Assert.Equal(Result.Created, indexResponse.Result);

        _output.WriteLine($"✅ Document indexed: {indexResponse.Id}");
    }

    [Fact]
    public async Task SearchDocuments_ShouldReturnResults()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        await _client.IndexAsync(new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Searchable log {testId}",
            TestId = testId
        }, idx => idx.Index("test-logs").Refresh(Elasticsearch.Net.Refresh.True));

        // Act
        var searchResponse = await _client.SearchAsync<dynamic>(s => s
            .Index("test-logs")
            .Query(q => q
                .Match(m => m
                    .Field("testId")
                    .Query(testId))));

        // Assert
        Assert.True(searchResponse.IsValid);
        Assert.True(searchResponse.Total > 0);

        _output.WriteLine($"✅ Found {searchResponse.Total} documents");
    }

    [Fact]
    public async Task BulkIndexDocuments_ShouldSucceed()
    {
        // Arrange
        var documents = Enumerable.Range(1, 10).Select(i => new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Information",
            Message = $"Bulk log message {i}",
            ServiceName = "TestService",
            BatchId = Guid.NewGuid().ToString()
        }).ToList();

        // Act
        var bulkResponse = await _client.BulkAsync(b => b
            .Index("test-logs")
            .IndexMany(documents));

        // Assert
        Assert.True(bulkResponse.IsValid);
        Assert.Equal(10, bulkResponse.Items.Count);
        Assert.All(bulkResponse.Items, item => Assert.True(item.IsValid));

        _output.WriteLine($"✅ Bulk indexed {bulkResponse.Items.Count} documents");
    }

    [Fact]
    public async Task Add_logEntry_to_Templated_Index()
    {
        // Arrange
        var uniqueTraceId = Guid.NewGuid().ToString();
        var logEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Error",
            Message = "This is a test error log entry",
            ServiceName = "TestService",
            Environment = "Test",
            TraceId = uniqueTraceId,
            ErrorCode = "TEST_ERROR_001",
            ErrorDetails = "Simulated error for testing templated index pattern"
        };

        // Act - Index to templated pattern "logs-test.error"
        var indexResponse = await _client.IndexAsync(logEntry, idx => idx
            .Index("logs-test.error")
            .Refresh(Elasticsearch.Net.Refresh.True));

        // Assert - Verify indexing succeeded
        Assert.True(indexResponse.IsValid, $"Indexing failed: {indexResponse.DebugInformation}");
        Assert.Equal(Result.Created, indexResponse.Result);

        _output.WriteLine($"✅ Document indexed to 'logs-test.error': {indexResponse.Id}");
        _output.WriteLine($"✅ TraceId: {uniqueTraceId}");

        // Act - Search for the log entry
        var searchResponse = await _client.SearchAsync<dynamic>(s => s
            .Index("logs-test.error")
            .Query(q => q
                .Match(m => m
                    .Field("traceId")
                    .Query(uniqueTraceId))));

        // Assert - Verify log entry exists
        Assert.True(searchResponse.IsValid, $"Search failed: {searchResponse.DebugInformation}");
        Assert.True(searchResponse.Total > 0, "Expected at least one document to be found");
        Assert.Equal(1, searchResponse.Total);

        var documentDict = ((IDictionary<string, object>)searchResponse.Documents)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            );

        var traceId = documentDict.FirstOrDefault(x => x.Key == "traceId").Value.ToString();
        var message = documentDict.FirstOrDefault(x => x.Key == "message").Value.ToString();
        var level = documentDict.FirstOrDefault(x => x.Key == "level").Value.ToString();

        Assert.Equal(uniqueTraceId, traceId);
        Assert.Equal("Error", level);
        Assert.Equal("This is a test error log entry", message);

        _output.WriteLine($"✅ Found {searchResponse.Total} document(s) in 'logs-test.error' index");
        _output.WriteLine($"✅ Verified log entry with TraceId: {traceId}");
        _output.WriteLine($"✅ Log Level: {level}");
        _output.WriteLine($"✅ Message: {message}");
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
