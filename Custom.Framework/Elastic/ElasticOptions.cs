namespace Custom.Framework.Elastic;

/// <summary>
/// Configuration options for Elasticsearch integration
/// </summary>
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

/// <summary>
/// Index lifecycle management policy configuration
/// </summary>
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
