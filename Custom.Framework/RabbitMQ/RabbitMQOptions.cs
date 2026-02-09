namespace Custom.Framework.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ
/// Optimized for hospitality industry high-throughput scenarios
/// </summary>
public class RabbitMQOptions
{
    /// <summary>
    /// RabbitMQ connection string (amqp://user:pass@host:port/vhost)
    /// </summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Host name
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Port (default: 5672)
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Username
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Virtual host
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Heartbeat interval in seconds
    /// </summary>
    public ushort Heartbeat { get; set; } = 60;

    /// <summary>
    /// Automatic recovery enabled
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Network recovery interval in seconds
    /// </summary>
    public int NetworkRecoveryInterval { get; set; } = 10;

    /// <summary>
    /// Number of channels per connection (for high throughput)
    /// </summary>
    public int ChannelsPerConnection { get; set; } = 10;

    /// <summary>
    /// Prefetch count for consumers (messages to fetch at once)
    /// High value for high throughput
    /// </summary>
    public ushort PrefetchCount { get; set; } = 100;

    /// <summary>
    /// Enable publisher confirms (reliability vs throughput trade-off)
    /// </summary>
    public bool PublisherConfirms { get; set; } = true;

    /// <summary>
    /// Message time-to-live in milliseconds
    /// </summary>
    public int? MessageTtl { get; set; } = 3600000; // 1 hour

    /// <summary>
    /// Maximum retry attempts for failed messages
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Dead letter exchange name
    /// </summary>
    public string DeadLetterExchange { get; set; } = "hospitality.dlx";

    /// <summary>
    /// Enable message persistence (durable messages)
    /// </summary>
    public bool MessagePersistence { get; set; } = true;

    /// <summary>
    /// Exchange configurations for hospitality domain
    /// </summary>
    public Dictionary<string, ExchangeConfig> Exchanges { get; set; } = new()
    {
        ["hospitality.reservations"] = new ExchangeConfig
        {
            Type = "topic",
            Durable = true,
            AutoDelete = false
        },
        ["hospitality.bookings"] = new ExchangeConfig
        {
            Type = "topic",
            Durable = true,
            AutoDelete = false
        },
        ["hospitality.payments"] = new ExchangeConfig
        {
            Type = "topic",
            Durable = true,
            AutoDelete = false
        },
        ["hospitality.notifications"] = new ExchangeConfig
        {
            Type = "fanout",
            Durable = true,
            AutoDelete = false
        }
    };

    /// <summary>
    /// Queue configurations for hospitality domain
    /// </summary>
    public Dictionary<string, QueueConfig> Queues { get; set; } = new()
    {
        ["reservations.created"] = new QueueConfig
        {
            Durable = true,
            Exclusive = false,
            AutoDelete = false,
            MaxLength = 100000,
            MaxLengthBytes = 104857600 // 100MB
        },
        ["reservations.updated"] = new QueueConfig
        {
            Durable = true,
            Exclusive = false,
            AutoDelete = false
        },
        ["bookings.confirmed"] = new QueueConfig
        {
            Durable = true,
            Exclusive = false,
            AutoDelete = false
        },
        ["payments.processed"] = new QueueConfig
        {
            Durable = true,
            Exclusive = false,
            AutoDelete = false
        }
    };

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Application name for connection identification
    /// </summary>
    public string ApplicationName { get; set; } = "Hospitality.Service";
}

/// <summary>
/// Exchange configuration
/// </summary>
public class ExchangeConfig
{
    public string Type { get; set; } = "topic"; // topic, direct, fanout, headers
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public Dictionary<string, object> Arguments { get; set; } = new();
}

/// <summary>
/// Queue configuration
/// </summary>
public class QueueConfig
{
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
    public int? MaxLength { get; set; }
    public long? MaxLengthBytes { get; set; }
    public int? MessageTtl { get; set; }
    public Dictionary<string, object> Arguments { get; set; } = new();
}
