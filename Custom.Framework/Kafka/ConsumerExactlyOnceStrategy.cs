using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "Exactly Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy provides the strongest delivery guarantee by ensuring messages are
    /// neither lost nor duplicated.
    /// </summary>
    internal sealed class ConsumerExactlyOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _options;
        private readonly Confluent.Kafka.ConsumerConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerExactlyOnceStrategy"/> class.
        /// Configures the consumer for "Exactly Once" delivery semantics with transactional support.
        /// </summary>
        public ConsumerExactlyOnceStrategy(Confluent.Kafka.ConsumerConfig config, ConsumerSettings options)
        {
            _options = options;
            _config = config;

            // Consumer Configuration:
            // Note: This strategy handles consumer-side configuration only. Complete exactly-once 
            // semantics require coordinated producer configuration and transactional application logic.

            // EnableAutoCommit = false: Manual offset control for transactional consistency
            // This ensures that offset commits can be included in the same transaction as message processing
            config.EnableAutoCommit = false;

            // IsolationLevel = ReadCommitted: Only consume committed transactional messages
            // This prevents reading uncommitted or aborted transactional messages, maintaining consistency
            config.IsolationLevel = Confluent.Kafka.IsolationLevel.ReadCommitted;
        }

        public Confluent.Kafka.ConsumerConfig ConsumerConfig => _config;

        /// <summary>
        /// Handles post-processing logic for the "Exactly Once" strategy by manually committing
        /// the message offset after successful processing.
        /// </summary>
        public void HandleAfterProcess(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result)
        {
            // commit offsets manually after processing
            // Note: For true exactly-once semantics, this commit should be part of a larger
            // transaction that includes all processing operations
            consumer.Commit(result);
        }
    }
}