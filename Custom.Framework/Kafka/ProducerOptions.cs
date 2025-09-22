using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Settings specific to Kafka producers.
    /// </summary>
    public class ProducerOptions : ClientConfig
    {
        /// <summary>
        /// Gets or sets the maximum size of a message that can be produced, in bytes.
        /// Default is 1MB to handle large messages while preventing memory issues.
        /// </summary>
        public new int MessageMaxBytes { get; set; } = 1 * 1024 * 1024; // 1MB

        /// <summary>
        /// Gets or sets the producer batch size in bytes. Messages are batched together for efficiency.
        /// Default is 32KB to balance between latency and throughput.
        /// </summary>
        public int BatchSize { get; set; } = 32 * 1024;  // 32KB

        /// <summary>
        /// Gets or sets the time the producer waits to batch messages before sending, in milliseconds.
        /// Lower values reduce latency but may decrease throughput.
        /// </summary>
        public int LingerMs { get; set; } = 5;

        /// <summary>
        /// Gets or sets the compression level for messages (0-9).
        /// Higher values provide better compression but require more CPU.
        /// </summary>
        public int CompressionLevel { get; set; } = 5;

        /// <summary>
        /// Gets or sets the compression algorithm used for messages.
        /// GZIP provides good compression ratio for text-based messages.
        /// </summary>
        public string CompressionType { get; set; } = "gzip";

        // Producer-specific overrides (defaults inherited from KafkaOptions still apply)
        public DeliverySemantics DeliverySemantics { get; set; } = DeliverySemantics.AtLeastOnce;

        public bool? EnableIdempotence { get; set; } = true;

        public int? TransactionTimeoutMs { get; set; }

        // Producer-side duplicate detection support
        public bool EnableDuplicateDetection { get; set; } = false;

        public int RetryBackoffMs { get; set; }

        // Producer-specific config entries
        public IDictionary<string, string>? ProducerConfig { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for transient operations.
        /// Shared default for client-side retry behavior.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether metrics collection is enabled.
        /// Tracks performance metrics like throughput and latency.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets whether health checking is enabled for Kafka connectivity.
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// Gets or sets the interval between health checks.
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Optional per-check timeout used by health checks.
        /// </summary>
        public TimeSpan? HealthCheckTimeout { get; set; }

        public string SaslMechanism { get; set; }
        public string SecurityProtocol { get; set; }
    }
}