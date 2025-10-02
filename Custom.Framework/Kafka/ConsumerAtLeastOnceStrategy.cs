using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Least Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy ensures message durability by committing offsets only after successful processing.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered at least once, meaning they will never be lost but may be duplicated
    /// if processing fails and the message is redelivered. This is achieved through manual offset commits
    /// that occur only after successful message processing.</para>
    /// 
    /// <para><strong>Processing Flow:</strong></para>
    /// <list type="number">
    /// <item>Consumer receives message from Kafka</item>
    /// <item>Application processes the message</item>
    /// <item>If processing succeeds → Manual commit marks message as consumed</item>
    /// <item>If processing fails → No commit occurs, message will be redelivered</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> No message loss, guaranteed processing, fault tolerance</item>
    /// <item><strong>Disadvantages:</strong> Possible duplicate processing, higher latency, requires idempotent handling</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>Financial transactions and payment processing</item>
    /// <item>Order management and inventory updates</item>
    /// <item>Critical business events that must not be lost</item>
    /// <item>Audit trails and compliance data</item>
    /// <item>User account modifications and authentication events</item>
    /// <item>Data synchronization between systems</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>High-throughput scenarios where duplicates are costly</item>
    /// <item>Real-time systems requiring minimal latency</item>
    /// <item>Applications that cannot handle duplicate processing</item>
    /// <item>Simple logging where message loss is acceptable</item>
    /// </list>
    /// 
    /// <para><strong>Important Considerations:</strong></para>
    /// <list type="bullet">
    /// <item>Applications must implement idempotent message processing</item>
    /// <item>Database operations should use upsert patterns or unique constraints</item>
    /// <item>Consider implementing deduplication logic for critical operations</item>
    /// <item>Monitor consumer lag to ensure timely processing</item>
    /// </list>
    /// 
    /// <para><strong>Configuration:</strong></para>
    /// <para>Auto-commit is disabled to provide manual control over offset commits. This ensures that
    /// only successfully processed messages are marked as consumed, preventing message loss during failures.</para>
    /// </remarks>
    internal sealed class ConsumerAtLeastOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerAtLeastOnceStrategy"/> class.
        /// Configures the consumer for "At Least Once" delivery semantics with manual offset commits.
        /// </summary>
        /// <param name="config">The Kafka consumer configuration to modify.</param>
        /// <param name="options">The consumer options for this strategy.</param>
        public ConsumerAtLeastOnceStrategy(ConsumerConfig config, ConsumerSettings options)
        {
            _options = options;

            // Auto-commit is disabled in the constructor. This means:
            // • Messages are not automatically marked as processed
            // • The application has full control over when to commit
            // • Only successfully processed messages get committed
            // • Prevents message loss by ensuring commits happen after processing

            config.EnableAutoCommit = false; // ← Manual control over commits
        }

        /// <summary>
        /// Handles post-processing logic for the "At Least Once" strategy by manually committing
        /// the message offset after successful processing.
        /// </summary>
        /// <para>This method performs manual offset commitment to implement the "At Least Once" guarantee.</para>
        /// <para>The commit operation is synchronous and will block until Kafka acknowledges the offset update.
        /// This ensures that the commit is durable before the method completes.</para>
        /// </remarks>
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