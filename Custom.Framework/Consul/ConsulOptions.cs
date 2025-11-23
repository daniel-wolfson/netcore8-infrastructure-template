namespace Custom.Framework.Consul;

/// <summary>
/// Configuration options for Consul service registration
/// </summary>
public class ConsulOptions
{
    /// <summary>
    /// Name of the service (multiple instances can share the same name)
    /// </summary>
    public string ServiceName { get; set; } = "my-service";

    /// <summary>
    /// Unique identifier for this service instance
    /// </summary>
    public string ServiceId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Address where this service can be reached
    /// </summary>
    public string ServiceAddress { get; set; } = "localhost";

    /// <summary>
    /// Port where this service is listening
    /// </summary>
    public int ServicePort { get; set; } = 5000;

    /// <summary>
    /// Tags for service discovery and filtering
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Metadata key-value pairs
    /// </summary>
    public Dictionary<string, string> Meta { get; set; } = new();

    /// <summary>
    /// Consul server address
    /// </summary>
    public string ConsulAddress { get; set; } = "http://localhost:8500";

    /// <summary>
    /// Consul datacenter name
    /// </summary>
    public string? Datacenter { get; set; }

    /// <summary>
    /// Path to health check endpoint (relative to service address)
    /// </summary>
    public string HealthCheckPath { get; set; } = "/health";

    /// <summary>
    /// How often Consul should check service health
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum time to wait for health check response
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long after a critical health check the service should be deregistered
    /// </summary>
    public TimeSpan DeregisterCriticalServiceAfter { get; set; } = TimeSpan.FromMinutes(1);
}
