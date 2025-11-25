using Confluent.Kafka;
using Nest;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Elastic;

/// <summary>
/// Diagnostic tests to verify Kafka → Logstash → Elasticsearch connectivity
/// Run these tests first to validate your setup
/// </summary>
public class ElasticLogstashDiagnosticTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElasticTestContainer _container = default!;
    private IElasticClient _elasticClient = default!;

    public ElasticLogstashDiagnosticTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _container = new ElasticTestContainer(_output)
        {
            EnableLogstash = true
        };
        await _container.InitializeAsync();

        var settings = new ConnectionSettings(new Uri(_container.ElasticUrl))
            .DefaultIndex("logs-test");

        _elasticClient = new ElasticClient(settings);
    }

    [Fact]
    public async Task Step1_Verify_Kafka_Is_Running()
    {
        _output.WriteLine("🔍 Step 1: Verifying Kafka is running...");

        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = "localhost:9092",
                SocketTimeoutMs = 5000
            };

            using var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = "localhost:9092"
            }).Build();
            
            // Try to get metadata - this will fail if Kafka is not accessible
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            
            _output.WriteLine($"✅ Kafka is running and accessible");
            _output.WriteLine($"   Brokers: {metadata.Brokers.Count}");
            _output.WriteLine($"   Topics: {metadata.Topics.Count}");

            foreach (var topic in metadata.Topics)
            {
                _output.WriteLine($"     - {topic.Topic} ({topic.Partitions.Count} partitions)");
            }

            Assert.True(metadata.Brokers.Count > 0, "Expected at least one Kafka broker");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Kafka is NOT accessible: {ex.Message}");
            _output.WriteLine("   Please start Kafka on localhost:9092");
            throw;
        }
    }

    [Fact]
    public async Task Step2_Verify_Kafka_Topics_Exist()
    {
        _output.WriteLine("🔍 Step 2: Verifying Kafka topics exist...");

        var requiredTopics = new[] { "logs-test.error", "test-logs" };

        try
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = "localhost:9092"
            }).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var existingTopics = metadata.Topics.Select(t => t.Topic).ToList();

            foreach (var topic in requiredTopics)
            {
                if (existingTopics.Contains(topic))
                {
                    _output.WriteLine($"✅ Topic '{topic}' exists");
                }
                else
                {
                    _output.WriteLine($"❌ Topic '{topic}' does NOT exist");
                    _output.WriteLine($"   Create it with:");
                    _output.WriteLine($"   kafka-topics --create --topic {topic} --bootstrap-server localhost:9092");
                }
            }

            Assert.True(existingTopics.Contains("logs-test.error"), "Topic 'logs-test.error' must exist");
            Assert.True(existingTopics.Contains("test-logs"), "Topic 'test-logs' must exist");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Failed to check topics: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task Step3_Verify_Elasticsearch_Is_Accessible()
    {
        _output.WriteLine("🔍 Step 3: Verifying Elasticsearch is accessible...");

        var healthResponse = await _elasticClient.Cluster.HealthAsync();

        Assert.True(healthResponse.IsValid, "Elasticsearch health check failed");

        _output.WriteLine($"✅ Elasticsearch is accessible");
        _output.WriteLine($"   URL: {_container.ElasticUrl}");
        _output.WriteLine($"   Cluster: {healthResponse.ClusterName}");
        _output.WriteLine($"   Status: {healthResponse.Status}");
        _output.WriteLine($"   Nodes: {healthResponse.NumberOfNodes}");
    }

    [Fact]
    public async Task Step4_Verify_Logstash_Is_Running()
    {
        _output.WriteLine("🔍 Step 4: Verifying Logstash is running...");

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(_container.LogstashUrl);

        Assert.True(response.IsSuccessStatusCode, $"Logstash health check failed: {response.StatusCode}");

        _output.WriteLine($"✅ Logstash is accessible");
        _output.WriteLine($"   URL: {_container.LogstashUrl}");
        _output.WriteLine($"   Status: {response.StatusCode}");
    }

    [Fact]
    public async Task Step5_Verify_Logstash_Pipeline_Loaded()
    {
        _output.WriteLine("🔍 Step 5: Verifying Logstash pipeline is loaded...");

        using var httpClient = new HttpClient();
        var nodeStatsUrl = $"{_container.LogstashUrl}/_node/stats";
        
        var response = await httpClient.GetAsync(nodeStatsUrl);
        Assert.True(response.IsSuccessStatusCode, "Failed to get Logstash stats");

        var content = await response.Content.ReadAsStringAsync();
        
        _output.WriteLine($"✅ Logstash stats retrieved");

        if (content.Contains("\"pipeline\""))
        {
            _output.WriteLine($"✅ Pipeline is loaded");
        }
        else
        {
            _output.WriteLine($"⚠️  Pipeline may not be loaded");
        }

        // Get the logs to see pipeline status
        var logs = await _container.GetLogstashLogsAsync();
        var logLines = logs.Split('\n');

        _output.WriteLine("\n📋 Logstash Pipeline Status:");
        foreach (var line in logLines.Where(l => l.Contains("pipeline", StringComparison.OrdinalIgnoreCase)).TakeLast(10))
        {
            _output.WriteLine($"   {line.Trim()}");
        }
    }

    [Fact]
    public async Task Step6_Verify_Logstash_Can_Connect_To_Kafka()
    {
        _output.WriteLine("🔍 Step 6: Verifying Logstash can connect to Kafka...");

        // Wait a bit for Logstash to attempt connection
        await Task.Delay(5000);

        var logs = await _container.GetLogstashLogsAsync();
        var logLines = logs.Split('\n');

        // Look for Kafka-related log entries
        var kafkaLogs = logLines
            .Where(l => l.Contains("kafka", StringComparison.OrdinalIgnoreCase))
            .TakeLast(20)
            .ToList();

        _output.WriteLine("\n📋 Kafka Connection Logs:");
        foreach (var line in kafkaLogs)
        {
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine($"   ❌ {line.Trim()}");
            }
            else
            {
                _output.WriteLine($"   ℹ️  {line.Trim()}");
            }
        }

        // Check for success indicators
        var hasStarted = logLines.Any(l => l.Contains("Kafka input") && l.Contains("started"));
        var hasSubscribed = logLines.Any(l => l.Contains("Subscribed to topics"));
        var hasError = logLines.Any(l => l.Contains("error") || l.Contains("failed"));

        if (hasStarted)
            _output.WriteLine("\n✅ Kafka input plugin started");
        
        if (hasSubscribed)
            _output.WriteLine("✅ Subscribed to topics");

        if (hasError)
        {
            var errors = string.Join("\n", logLines
                .Where(l => l.Contains("error") || l.Contains("failed"))
                .ToList());

            _output.WriteLine($"❌ Errors detected in Kafka connection: {errors}");
        }

        if (!hasStarted || !hasSubscribed || !hasError)
        {
            _output.WriteLine("\n⚠️  Logstash may not have successfully connected to Kafka");
            _output.WriteLine("   Check the logs above for connection errors");
        }
    }

    [Fact]
    public async Task Step7_End_To_End_Message_Flow()
    {
        _output.WriteLine("🔍 Step 7: Testing end-to-end message flow...");

        // Send a test message to Kafka
        var traceId = $"diagnostic-{Guid.NewGuid():N}";
        var testMessage = new
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Level = "Information",
            Message = "Diagnostic test message",
            TraceId = traceId,
            ServiceName = "DiagnosticTest"
        };

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = "localhost:9092"
        }).Build();

        var json = System.Text.Json.JsonSerializer.Serialize(testMessage);
        
        _output.WriteLine($"📤 Sending message to Kafka:");
        _output.WriteLine($"   Topic: test-logs");
        _output.WriteLine($"   TraceId: {traceId}");

        var deliveryResult = await producer.ProduceAsync("test-logs",
            new Message<string, string> { Key = traceId, Value = json });

        _output.WriteLine($"✅ Message sent: {deliveryResult.TopicPartitionOffset}");

        // Wait for Logstash to process
        _output.WriteLine("\n⏳ Waiting 20 seconds for Logstash to process...");
        await Task.Delay(20000);

        // Search in Elasticsearch
        _output.WriteLine("🔍 Searching in Elasticsearch...");
        var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
            .Index("logs-*")
            .Query(q => q
                .Match(m => m
                    .Field("TraceId")
                    .Query(traceId))));

        if (searchResponse.Total > 0)
        {
            _output.WriteLine($"✅ SUCCESS! Message found in Elasticsearch");
            var doc = searchResponse.Documents.First();
            _output.WriteLine($"   TraceId: {doc.TraceId}");
            _output.WriteLine($"   Message: {doc.Message}");
            _output.WriteLine($"   Pipeline: {doc.pipeline}");
            _output.WriteLine($"   Processed at: {doc.logstash_processed_at}");
        }
        else
        {
            _output.WriteLine($"❌ FAILED: Message NOT found in Elasticsearch");
            
            // Show what indices exist
            var catIndices = await _elasticClient.Cat.IndicesAsync(i => i.Index("logs-*"));
            _output.WriteLine("\nExisting indices:");
            foreach (var index in catIndices.Records)
            {
                _output.WriteLine($"   - {index.Index} ({index.DocsCount} docs)");
            }

            // Show Logstash logs
            _output.WriteLine("\nRecent Logstash logs:");
            var logs = await _container.GetLogstashLogsAsync();
            var recentLogs = logs.Split('\n').TakeLast(30);
            foreach (var line in recentLogs.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                _output.WriteLine($"   {line}");
            }

            Assert.Fail("Message did not flow through Kafka → Logstash → Elasticsearch");
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
