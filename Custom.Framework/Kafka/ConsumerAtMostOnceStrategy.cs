using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Most Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy prioritizes performance and minimal resource usage over message durability.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered at most once, meaning they may be lost but will never be duplicated.
    /// This is achieved through automatic offset commits that occur before message processing.</para>
    /// 
    /// <para><strong>Processing Flow:</strong></para>
    /// <list type="number">
    /// <item>Message is received from Kafka</item>
    /// <item>Offset is automatically committed at regular intervals (based on AutoCommitIntervalMs)</item>
    /// <item>Message is processed by the application</item>
    /// <item>If processing fails after commit, the message is permanently lost</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> High throughput, low latency, minimal memory usage, no duplicate processing</item>
    /// <item><strong>Disadvantages:</strong> Potential message loss during failures, no processing guarantees</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>Logging and monitoring data where occasional loss is acceptable</item>
    /// <item>Metrics collection and telemetry</item>
    /// <item>Cache invalidation notifications</item>
    /// <item>Real-time analytics where recent data is more valuable</item>
    /// <item>System notifications that can be regenerated</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>Financial transactions or payment processing</item>
    /// <item>Order management and fulfillment</item>
    /// <item>Critical business events that must be processed</item>
    /// <item>Audit trails and compliance data</item>
    /// <item>User account modifications</item>
    /// </list>
    /// 
    /// <para><strong>Configuration:</strong></para>
    /// <para>Auto-commit is enabled with configurable intervals. Shorter intervals reduce potential
    /// message loss but may impact performance. Longer intervals improve performance but increase
    /// the window for potential message loss during failures.</para>
    /// </remarks>
    internal sealed class ConsumerAtMostOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _appConfig;

        public ConsumerAtMostOnceStrategy(ConsumerConfig config, ConsumerSettings options)
        {
            _appConfig = options;

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