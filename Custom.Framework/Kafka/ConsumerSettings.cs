namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Settings specific to Kafka consumers.
    /// </summary>
    public class ConsumerSettings : KafkaSettings
    {
        /// <summary>
        /// Gets or sets the consumer group identifier. Used for consumer group coordination and offset management.
        /// </summary>
        public string GroupId { get; set; } = "default-group";

        // Consumer fetch settings actually used by KafkaConsumer
        public int MaxFetchBytes { get; set; } = 5 * 1024 * 1024; // 5MB
        public int MaxPartitionFetchBytes { get; set; } = 2 * 1024 * 1024; // 2MB

        // Auto-commit (used by AtMostOnce strategy)
        public int? AutoCommitIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Retry/backoff (default inherited from KafkaSettings, keep consumer override if needed).
        /// Gets or sets the delay between retry attempts.
        /// Shared backoff used by producer/consumer logic where applicable.
        /// </summary>
        public TimeSpan RetryBackoffMs { get; set; } = TimeSpan.FromMilliseconds(100);

        // Additional consumer-specific config entries
        public IDictionary<string, string>? ConsumerConfig { get; set; }

        // Delivery semantics (kept here so factory selection remains simple)
        public DeliverySemantics DeliverySemantics { get; set; } = DeliverySemantics.AtLeastOnce;
    }
}