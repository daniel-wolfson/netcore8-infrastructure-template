using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for Kafka consumer configuration settings combining custom properties with Confluent.Kafka settings
    /// </summary>
    public interface IConsumerSettings
    {
        // Custom properties

        /// <summary>
        /// Consumer name used for identification and logging.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Topics to subscribe to.
        /// </summary>
        string[] Topics { get; set; }

        /// <summary>
        /// Maximum capacity of the internal channel for buffering consumed messages before processing.
        /// Default: 1000
        /// </summary>
        int ChannelCapacity { get; set; }

        /// <summary>
        /// Additional custom consumer configuration entries not covered by standard properties.
        /// </summary>
        IDictionary<string, string>? CustomConsumerConfig { get; set; }

        /// <summary>
        /// Suffix appended to topic names for dead letter queue topics.
        /// Default: "-dlq"
        /// </summary>
        string DeadLetterQueueTopicSuffix { get; set; }

        /// <summary>
        /// Delivery semantics strategy (AtMostOnce, AtLeastOnce, ExactlyOnce, DeadLetter).
        /// Default: AtLeastOnce
        /// </summary>
        DeliverySemantics DeliverySemantics { get; set; }

        /// <summary>
        /// Enable sending permanently failed messages to a dead letter queue.
        /// Default: true
        /// </summary>
        bool EnableDeadLetterQueue { get; set; }

        /// <summary>
        /// Enable health check endpoint for this consumer.
        /// </summary>
        bool EnableHeBalthCheck { get; set; }

        /// <summary>
        /// Enable metrics collection for this consumer.
        /// </summary>
        bool EnableMetrics { get; set; }

        /// <summary>
        /// Timeout for health check operations.
        /// </summary>
        TimeSpan? HealthCheckTimeout { get; }

        /// <summary>
        /// Maximum number of retry attempts for message processing before sending to dead letter queue.
        /// </summary>
        int MaxRetries { get; set; }

        /// <summary>
        /// Base delay between retry attempts. Exponential backoff is applied.
        /// Default: 100ms
        /// </summary>
        TimeSpan RetryBackoffMs { get; set; }

        // Properties from ClientConfig base class

        /// <summary>
        /// Initial list of brokers as a CSV list of broker host or host:port.
        /// Example: "localhost:9092" or "broker1:9092,broker2:9092,broker3:9092"
        /// </summary>
        string? BootstrapServers { get; set; }

        /// <summary>
        /// Client identifier. Used for logging and metrics.
        /// </summary>
        string? ClientId { get; set; }

        /// <summary>
        /// Protocol used to communicate with brokers.
        /// Default: Plaintext
        /// </summary>
        SecurityProtocol? SecurityProtocol { get; set; }

        /// <summary>
        /// Period of time in milliseconds after which we force a refresh of metadata even if we haven't seen any partition leadership changes.
        /// Default: 300000 (5 minutes)
        /// </summary>
        int? MetadataMaxAgeMs { get; set; }

        /// <summary>
        /// Default timeout for network requests. Producer: ProduceRequests will use the lesser value of socket.timeout.ms and remaining message.timeout.ms.
        /// Consumer: FetchRequests will use fetch.max.wait.ms + socket.timeout.ms.
        /// Default: 60000 (1 minute)
        /// </summary>
        int? SocketTimeoutMs { get; set; }

        /// <summary>
        /// Enable TCP keep-alives (SO_KEEPALIVE) on broker sockets.
        /// Default: false
        /// </summary>
        bool? SocketKeepaliveEnable { get; set; }

        /// <summary>
        /// Close idle connections after the number of milliseconds specified by this config.
        /// Default: 0 (disabled)
        /// </summary>
        int? ConnectionsMaxIdleMs { get; set; }

        /// <summary>
        /// The initial time to wait before reconnecting to a broker after the connection has been closed. The time is increased exponentially until reconnect.backoff.max.ms is reached.
        /// Default: 50ms
        /// </summary>
        int? ReconnectBackoffMs { get; set; }

        /// <summary>
        /// The maximum time to wait when reconnecting to a broker that has repeatedly failed to connect.
        /// Default: 10000ms (10 seconds)
        /// </summary>
        int? ReconnectBackoffMaxMs { get; set; }

        /// <summary>
        /// SASL mechanism to use for authentication.
        /// Supported: Gssapi, Plain, ScramSha256, ScramSha512, OAuthBearer
        /// </summary>
        SaslMechanism? SaslMechanism { get; set; }

        /// <summary>
        /// SASL username for use with the PLAIN and SASL-SCRAM mechanisms.
        /// </summary>
        string? SaslUsername { get; set; }

        /// <summary>
        /// SASL password for use with the PLAIN and SASL-SCRAM mechanisms.
        /// </summary>
        string? SaslPassword { get; set; }

        /// <summary>
        /// File or directory path to CA certificate(s) for verifying the broker's key.
        /// </summary>
        string? SslCaLocation { get; set; }

        /// <summary>
        /// Path to client's public key (PEM) used for authentication.
        /// </summary>
        string? SslCertificateLocation { get; set; }

        /// <summary>
        /// Path to client's private key (PEM) used for authentication.
        /// </summary>
        string? SslKeyLocation { get; set; }

        /// <summary>
        /// Private key passphrase (for use with ssl.key.location).
        /// </summary>
        string? SslKeyPassword { get; set; }

        /// <summary>
        /// Enable OpenSSL's builtin broker (server) certificate verification.
        /// Default: true
        /// </summary>
        bool? EnableSslCertificateVerification { get; set; }

        /// <summary>
        /// Broker socket send buffer size. SO_SNDBUF socket option.
        /// Default: 0 (system default)
        /// </summary>
        int? SocketSendBufferBytes { get; set; }

        /// <summary>
        /// Broker socket receive buffer size. SO_RCVBUF socket option.
        /// Default: 0 (system default)
        /// </summary>
        int? SocketReceiveBufferBytes { get; set; }

        /// <summary>
        /// Log broker disconnections. It might be useful to turn this off when interacting with 0.9 brokers with an aggressive connection.max.idle.ms value.
        /// Default: true
        /// </summary>
        bool? LogConnectionClose { get; set; }

        // Properties from ConsumerConfig

        /// <summary>
        /// Client group id string. All clients sharing the same group.id belong to the same group.
        /// Required for using Kafka's consumer group functionality.
        /// </summary>
        string? GroupId { get; set; }

        /// <summary>
        /// Automatically and periodically commit offsets in the background.
        /// Note: setting this to false does not prevent the consumer from fetching previously committed start offsets.
        /// Default: true
        /// </summary>
        bool? EnableAutoCommit { get; set; }

        /// <summary>
        /// The frequency in milliseconds that the consumer offsets are auto-committed to Kafka if enable.auto.commit is set to true.
        /// Default: 5000 (5 seconds)
        /// </summary>
        int? AutoCommitIntervalMs { get; set; }

        /// <summary>
        /// Action to take when there is no initial offset in offset store or the desired offset is out of range.
        /// - Latest: automatically reset the offset to the latest offset
        /// - Earliest: automatically reset the offset to the earliest offset
        /// - Error: trigger an error which is retrieved by consuming messages
        /// Default: Latest
        /// </summary>
        AutoOffsetReset? AutoOffsetReset { get; set; }

        /// <summary>
        /// Client group session and failure detection timeout. The consumer sends periodic heartbeats (heartbeat.interval.ms) to indicate its liveness to the broker.
        /// If no heartbeats are received by the broker before the expiration of this session timeout, the broker will remove this consumer from the group and initiate a rebalance.
        /// Default: 10000 (10 seconds)
        /// </summary>
        int? SessionTimeoutMs { get; set; }

        /// <summary>
        /// The expected time in milliseconds between heartbeats to the consumer coordinator when using Kafka's group management feature.
        /// Heartbeats are used to ensure that the consumer's session stays active. This value must be lower than session.timeout.ms.
        /// Typically set to 1/3 of session.timeout.ms.
        /// Default: 3000 (3 seconds)
        /// </summary>
        int? HeartbeatIntervalMs { get; set; }

        /// <summary>
        /// The maximum delay in milliseconds between invocations of consume() when using consumer group management.
        /// This places an upper bound on the amount of time that the consumer can be idle before fetching more records.
        /// If consume() is not called before expiration of this timeout, the consumer is considered failed and will be removed from the group.
        /// Default: 300000 (5 minutes)
        /// </summary>
        int? MaxPollIntervalMs { get; set; }

        /// <summary>
        /// Automatically store offset of last message provided to application.
        /// The offset store is updated when the message is passed to the application.
        /// Default: true
        /// </summary>
        bool? EnableAutoOffsetStore { get; set; }

        /// <summary>
        /// Minimum number of bytes the broker responds with. If fetch.wait.max.ms expires the accumulated data will be sent to the client regardless of this setting.
        /// Setting this to a large value can improve throughput by batching messages but increases latency.
        /// Default: 1
        /// </summary>
        int? FetchMinBytes { get; set; }

        /// <summary>
        /// Maximum amount of data the broker shall return for a Fetch request.
        /// Messages are fetched in batches by the consumer and if the first message batch in the first non-empty partition of the fetch is larger than this value,
        /// the message batch will still be returned to ensure the consumer can make progress.
        /// Default: 52428800 (50 MB)
        /// </summary>
        int? FetchMaxBytes { get; set; }

        /// <summary>
        /// Maximum amount of data per-partition the server will return. Records are fetched in batches.
        /// If the first record batch in the first non-empty partition of the fetch is larger than this value,
        /// the batch will still be returned to ensure the consumer can make progress.
        /// Default: 1048576 (1 MB)
        /// </summary>
        int? MaxPartitionFetchBytes { get; set; }

        /// <summary>
        /// Maximum time the broker may wait to fill the response with fetch.min.bytes.
        /// This helps control latency by ensuring the broker doesn't wait indefinitely for data.
        /// Default: 500ms
        /// </summary>
        int? FetchWaitMaxMs { get; set; }

        /// <summary>
        /// Controls how to read messages written transactionally.
        /// - ReadUncommitted: consume all messages (default)
        /// - ReadCommitted: only consume non-transactional messages or committed transactional messages. In read_committed mode, the consumer will only return transactional messages which have been committed.
        /// Default: ReadUncommitted
        /// </summary>
        IsolationLevel? IsolationLevel { get; set; }

        /// <summary>
        /// Verify CRC32 of consumed messages, ensuring no on-the-wire or on-disk corruption to the messages occurred.
        /// This check adds some overhead, so it may be disabled in cases seeking extreme performance.
        /// Default: false
        /// </summary>
        bool? CheckCrcs { get; set; }

        /// <summary>
        /// Enable static group membership. Static members can leave and rejoin a group within the configured session.timeout.ms without prompting a group rebalance.
        /// This should be used in combination with a larger session.timeout.ms to avoid group rebalances caused by transient unavailability (e.g. process restart, configuration update).
        /// If set, the consumer must use this ID consistently across restarts.
        /// </summary>
        string? GroupInstanceId { get; set; }

        /// <summary>
        /// The name of one or more partition assignment strategies that the client will use to distribute partition ownership amongst consumer instances.
        /// - Range: Assigns partitions on a per-topic basis
        /// - RoundRobin: Assigns partitions to consumers in a round-robin fashion
        /// - Sticky: Minimizes partition movement when a rebalance occurs
        /// - CooperativeSticky: Similar to sticky but uses incremental rebalancing (avoids stop-the-world rebalances)
        /// Default: Range
        /// </summary>
        PartitionAssignmentStrategy? PartitionAssignmentStrategy { get; set; }
    }

    /// <summary>
    /// Settings specific to Kafka consumers with custom framework enhancements.
    /// </summary>
    public class ConsumerSettings : ConsumerConfig, IConsumerSettings
    {
        /// <summary>
        /// Consumer name used for identification and logging.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Topics to subscribe to.
        /// </summary>
        public required string[] Topics { get; set; } = [];

        /// <summary>
        /// Maximum capacity of the internal channel for buffering consumed messages before processing.
        /// Default: 1000
        /// </summary>
        public int ChannelCapacity { get; set; } = 1000;

        /// <summary>
        /// Additional custom consumer configuration entries not covered by standard properties.
        /// </summary>
        public IDictionary<string, string>? CustomConsumerConfig { get; set; }

        /// <summary>
        /// Suffix appended to topic names for dead letter queue topics.
        /// Default: "-dlq"
        /// </summary>
        public string DeadLetterQueueTopicSuffix { get; set; } = "-dlq";

        /// <summary>
        /// Delivery semantics strategy (AtMostOnce, AtLeastOnce, ExactlyOnce, DeadLetter).
        /// Default: AtLeastOnce
        /// </summary>
        public DeliverySemantics DeliverySemantics { get; set; } = DeliverySemantics.AtLeastOnce;

        /// <summary>
        /// Enable sending permanently failed messages to a dead letter queue.
        /// Default: true
        /// </summary>
        public bool EnableDeadLetterQueue { get; set; } = true;

        /// <summary>
        /// Enable health check endpoint for this consumer.
        /// </summary>
        public bool EnableHeBalthCheck { get; set; }

        /// <summary>
        /// Enable metrics collection for this consumer.
        /// </summary>
        public bool EnableMetrics { get; set; }

        /// <summary>
        /// Timeout for health check operations.
        /// </summary>
        public TimeSpan? HealthCheckTimeout { get; internal set; }

        /// <summary>
        /// Maximum number of retry attempts for message processing before sending to dead letter queue.
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Base delay between retry attempts. Exponential backoff is applied.
        /// Default: 100ms
        /// </summary>
        public TimeSpan RetryBackoffMs { get; set; } = TimeSpan.FromMilliseconds(100);
        SecurityProtocol? IConsumerSettings.SecurityProtocol { get; set; }
        SaslMechanism? IConsumerSettings.SaslMechanism { get; set; }
        AutoOffsetReset? IConsumerSettings.AutoOffsetReset { get; set; }
        IsolationLevel? IConsumerSettings.IsolationLevel { get; set; }
        PartitionAssignmentStrategy? IConsumerSettings.PartitionAssignmentStrategy { get; set; }
    }
}