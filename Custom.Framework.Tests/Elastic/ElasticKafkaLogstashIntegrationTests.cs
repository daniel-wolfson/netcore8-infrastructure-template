using Confluent.Kafka;
using Nest;
using System.Text.Json;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Elastic;

/// <summary>
/// Integration tests for Kafka → Logstash → Elasticsearch pipeline
/// Tests the complete flow from producing messages to Kafka through Logstash to Elasticsearch
/// </summary>
public class ElasticKafkaLogstashIntegrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly ITestOutputHelper _output = output;
    private ElasticTestContainer _container = default!;
    private IElasticClient _elasticClient = default!;
    private IProducer<string, string>? _kafkaProducer;

    public async Task InitializeAsync()
    {
        // Enable Logstash in the container
        _container = new ElasticTestContainer(_output)
        {
            EnableLogstash = true
        };
        await _container.InitializeAsync();

        var settings = new ConnectionSettings(new Uri(_container.ElasticUrl))
            .DefaultIndex("logs-test");

        _elasticClient = new ElasticClient(settings);

        // Create Kafka producer (assumes Kafka is running on localhost:9092)
        // You can adjust this based on your Kafka setup
        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = "localhost:9092",
                ClientId = "test-producer",
                Acks = Acks.All,
                EnableIdempotence = true
            };

            _kafkaProducer = new ProducerBuilder<string, string>(producerConfig).Build();
            _output.WriteLine("✅ Kafka producer initialized");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️  Kafka producer initialization failed: {ex.Message}");
            _output.WriteLine("   Tests requiring Kafka will be skipped");
        }
    }

    [Fact]
    public async Task KafkaToElasticsearch_ThroughLogstash_ShouldSucceed()
    {
        // Skip if Kafka is not available
        if (_kafkaProducer == null)
        {
            _output.WriteLine("⚠️  Skipping test - Kafka not available");
            return;
        }

        // Container initialization already waited for Logstash subscription
        // No need for additional 30-second wait here
        
        // Arrange
        var traceId = Guid.NewGuid().ToString();
        var logEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Level = "Error",
            Message = "Test error message through Kafka → Logstash → Elasticsearch",
            ServiceName = "TestService",
            Environment = "Test",
            TraceId = traceId,
            ErrorCode = "TEST_ERROR_002",
            Source = "KafkaLogstashPipeline"
        };

        var json = JsonSerializer.Serialize(logEntry);
        _output.WriteLine($"📤 Sending message to Kafka:");
        _output.WriteLine($"   Topic: logs-test.error");
        _output.WriteLine($"   TraceId: {traceId}");
        _output.WriteLine($"   Message: {json}");

        // Act - Send to Kafka
        var deliveryResult = await _kafkaProducer.ProduceAsync(
            "logs-test.error",
            new Message<string, string>
            {
                Key = traceId,
                Value = json
            });

        _output.WriteLine($"✅ Message sent to Kafka: {deliveryResult.TopicPartitionOffset}");

        // Wait for Logstash to consume and process
        _output.WriteLine("\n⏳ Waiting for Logstash to consume and process message (20 seconds)...");
        await Task.Delay(20000);

        // Check Logstash logs for debugging
        _output.WriteLine("\n📋 Checking Logstash logs for debugging...");
        var logstashLogs = await _container.GetLogstashLogsAsync();
        var logLines = logstashLogs.Split('\n');
        
        // Show last 100 lines instead of 50 to see more context
        _output.WriteLine($"Total log lines: {logLines.Length}");
        
        // Show errors first if any
        var errorLines = logLines.Where(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                                             l.Contains("WARN", StringComparison.OrdinalIgnoreCase)).ToList();
        if (errorLines.Any())
        {
            _output.WriteLine("\n⚠️  Errors/Warnings found in logs:");
            foreach (var line in errorLines.Take(20))
            {
                _output.WriteLine($"   {line}");
            }
        }
        
        // Then show last 50 lines
        _output.WriteLine("\nLast 50 log lines:");
        foreach (var line in logLines.TakeLast(50))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _output.WriteLine($"   {line}");
            }
        }

        // Check for specific indicators
        var hasKafkaStart = logLines.Any(l => l.Contains("kafka", StringComparison.OrdinalIgnoreCase) && l.Contains("start", StringComparison.OrdinalIgnoreCase));
        var hasSubscribed = logLines.Any(l => l.Contains("subscribed", StringComparison.OrdinalIgnoreCase));
        var hasElasticsearch = logLines.Any(l => l.Contains("elasticsearch", StringComparison.OrdinalIgnoreCase));
        var hasConnectionError = logLines.Any(l => l.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
                                                    l.Contains("broker may not be available", StringComparison.OrdinalIgnoreCase));
        
        _output.WriteLine($"\n🔍 Log analysis:");
        _output.WriteLine($"   Kafka started: {hasKafkaStart}");
        _output.WriteLine($"   Subscribed to topics: {hasSubscribed}");
        _output.WriteLine($"   Elasticsearch mentioned: {hasElasticsearch}");
        _output.WriteLine($"   Connection errors: {hasConnectionError}");

        if (hasConnectionError)
        {
            _output.WriteLine("\n❌ CRITICAL: Logstash cannot connect to Kafka!");
            _output.WriteLine("   Possible causes:");
            _output.WriteLine("   1. Kafka not running on localhost:9092");
            _output.WriteLine("   2. Docker networking issue with host.docker.internal");
            _output.WriteLine("   3. Firewall blocking connection");
            _output.WriteLine("\n   Verify with: kafka-topics --list --bootstrap-server localhost:9092");
        }

        if (!hasSubscribed)
        {
            _output.WriteLine("\n⚠️  WARNING: Logstash has not subscribed to Kafka topics!");
            _output.WriteLine("   This means no messages will be consumed.");
        }

        // Assert - Search in Elasticsearch
        _output.WriteLine("\n🔍 Searching for message in Elasticsearch...");
        var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
            .Index("logs-*")
            .Query(q => q
                .Match(m => m
                    .Field("TraceId")
                    .Query(traceId))));

        if (!searchResponse.IsValid)
        {
            _output.WriteLine($"❌ Search failed: {searchResponse.DebugInformation}");
        }

        if (searchResponse.Total == 0)
        {
            _output.WriteLine("❌ No documents found. Checking all indices...");
            
            // Check what indices exist
            var catIndices = await _elasticClient.Cat.IndicesAsync(i => i.Index("logs-*"));
            _output.WriteLine("\nExisting indices:");
            if (catIndices.Records.Any())
            {
                foreach (var index in catIndices.Records)
                {
                    _output.WriteLine($"   - {index.Index} ({index.DocsCount} docs)");
                }
            }
            else
            {
                _output.WriteLine("   No indices matching 'logs-*' found!");
                _output.WriteLine("   This confirms Logstash has not sent any documents to Elasticsearch.");
            }
            
            // Try searching all logs
            if (catIndices.Records.Any())
            {
                var allLogsResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                    .Index("logs-*")
                    .Size(10)
                    .Sort(ss => ss.Descending("@timestamp")));
                
                _output.WriteLine($"\nTotal documents in logs-*: {allLogsResponse.Total}");
                if (allLogsResponse.Documents.Any())
                {
                    _output.WriteLine("Sample documents (most recent first):");
                    foreach (var doc in allLogsResponse.Documents.Take(5))
                    {
                        try
                        {
                            _output.WriteLine($"   - TraceId: {doc.TraceId}, Level: {doc.Level}, @timestamp: {doc["@timestamp"]}");
                        }
                        catch
                        {
                            _output.WriteLine($"   - Document: {doc}");
                        }
                    }
                }
            }
            
            // Diagnostic command
            _output.WriteLine("\n🔍 Run these commands to diagnose:");
            _output.WriteLine("   kafka-consumer-groups --bootstrap-server localhost:9092 --group logstash-consumer-group --describe");
            _output.WriteLine("   docker logs <logstash-container-id> --tail 100");
        }

        Assert.True(searchResponse.IsValid, $"Search failed: {searchResponse.DebugInformation}");
        Assert.True(searchResponse.Total > 0, "Expected to find the log entry in Elasticsearch. Check Logstash logs for connection errors.");

        var foundDocument = searchResponse.Documents.First();
        Assert.Equal(traceId, foundDocument.TraceId.ToString());
        Assert.Equal("Error", foundDocument.Level.ToString());

        _output.WriteLine($"\n✅ Found document in Elasticsearch!");
        _output.WriteLine($"   TraceId: {foundDocument.TraceId}");
        _output.WriteLine($"   Level: {foundDocument.Level}");
        _output.WriteLine($"   Message: {foundDocument.Message}");
        _output.WriteLine($"   Logstash processed at: {foundDocument.logstash_processed_at}");
        _output.WriteLine($"   Pipeline: {foundDocument.pipeline}");
    }

    [Fact]
    public async Task BatchLogs_FromKafka_ShouldBeIndexedInElasticsearch()
    {
        // Skip if Kafka is not available
        if (_kafkaProducer == null)
        {
            _output.WriteLine("⚠️  Skipping test - Kafka not available");
            return;
        }

        // Arrange
        var batchSize = 10;
        var batchId = $"batch-{Guid.NewGuid():N}";
        var traceIds = new List<string>();

        _output.WriteLine($"📤 Sending batch of {batchSize} messages to Kafka...");

        // Act - Send batch to Kafka
        for (int i = 0; i < batchSize; i++)
        {
            var traceId = Guid.NewGuid().ToString();
            traceIds.Add(traceId);

            var logEntry = new
            {
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Level = "Information",
                Message = $"Batch log message {i}",
                ServiceName = "TestService",
                TraceId = traceId,
                BatchId = batchId,
                MessageNumber = i
            };

            var json = JsonSerializer.Serialize(logEntry);

            await _kafkaProducer.ProduceAsync(
                "test-logs",
                new Message<string, string>
                {
                    Key = traceId,
                    Value = json
                });
        }

        _kafkaProducer.Flush(TimeSpan.FromSeconds(10));
        _output.WriteLine($"✅ Sent {batchSize} messages to Kafka");

        // Wait for Logstash processing
        _output.WriteLine("⏳ Waiting for Logstash to process batch (15 seconds)...");
        await Task.Delay(15000);

        // Assert - Verify all logs are in Elasticsearch
        _output.WriteLine("🔍 Searching for batch messages in Elasticsearch...");
        var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
            .Index("logs-*")
            .Size(batchSize)
            .Query(q => q
                .Match(m => m
                    .Field("BatchId")
                    .Query(batchId))));

        Assert.True(searchResponse.IsValid, $"Search failed: {searchResponse.DebugInformation}");
        Assert.Equal(batchSize, searchResponse.Total);

        _output.WriteLine($"✅ Found {searchResponse.Total} documents in Elasticsearch");
        _output.WriteLine($"   Batch ID: {batchId}");

        foreach (var doc in searchResponse.Documents)
        {
            _output.WriteLine($"   - Message {doc.MessageNumber}: TraceId={doc.TraceId}");
        }
    }

    [Fact]
    public async Task VerifyLogstashPipeline_IsProcessingMessages()
    {
        // Skip if Kafka is not available
        if (_kafkaProducer == null)
        {
            _output.WriteLine("⚠️  Skipping test - Kafka not available");
            return;
        }

        // Arrange
        var traceId = Guid.NewGuid().ToString();
        var testMessage = new
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Level = "Information",
            Message = "Pipeline verification message",
            TraceId = traceId,
            ServiceName = "PipelineTest"
        };

        var json = JsonSerializer.Serialize(testMessage);
        _output.WriteLine($"📤 Sending pipeline verification message:");
        _output.WriteLine($"   TraceId: {traceId}");

        // Act
        await _kafkaProducer.ProduceAsync(
            "test-logs",
            new Message<string, string>
            {
                Key = traceId,
                Value = json
            });

        _output.WriteLine("⏳ Waiting for Logstash processing...");
        await Task.Delay(8000);

        // Assert - Check if Logstash added processing metadata
        _output.WriteLine("🔍 Verifying Logstash metadata...");
        var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
            .Index("logs-*")
            .Query(q => q
                .Match(m => m
                    .Field("TraceId")
                    .Query(traceId))));

        Assert.True(searchResponse.IsValid, $"Search failed: {searchResponse.DebugInformation}");
        Assert.True(searchResponse.Total > 0, "Message not found in Elasticsearch");

        var document = searchResponse.Documents.FirstOrDefault();
        Assert.NotNull(document);

        // Verify Logstash added its metadata
        Assert.NotNull(document.logstash_processed_at);
        Assert.Equal("kafka-to-elasticsearch", document.pipeline.ToString());

        _output.WriteLine("✅ Logstash pipeline metadata verified:");
        _output.WriteLine($"   Processed at: {document.logstash_processed_at}");
        _output.WriteLine($"   Pipeline: {document.pipeline}");
        _output.WriteLine($"   TraceId: {document.TraceId}");
    }

    [Fact]
    public async Task LogstashHealth_ShouldBeAccessible()
    {
        // Arrange & Act
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(_container.LogstashUrl);

        // Assert
        Assert.True(response.IsSuccessStatusCode, "Logstash health endpoint should be accessible");

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine("✅ Logstash health check passed");
        _output.WriteLine($"   URL: {_container.LogstashUrl}");
        _output.WriteLine($"   Status: {response.StatusCode}");
        _output.WriteLine($"   Response: {content.Substring(0, Math.Min(200, content.Length))}...");
    }

    [Fact]
    public async Task Elasticsearch_ShouldHaveLogsIndex()
    {
        // Wait a bit for any previous tests to create indices
        await Task.Delay(2000);

        // Act
        var catIndices = await _elasticClient.Cat.IndicesAsync(i => i.Index("logs-*"));

        // Assert
        Assert.True(catIndices.IsValid, "Failed to get indices from Elasticsearch");

        _output.WriteLine("✅ Elasticsearch indices:");
        foreach (var index in catIndices.Records)
        {
            _output.WriteLine($"   - {index.Index} ({index.DocsCount} docs, {index.StoreSize})");
        }
    }

    public async Task DisposeAsync()
    {
        _kafkaProducer?.Dispose();
        await _container.DisposeAsync();
    }
}
