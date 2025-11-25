# Kafka ? Logstash ? Elasticsearch - Quick Setup Guide

## ? What Was Added

### 1. **Updated ElasticTestContainer**
   - Added Logstash container support
   - Added `EnableLogstash` property to optionally start Logstash
   - Created Logstash pipeline configuration

### 2. **Created KafkaLogstashElasticIntegrationTests**
   - Complete integration tests for the Kafka ? Logstash ? Elasticsearch flow
   - Tests single message flow
   - Tests batch processing
   - Tests Logstash metadata enrichment

### 3. **Created Documentation**
   - Comprehensive README explaining the architecture
   - Troubleshooting guide
   - Performance tuning tips

## ?? How to Use

### Option 1: Run Integration Tests

```bash
# Make sure Kafka is running on localhost:9092
docker-compose -f kafka-docker-compose.yml up -d

# Run the tests
dotnet test --filter "KafkaLogstashElasticIntegrationTests"
```

### Option 2: Use in Your Own Tests

```csharp
using Custom.Framework.Tests.Elastic;
using Xunit.Abstractions;

public class MyTests : IAsyncLifetime
{
    private ElasticTestContainer _container;
    
    public async Task InitializeAsync()
    {
        _container = new ElasticTestContainer(output)
        {
            EnableLogstash = true  // ? Enable the full pipeline
        };
        await _container.InitializeAsync();
    }
    
    [Fact]
    public async Task MyTest()
    {
        // Elasticsearch is at: _container.ElasticUrl
        // Logstash is at: _container.LogstashUrl
        // Kibana is at: _container.KibanaUrl
    }
    
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Option 3: Send Logs from Your Application

```csharp
using Confluent.Kafka;
using System.Text.Json;

// 1. Create Kafka producer
var producerConfig = new ProducerConfig
{
    BootstrapServers = "localhost:9092"
};
var producer = new ProducerBuilder<string, string>(producerConfig).Build();

// 2. Create structured log entry
var logEntry = new
{
    Timestamp = DateTimeOffset.UtcNow.ToString("o"),
    Level = "Error",
    Message = "Payment processing failed",
    ServiceName = "PaymentService",
    TraceId = Guid.NewGuid().ToString(),
    UserId = "user123",
    Amount = 99.99m,
    ErrorCode = "PAYMENT_DECLINED"
};

// 3. Send to Kafka
var json = JsonSerializer.Serialize(logEntry);
await producer.ProduceAsync("logs-test.error", 
    new Message<string, string> { Value = json });

// 4. Logstash will automatically:
//    - Consume from Kafka
//    - Parse the JSON
//    - Add metadata (logstash_processed_at, pipeline)
//    - Index to Elasticsearch as logs-YYYY.MM.DD

// 5. View in Kibana at http://localhost:5601
```

## ?? What Each Container Does

### Elasticsearch (Port 9200)
- **Purpose**: Store and search logs
- **Index Pattern**: `logs-YYYY.MM.DD` (daily indices)
- **Health Check**: `http://localhost:9200/_cluster/health`

### Logstash (Ports 5044, 9600)
- **Purpose**: Process logs from Kafka ? Elasticsearch
- **Input**: Consumes from Kafka topics (`logs-test.error`, `test-logs`)
- **Filter**: Parses JSON, adds metadata
- **Output**: Sends to Elasticsearch
- **Health Check**: `http://localhost:9600/`

### Kibana (Port 5601)
- **Purpose**: Visualize and explore logs
- **URL**: `http://localhost:5601`
- **Features**: Dashboards, search, filtering, aggregations

## ?? Viewing Your Logs

### In Kibana

1. Open http://localhost:5601
2. Go to **Management ? Stack Management ? Index Patterns**
3. Create index pattern: `logs-*`
4. Go to **Discover**
5. Search your logs:
   - By TraceId: `TraceId:"abc-123"`
   - By Level: `Level:"Error"`
   - By Service: `ServiceName:"PaymentService"`
   - Time range: Last 15 minutes, Last hour, etc.

### Using Elasticsearch API

```bash
# Search all logs
curl http://localhost:9200/logs-*/_search?pretty

# Search by TraceId
curl -X POST http://localhost:9200/logs-*/_search?pretty -H 'Content-Type: application/json' -d'
{
  "query": {
    "match": {
      "TraceId": "your-trace-id"
    }
  }
}'

# Count logs by level
curl -X POST http://localhost:9200/logs-*/_search?pretty -H 'Content-Type: application/json' -d'
{
  "size": 0,
  "aggs": {
    "by_level": {
      "terms": { "field": "Level.keyword" }
    }
  }
}'
```

## ??? Prerequisites

### 1. Kafka Must Be Running

The integration assumes Kafka is available at `localhost:9092`. You can start it using:

```bash
# Using docker-compose (if you have kafka-docker-compose.yml)
docker-compose -f kafka-docker-compose.yml up -d

# Or using your existing Kafka setup
# Make sure topics exist:
kafka-topics --create --topic logs-test.error --bootstrap-server localhost:9092
kafka-topics --create --topic test-logs --bootstrap-server localhost:9092
```

### 2. Docker Desktop Running

All containers (Elasticsearch, Logstash, Kibana) run in Docker.

## ?? Architecture Flow

```
Your .NET App
    ?
    ? (produces JSON logs)
    ?
  KAFKA
    ? Topics: logs-test.error, test-logs
    ?
    ? (Logstash consumes)
    ?
LOGSTASH
    ? Pipeline:
    ? - Input: Kafka consumer
    ? - Filter: JSON parsing, metadata enrichment
    ? - Output: Elasticsearch
    ?
    ?
ELASTICSEARCH
    ? Index: logs-YYYY.MM.DD
    ? Document ID: TraceId
    ?
    ?
KIBANA
    ? Visualization & Search
    ??? http://localhost:5601
```

## ?? Key Features

### 1. Automatic Metadata Enrichment

Logstash automatically adds:
- `logstash_processed_at` - When Logstash processed the message
- `pipeline` - Pipeline name ("kafka-to-elasticsearch")
- `[@metadata][index_date]` - For daily index routing

### 2. TraceId-Based Correlation

Use TraceId to correlate logs across:
- Multiple services
- Multiple requests
- Distributed transactions

### 3. Daily Indices

Logs are automatically routed to daily indices:
- `logs-2024.01.15`
- `logs-2024.01.16`
- etc.

Benefits:
- Easy to delete old logs
- Better search performance
- Simplified backup/restore

### 4. Graceful Degradation

If Elasticsearch is down:
- Kafka buffers messages
- Logstash retries automatically
- No log loss

## ?? Running the Tests

```bash
# Run all Kafka-Logstash-Elasticsearch tests
dotnet test --filter "KafkaLogstashElasticIntegrationTests"

# Run specific test
dotnet test --filter "KafkaLogstashElasticIntegrationTests.KafkaToElasticsearch_ThroughLogstash_ShouldSucceed"

# Run with verbose output
dotnet test --filter "KafkaLogstashElasticIntegrationTests" --logger "console;verbosity=detailed"
```

### Expected Test Output

```
?? Starting Elasticsearch test infrastructure...
? Created network: elastic-test-network
? Starting Elasticsearch...
? Elasticsearch ready at http://localhost:xxxxx
? Starting Logstash...
? Logstash pipeline created at: C:\Temp\logstash-pipeline-xxx
? Logstash ready at http://localhost:xxxxx
? Starting Kibana...
? Kibana ready at http://localhost:xxxxx
? Elasticsearch stack is ready!

?? Sending message to Kafka:
   Topic: logs-test.error
   TraceId: abc-123-def-456
? Message sent to Kafka: Partition 0, Offset 42
? Waiting for Logstash to process message (10 seconds)...
?? Searching for message in Elasticsearch...
? Found document in Elasticsearch!
   TraceId: abc-123-def-456
   Level: Error
   Message: Test error message...
   Logstash processed at: 2024-01-15T10:30:45Z
   Pipeline: kafka-to-elasticsearch
```

## ?? Troubleshooting

### Tests Fail - Kafka Not Available

If you see:
```
??  Kafka producer initialization failed
   Tests requiring Kafka will be skipped
```

**Solution**: Start Kafka before running tests:
```bash
docker-compose -f kafka-docker-compose.yml up -d
```

### Logstash Container Fails to Start

Check Logstash logs:
```bash
docker logs <logstash-container-id>
```

Common issues:
- Invalid pipeline configuration syntax
- Cannot connect to Kafka
- Cannot connect to Elasticsearch

### Messages Not Appearing in Elasticsearch

1. **Check Kafka has the message**:
   ```bash
   kafka-console-consumer --bootstrap-server localhost:9092 \
     --topic logs-test.error --from-beginning
   ```

2. **Check Logstash is consuming**:
   ```bash
   docker logs <logstash-container-id> -f
   ```

3. **Check Elasticsearch indices**:
   ```bash
   curl http://localhost:9200/_cat/indices?v
   ```

## ?? Next Steps

1. ? **Run the tests** to verify everything works
2. ? **Explore Kibana** dashboards and visualizations
3. ? **Integrate with your application** using Kafka producer
4. ? **Create Kibana dashboards** for your specific use cases
5. ? **Set up alerts** for errors and anomalies
6. ? **Configure log retention** policies (ILM)

## ?? Additional Documentation

- [Full Documentation](./KAFKA-LOGSTASH-ELASTICSEARCH.md) - Complete guide
- [ElasticTestContainer.cs](./ElasticTestContainer.cs) - Container implementation
- [KafkaLogstashElasticIntegrationTests.cs](./KafkaLogstashElasticIntegrationTests.cs) - Test examples
- [Logstash Configuration](https://www.elastic.co/guide/en/logstash/current/configuration.html)

## ? Summary

You now have a complete **Kafka ? Logstash ? Elasticsearch** pipeline! 

**What you can do:**
- ? Send structured logs from .NET apps to Kafka
- ? Automatically process and enrich logs with Logstash
- ? Store and search logs in Elasticsearch
- ? Visualize logs in Kibana
- ? Run integration tests for the entire pipeline
- ? Handle millions of log events per day

**Key Benefits:**
- ?? **Decoupled** - Services don't depend on Elasticsearch
- ?? **Buffered** - Kafka handles traffic spikes
- ?? **Searchable** - Full-text search in Elasticsearch
- ?? **Visual** - Dashboards and charts in Kibana
- ??? **Reliable** - Message durability and replay
- ?? **Scalable** - Each component scales independently

Happy logging! ??
