using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Most Once" delivery guarantee strategy for Kafka message production.
    /// This strategy prioritizes performance and low latency over message durability by using
    /// fire-and-forget semantics with no acknowledgment requirements.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered at most once, meaning they may be lost but will never be duplicated.
    /// This is achieved through fire-and-forget semantics where the producer does not wait for
    /// acknowledgments and performs no retries on failures.</para>
    /// 
    /// <para><strong>Production Flow:</strong></para>
    /// <list type="number">
    /// <item>Producer sends message to Kafka broker</item>
    /// <item>Producer immediately considers the message sent (no acknowledgment wait)</item>
    /// <item>If network issues or broker failures occur, message may be lost</item>
    /// <item>No retries are performed on any failures</item>
    /// <item>Maximum throughput and minimum latency are achieved</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> Highest throughput, lowest latency, minimal resource usage, no duplicate risk</item>
    /// <item><strong>Disadvantages:</strong> Potential message loss, no durability guarantees, no failure recovery</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>High-frequency metrics and telemetry data</item>
    /// <item>Real-time analytics where recent data is more valuable than historical</item>
    /// <item>Gaming events and real-time user interactions</item>
    /// <item>System monitoring and health check pings</item>
    /// <item>Cache invalidation notifications</item>
    /// <item>Log aggregation where occasional loss is acceptable</item>
    /// <item>High-throughput sensor data collection</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>Financial transactions or payment processing</item>
    /// <item>Order management and inventory systems</item>
    /// <item>Critical business events that must not be lost</item>
    /// <item>Audit trails and compliance data</item>
    /// <item>User account modifications</item>
    /// <item>Any scenario where message loss is unacceptable</item>
    /// </list>
    /// 
    /// <para><strong>Configuration Features:</strong></para>
    /// <list type="bullet">
    /// <item><strong>No Acknowledgments:</strong> Uses Acks.None for maximum performance</item>
    /// <item><strong>No Idempotence:</strong> Disabled to reduce overhead</item>
    /// <item><strong>No Retries:</strong> Zero retry attempts to prevent delays</item>
    /// <item><strong>Fire-and-Forget:</strong> Immediate return without waiting for delivery confirmation</item>
    /// </list>
    /// 
    /// <para><strong>Performance Characteristics:</strong></para>
    /// <list type="bullet">
    /// <item>Lowest possible latency for message production</item>
    /// <item>Highest throughput due to no blocking operations</item>
    /// <item>Minimal memory and CPU overhead</item>
    /// <item>No network round-trips waiting for acknowledgments</item>
    /// </list>
    /// 
    /// <para><strong>Best Practices:</strong></para>
    /// <list type="bullet">
    /// <item>Use only when message loss is acceptable for your use case</item>
    /// <item>Monitor broker health to minimize infrastructure-related losses</item>
    /// <item>Consider implementing application-level monitoring for critical loss detection</item>
    /// <item>Ensure downstream systems can handle missing messages gracefully</item>
    /// </list>
    /// </remarks>
    internal sealed class ProducerAtMostOnceStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerOptions _options;

        /// <summary>
        /// Gets a value indicating whether this strategy requires a transactional producer.
        /// Returns false as "At Most Once" semantics do not require transactions.
        /// </summary>
        public bool RequiresTransactionalProducer => false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducerAtMostOnceStrategy"/> class.
        /// Configures the producer for "At Most Once" delivery semantics with maximum performance.
        /// </summary>
        /// <param name="config">The Kafka producer configuration to modify.</param>
        /// <param name="options">The producer options containing strategy-specific settings.</param>
        /// <remarks>
        /// <para>This constructor configures the producer for maximum performance with no durability guarantees:</para>
        /// <list type="bullet">
        /// <item><strong>Acks.None:</strong> Producer doesn't wait for any acknowledgment from brokers</item>
        /// <item><strong>No Idempotence:</strong> Disabled to reduce overhead and complexity</item>
        /// <item><strong>Zero Retries:</strong> No retry attempts to prevent delays and ensure at-most-once semantics</item>
        /// </list>
        /// </remarks>
        public ProducerAtMostOnceStrategy(ProducerConfig config, ProducerOptions options)
        {
            _options = options;

            // Acks.None: Producer doesn't wait for acknowledgment from any brokers
            // This provides the fastest possible message sending but offers no durability guarantees
            // Messages may be lost if brokers fail or network issues occur
            config.Acks = Acks.None;

            // EnableIdempotence = false: Disabled to reduce overhead
            // Idempotence is not needed since we're not retrying and duplicates are not a concern
            // This reduces memory usage and processing overhead on both client and broker
            config.EnableIdempotence = false;

            // MessageSendMaxRetries = 0: No retry attempts
            // Ensures true "at most once" semantics by never attempting redelivery
            // Any failed sends are immediately abandoned to prevent duplicates
            config.MessageSendMaxRetries = 0;
        }

        /// <summary>
        /// Produces a message asynchronously using the "At Most Once" delivery strategy.
        /// Uses fire-and-forget semantics for maximum performance.
        /// </summary>
        /// <param name="producer">The Kafka producer instance to use for message production.</param>
        /// <param name="transactionalProducer">Not used by this strategy (can be null).</param>
        /// <param name="topic">The Kafka topic to produce the message to.</param>
        /// <param name="message">The message to produce.</param>
        /// <param name="cancellationToken">Token to cancel the produce operation.</param>
        /// <returns>A task representing the asynchronous produce operation with delivery results.</returns>
        /// <remarks>
        /// <para>This method implements fire-and-forget production semantics:</para>
        /// <list type="number">
        /// <item>Sends the message to the broker without waiting for acknowledgment</item>
        /// <item>Returns immediately with delivery result (may not reflect actual delivery status)</item>
        /// <item>No retries are performed if the initial send fails</item>
        /// <item>Provides maximum throughput and minimum latency</item>
        /// </list>
        /// 
        /// <para><strong>Fire-and-Forget Semantics:</strong></para>
        /// <para>While the method still awaits the PublishAsync call to get the DeliveryResult,
        /// the underlying producer configuration (Acks.None) means the broker responds immediately
        /// without ensuring the message is actually persisted. This maintains the API contract
        /// while providing fire-and-forget performance characteristics.</para>
        /// 
        /// <para><strong>Error Handling:</strong></para>
        /// <para>Most errors will be returned immediately due to the no-acknowledgment configuration.
        /// However, some errors (invalid topic, message too large) may still be detected and thrown.
        /// Applications should handle these errors but understand that many delivery failures
        /// will not be detected with this strategy.</para>
        /// 
        /// <para><strong>Performance Notes:</strong></para>
        /// <para>This strategy provides the highest possible throughput and lowest latency
        /// for Kafka message production. Use when performance is more important than reliability.</para>
        /// </remarks>
        public Task<DeliveryResult<string, string>> PublishAsync(
            string topic,
            Message<string, string> message,
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            CancellationToken cancellationToken)
        {
            // Fire and forget semantics (still await produce to get DeliveryResult)
            // The producer configuration ensures maximum performance through:
            // - No acknowledgment waiting (Acks.None)
            // - No retries on failures
            // - No idempotence overhead
            return producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}