# Kafka ? Logstash ? Elasticsearch Integration

## ?? Overview

This document describes the **Kafka ? Logstash ? Elasticsearch** pipeline integration in Custom.Framework, providing a complete observability solution for distributed .NET applications.

## ??? Architecture

```
???????????????????????????????????????????????????????????????????
? .NET APPLICATION LAYER                                          ?
?                                                                  ?
?  ????????????????????????????????????????????????              ?
?  ? Custom.Framework.Kafka                       ?              ?
?  ? - Structured logging                         ?              ?
?  ? - Publish logs as JSON to Kafka topics       ?              ?
?  ? - High-throughput async operations           ?              ?
?  ????????????????????????????????????????????????              ?
????????????????????????????????????????????????????????????????????
                       ?
                       ?
        ????????????????????????????????????
        ? KAFKA (Message Broker)           ?
        ? - Topic: logs-test.error         ?
        ? - Topic: test-logs               ?
        ? - Decouples producers/consumers  ?
        ? - Provides buffering & replay    ?
        ????????????????????????????????????
                        ?
                        ?
        ????????????????????????????????????
        ? LOGSTASH (Pipeline Processor)    ?
        ?                                  ?
        ? Input:  Kafka consumer           ?
        ? Filter: Parse JSON, enrich data  ?
        ? Output: Elasticsearch            ?
        ?                                  ?
        ? Metadata Added:                  ?
        ? - logstash_processed_at          ?
        ? - pipeline: "kafka-to-es"        ?
        ? - index_date: "YYYY.MM.DD"       ?
        ????????????????????????????????????
                        ?
                        ?
        ????????????????????????????????????
        ? ELASTICSEARCH (Storage/Search)   ?
        ? - Index: logs-YYYY.MM.DD         ?
        ? - Full-text search               ?
        ? - Aggregations & analytics       ?
        ????????????????????????????????????
                       ?
                       ?
        ????????????????????????????????????
        ? KIBANA (Visualization)           ?
        ? - Dashboards                     ?
        ? - Log exploration                ?
        ? - Alerting                       ?
        ? http://localhost:5601            ?
        ????????????????????????????????????
```

## ?? Quick Start

### 1. Enable Logstash in Tests

```csharp
var container = new ElasticTestContainer(output)
{
    EnableLogstash = true  // ? Enable Logstash
};
await container.InitializeAsync();
```

### 2. Send Logs to Kafka

```csharp
using Confluent.Kafka;
using System.Text.Json;

var producerConfig = new ProducerConfig
{
    BootstrapServers = "localhost:9092"
};

var producer = new ProducerBuilder<string, string>(producerConfig).Build();

var logEntry = new
{
    Timestamp = DateTimeOffset.UtcNow,
    Level = "Error",
    Message = "Application error occurred",
    ServiceName = "MyService",
    TraceId = Guid.NewGuid().ToString(),
    ErrorCode = "APP_ERROR_001"
};

var json = JsonSerializer.Serialize(logEntry);

await producer.ProduceAsync("logs-test.error", 
    new Message<string, string> 
    { 
        Key = logEntry.TraceId,
        Value = json 
    });
```

### 3. View Logs in Kibana

1. Navigate to **http://localhost:5601**
2. Go to **Discover**
3. Create index pattern: `logs-*`
4. Search using TraceId, Level, ServiceName, etc.

## ?? Components

### Elasticsearch Container

```csharp
_elasticContainer = new ContainerBuilder()
    .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.12.0")
    .WithNetwork(_network)
    .WithNetworkAliases("elasticsearch")
    .WithPortBinding(9200, true)
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("xpack.security.enabled", "false")
    .Build();
```

### Logstash Container

```csharp
_logstashContainer = new ContainerBuilder()
    .WithImage("docker.elastic.co/logstash/logstash:8.12.0")
    .WithNetwork(_network)
    .WithNetworkAliases("logstash")
    .WithPortBinding(5044, true)  // Beats input
    .WithPortBinding(9600, true)  // API
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
    .WithBindMount(pipelineConfigPath, "/usr/share/logstash/pipeline")
    .Build();
```

### Kibana Container

```csharp
_kibanaContainer = new ContainerBuilder()
    .WithImage("docker.elastic.co/kibana/kibana:8.12.0")
    .WithNetwork(_network)
    .WithPortBinding(5601, true)
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
    .Build();
```

## ?? Logstash Pipeline Configuration

The Logstash pipeline (`logstash.conf`) processes messages from Kafka:

```ruby
input {
  kafka {
    bootstrap_servers => "kafka:9092"
    topics => ["logs-test.error", "test-logs"]
    group_id => "logstash-consumer-group"
    codec => json
    auto_offset_reset => "latest"
    consumer_threads => 2
  }
}

filter {
  # Parse JSON message
  if [message] {
    json {
      source => "message"
      target => "parsed"
    }
    
    # Copy parsed fields to root
    ruby {
      code => "
        parsed = event.get('parsed')
        if parsed.is_a?(Hash)
          parsed.each { |k, v| event.set(k, v) }
          event.remove('parsed')
        end
      "
    }
  }

  # Add timestamp if missing
  if ![Timestamp] {
    mutate {
      add_field => { "Timestamp" => "%{@timestamp}" }
    }
  }

  # Add index date for daily indices
  mutate {
    add_field => { "[@metadata][index_date]" => "%{+YYYY.MM.dd}" }
  }

  # Add Logstash metadata
  mutate {
    add_field => { 
      "logstash_processed_at" => "%{@timestamp}"
      "pipeline" => "kafka-to-elasticsearch"
    }
  }
}

output {
  elasticsearch {
    hosts => ["http://elasticsearch:9200"]
    index => "logs-%{[@metadata][index_date]}"
    document_id => "%{TraceId}"
  }

  # Debug output
  stdout {
    codec => rubydebug {
      metadata => true
    }
  }
}
```

## ?? Integration Tests

### Test 1: Single Message Flow

```csharp
[Fact]
public async Task KafkaToElasticsearch_ThroughLogstash_ShouldSucceed()
{
    // Arrange
    var traceId = Guid.NewGuid().ToString();
    var logEntry = new { /* ... */ };
    var json = JsonSerializer.Serialize(logEntry);

    // Act - Send to Kafka
    await _kafkaProducer.ProduceAsync("logs-test.error", 
        new Message<string, string> { Value = json });

    await Task.Delay(10000); // Wait for processing

    // Assert - Verify in Elasticsearch
    var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
        .Index("logs-*")
        .Query(q => q.Match(m => m.Field("TraceId").Query(traceId))));

    Assert.True(searchResponse.Total > 0);
}
```

### Test 2: Batch Processing

```csharp
[Fact]
public async Task BatchLogs_FromKafka_ShouldBeIndexedInElasticsearch()
{
    // Send 10 messages to Kafka
    for (int i = 0; i < 10; i++)
    {
        await _kafkaProducer.ProduceAsync("test-logs", message);
    }

    await Task.Delay(15000);

    // Verify all messages in Elasticsearch
    var searchResponse = await _elasticClient.SearchAsync<dynamic>(/* ... */);
    Assert.Equal(10, searchResponse.Total);
}
```

### Test 3: Logstash Metadata Verification

```csharp
[Fact]
public async Task VerifyLogstashPipeline_IsProcessingMessages()
{
    // Send message and verify Logstash added metadata
    var document = searchResponse.Documents.First();
    
    Assert.NotNull(document.logstash_processed_at);
    Assert.Equal("kafka-to-elasticsearch", document.pipeline);
}
```

## ?? Benefits

### 1. **Decoupling**
- Applications publish to Kafka without caring about Elasticsearch
- Services can restart without losing log messages
- Multiple consumers can read the same logs

### 2. **Buffering**
- Kafka acts as a buffer if Elasticsearch is down or slow
- Messages are persisted and can be replayed
- Handles traffic spikes gracefully

### 3. **Transformation**
- Logstash enriches logs with metadata
- Centralized parsing and filtering
- Consistent log format across services

### 4. **Scalability**
- Each component scales independently
- Kafka partitions for parallel processing
- Multiple Logstash instances for throughput

### 5. **Reliability**
- Kafka provides durability (replicated storage)
- At-least-once delivery guarantees
- Automatic retries on failures

## ?? Monitoring

### Logstash Health

```bash
curl http://localhost:9600
```

### Logstash Stats

```bash
curl http://localhost:9600/_node/stats
```

### Check Kafka Consumer Lag

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

### Elasticsearch Indices

```bash
curl http://localhost:9200/_cat/indices?v
```

### View Logs in Logstash

```bash
docker logs logstash-container -f
```

## ?? Troubleshooting

### Logs Not Appearing in Elasticsearch

#### 1. Check Kafka Topic

```bash
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error \
  --from-beginning
```

#### 2. Check Logstash Logs

```bash
docker logs <logstash-container-id> --tail=100
```

Look for:
- Connection errors to Kafka
- Parsing errors
- Elasticsearch indexing errors

#### 3. Check Elasticsearch

```bash
# List indices
curl http://localhost:9200/_cat/indices

# Search all logs
curl http://localhost:9200/logs-*/_search?pretty
```

### Logstash Not Consuming from Kafka

**Symptoms:**
- Kafka has messages but Logstash not processing
- Consumer lag increasing

**Solutions:**
1. Check network connectivity between containers
2. Verify Kafka topic exists
3. Check Logstash consumer group in Kafka
4. Review Logstash pipeline config syntax

### High Latency

**Causes:**
- Elasticsearch slow to index
- Logstash pipeline bottleneck
- Network issues

**Solutions:**
- Increase Logstash `consumer_threads`
- Add more Logstash instances
- Optimize Elasticsearch index settings
- Use daily indices with ILM

## ? Performance Tuning

### Logstash Configuration

```ruby
input {
  kafka {
    consumer_threads => 4        # Parallel consumers
    max_poll_records => 500      # Batch size
    session_timeout_ms => 60000  # Heartbeat timeout
  }
}
```

### Elasticsearch Optimization

```bash
# Use daily indices for easier management
index => "logs-%{+YYYY.MM.dd}"

# Bulk indexing
PUT /_cluster/settings
{
  "transient": {
    "indices.memory.index_buffer_size": "30%"
  }
}
```

### Kafka Producer Settings

```csharp
var config = new ProducerConfig
{
    Acks = Acks.Leader,        // Faster than Acks.All
    LingerMs = 10,             // Batch messages
    CompressionType = CompressionType.Snappy,
    EnableIdempotence = true   // Prevent duplicates
};
```

## ?? Scaling Guidelines

### Logstash Scaling

- **Vertical**: Increase CPU/memory for single instance
- **Horizontal**: Add more Logstash instances with same consumer group
- **Rule of thumb**: 1 Logstash instance per 10,000 events/sec

### Elasticsearch Scaling

- Use **daily indices**: `logs-2024.01.15`
- Enable **Index Lifecycle Management (ILM)**
- Configure **hot/warm/cold** architecture for cost savings

### Kafka Scaling

- Increase **partition count** for higher throughput
- Add **broker nodes** for more storage
- Configure **replication factor** for reliability

## ?? Best Practices

### 1. Structured Logging

```csharp
// ? Good - Structured
var log = new {
    Timestamp = DateTimeOffset.UtcNow,
    Level = "Error",
    Message = "Payment failed",
    TraceId = traceId,
    UserId = userId,
    Amount = amount,
    ErrorCode = "PAYMENT_DECLINED"
};

// ? Bad - Unstructured
var message = $"Error at {DateTime.Now}: Payment failed for user {userId}";
```

### 2. Use TraceId for Correlation

Always include a `TraceId` to correlate logs across services:

```csharp
var traceId = Activity.Current?.TraceId.ToString() 
    ?? Guid.NewGuid().ToString();
```

### 3. Log Levels

- **Error**: Failures requiring attention
- **Warning**: Potential issues
- **Information**: Normal flow milestones
- **Debug**: Detailed diagnostic information

### 4. Index Management

- Use **daily indices** for easy cleanup
- Enable **ILM** to automatically delete old indices
- Set **retention period** based on compliance needs

### 5. Security

For production:
- Enable **Elasticsearch authentication**
- Use **TLS** for Kafka connections
- Encrypt **sensitive fields** before logging

## ?? Additional Resources

- [Elasticsearch Integration Tests](./ElasticsearchIntegrationTests.cs)
- [ElasticTestContainer](./ElasticTestContainer.cs)
- [Kafka Integration Guide](../../Custom.Framework/Kafka/_KafkaInfo.md)
- [Elasticsearch Documentation](https://www.elastic.co/guide/en/elasticsearch/reference/current/index.html)
- [Logstash Documentation](https://www.elastic.co/guide/en/logstash/current/index.html)
- [Kibana Documentation](https://www.elastic.co/guide/en/kibana/current/index.html)

## ?? Summary

The Kafka ? Logstash ? Elasticsearch pipeline provides:

? **Decoupled architecture** - Services don't depend on Elasticsearch  
? **Buffering** - Kafka handles traffic spikes  
? **Transformation** - Logstash enriches logs  
? **Scalability** - Each component scales independently  
? **Reliability** - Kafka provides message durability  
? **Observability** - Full-text search and visualization in Kibana  

This setup is production-ready and can handle millions of log events per day!
