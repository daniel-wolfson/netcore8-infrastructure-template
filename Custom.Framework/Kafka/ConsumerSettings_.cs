using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// not used, potentially fro delete
    /// </summary>
    public class ConsumerSettings_ : IConsumerSettings
    {
        // Custom properties

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

        // Properties from ClientConfig base class

        /// <summary>
        /// Initial list of brokers as a CSV list of broker host or host:port.
        /// Example: "localhost:9092" or "broker1:9092,broker2:9092,broker3:9092"
        /// </summary>
        public string? BootstrapServers { get; set; }

        /// <summary>
        /// Client identifier. Used for logging and metrics.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Protocol used to communicate with brokers.
        /// Default: Plaintext
        /// </summary>
        public SecurityProtocol? SecurityProtocol { get; set; } = Confluent.Kafka.SecurityProtocol.Plaintext;

        /// <summary>
        /// Period of time in milliseconds after which we force a refresh of metadata even if we haven't seen any partition leadership changes.
        /// Default: 300000 (5 minutes)
        /// </summary>
        public int? MetadataMaxAgeMs { get; set; } = 300000;

        /// <summary>
        /// Default timeout for network requests. Producer: ProduceRequests will use the lesser value of socket.timeout.ms and remaining message.timeout.ms.
        /// Consumer: FetchRequests will use fetch.max.wait.ms + socket.timeout.ms.
        /// Default: 60000 (1 minute)
        /// </summary>
        public int? SocketTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Enable TCP keep-alives (SO_KEEPALIVE) on broker sockets.
        /// Default: false
        /// </summary>
        public bool? SocketKeepaliveEnable { get; set; } = false;

        /// <summary>
        /// Close idle connections after the number of milliseconds specified by this config.
        /// Default: 0 (disabled)
        /// </summary>
        public int? ConnectionsMaxIdleMs { get; set; } = 0;

        /// <summary>
        /// The initial time to wait before reconnecting to a broker after the connection has been closed. The time is increased exponentially until reconnect.backoff.max.ms is reached.
        /// Default: 50ms
        /// </summary>
        public int? ReconnectBackoffMs { get; set; } = 50;

        /// <summary>
        /// The maximum time to wait when reconnecting to a broker that has repeatedly failed to connect.
        /// Default: 10000ms (10 seconds)
        /// </summary>
        public int? ReconnectBackoffMaxMs { get; set; } = 10000;

        /// <summary>
        /// SASL mechanism to use for authentication.
        /// Supported: Gssapi, Plain, ScramSha256, ScramSha512, OAuthBearer
        /// </summary>
        public SaslMechanism? SaslMechanism { get; set; }

        /// <summary>
        /// SASL username for use with the PLAIN and SASL-SCRAM mechanisms.
        /// </summary>
        public string? SaslUsername { get; set; }

        /// <summary>
        /// SASL password for use with the PLAIN and SASL-SCRAM mechanisms.
        /// </summary>
        public string? SaslPassword { get; set; }

        /// <summary>
        /// File or directory path to CA certificate(s) for verifying the broker's key.
        /// </summary>
        public string? SslCaLocation { get; set; }

        /// <summary>
        /// Path to client's public key (PEM) used for authentication.
        /// </summary>
        public string? SslCertificateLocation { get; set; }

        /// <summary>
        /// Path to client's private key (PEM) used for authentication.
        /// </summary>
        public string? SslKeyLocation { get; set; }

        /// <summary>
        /// Private key passphrase (for use with ssl.key.location).
        /// </summary>
        public string? SslKeyPassword { get; set; }

        /// <summary>
        /// Enable OpenSSL's builtin broker (server) certificate verification.
        /// Default: true
        /// </summary>
        public bool? EnableSslCertificateVerification { get; set; } = true;

        /// <summary>
        /// Broker socket send buffer size. SO_SNDBUF socket option.
        /// Default: 0 (system default)
        /// </summary>
        public int? SocketSendBufferBytes { get; set; } = 0;

        /// <summary>
        /// Broker socket receive buffer size. SO_RCVBUF socket option.
        /// Default: 0 (system default)
        /// </summary>
        public int? SocketReceiveBufferBytes { get; set; } = 0;

        /// <summary>
        /// Log broker disconnections. It might be useful to turn this off when interacting with 0.9 brokers with an aggressive connection.max.idle.ms value.
        /// Default: true
        /// </summary>
        public bool? LogConnectionClose { get; set; } = true;

        // Properties from ConsumerConfig

        /// <summary>
        /// Client group id string. All clients sharing the same group.id belong to the same group.
        /// Required for using Kafka's consumer group functionality.
        /// </summary>
        public string? GroupId { get; set; }

        /// <summary>
        /// Automatically and periodically commit offsets in the background.
        /// Note: setting this to false does not prevent the consumer from fetching previously committed start offsets.
        /// Default: true
        /// </summary>
        public bool? EnableAutoCommit { get; set; } = true;

        /// <summary>
        /// The frequency in milliseconds that the consumer offsets are auto-committed to Kafka if enable.auto.commit is set to true.
        /// Default: 5000 (5 seconds)
        /// </summary>
        public int? AutoCommitIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Action to take when there is no initial offset in offset store or the desired offset is out of range.
        /// - Latest: automatically reset the offset to the latest offset
        /// - Earliest: automatically reset the offset to the earliest offset
        /// - Error: trigger an error which is retrieved by consuming messages
        /// Default: Latest
        /// </summary>
        public AutoOffsetReset? AutoOffsetReset { get; set; } = Confluent.Kafka.AutoOffsetReset.Latest;

        /// <summary>
        /// Client group session and failure detection timeout. The consumer sends periodic heartbeats (heartbeat.interval.ms) to indicate its liveness to the broker.
        /// If no heartbeats are received by the broker before the expiration of this session timeout, the broker will remove this consumer from the group and initiate a rebalance.
        /// Default: 10000 (10 seconds)
        /// </summary>
        public int? SessionTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// The expected time in milliseconds between heartbeats to the consumer coordinator when using Kafka's group management feature.
        /// Heartbeats are used to ensure that the consumer's session stays active. This value must be lower than session.timeout.ms.
        /// Typically set to 1/3 of session.timeout.ms.
        /// Default: 3000 (3 seconds)
        /// </summary>
        public int? HeartbeatIntervalMs { get; set; } = 3000;

        /// <summary>
        /// The maximum delay in milliseconds between invocations of consume() when using consumer group management.
        /// This places an upper bound on the amount of time that the consumer can be idle before fetching more records.
        /// If consume() is not called before expiration of this timeout, the consumer is considered failed and will be removed from the group.
        /// Default: 300000 (5 minutes)
        /// </summary>
        public int? MaxPollIntervalMs { get; set; } = 300000;

        /// <summary>
        /// Automatically store offset of last message provided to application.
        /// The offset store is updated when the message is passed to the application.
        /// Default: true
        /// </summary>
        public bool? EnableAutoOffsetStore { get; set; } = true;

        /// <summary>
        /// Minimum number of bytes the broker responds with. If fetch.wait.max.ms expires the accumulated data will be sent to the client regardless of this setting.
        /// Setting this to a large value can improve throughput by batching messages but increases latency.
        /// Default: 1
        /// </summary>
        public int? FetchMinBytes { get; set; } = 1;

        /// <summary>
        /// Maximum amount of data the broker shall return for a Fetch request.
        /// Messages are fetched in batches by the consumer and if the first message batch in the first non-empty partition of the fetch is larger than this value,
        /// the message batch will still be returned to ensure the consumer can make progress.
        /// Default: 52428800 (50 MB)
        /// </summary>
        public int? FetchMaxBytes { get; set; } = 52428800;

        /// <summary>
        /// Maximum amount of data per-partition the server will return. Records are fetched in batches.
        /// If the first record batch in the first non-empty partition of the fetch is larger than this value,
        /// the batch will still be returned to ensure the consumer can make progress.
        /// Default: 1048576 (1 MB)
        /// </summary>
        public int? MaxPartitionFetchBytes { get; set; } = 1048576;

        /// <summary>
        /// Maximum time the broker may wait to fill the response with fetch.min.bytes.
        /// This helps control latency by ensuring the broker doesn't wait indefinitely for data.
        /// Default: 500ms
        /// </summary>
        public int? FetchWaitMaxMs { get; set; } = 500;

        /// <summary>
        /// Controls how to read messages written transactionally.
        /// - ReadUncommitted: consume all messages (default)
        /// - ReadCommitted: only consume non-transactional messages or committed transactional messages. In read_committed mode, the consumer will only return transactional messages which have been committed.
        /// Default: ReadUncommitted
        /// </summary>
        public IsolationLevel? IsolationLevel { get; set; } = Confluent.Kafka.IsolationLevel.ReadUncommitted;

        /// <summary>
        /// Verify CRC32 of consumed messages, ensuring no on-the-wire or on-disk corruption to the messages occurred.
        /// This check adds some overhead, so it may be disabled in cases seeking extreme performance.
        /// Default: false
        /// </summary>
        public bool? CheckCrcs { get; set; } = false;

        /// <summary>
        /// Enable static group membership. Static members can leave and rejoin a group within the configured session.timeout.ms without prompting a group rebalance.
        /// This should be used in combination with a larger session.timeout.ms to avoid group rebalances caused by transient unavailability (e.g. process restart, configuration update).
        /// If set, the consumer must use this ID consistently across restarts.
        /// </summary>
        public string? GroupInstanceId { get; set; }

        /// <summary>
        /// The name of one or more partition assignment strategies that the client will use to distribute partition ownership amongst consumer instances.
        /// - Range: Assigns partitions on a per-topic basis
        /// - RoundRobin: Assigns partitions to consumers in a round-robin fashion
        /// - Sticky: Minimizes partition movement when a rebalance occurs
        /// - CooperativeSticky: Similar to sticky but uses incremental rebalancing (avoids stop-the-world rebalances)
        /// Default: Range
        /// </summary>
        public PartitionAssignmentStrategy? PartitionAssignmentStrategy { get; set; } = Confluent.Kafka.PartitionAssignmentStrategy.Range;

        public Dictionary<string, string> ToDictionary()
        {
            var config = new Dictionary<string, string>();

            // ClientConfig properties
            if (!string.IsNullOrEmpty(BootstrapServers))
                config["bootstrap.servers"] = BootstrapServers;

            if (!string.IsNullOrEmpty(ClientId))
                config["client.id"] = ClientId;

            if (SecurityProtocol.HasValue)
                config["security.protocol"] = SecurityProtocol.Value.ToString().ToLowerInvariant();

            if (MetadataMaxAgeMs.HasValue)
                config["metadata.max.age.ms"] = MetadataMaxAgeMs.Value.ToString();

            if (SocketTimeoutMs.HasValue)
                config["socket.timeout.ms"] = SocketTimeoutMs.Value.ToString();

            if (SocketKeepaliveEnable.HasValue)
                config["socket.keepalive.enable"] = SocketKeepaliveEnable.Value.ToString().ToLowerInvariant();

            if (ConnectionsMaxIdleMs.HasValue)
                config["connections.max.idle.ms"] = ConnectionsMaxIdleMs.Value.ToString();

            if (ReconnectBackoffMs.HasValue)
                config["reconnect.backoff.ms"] = ReconnectBackoffMs.Value.ToString();

            if (ReconnectBackoffMaxMs.HasValue)
                config["reconnect.backoff.max.ms"] = ReconnectBackoffMaxMs.Value.ToString();

            if (SaslMechanism.HasValue)
                config["sasl.mechanism"] = SaslMechanism.Value.ToString().ToUpperInvariant().Replace("SCRAMSHA", "SCRAM-SHA-");

            if (!string.IsNullOrEmpty(SaslUsername))
                config["sasl.username"] = SaslUsername;

            if (!string.IsNullOrEmpty(SaslPassword))
                config["sasl.password"] = SaslPassword;

            if (!string.IsNullOrEmpty(SslCaLocation))
                config["ssl.ca.location"] = SslCaLocation;

            if (!string.IsNullOrEmpty(SslCertificateLocation))
                config["ssl.certificate.location"] = SslCertificateLocation;

            if (!string.IsNullOrEmpty(SslKeyLocation))
                config["ssl.key.location"] = SslKeyLocation;

            if (!string.IsNullOrEmpty(SslKeyPassword))
                config["ssl.key.password"] = SslKeyPassword;

            if (EnableSslCertificateVerification.HasValue)
                config["enable.ssl.certificate.verification"] = EnableSslCertificateVerification.Value.ToString().ToLowerInvariant();

            if (SocketSendBufferBytes.HasValue)
                config["socket.send.buffer.bytes"] = SocketSendBufferBytes.Value.ToString();

            if (SocketReceiveBufferBytes.HasValue)
                config["socket.receive.buffer.bytes"] = SocketReceiveBufferBytes.Value.ToString();

            if (LogConnectionClose.HasValue)
                config["log.connection.close"] = LogConnectionClose.Value.ToString().ToLowerInvariant();

            // ConsumerConfig properties
            if (!string.IsNullOrEmpty(GroupId))
                config["group.id"] = GroupId;

            if (EnableAutoCommit.HasValue)
                config["enable.auto.commit"] = EnableAutoCommit.Value.ToString().ToLowerInvariant();

            if (AutoCommitIntervalMs.HasValue)
                config["auto.commit.interval.ms"] = AutoCommitIntervalMs.Value.ToString();

            if (AutoOffsetReset.HasValue)
                config["auto.offset.reset"] = AutoOffsetReset.Value.ToString().ToLowerInvariant();

            if (SessionTimeoutMs.HasValue)
                config["session.timeout.ms"] = SessionTimeoutMs.Value.ToString();

            if (HeartbeatIntervalMs.HasValue)
                config["heartbeat.interval.ms"] = HeartbeatIntervalMs.Value.ToString();

            if (MaxPollIntervalMs.HasValue)
                config["max.poll.interval.ms"] = MaxPollIntervalMs.Value.ToString();

            if (EnableAutoOffsetStore.HasValue)
                config["enable.auto.offset.store"] = EnableAutoOffsetStore.Value.ToString().ToLowerInvariant();

            if (FetchMinBytes.HasValue)
                config["fetch.min.bytes"] = FetchMinBytes.Value.ToString();

            if (FetchMaxBytes.HasValue)
                config["fetch.max.bytes"] = FetchMaxBytes.Value.ToString();

            if (MaxPartitionFetchBytes.HasValue)
                config["max.partition.fetch.bytes"] = MaxPartitionFetchBytes.Value.ToString();

            if (FetchWaitMaxMs.HasValue)
                config["fetch.wait.max.ms"] = FetchWaitMaxMs.Value.ToString();

            if (IsolationLevel.HasValue)
                config["isolation.level"] = IsolationLevel.Value == Confluent.Kafka.IsolationLevel.ReadUncommitted
                    ? "read_uncommitted"
                    : "read_committed";

            if (CheckCrcs.HasValue)
                config["check.crcs"] = CheckCrcs.Value.ToString().ToLowerInvariant();

            if (!string.IsNullOrEmpty(GroupInstanceId))
                config["group.instance.id"] = GroupInstanceId;

            if (PartitionAssignmentStrategy.HasValue)
            {
                var strategy = PartitionAssignmentStrategy.Value switch
                {
                    Confluent.Kafka.PartitionAssignmentStrategy.Range => "range",
                    Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin => "roundrobin",
                    Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky => "cooperative-sticky",
                    _ => "range"
                };
                config["partition.assignment.strategy"] = strategy;
            }

            // Add custom consumer config entries (these can override above settings if needed)
            if (CustomConsumerConfig != null)
            {
                foreach (var kvp in CustomConsumerConfig)
                {
                    config[kvp.Key] = kvp.Value;
                }
            }

            return config;
        }
    }

    /// <summary>
    /// Security protocol for Kafka connections
    /// </summary>
    public enum SecurityProtocol_
    {
        /// <summary>Plaintext</summary>
        Plaintext,
        /// <summary>SSL/TLS</summary>
        Ssl,
        /// <summary>SASL over plaintext</summary>
        SaslPlaintext,
        /// <summary>SASL over SSL/TLS</summary>
        SaslSsl
    }

    /// <summary>
    /// Action to take when there is no initial offset in Kafka or if the current offset does not exist any more on the server
    /// </summary>
    public enum AutoOffsetReset_
    {
        /// <summary>Automatically reset the offset to the latest offset</summary>
        Latest,
        /// <summary>Automatically reset the offset to the earliest offset</summary>
        Earliest,
        /// <summary>Trigger an error which is retrieved by consuming messages and checking 'message.Error'</summary>
        Error
    }

    /// <summary>
    /// SASL mechanism to use for authentication
    /// </summary>
    public enum SaslMechanism_
    {
        /// <summary>GSSAPI (Kerberos)</summary>
        Gssapi,
        /// <summary>PLAIN</summary>
        Plain,
        /// <summary>SCRAM-SHA-256</summary>
        ScramSha256,
        /// <summary>SCRAM-SHA-512</summary>
        ScramSha512,
        /// <summary>OAUTHBEARER</summary>
        OAuthBearer
    }

    /// <summary>
    /// Controls how to read messages written transactionally
    /// </summary>
    public enum IsolationLevel_
    {
        /// <summary>Read uncommitted - consumer will read all messages</summary>
        ReadUncommitted,
        /// <summary>Read committed - consumer will only read non-transactional messages or committed transactional messages</summary>
        ReadCommitted
    }

    /// <summary>
    /// Partition assignment strategy
    /// </summary>
    public enum PartitionAssignmentStrategy_
    {
        /// <summary>Range assignor</summary>
        Range,
        /// <summary>Round-robin assignor</summary>
        RoundRobin,
        /// <summary>Sticky assignor</summary>
        Sticky,
        /// <summary>Cooperative sticky assignor</summary>
        CooperativeSticky
    }
}