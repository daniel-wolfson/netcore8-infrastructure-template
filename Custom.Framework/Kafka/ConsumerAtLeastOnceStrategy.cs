using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Least Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy ensures message durability by committing offsets only after successful processing.
    /// </summary>
    internal sealed class ConsumerAtLeastOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _options;
        private readonly Confluent.Kafka.ConsumerConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerAtLeastOnceStrategy"/> class.
        /// Configures the consumer for "At Least Once" delivery semantics with manual offset commits.
        /// </summary>
        /// <param name="config">The Kafka consumer configuration to modify.</param>
        /// <param name="options">The consumer options for this strategy.</param>
        public ConsumerAtLeastOnceStrategy(Confluent.Kafka.ConsumerConfig config, ConsumerSettings options)
        {
            _options = options;
            _config = config;

            // Auto-commit is disabled in the constructor. This means:
            // • Messages are not automatically marked as processed
            // • The application has full control over when to commit
            // • Only successfully processed messages get committed
            // • Prevents message loss by ensuring commits happen after processing

            config.EnableAutoCommit = false; // ← Manual control over commits
        }

        public Confluent.Kafka.ConsumerConfig ConsumerConfig => _config;

        /// <summary>
        /// Handles post-processing logic for the "At Least Once" strategy by manually committing
        /// the message offset after successful processing.
        /// </summary>
        public void HandleAfterProcess(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result)
        {
            // Key Functions:
            // 1.Offset Commitment: It tells Kafka "I have successfully processed this message, mark it as consumed"
            // 2.Progress Tracking: Updates the consumer's position in the topic partition
            // 3.Prevents Reprocessing: Ensures this specific message won't be delivered again to consumers in the same group
            consumer.Commit(result);
        }
    }
}