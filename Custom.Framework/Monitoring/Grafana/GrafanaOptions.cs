namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// Configuration options for Grafana integration
/// </summary>
public class GrafanaOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string ConfigSectionName = "Grafana";

    /// <summary>
    /// Enable Grafana integration
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Grafana server URL
    /// Example: http://grafana:3000 or https://grafana.example.com
    /// </summary>
    public string Url { get; set; } = "http://localhost:3000";

    /// <summary>
    /// Grafana API key for authentication
    /// Create this in Grafana: Configuration ? API Keys
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Basic authentication username (if not using API key)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Basic authentication password (if not using API key)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Organization ID (default: 1)
    /// </summary>
    public int OrganizationId { get; set; } = 1;

    /// <summary>
    /// Automatically provision dashboards on application startup
    /// </summary>
    public bool AutoProvisionDashboards { get; set; } = false;

    /// <summary>
    /// Automatically provision data sources on application startup
    /// </summary>
    public bool AutoProvisionDataSources { get; set; } = false;

    /// <summary>
    /// Dashboard provisioning options
    /// </summary>
    public DashboardProvisioningOptions Dashboards { get; set; } = new();

    /// <summary>
    /// Data source provisioning options
    /// </summary>
    public DataSourceProvisioningOptions DataSources { get; set; } = new();

    /// <summary>
    /// Annotation options for marking events on graphs
    /// </summary>
    public AnnotationOptions Annotations { get; set; } = new();

    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Retry failed requests
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}

/// <summary>
/// Dashboard provisioning configuration
/// </summary>
public class DashboardProvisioningOptions
{
    /// <summary>
    /// Provision Prometheus metrics dashboard
    /// </summary>
    public bool ProvisionPrometheus { get; set; } = true;

    /// <summary>
    /// Provision Kafka metrics dashboard
    /// </summary>
    public bool ProvisionKafka { get; set; } = false;

    /// <summary>
    /// Provision Aurora/Database metrics dashboard
    /// </summary>
    public bool ProvisionDatabase { get; set; } = false;

    /// <summary>
    /// Provision DynamoDB metrics dashboard
    /// </summary>
    public bool ProvisionDynamoDB { get; set; } = false;

    /// <summary>
    /// Dashboard folder name in Grafana
    /// </summary>
    public string FolderName { get; set; } = "Custom Framework";

    /// <summary>
    /// Overwrite existing dashboards
    /// </summary>
    public bool Overwrite { get; set; } = true;
}

/// <summary>
/// Data source provisioning configuration
/// </summary>
public class DataSourceProvisioningOptions
{
    /// <summary>
    /// Prometheus data source URL
    /// </summary>
    public string PrometheusUrl { get; set; } = "http://prometheus:9090";

    /// <summary>
    /// Prometheus data source name
    /// </summary>
    public string PrometheusName { get; set; } = "Prometheus";

    /// <summary>
    /// Set Prometheus as default data source
    /// </summary>
    public bool SetPrometheusAsDefault { get; set; } = true;

    /// <summary>
    /// Additional data source scrape interval (e.g., "15s")
    /// </summary>
    public string? ScrapeInterval { get; set; }
}

/// <summary>
/// Annotation configuration
/// </summary>
public class AnnotationOptions
{
    /// <summary>
    /// Enable automatic deployment annotations
    /// </summary>
    public bool EnableDeploymentAnnotations { get; set; } = true;

    /// <summary>
    /// Enable error/exception annotations
    /// </summary>
    public bool EnableErrorAnnotations { get; set; } = false;

    /// <summary>
    /// Default tags for annotations
    /// </summary>
    public List<string> DefaultTags { get; set; } = new() { "framework" };

    /// <summary>
    /// Annotation dashboard UID (leave null for all dashboards)
    /// </summary>
    public string? DashboardUid { get; set; }
}
