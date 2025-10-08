using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "At Most Once" delivery guarantee strategy for Kafka message production.
    /// This strategy prioritizes performance and low latency over message durability by using
    /// fire-and-forget semantics with no acknowledgment requirements.
    /// </summary>
    internal sealed class ProducerAtMostOnceStrategy: IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _options;
        private readonly ProducerConfig _config;

        /// <summary>
        /// Gets a value indicating whether this strategy requires a transactional producer.
        /// Returns false as "At Most Once" semantics do not require transactions.
        /// </summary>
        public bool RequiresTransactionalProducer => false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducerAtMostOnceStrategy"/> class.
        /// Configures the producer for "At Most Once" delivery semantics with maximum performance.
        /// </summary>
        public ProducerAtMostOnceStrategy(ProducerConfig config, ProducerSettings options)
        {
            _options = options;
            _config = config;

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
        public Task<DeliveryResult<string, byte[]>> PublishAsync(
            string topic,
            Message<string, byte[]> message,
            IProducer<string, byte[]> producer,
            IProducer<string, byte[]>? transactionalProducer,
            CancellationToken cancellationToken)
        {
            // Fire and forget semantics (still await produce to get DeliveryResult)
            // The producer configuration ensures maximum performance through:
            // - No acknowledgment waiting (Acks.None)
            // - No retries on failures
            // - No idempotence overhead
            return producer.ProduceAsync(topic, message, cancellationToken);
        }

        public ProducerConfig ProducerConfig { get; }
    }
}