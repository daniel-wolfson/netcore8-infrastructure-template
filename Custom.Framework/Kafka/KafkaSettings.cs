namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Common settings used by both Kafka producers and consumers.
    /// Only truly shared configuration belongs here.
    /// </summary>
    public class KafkaSettings
    {
        /// <summary>
        /// Gets or sets the Kafka bootstrap servers list in the format "host1:port1,host2:port2".
        /// This is the initial connection point for the Kafka cluster.
        /// </summary>
        public string BootstrapServers { get; set; } = "localhost:9092";

        /// <summary>
        /// Gets or sets an identifier for the client application. Used for logging and metrics.
        /// </summary>
        public string ClientId { get; set; } = "isrotel-client";

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

        /// <summary>
        /// SASL username for authentication (optional).
        /// </summary>
        public string? SaslUsername { get; set; }

        /// <summary>
        /// SASL password for authentication (optional).
        /// </summary>
        public string? SaslPassword { get; set; }

        /// <summary>
        /// SASL mechanism (e.g. Plain, ScramSha256) (optional).
        /// </summary>
        public string? SaslMechanism { get; set; }

        /// <summary>
        /// Security protocol (e.g. Plaintext, Ssl, SaslSsl) (optional).
        /// </summary>
        public string? SecurityProtocol { get; set; }
    }
}