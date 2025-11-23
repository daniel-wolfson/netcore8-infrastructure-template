# Elasticsearch Integration - Quick Start Guide

## Overview
Elasticsearch integration for Custom.Framework providing centralized logging with OpenTelemetry trace correlation.

## Installation

### 1. NuGet Packages
The following packages have been added to `Custom.Framework.csproj`:
- NEST 7.17.5
- Elasticsearch.Net 7.17.5
- Serilog.Sinks.Elasticsearch 9.0.3
- Serilog.Enrichers.Environment 2.3.0
- Serilog.Enrichers.Thread 3.1.0
- Serilog.Enrichers.Process 2.0.2

For testing (`Custom.Framework.Tests.csproj`):
- Testcontainers.Elasticsearch 4.8.1

## Quick Start

### 1. Add Configuration (appsettings.json)
```json
{
  "Elasticsearch": {
    "Nodes": ["http://localhost:9200"],
    "Username": "elastic",
    "Password": "changeme",
    "IndexFormat": "logs-development-{0:yyyy.MM.dd}",
    "AutoRegisterTemplate": true,
    "NumberOfShards": 1,
    "NumberOfReplicas": 0,
    "BufferSize": 50,
    "MinimumLogLevel": "Information",
    "EnableHealthCheck": true,
    "EnableMetrics": true
  }
}
```

### 2. Configure in Program.cs
```csharp
using Custom.Framework.Elastic;

// Configure Serilog with Elasticsearch
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithElasticsearchContext(context.Configuration)
        .Enrich.With<OpenTelemetryEnricher>()
        .AddElasticsearchSink(context.Configuration)
        .WriteTo.Console();
});

// Add Elasticsearch services
builder.Services.AddElasticsearch(builder.Configuration);
```

### 3. Use in Your Code
```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessOrder(Order order)
    {
        using var activity = Activity.Current?.Source.StartActivity("ProcessOrder");
        activity?.SetTag("order.id", order.Id);

        _logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId}",
            order.Id,
            order.CustomerId);

        // Your logic here...
    }
}
```

## Components

### Core Components
- **ElasticOptions**: Configuration model
- **ElasticClientFactory**: NEST client factory with connection pooling
- **ElasticExtensions**: Dependency injection and Serilog configuration
- **OpenTelemetryEnricher**: Adds TraceId/SpanId to logs
- **ElasticsearchHealthCheck**: Health monitoring
- **ElasticsearchMetrics**: OpenTelemetry metrics

### Testing
- **ElasticTestContainer**: Docker-based Elasticsearch for testing
- **ElasticsearchIntegrationTests**: Sample integration tests

## Features

? **Centralized Logging**: All logs sent to Elasticsearch  
? **Trace Correlation**: Automatic TraceId/SpanId enrichment  
? **Health Checks**: Monitor Elasticsearch cluster status  
? **Metrics**: Track indexing rate, errors, latency  
? **Dead Letter Queue**: Failed logs saved locally  
? **Bulk Indexing**: High-performance batching  
? **Testing Infrastructure**: Testcontainers support  

## Testing

Run integration tests:
```bash
dotnet test --filter "FullyQualifiedName~ElasticsearchIntegrationTests"
```

## Query Logs

### Kibana Console
```json
GET logs-development-*/_search
{
  "query": {
    "match": { "Level": "Error" }
  },
  "sort": [{ "@timestamp": "desc" }]
}
```

### Find logs by TraceId
```json
GET logs-development-*/_search
{
  "query": {
    "term": { "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736" }
  }
}
```

## Health Check Endpoint

Access at: `https://your-api/health`

Response includes:
- Cluster status (Green/Yellow/Red)
- Number of nodes
- Shard statistics

## Metrics

OpenTelemetry metrics exposed:
- `elasticsearch.documents.indexed`
- `elasticsearch.indexing.errors`
- `elasticsearch.indexing.duration`
- `elasticsearch.search.requests`
- `elasticsearch.search.duration`

## Production Configuration

```json
{
  "Elasticsearch": {
    "Nodes": [
      "https://es-node1.production.com:9200",
      "https://es-node2.production.com:9200",
      "https://es-node3.production.com:9200"
    ],
    "ApiKey": "${ELASTIC_API_KEY}",
    "IndexFormat": "logs-production-{0:yyyy.MM.dd}",
    "NumberOfShards": 3,
    "NumberOfReplicas": 2,
    "MinimumLogLevel": "Information",
    "EnableCompression": true
  }
}
```

## Troubleshooting

### Logs not appearing in Elasticsearch
1. Check Elasticsearch is running: `curl http://localhost:9200/_cluster/health`
2. Check health endpoint: `https://your-api/health`
3. Check dead letter queue: `./logs/elastic-dlq/`
4. Review Serilog self-log output

### Performance Issues
- Increase `BufferSize` (default: 50)
- Enable compression
- Add more Elasticsearch nodes
- Tune index shards/replicas

## Next Steps

1. ? Review the full plan: `elastic-integration-readme.md`
2. ? Configure Index Lifecycle Management (ILM)
3. ? Set up Kibana dashboards
4. ? Configure alerts for errors
5. ? Implement log sampling for high-volume services

## Reference

- Full Documentation: `Custom.Framework/Elastic/elastic-integration-readme.md`
- Configuration Example: `Custom.Framework/Elastic/appsettings.elasticsearch.json`
- Tests: `Custom.Framework.Tests/Elastic/`
