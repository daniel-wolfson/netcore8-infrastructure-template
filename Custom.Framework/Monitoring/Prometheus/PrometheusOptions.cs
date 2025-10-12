namespace Custom.Framework.Monitoring.Prometheus;

/// <summary>
/// Configuration options for Prometheus monitoring
/// </summary>
public class PrometheusOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string ConfigSectionName = "Prometheus";

    /// <summary>
    /// Enable Prometheus metrics collection
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Metrics endpoint path
    /// Default: /metrics
    /// </summary>
    public string MetricsEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Application name used for metrics labeling
    /// </summary>
    public string ApplicationName { get; set; } = "custom-framework-app";

    /// <summary>
    /// Environment name (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Enable ASP.NET Core metrics (HTTP requests, response times, etc.)
    /// </summary>
    public bool EnableAspNetCoreMetrics { get; set; } = true;

    /// <summary>
    /// Enable process metrics (CPU, memory, threads)
    /// </summary>
    public bool EnableProcessMetrics { get; set; } = true;

    /// <summary>
    /// Enable runtime metrics (.NET GC, exceptions, etc.)
    /// </summary>
    public bool EnableRuntimeMetrics { get; set; } = true;

    /// <summary>
    /// Enable database metrics (EF Core queries, connections)
    /// </summary>
    public bool EnableDatabaseMetrics { get; set; } = true;

    /// <summary>
    /// Enable HTTP client metrics (outgoing HTTP calls)
    /// </summary>
    public bool EnableHttpClientMetrics { get; set; } = true;

    /// <summary>
    /// Custom labels to add to all metrics
    /// </summary>
    public Dictionary<string, string> CustomLabels { get; set; } = new();

    /// <summary>
    /// Histogram buckets for duration metrics (in seconds)
    /// Default: [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
    /// </summary>
    public double[] HistogramBuckets { get; set; } = 
    {
        0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
    };

    /// <summary>
    /// Pushgateway configuration (optional)
    /// Used for batch jobs and short-lived services
    /// </summary>
    public PushgatewayOptions? Pushgateway { get; set; }
}

/// <summary>
/// Configuration for Prometheus Pushgateway
/// </summary>
public class PushgatewayOptions
{
    /// <summary>
    /// Enable Pushgateway integration
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Pushgateway endpoint URL
    /// Example: http://pushgateway:9091
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Job name for Pushgateway
    /// </summary>
    public string JobName { get; set; } = "custom-framework-job";

    /// <summary>
    /// Push interval in seconds
    /// </summary>
    public int PushIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Basic authentication username (optional)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Basic authentication password (optional)
    /// </summary>
    public string? Password { get; set; }
}
