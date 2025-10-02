using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Settings specific to Kafka consumers.
    /// </summary>
    public class ConsumerSettings : ConsumerConfig
    {
        public required string Name { get; set; }
        public required string[] Topics { get; set; } = [];

        // Additional consumer-specific config entries
        public IDictionary<string, string>? CustomConsumerConfig { get; set; }

        // Delivery semantics (kept here so factory selection remains simple)
        public DeliverySemantics DeliverySemantics { get; set; } = DeliverySemantics.AtLeastOnce;

        /// <summary>
        /// Gets or sets the suffix for dead letter queue topic names.
        /// </summary>
        public string DeadLetterQueueTopicSuffix { get; set; } = "-dlq";

        public bool EnableMetrics { get; set; }

        public bool EnableHeBalthCheck { get; set; }

        /// <summary>
        /// Gets or sets whether dead letter queue is enabled for permanently failed messages.
        /// </summary>
        public bool EnableDeadLetterQueue { get; set; } = true;

        public TimeSpan? HealthCheckTimeout { get; internal set; }

        public int MaxRetries { get; set; }

        /// <summary>
        /// Retry/backoff (default inherited from KafkaOptions, keep consumer override if needed).
        /// Gets or sets the delay between retry attempts.
        /// Shared backoff used by producer/consumer logic where applicable.
        /// </summary>
        public TimeSpan RetryBackoffMs { get; set; } = TimeSpan.FromMilliseconds(100);

        //public string GroupId { get; set; } = "default-group";
        //public int MaxFetchBytes { get; set; } = 5 * 1024 * 1024; // 5MB
        //public int MaxPartitionFetchBytes { get; set; } = 2 * 1024 * 1024; // 2MB
        //public int FetchErrorBackoffMs { get; set; } //default: 500
        //public int? AutoCommitIntervalMs { get; set; } = 5000; //Auto-commit(used by AtMostOnce strategy)
    }
}