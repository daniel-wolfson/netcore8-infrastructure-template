namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// Configuration options for Azure Cosmos DB connection
/// </summary>
public class CosmosDbOptions
{
    /// <summary>
    /// Cosmos DB account endpoint URI
    /// </summary>
    public string AccountEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Cosmos DB account key
    /// </summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// </summary>
    public string DatabaseName { get; set; } = "HospitalityOrders";

    /// <summary>
    /// Container name for order data
    /// </summary>
    public string ContainerName { get; set; } = "Orders";

    /// <summary>
    /// Partition key path (default: /hotelCode)
    /// </summary>
    public string PartitionKeyPath { get; set; } = "/hotelCode";

    /// <summary>
    /// Default TTL in seconds for pending orders (default: 10 minutes = 600 seconds)
    /// </summary>
    public int DefaultTtlSeconds { get; set; } = 600;

    /// <summary>
    /// TTL in seconds for succeeded orders (default: 7 days = 604800 seconds)
    /// </summary>
    public int SucceededTtlSeconds { get; set; } = 604800;

    /// <summary>
    /// Maximum throughput (RU/s) for the container
    /// </summary>
    public int? MaxThroughput { get; set; } = 4000;

    /// <summary>
    /// Enable automatic throughput scaling
    /// </summary>
    public bool EnableAutoscale { get; set; } = true;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeout { get; set; } = 60;

    /// <summary>
    /// Maximum retry attempts on throttling
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum retry wait time in seconds
    /// </summary>
    public int MaxRetryWaitTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Use connection mode: Direct or Gateway (default: Direct for better performance)
    /// </summary>
    public string ConnectionMode { get; set; } = "Direct";

    /// <summary>
    /// Application name for telemetry
    /// </summary>
    public string ApplicationName { get; set; } = "HospitalityReservationSystem";

    /// <summary>
    /// Application region (preferred region for read/write operations)
    /// </summary>
    public string? ApplicationRegion { get; set; }

    /// <summary>
    /// Enable content response on write operations
    /// </summary>
    public bool EnableContentResponseOnWrite { get; set; } = false;

    /// <summary>
    /// Allow bulk execution for batch operations
    /// </summary>
    public bool AllowBulkExecution { get; set; } = true;

    /// <summary>
    /// Limit the number of concurrent operations
    /// </summary>
    public int? MaxConcurrentConnections { get; set; }

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Use emulator for local development
    /// </summary>
    public bool UseEmulator { get; set; } = false;

    /// <summary>
    /// Emulator endpoint (default: https://localhost:8081)
    /// </summary>
    public string EmulatorEndpoint { get; set; } = "https://localhost:8081";

    /// <summary>
    /// Emulator key (well-known emulator key)
    /// </summary>
    public string EmulatorKey { get; set; } = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>
    /// Build endpoint based on configuration
    /// </summary>
    public string GetEndpoint()
    {
        return UseEmulator ? EmulatorEndpoint : AccountEndpoint;
    }

    /// <summary>
    /// Build key based on configuration
    /// </summary>
    public string GetKey()
    {
        return UseEmulator ? EmulatorKey : AccountKey;
    }
}
