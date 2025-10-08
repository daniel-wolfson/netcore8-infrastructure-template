using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Most Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy prioritizes performance and minimal resource usage over message durability.
    /// </summary>
    /// <remarks>
    internal sealed class ConsumerAtMostOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _appConfig;
        private readonly Confluent.Kafka.ConsumerConfig _config;

        public ConsumerAtMostOnceStrategy(Confluent.Kafka.ConsumerConfig config, ConsumerSettings options)
        {
            _appConfig = options;
            _config = config;

            // Auto-commit is enabled in the constructor. This means:
            // • Messages are marked as processed automatically at intervals (AutoCommitIntervalMs)
            // • The application does not need to manage commits manually
            // • There is a risk of message loss if processing fails after auto-commit
            // • Reduces duplicate processing risk compared to manual commits
            //
            // Suitable for: Logging, metrics collection, non-critical data
            // Not suitable for: Financial transactions, order processing, critical data

            config.EnableAutoCommit = true;
            config.AutoCommitIntervalMs = _appConfig.AutoCommitIntervalMs;
        }

        public Confluent.Kafka.ConsumerConfig ConsumerConfig => _config;

        /// <summary>
        /// Handles post-processing logic for the "At Most Once" strategy.
        /// Since auto-commit is enabled, no manual offset management is required.
        /// </summary>
        /// <remarks>
        /// In the "At Most Once" strategy, offsets are automatically committed by the Kafka client
        /// at regular intervals, so this method performs no additional operations.
        /// </remarks>
        public void HandleAfterProcess(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result)
        {
            // Message Received → Auto-commit (marks as processed) → Process Message
            // auto commit enabled - nothing to do
        }


    }
}