using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Least Once" delivery guarantee strategy for Kafka message production.
    /// This strategy ensures message durability by requiring acknowledgment from all in-sync replicas
    /// and implementing retry mechanisms for transient failures.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered at least once, meaning they will never be lost but may be duplicated
    /// if retries occur due to transient failures. This is achieved through waiting for acknowledgment
    /// from all in-sync replicas and automatic retry mechanisms.</para>
    /// 
    /// <para><strong>Production Flow:</strong></para>
    /// <list type="number">
    /// <item>Producer sends message to Kafka broker</item>
    /// <item>Message is replicated to all in-sync replicas (Acks.All)</item>
    /// <item>All replicas acknowledge successful write</item>
    /// <item>Producer receives delivery confirmation</item>
    /// <item>If any step fails, message is automatically retried</item>
    /// <item>Optionally adds unique message ID for duplicate detection</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> No message loss, fault tolerance, automatic retry handling</item>
    /// <item><strong>Disadvantages:</strong> Possible duplicates during retries, higher latency, increased network traffic</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>Financial transactions and payment notifications</item>
    /// <item>Order processing and inventory updates</item>
    /// <item>Critical business events that must not be lost</item>
    /// <item>User account modifications and authentication events</item>
    /// <item>System alerts and notifications</item>
    /// <item>Data replication between systems</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>High-frequency trading systems requiring minimal latency</item>
    /// <item>Real-time gaming or streaming applications</item>
    /// <item>Simple logging where duplicates are costly to handle</item>
    /// <item>Applications that cannot implement idempotent message handling</item>
    /// </list>
    /// 
    /// <para><strong>Configuration Features:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Acknowledgment Level:</strong> Waits for all in-sync replicas (Acks.All)</item>
    /// <item><strong>Optional Idempotence:</strong> Can enable producer idempotence to reduce duplicates</item>
    /// <item><strong>Retry Logic:</strong> Configurable maximum retry attempts for transient failures</item>
    /// <item><strong>Duplicate Detection:</strong> Optional message ID headers for consumer-side deduplication</item>
    /// </list>
    /// 
    /// <para><strong>Best Practices:</strong></para>
    /// <list type="bullet">
    /// <item>Enable duplicate detection when downstream systems can benefit from deduplication</item>
    /// <item>Consider enabling idempotence to reduce duplicate risk</item>
    /// <item>Monitor retry metrics to detect infrastructure issues</item>
    /// <item>Ensure downstream consumers can handle potential duplicates gracefully</item>
    /// </list>
    /// </remarks>
    internal sealed class ProducerAtLeastOnceStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerOptions _options;

        /// <summary>
        /// Gets a value indicating whether this strategy requires a transactional producer.
        /// Returns false as "At Least Once" semantics do not require transactions.
        /// </summary>
        public bool RequiresTransactionalProducer => false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducerAtLeastOnceStrategy"/> class.
        /// Configures the producer for "At Least Once" delivery semantics with durability guarantees.
        /// </summary>
        /// <param name="config">The Kafka producer configuration to modify.</param>
        /// <param name="options">The producer options containing strategy-specific settings.</param>
        /// <remarks>
        /// <para>This constructor configures the producer for maximum durability:</para>
        /// <list type="bullet">
        /// <item><strong>Acks.All:</strong> Waits for acknowledgment from all in-sync replicas before considering send successful</item>
        /// <item><strong>Idempotence:</strong> Optionally enables producer idempotence to reduce duplicate risk from retries</item>
        /// <item><strong>Retries:</strong> Configures maximum retry attempts for handling transient failures</item>
        /// </list>
        /// </remarks>
        public ProducerAtLeastOnceStrategy(ProducerConfig config, ProducerOptions options)
        {
            _options = options;

            // Acks.All: Wait for acknowledgment from all in-sync replicas
            // This ensures the message is durably stored before considering it sent
            // Provides the highest durability guarantee but increases latency
            config.Acks = Acks.All;

            // EnableIdempotence: Optional feature to reduce duplicate messages
            // When enabled, the producer assigns sequence numbers to detect and deduplicate retries
            // Helps reduce (but doesn't eliminate) duplicate risk during network issues
            config.EnableIdempotence = options.EnableIdempotence ?? false;

            // MessageSendMaxRetries: Configure retry attempts for transient failures
            // Higher values increase reliability but may increase latency during issues
            // Set based on acceptable latency vs. reliability trade-offs
            config.MessageSendMaxRetries = options.MaxRetries;
        }

        /// <summary>
        /// Produces a message asynchronously using the "At Least Once" delivery strategy.
        /// Optionally adds message ID headers for duplicate detection support.
        /// </summary>
        /// <param name="producer">The Kafka producer instance to use for message production.</param>
        /// <param name="transactionalProducer">Not used by this strategy (can be null).</param>
        /// <param name="topic">The Kafka topic to produce the message to.</param>
        /// <param name="message">The message to produce.</param>
        /// <param name="cancellationToken">Token to cancel the produce operation.</param>
        /// <returns>A task representing the asynchronous produce operation with delivery results.</returns>
        /// <remarks>
        /// <para>This method implements the core "At Least Once" production logic:</para>
        /// <list type="number">
        /// <item>Optionally adds a unique message ID header for downstream duplicate detection</item>
        /// <item>Sends the message with durability guarantees (all replicas must acknowledge)</item>
        /// <item>Automatically retries on transient failures based on configuration</item>
        /// <item>Returns delivery results including partition and offset information</item>
        /// </list>
        /// 
        /// <para><strong>Duplicate Detection:</strong></para>
        /// <para>When EnableDuplicateDetection is true, adds a "message-id" header with a unique GUID.
        /// This allows downstream consumers to implement deduplication logic by tracking processed message IDs.</para>
        /// 
        /// <para><strong>Error Handling:</strong></para>
        /// <para>Transient errors (network issues, leader elections) are automatically retried.
        /// Permanent errors (invalid topic, message too large) will fail immediately.
        /// Applications should implement appropriate error handling for failed deliveries.</para>
        /// </remarks>
        public Task<DeliveryResult<string, string>> PublishAsync(
            string topic,
            Message<string, string> message,
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            CancellationToken cancellationToken)
        {
            if (_options.EnableDuplicateDetection)
            {
                // Add message ID header for deduplication support
                // Downstream consumers can use this ID to detect and handle duplicates
                // Uses GUID to ensure uniqueness across all messages and producers
                message.Headers ??= [];
                message.Headers.Add("message-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            // Produce message with "At Least Once" guarantees
            // The producer configuration ensures durability through Acks.All and retry logic
            return producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}