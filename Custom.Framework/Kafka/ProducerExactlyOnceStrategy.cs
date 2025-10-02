using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "Exactly Once" delivery guarantee strategy for Kafka message production.
    /// This strategy provides the strongest delivery guarantee by ensuring messages are neither lost nor duplicated
    /// through the use of Kafka transactions and idempotent producers.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered exactly once, meaning they will never be lost and never be duplicated.
    /// This is achieved through Kafka's transactional capabilities, which coordinate message production,
    /// processing, and offset commits in atomic operations.</para>
    /// 
    /// <para><strong>Production Flow:</strong></para>
    /// <list type="number">
    /// <item>Initialize transaction coordinator and begin transaction</item>
    /// <item>Producer sends message within the transaction scope</item>
    /// <item>Message is replicated to all in-sync replicas with transaction markers</item>
    /// <item>Transaction is committed, making the message visible to consumers</item>
    /// <item>If any step fails, the entire transaction is rolled back</item>
    /// <item>Consumers with ReadCommitted isolation only see committed messages</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> No message loss, no duplicates, strong consistency guarantees, 
    /// atomic operations</item>
    /// <item><strong>Disadvantages:</strong> Highest latency, lowest throughput, 
    /// complex infrastructure requirements, resource intensive</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>Financial systems requiring strict consistency (payments, transfers, trading)</item>
    /// <item>Critical business workflows where duplicates cause data corruption</item>
    /// <item>Regulatory compliance scenarios requiring perfect audit trails</item>
    /// <item>Cross-system data synchronization with consistency requirements</item>
    /// <item>Inventory management with precise stock tracking</item>
    /// <item>Billing and invoicing systems</item>
    /// <item>Multi-step business processes requiring atomicity</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>High-throughput scenarios where performance is critical</item>
    /// <item>Real-time systems requiring minimal latency</item>
    /// <item>Simple logging or monitoring where duplicates are acceptable</item>
    /// <item>Systems that cannot implement transactional coordination</item>
    /// <item>Applications with limited infrastructure complexity tolerance</item>
    /// <item>Scenarios where eventual consistency is sufficient</item>
    /// </list>
    /// 
    /// <para><strong>System Requirements:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Kafka Cluster:</strong> Must support transactions (Kafka 0.11+ with proper broker configuration)</item>
    /// <item><strong>Transaction Coordinator:</strong> Dedicated Kafka internal topic for transaction state management</item>
    /// <item><strong>Idempotent Producer:</strong> Automatically enabled to prevent duplicates within transactions</item>
    /// <item><strong>Consumer Configuration:</strong> Should use ReadCommitted isolation level</item>
    /// </list>
    /// 
    /// <para><strong>Configuration Features:</strong></para>
    /// <list type="bullet">
    /// <item><strong>All Replica Acknowledgment:</strong> Uses Acks.All for maximum durability</item>
    /// <item><strong>Idempotence:</strong> Automatically enabled to prevent duplicates from retries</item>
    /// <item><strong>Retry Logic:</strong> Configurable retries within transactional boundaries</item>
    /// <item><strong>Transaction Management:</strong> Automatic begin/commit/abort transaction handling</item>
    /// </list>
    /// 
    /// <para><strong>Best Practices:</strong></para>
    /// <list type="bullet">
    /// <item>Keep transactions short to minimize resource holding time</item>
    /// <item>Implement proper error handling and transaction rollback logic</item>
    /// <item>Monitor transaction coordinator health and performance</item>
    /// <item>Consider batching multiple messages within single transactions for efficiency</item>
    /// <item>Ensure proper transaction timeout configuration</item>
    /// </list>
    /// </remarks>
    internal sealed class ProducerExactlyOnceStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _options;

        /// <summary>
        /// Gets a value indicating whether this strategy requires a transactional producer.
        /// Returns true as "Exactly Once" semantics require transactional capabilities.
        /// </summary>
        public bool RequiresTransactionalProducer => true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducerExactlyOnceStrategy"/> class.
        /// Configures the producer for "Exactly Once" delivery semantics with transactional support.
        /// </summary>
        /// <param name="config">The Kafka producer configuration to modify.</param>
        /// <param name="options">The producer options containing strategy-specific settings.</param>
        /// <remarks>
        /// <para>This constructor configures the producer for exactly-once semantics:</para>
        /// <list type="bullet">
        /// <item><strong>Acks.All:</strong> Waits for acknowledgment from all in-sync replicas for maximum durability</item>
        /// <item><strong>Idempotence:</strong> Enabled to prevent duplicates from retries within transactions</item>
        /// <item><strong>Retries:</strong> Configures retry attempts within transactional boundaries</item>
        /// </list>
        /// <para>Additional transactional configuration (transactional.id, etc.) must be set separately
        /// when creating the transactional producer instance.</para>
        /// </remarks>
        public ProducerExactlyOnceStrategy(ProducerConfig config, ProducerSettings options)
        {
            _options = options;
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
    }
}