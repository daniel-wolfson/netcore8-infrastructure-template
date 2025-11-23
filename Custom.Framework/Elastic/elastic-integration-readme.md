# Elasticsearch Integration Plan for Custom.Framework

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Implementation Plan](#implementation-plan)
4. [Components](#components)
5. [Configuration](#configuration)
6. [Usage Examples](#usage-examples)
7. [Testing Strategy](#testing-strategy)
8. [Performance Optimization](#performance-optimization)
9. [Monitoring & Observability](#monitoring--observability)
10. [Migration Path](#migration-path)
11. [Best Practices](#best-practices)

---

## Overview

### Purpose
Integrate Elasticsearch into the Custom.Framework infrastructure to provide:
- **Centralized log aggregation** from all services
- **Structured logging** with rich querying capabilities
- **Real-time log search and analysis**
- **Log retention and archival** policies
- **Correlation with distributed tracing** (OpenTelemetry integration)
- **Alerting and monitoring** based on log patterns

### Goals
1. ? Seamless integration with existing Serilog logging infrastructure
2. ? Support for both synchronous and asynchronous log shipping
3. ? High-performance bulk indexing with batching
4. ? Integration with OpenTelemetry for trace-log correlation
5. ? Support for structured logging with custom fields
6. ? Docker-based testing infrastructure (ElasticTestContainer)
7. ? Health checks and metrics integration
8. ? Configuration via appsettings.json

### Technology Stack
- **Elasticsearch**: 8.x
- **Client Library**: NEST (Elasticsearch.Net)
- **Serilog Sink**: Serilog.Sinks.Elasticsearch
- **Testing**: Testcontainers
- **Monitoring**: OpenTelemetry metrics

---

## Architecture

### High-Level Architecture

```plaintext
???????????????????????????????????????????????????????????????
?                  Application Services                       ?
?  ???????????  ???????????  ???????????  ???????????       ?
?  ?Service A?  ?Service B?  ?Service C?  ?Service D?       ?
?  ???????????  ???????????  ???????????  ???????????       ?
?       ?            ?            ?            ?             ?
?       ????????????????????????????????????????             ?
?                        ?                                    ?
?              ????????????????????                          ?
?              ?  Serilog Logger  ?                          ?
?              ?   + Enrichers    ?                          ?
?              ????????????????????                          ?
?                        ?                                    ?
?              ????????????????????????                      ?
?              ? Elasticsearch Sink   ?                      ?
?              ?  (Bulk Processor)    ?                      ?
?              ????????????????????????                      ?
????????????????????????????????????????????????????????????
                         ?
                         ? HTTP/HTTPS
                         ? (Bulk API)
                         ?
              ????????????????????????
              ?   Elasticsearch      ?
              ?     Cluster          ?
              ?  ??????????????????  ?
              ?  ? Index Template ?  ?
              ?  ?  Lifecycle     ?  ?
              ?  ?  Management    ?  ?
              ?  ??????????????????  ?
              ?                      ?
              ?  Indices:            ?
              ?  - logs-{env}-YYYY.MM.DD ?
              ?  - traces-{env}-YYYY.MM.DD ?
              ????????????????????????
                         ?
                         ?
              ????????????????????????
              ?      Kibana          ?
              ?  (Visualization)     ?
              ????????????????????????
```

### Log Flow

```plaintext
Application Log Entry
        ?
        ???? Correlation ID (from OpenTelemetry)
        ???? Trace ID (ActivityTraceId)
        ???? Span ID (ActivitySpanId)
        ???? Service Name
        ???? Environment
        ???? Log Level
        ???? Timestamp
        ???? Message
        ???? Exception (if any)
        ???? Custom Properties
                ?
                ?
        Serilog Pipeline
                ?
                ???? Enrichers
                ?    ???? Environment
                ?    ???? Machine Name
                ?    ???? Thread ID
                ?    ???? Process ID
                ?    ???? OpenTelemetry Context
                ?
                ?
        Elasticsearch Sink
                ?
                ???? Buffering (Channel-based)
                ???? Batching (configurable size)
                ???? Bulk Indexing
                ???? Error Handling (retry + DLQ)
                ?
                ?
        Elasticsearch Index
                ?
                ???? Dynamic Mapping
                ???? Index Lifecycle Policy
                ???? Retention Rules
```

---

## Implementation Plan

### Phase 1: Core Infrastructure (Week 1)
**Priority: HIGH**

#### 1.1 NuGet Packages
```xml
<!-- Custom.Framework.csproj -->
<PackageReference Include="NEST" Version="7.17.5" />
<PackageReference Include="Elasticsearch.Net" Version="7.17.5" />
<PackageReference Include="Serilog.Sinks.Elasticsearch" Version="9.0.3" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="2.3.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Process" Version="2.0.2" />

<!-- Custom.Framework.Tests.csproj -->
<PackageReference Include="Testcontainers.Elasticsearch" Version="3.7.0" />
```

#### 1.2 Configuration Models
Create `ElasticOptions.cs`:
```csharp
namespace Custom.Framework.Elastic;

public class ElasticOptions
{
    /// <summary>
    /// Elasticsearch cluster URIs (comma-separated)
    /// Example: "http://localhost:9200,http://es-node2:9200"
    /// </summary>
    public string[] Nodes { get; set; } = new[] { "http://localhost:9200" };

    /// <summary>
    /// API key for authentication (recommended for production)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Basic auth username (alternative to API key)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Basic auth password
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Index name format. 
    /// Default: "logs-{environment}-{yyyy.MM.dd}"
    /// Variables: {environment}, {serviceName}, {yyyy.MM.dd}, {yyyy.MM}, {yyyy}
    /// </summary>
    public string IndexFormat { get; set; } = "logs-{environment}-{0:yyyy.MM.dd}";

    /// <summary>
    /// Enable automatic index creation
    /// </summary>
    public bool AutoRegisterTemplate { get; set; } = true;

    /// <summary>
    /// Number of shards per index
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas per index
    /// </summary>
    public int NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// Buffer size for batching logs before sending
    /// </summary>
    public int BufferSize { get; set; } = 50;

    /// <summary>
    /// Minimum log level to send to Elasticsearch
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Information";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeout { get; set; } = 30;

    /// <summary>
    /// Maximum retries for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Enable dead letter queue for failed logs
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Dead letter queue path (file system)
    /// </summary>
    public string? DeadLetterQueuePath { get; set; }

    /// <summary>
    /// Enable compression for HTTP requests
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Enable health check
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// Health check timeout
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Enable metrics collection
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Index lifecycle management settings
    /// </summary>
    public IndexLifecyclePolicy? LifecyclePolicy { get; set; }
}

public class IndexLifecyclePolicy
{
    /// <summary>
    /// Hot phase: Maximum index size before rollover
    /// </summary>
    public string? MaxSize { get; set; } = "50gb";

    /// <summary>
    /// Hot phase: Maximum index age before rollover
    /// </summary>
    public TimeSpan? MaxAge { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Warm phase: Move to warm after this duration
    /// </summary>
    public TimeSpan? WarmAfter { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Cold phase: Move to cold after this duration
    /// </summary>
    public TimeSpan? ColdAfter { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Delete phase: Delete index after this duration
    /// </summary>
    public TimeSpan? DeleteAfter { get; set; } = TimeSpan.FromDays(90);
}
```

#### 1.3 Elasticsearch Client Factory
Create `ElasticClientFactory.cs`:
```csharp
namespace Custom.Framework.Elastic;

public interface IElasticClientFactory
{
    IElasticClient CreateClient();
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

public class ElasticClientFactory : IElasticClientFactory
{
    private readonly ElasticOptions _options;
    private readonly ILogger<ElasticClientFactory> _logger;
    private readonly Lazy<IElasticClient> _client;

    public ElasticClientFactory(
        IOptions<ElasticOptions> options,
        ILogger<ElasticClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<IElasticClient>(CreateElasticClient);
    }

    public IElasticClient CreateClient() => _client.Value;

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Value.PingAsync(ct: cancellationToken);
            return response.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping Elasticsearch cluster");
            return false;
        }
    }

    private IElasticClient CreateElasticClient()
    {
        var nodes = _options.Nodes
            .Select(n => new Uri(n))
            .ToArray();

        var connectionPool = nodes.Length == 1
            ? new SingleNodeConnectionPool(nodes[0])
            : new StaticConnectionPool(nodes);

        var settings = new ConnectionSettings(connectionPool)
            .RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeout))
            .MaximumRetries(_options.MaxRetries)
            .EnableHttpCompression(_options.EnableCompression)
            .ThrowExceptions(false) // Handle errors gracefully
            .DisableDirectStreaming() // For debugging
            .OnRequestCompleted(details =>
            {
                if (!details.Success)
                {
                    _logger.LogWarning(
                        "Elasticsearch request failed: {Method} {Uri} - {DebugInformation}",
                        details.HttpMethod,
                        details.Uri,
                        details.DebugInformation);
                }
            });

        // Authentication
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            settings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(_options.ApiKey));
        }
        else if (!string.IsNullOrEmpty(_options.Username))
        {
            settings.BasicAuthentication(_options.Username, _options.Password);
        }

        return new ElasticClient(settings);
    }
}
```

### Phase 2: Serilog Integration (Week 1-2)
**Priority: HIGH**

#### 2.1 Serilog Elasticsearch Sink Configuration
Create `ElasticExtensions.cs`:
```csharp
namespace Custom.Framework.Elastic;

public static class ElasticExtensions
{
    public static IServiceCollection AddElasticsearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ElasticOptions>(
            configuration.GetSection("Elasticsearch"));

        services.AddSingleton<IElasticClientFactory, ElasticClientFactory>();

        // Register health check
        var options = configuration
            .GetSection("Elasticsearch")
            .Get<ElasticOptions>();

        if (options?.EnableHealthCheck == true)
        {
            services.AddHealthChecks()
                .AddCheck<ElasticsearchHealthCheck>("Elasticsearch");
        }

        // Register metrics
        if (options?.EnableMetrics == true)
        {
            services.AddSingleton<ElasticsearchMetrics>();
        }

        return services;
    }

    public static LoggerConfiguration AddElasticsearchSink(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("Elasticsearch")
            .Get<ElasticOptions>();

        if (options == null || options.Nodes.Length == 0)
        {
            Log.Warning("Elasticsearch configuration not found. Skipping Elasticsearch sink.");
            return loggerConfiguration;
        }

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var serviceName = configuration["ServiceName"] ?? 
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";

        var indexFormat = options.IndexFormat
            .Replace("{environment}", environment.ToLowerInvariant())
            .Replace("{serviceName}", serviceName.ToLowerInvariant());

        return loggerConfiguration.WriteTo.Elasticsearch(
            new ElasticsearchSinkOptions(options.Nodes.Select(n => new Uri(n)))
            {
                IndexFormat = indexFormat,
                AutoRegisterTemplate = options.AutoRegisterTemplate,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                NumberOfShards = options.NumberOfShards,
                NumberOfReplicas = options.NumberOfReplicas,
                BufferBaseFilename = options.DeadLetterQueuePath,
                BufferFileCountLimit = 31,
                BufferLogShippingInterval = TimeSpan.FromSeconds(5),
                MinimumLogEventLevel = Enum.Parse<LogEventLevel>(options.MinimumLogLevel),
                ModifyConnectionSettings = conn =>
                {
                    if (!string.IsNullOrEmpty(options.ApiKey))
                    {
                        conn.ApiKeyAuthentication(
                            new ApiKeyAuthenticationCredentials(options.ApiKey));
                    }
                    else if (!string.IsNullOrEmpty(options.Username))
                    {
                        conn.BasicAuthentication(options.Username, options.Password);
                    }

                    conn.RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeout))
                        .MaximumRetries(options.MaxRetries)
                        .EnableHttpCompression(options.EnableCompression);

                    return conn;
                },
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                   (options.EnableDeadLetterQueue
                                       ? EmitEventFailureHandling.WriteToFailureSink
                                       : EmitEventFailureHandling.ThrowException),
                FailureSink = options.EnableDeadLetterQueue
                    ? new FileSink(
                        Path.Combine(
                            options.DeadLetterQueuePath ?? "./logs/dlq",
                            "elastic-dlq-.txt"),
                        new JsonFormatter(),
                        null)
                    : null
            });
    }

    public static LoggerConfiguration EnrichWithElasticsearchContext(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var serviceName = configuration["ServiceName"] ?? 
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
        var version = configuration["Version"] ?? "1.0.0";

        return loggerConfiguration
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("ServiceVersion", version)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentUserName();
    }
}
```

#### 2.2 OpenTelemetry Correlation Enricher
Create `OpenTelemetryEnricher.cs`:
```csharp
namespace Custom.Framework.Elastic;

public class OpenTelemetryEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString()));

            // Add baggage
            foreach (var baggage in activity.Baggage)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty($"Baggage.{baggage.Key}", baggage.Value));
            }

            // Add tags
            foreach (var tag in activity.Tags)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty($"Tag.{tag.Key}", tag.Value));
            }
        }
    }
}
```

### Phase 3: Health Checks & Metrics (Week 2)
**Priority: MEDIUM**

#### 3.1 Health Check
Create `ElasticsearchHealthCheck.cs`:
```csharp
namespace Custom.Framework.Elastic;

public class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly IElasticClientFactory _clientFactory;
    private readonly ElasticOptions _options;

    public ElasticsearchHealthCheck(
        IElasticClientFactory clientFactory,
        IOptions<ElasticOptions> options)
    {
        _clientFactory = clientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.HealthCheckTimeout);

            var client = _clientFactory.CreateClient();
            var pingResponse = await client.PingAsync(ct: cts.Token);

            if (!pingResponse.IsValid)
            {
                return HealthCheckResult.Unhealthy(
                    "Elasticsearch ping failed",
                    pingResponse.OriginalException);
            }

            var clusterHealth = await client.Cluster.HealthAsync(ct: cts.Token);

            if (!clusterHealth.IsValid)
            {
                return HealthCheckResult.Degraded(
                    "Unable to retrieve cluster health",
                    clusterHealth.OriginalException);
            }

            var data = new Dictionary<string, object>
            {
                ["cluster_name"] = clusterHealth.ClusterName,
                ["status"] = clusterHealth.Status.ToString(),
                ["number_of_nodes"] = clusterHealth.NumberOfNodes,
                ["number_of_data_nodes"] = clusterHealth.NumberOfDataNodes,
                ["active_primary_shards"] = clusterHealth.ActivePrimaryShards,
                ["active_shards"] = clusterHealth.ActiveShards,
                ["relocating_shards"] = clusterHealth.RelocatingShards,
                ["initializing_shards"] = clusterHealth.InitializingShards,
                ["unassigned_shards"] = clusterHealth.UnassignedShards
            };

            return clusterHealth.Status switch
            {
                Health.Green => HealthCheckResult.Healthy("Elasticsearch cluster is healthy", data),
                Health.Yellow => HealthCheckResult.Degraded("Elasticsearch cluster is in yellow state", null, data),
                Health.Red => HealthCheckResult.Unhealthy("Elasticsearch cluster is in red state", null, data),
                _ => HealthCheckResult.Unhealthy("Unknown cluster state", null, data)
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check Elasticsearch health",
                ex);
        }
    }
}
```

#### 3.2 Metrics
Create `ElasticsearchMetrics.cs`:
```csharp
namespace Custom.Framework.Elastic;

public class ElasticsearchMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _documentsIndexed;
    private readonly Counter<long> _indexingErrors;
    private readonly Histogram<double> _indexingDuration;
    private readonly Counter<long> _searchRequests;
    private readonly Histogram<double> _searchDuration;

    public ElasticsearchMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Custom.Framework.Elasticsearch");

        _documentsIndexed = _meter.CreateCounter<long>(
            "elasticsearch.documents.indexed",
            unit: "documents",
            description: "Number of documents successfully indexed");

        _indexingErrors = _meter.CreateCounter<long>(
            "elasticsearch.indexing.errors",
            unit: "errors",
            description: "Number of indexing errors");

        _indexingDuration = _meter.CreateHistogram<double>(
            "elasticsearch.indexing.duration",
            unit: "ms",
            description: "Duration of indexing operations");

        _searchRequests = _meter.CreateCounter<long>(
            "elasticsearch.search.requests",
            unit: "requests",
            description: "Number of search requests");

        _searchDuration = _meter.CreateHistogram<double>(
            "elasticsearch.search.duration",
            unit: "ms",
            description: "Duration of search operations");
    }

    public void RecordDocumentsIndexed(long count, string index)
    {
        _documentsIndexed.Add(count, new KeyValuePair<string, object?>("index", index));
    }

    public void RecordIndexingError(string index, string errorType)
    {
        _indexingErrors.Add(1,
            new KeyValuePair<string, object?>("index", index),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void RecordIndexingDuration(double durationMs, string index)
    {
        _indexingDuration.Record(durationMs,
            new KeyValuePair<string, object?>("index", index));
    }

    public void RecordSearchRequest(string index)
    {
        _searchRequests.Add(1,
            new KeyValuePair<string, object?>("index", index));
    }

    public void RecordSearchDuration(double durationMs, string index)
    {
        _searchDuration.Record(durationMs,
            new KeyValuePair<string, object?>("index", index));
    }
}
```

### Phase 4: Testing Infrastructure (Week 2-3)
**Priority: MEDIUM**

#### 4.1 Elasticsearch Test Container
Create `Custom.Framework.Tests\Elastic\ElasticTestContainer.cs`:
```csharp
namespace Custom.Framework.Tests.Elastic;

public class ElasticTestContainer : IAsyncLifetime
{
    private readonly ITestOutputHelper? _output;
    private IContainer? _elasticContainer;
    private IContainer? _kibanaContainer;
    private INetwork? _network;
    private DockerClient? _dockerClient;

    public string ElasticUrl { get; private set; } = string.Empty;
    public string KibanaUrl { get; private set; } = string.Empty;
    public string Username { get; private set; } = "elastic";
    public string Password { get; private set; } = "changeme";

    public ElasticTestContainer(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _output?.WriteLine("?? Starting Elasticsearch test infrastructure...");

            await StartNetworkAsync();
            await StartElasticsearchAsync();
            await StartKibanaAsync();

            _output?.WriteLine("? Elasticsearch stack is ready!");
            _output?.WriteLine($"?? Elasticsearch: {ElasticUrl}");
            _output?.WriteLine($"?? Kibana: {KibanaUrl}");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"? Failed to start Elasticsearch: {ex.Message}");
            throw;
        }
    }

    private async Task StartNetworkAsync()
    {
        var networkName = "elastic-test-network";
        _dockerClient = new DockerClientConfiguration(
            new Uri("npipe://./pipe/docker_engine")).CreateClient();
        _dockerClient.DefaultTimeout = TimeSpan.FromSeconds(300);

        _network = new NetworkBuilder()
            .WithName(networkName)
            .Build();

        await _network.CreateAsync();
        _output?.WriteLine($"? Created network: {networkName}");
    }

    private async Task StartElasticsearchAsync()
    {
        _output?.WriteLine("? Starting Elasticsearch...");

        var elasticPort = 9200;
        ElasticUrl = $"http://localhost:{elasticPort}";

        _elasticContainer = new ContainerBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.12.0")
            .WithName("elasticsearch-test")
            .WithNetwork(_network)
            .WithNetworkAliases("elasticsearch")
            .WithPortBinding(elasticPort, 9200)
            .WithPortBinding(9300, 9300)
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("xpack.security.http.ssl.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(9200)
                    .ForPath("/_cluster/health")
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        await _elasticContainer.StartAsync();
        await Task.Delay(2000); // Additional wait for stability

        await VerifyElasticsearchHealthAsync();

        _output?.WriteLine($"? Elasticsearch ready at {ElasticUrl}");
    }

    private async Task StartKibanaAsync()
    {
        _output?.WriteLine("? Starting Kibana...");

        var kibanaPort = 5601;
        KibanaUrl = $"http://localhost:{kibanaPort}";

        _kibanaContainer = new ContainerBuilder()
            .WithImage("docker.elastic.co/kibana/kibana:8.12.0")
            .WithName("kibana-test")
            .WithNetwork(_network)
            .WithPortBinding(kibanaPort, 5601)
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(5601)
                    .ForPath("/api/status")
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        await _kibanaContainer.StartAsync();

        _output?.WriteLine($"? Kibana ready at {KibanaUrl}");
    }

    private async Task VerifyElasticsearchHealthAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{ElasticUrl}/_cluster/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        _output?.WriteLine($"? Cluster health: {content}");
    }

    public async Task DisposeAsync()
    {
        _output?.WriteLine("?? Stopping Elasticsearch test infrastructure...");

        if (_kibanaContainer != null)
            await _kibanaContainer.DisposeAsync();

        if (_elasticContainer != null)
            await _elasticContainer.DisposeAsync();

        if (_network != null)
            await _network.DeleteAsync();

        _dockerClient?.Dispose();

        _output?.WriteLine("? Elasticsearch stack stopped");
    }
}
```

#### 4.2 Integration Tests
Create `Custom.Framework.Tests\Elastic\ElasticsearchIntegrationTests.cs`:
```csharp
namespace Custom.Framework.Tests.Elastic;

public class ElasticsearchIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElasticTestContainer _container = default!;
    private IElasticClient _client = default!;

    public ElasticsearchIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
        Assert.Equal(Health.Green, healthResponse.Status);

        _output.WriteLine($"? Cluster: {healthResponse.ClusterName}");
        _output.WriteLine($"? Nodes: {healthResponse.NumberOfNodes}");
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

        _output.WriteLine($"? Document indexed: {indexResponse.Id}");
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
        }, idx => idx.Index("test-logs").Refresh(Refresh.WaitFor));

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

        _output.WriteLine($"? Found {searchResponse.Total} documents");
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Phase 5: Sample Application Integration (Week 3)
**Priority: LOW**

#### 5.1 Update `Program.cs` or Startup
```csharp
// Program.cs or ConfigureLogging extension
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithElasticsearchContext(context.Configuration)
        .Enrich.With<OpenTelemetryEnricher>()
        .AddElasticsearchSink(context.Configuration)
        .WriteTo.Console(); // Keep console for local development
});

// Add Elasticsearch services
builder.Services.AddElasticsearch(builder.Configuration);
```

---

## Configuration

### appsettings.json
```json
{
  "Elasticsearch": {
    "Nodes": [
      "http://localhost:9200"
    ],
    "Username": "elastic",
    "Password": "changeme",
    "IndexFormat": "logs-{environment}-{0:yyyy.MM.dd}",
    "AutoRegisterTemplate": true,
    "NumberOfShards": 1,
    "NumberOfReplicas": 1,
    "BufferSize": 50,
    "MinimumLogLevel": "Information",
    "RequestTimeout": 30,
    "MaxRetries": 3,
    "EnableDeadLetterQueue": true,
    "DeadLetterQueuePath": "./logs/elastic-dlq",
    "EnableCompression": true,
    "EnableHealthCheck": true,
    "HealthCheckTimeout": "00:00:05",
    "EnableMetrics": true,
    "LifecyclePolicy": {
      "MaxSize": "50gb",
      "MaxAge": "1.00:00:00",
      "WarmAfter": "7.00:00:00",
      "ColdAfter": "30.00:00:00",
      "DeleteAfter": "90.00:00:00"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### appsettings.Production.json
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
    "BufferSize": 100,
    "MinimumLogLevel": "Information",
    "EnableCompression": true
  }
}
```

---

## Usage Examples

### Basic Logging
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

        try
        {
            // Process order...
            
            _logger.LogInformation(
                "Order {OrderId} processed successfully",
                order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process order {OrderId}",
                order.Id);
            throw;
        }
    }
}
```

### Structured Logging with Custom Fields
```csharp
public class PaymentService
{
    private readonly ILogger<PaymentService> _logger;

    public async Task ProcessPayment(Payment payment)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["PaymentId"] = payment.Id,
            ["Amount"] = payment.Amount,
            ["Currency"] = payment.Currency,
            ["PaymentMethod"] = payment.Method
        }))
        {
            _logger.LogInformation("Processing payment");

            // Process payment...

            _logger.LogInformation(
                "Payment processed: {TransactionId}",
                payment.TransactionId);
        }
    }
}
```

### Query Logs in Kibana
```json
// Find all errors in the last hour
GET logs-production-*/_search
{
  "query": {
    "bool": {
      "must": [
        { "match": { "Level": "Error" }},
        { "range": { "@timestamp": { "gte": "now-1h" }}}
      ]
    }
  },
  "sort": [{ "@timestamp": "desc" }]
}

// Find logs for a specific trace
GET logs-production-*/_search
{
  "query": {
    "term": { "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736" }
  },
  "sort": [{ "@timestamp": "asc" }]
}

// Aggregate errors by service
GET logs-production-*/_search
{
  "size": 0,
  "query": {
    "match": { "Level": "Error" }
  },
  "aggs": {
    "errors_by_service": {
      "terms": { "field": "ServiceName.keyword" }
    }
  }
}
```

---

## Performance Optimization

### 1. Bulk Indexing
- **Batch Size**: 50-100 documents per batch (configurable via `BufferSize`)
- **Flush Interval**: 5 seconds (Serilog default)
- **Async Processing**: Non-blocking log shipping

### 2. Index Templates
```json
{
  "index_patterns": ["logs-*"],
  "template": {
    "settings": {
      "number_of_shards": 3,
      "number_of_replicas": 1,
      "refresh_interval": "5s"
    },
    "mappings": {
      "properties": {
        "@timestamp": { "type": "date" },
        "Level": { "type": "keyword" },
        "Message": { "type": "text" },
        "MessageTemplate": { "type": "keyword" },
        "TraceId": { "type": "keyword" },
        "SpanId": { "type": "keyword" },
        "ServiceName": { "type": "keyword" },
        "Environment": { "type": "keyword" }
      }
    }
  }
}
```

### 3. Index Lifecycle Management (ILM)
```json
{
  "policy": {
    "phases": {
      "hot": {
        "actions": {
          "rollover": {
            "max_size": "50gb",
            "max_age": "1d"
          }
        }
      },
      "warm": {
        "min_age": "7d",
        "actions": {
          "shrink": { "number_of_shards": 1 },
          "forcemerge": { "max_num_segments": 1 }
        }
      },
      "cold": {
        "min_age": "30d",
        "actions": {
          "freeze": {}
        }
      },
      "delete": {
        "min_age": "90d",
        "actions": {
          "delete": {}
        }
      }
    }
  }
}
```

---

## Monitoring & Observability

### Key Metrics to Monitor
1. **Indexing Rate**: Documents indexed per second
2. **Indexing Errors**: Failed document indexing attempts
3. **Search Latency**: Time to execute searches
4. **Cluster Health**: Red/Yellow/Green status
5. **Disk Usage**: Index size and growth rate
6. **Dead Letter Queue**: Failed logs count

### Alerts
- Cluster health turns Red or Yellow
- Indexing error rate > 1%
- Disk usage > 80%
- Search latency > 1s (p95)

---

## Migration Path

### From File-Based Logging
1. **Phase 1**: Add Elasticsearch sink alongside file logging
2. **Phase 2**: Validate log completeness in Elasticsearch
3. **Phase 3**: Reduce file logging verbosity
4. **Phase 4**: Disable file logging (keep for DLQ)

### From Azure Application Insights
1. **Phase 1**: Dual-write to both Elasticsearch and App Insights
2. **Phase 2**: Validate query parity
3. **Phase 3**: Migrate dashboards to Kibana
4. **Phase 4**: Disable App Insights logging

---

## Best Practices

### 1. Index Naming
- ? Use date-based indices: `logs-{env}-{yyyy.MM.dd}`
- ? Include environment in name: `logs-production-*`
- ? Avoid single large index

### 2. Structured Logging
- ? Use message templates: `"User {UserId} performed {Action}"`
- ? Add contextual properties via scopes
- ? Avoid string concatenation in logs

### 3. Security
- ? Use API keys (not basic auth) in production
- ? Enable TLS for Elasticsearch communication
- ? Rotate credentials regularly
- ? Never log sensitive data (PII, passwords, tokens)

### 4. Performance
- ? Use async logging
- ? Enable compression
- ? Set appropriate batch sizes
- ? Don't log in tight loops

### 5. Error Handling
- ? Enable dead letter queue
- ? Monitor DLQ size
- ? Set up alerts for persistent failures
- ? Don't let logging failures crash the application

---

## Deliverables

### Code Artifacts
1. ? `Custom.Framework/Elastic/ElasticOptions.cs`
2. ? `Custom.Framework/Elastic/ElasticClientFactory.cs`
3. ? `Custom.Framework/Elastic/ElasticExtensions.cs`
4. ? `Custom.Framework/Elastic/OpenTelemetryEnricher.cs`
5. ? `Custom.Framework/Elastic/ElasticsearchHealthCheck.cs`
6. ? `Custom.Framework/Elastic/ElasticsearchMetrics.cs`
7. ? `Custom.Framework.Tests/Elastic/ElasticTestContainer.cs`
8. ? `Custom.Framework.Tests/Elastic/ElasticsearchIntegrationTests.cs`

### Documentation
1. ? This README (elastic-integration-readme.md)
2. ? Configuration examples (appsettings.json)
3. ? Usage examples
4. ? Kibana dashboard templates
5. ? Runbook for troubleshooting

### Infrastructure
1. ? Docker Compose for local development
2. ? Testcontainers for integration tests
3. ? Index templates
4. ? ILM policies

---

## Timeline

| Week | Phase | Tasks | Status |
|------|-------|-------|--------|
| Week 1 | Phase 1 | Core infrastructure, NuGet packages, configuration models | ? Pending |
| Week 1-2 | Phase 2 | Serilog integration, OpenTelemetry enricher | ? Pending |
| Week 2 | Phase 3 | Health checks, metrics | ? Pending |
| Week 2-3 | Phase 4 | Testing infrastructure, integration tests | ? Pending |
| Week 3 | Phase 5 | Sample app integration, documentation | ? Pending |

---

## Success Criteria

- ? All logs from services are centralized in Elasticsearch
- ? Trace-to-log correlation works seamlessly
- ? Health checks report Elasticsearch status accurately
- ? Metrics are exposed for monitoring
- ? Integration tests pass consistently
- ? Documentation is complete and accurate
- ? Zero data loss (DLQ handles failures)
- ? Performance overhead < 5ms per log entry

---

## References

- [Elasticsearch .NET Client](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html)
- [Serilog.Sinks.Elasticsearch](https://github.com/serilog/serilog-sinks-elasticsearch)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Elasticsearch Index Lifecycle Management](https://www.elastic.co/guide/en/elasticsearch/reference/current/index-lifecycle-management.html)

---

**Version**: 1.0  
**Last Updated**: 2025-01-15  
**Author**: Custom.Framework Team  
**Status**: Planning Phase
