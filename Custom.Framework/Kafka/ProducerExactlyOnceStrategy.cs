using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "Exactly Once" delivery guarantee strategy for Kafka message production.
    /// This strategy provides the strongest delivery guarantee by ensuring messages are neither lost nor duplicated
    /// through the use of Kafka transactions and idempotent producers.
    /// </summary>
    internal sealed class ProducerExactlyOnceStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _options;
        private readonly ProducerConfig _config;

        /// <summary>
        /// Gets a value indicating whether this strategy requires a transactional producer.
        /// Returns true as "Exactly Once" semantics require transactional capabilities.
        /// </summary>
        public bool RequiresTransactionalProducer => true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducerExactlyOnceStrategy"/> class.
        /// Configures the producer for "Exactly Once" delivery semantics with transactional support.
        /// </summary>
        public ProducerExactlyOnceStrategy(ProducerConfig config, ProducerSettings options)
        {
            _options = options;
            _config = config;
            // Acks.All: Wait for acknowledgment from all in-sync replicas
            // Required for exactly-once semantics to ensure durability within transactions
            // All replicas must acknowledge before transaction can be committed
            config.Acks = Acks.All;

            // EnableIdempotence = true: Prevents duplicate messages from retries
            // Required for exactly-once semantics and automatically enabled for transactional producers
            // Producer assigns sequence numbers to detect and deduplicate retry attempts
            config.EnableIdempotence = true;

            // MessageSendMaxRetries: Configure retry attempts within transaction boundaries
            // Retries are performed within the same transaction to maintain atomicity
            // Failed transactions are rolled back, ensuring no partial state is committed
            config.MessageSendMaxRetries = options.MessageSendMaxRetries;
        }

        /// <summary>
        /// Produces a message asynchronously using the "Exactly Once" delivery strategy with full transaction management.
        /// Handles transaction initialization, message production, and commit/abort logic automatically.
        /// </summary>
        public async Task<DeliveryResult<string, byte[]>> PublishAsync(
            string topic, 
            Message<string, byte[]> message, 
            IProducer<string, byte[]> producer, 
            IProducer<string, byte[]>? transactionalProducer, 
            CancellationToken cancellationToken)
        {
            if (transactionalProducer == null)
                throw new InvalidOperationException("Transactional producer not initialized for ExactlyOnce delivery");

            // Initialize wrapper per call to avoid transaction conflicts between multiple producers
            var transactionalWrapper = new TransactionalKafkaProducer(transactionalProducer);
            transactionalWrapper.InitializeTransactions(TimeSpan.FromSeconds(10));

            // Use the wrapper's safe transaction execution
            var deliveryResult = await transactionalWrapper.ExecuteInTransactionAsync(async safeProducer =>
            {
                return await safeProducer.ProduceAsync(topic, message, cancellationToken).ConfigureAwait(false);
            },
            commitTimeout: TimeSpan.FromSeconds(10),
            abortTimeout: TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);

            return deliveryResult;
        }

        public ProducerConfig ProducerConfig { get; }
    }
}