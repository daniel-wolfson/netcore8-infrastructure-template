using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Implements the "Exactly Once" delivery guarantee strategy for Kafka message consumption.
    /// This strategy provides the strongest delivery guarantee by ensuring messages are
    /// neither lost nor duplicated.
    /// </summary>
    /// <remarks>
    /// <para><strong>Delivery Guarantee:</strong></para>
    /// <para>Messages are delivered exactly once, meaning they will never be lost and never be duplicated.
    /// This is the most complex delivery semantic and requires careful coordination between producers, consumers,
    /// and application logic using Kafka transactions.</para>
    /// 
    /// <para><strong>System Requirements:</strong></para>
    /// <list type="number">
    /// <item><strong>Producer Configuration:</strong> Must enable idempotence (enable.idempotence=true) 
    /// and use transactions</item>
    /// <item><strong>Consumer Configuration:</strong> Must disable auto-commit 
    /// and use ReadCommitted isolation level</item>
    /// <item><strong>Application Logic:</strong> Must process and produce messages within transactional boundaries</item>
    /// <item><strong>Kafka Cluster:</strong> Must support transactions (Kafka 0.11+ with proper configuration)</item>
    /// </list>
    /// 
    /// <para><strong>Processing Flow:</strong></para>
    /// <list type="number">
    /// <item>Consumer receives message from Kafka (only committed transactional messages)</item>
    /// <item>Application begins transaction scope</item>
    /// <item>Message is processed within the transaction</item>
    /// <item>Any produced messages are sent within the same transaction</item>
    /// <item>Transaction is committed, including offset commits</item>
    /// <item>If any step fails, entire transaction is rolled back</item>
    /// </list>
    /// 
    /// <para><strong>Trade-offs:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Advantages:</strong> No message loss, no duplicates, strong consistency guarantees</item>
    /// <item><strong>Disadvantages:</strong> Highest latency, lowest throughput, complex implementation, 
    /// requires transactional infrastructure</item>
    /// </list>
    /// 
    /// <para><strong>Suitable Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item>Financial systems requiring strict consistency (payments, transfers)</item>
    /// <item>Critical business workflows where duplicates cause data corruption</item>
    /// <item>Regulatory compliance scenarios requiring audit trails</item>
    /// <item>Cross-system data synchronization with consistency requirements</item>
    /// <item>Inventory management with precise stock tracking</item>
    /// <item>Billing and invoicing systems</item>
    /// </list>
    /// 
    /// <para><strong>Not Suitable For:</strong></para>
    /// <list type="bullet">
    /// <item>High-throughput scenarios where performance is critical</item>
    /// <item>Real-time systems requiring minimal latency</item>
    /// <item>Simple logging or monitoring where duplicates are acceptable</item>
    /// <item>Systems that cannot implement transactional processing</item>
    /// <item>Applications with limited infrastructure complexity tolerance</item>
    /// </list>
    /// 
    /// <para><strong>Implementation Requirements:</strong></para>
    /// <list type="bullet">
    /// <item>All database operations must be transactional and coordinated with Kafka transactions</item>
    /// <item>Application must handle transaction rollbacks and retries appropriately</item>
    /// <item>Kafka cluster must be configured with proper transaction coordination</item>
    /// <item>Monitoring and alerting for transaction failures is essential</item>
    /// <item>Consider using Kafka Streams for complex exactly-once processing scenarios</item>
    /// </list>
    /// 
    /// <para><strong>Configuration Notes:</strong></para>
    /// <para>This strategy configures only the consumer-side requirements for exactly-once semantics.
    /// Complete exactly-once delivery requires coordinated configuration across the entire system including
    /// producers, application logic, and infrastructure components.</para>
    /// </remarks>
    internal sealed class ConsumerExactlyOnceStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerExactlyOnceStrategy"/> class.
        /// Configures the consumer for "Exactly Once" delivery semantics with transactional support.
        /// </summary>
        /// <param name="config">The Kafka consumer configuration to modify.</param>
        /// <param name="options">The consumer options for this strategy.</param>
        /// <remarks>
        /// <para>This constructor configures only the consumer-side requirements for exactly-once semantics:</para>
        /// <list type="bullet">
        /// <item><strong>Manual Offset Control:</strong> Disables auto-commit to ensure offset 
        /// commits are part of transactions</item>
        /// <item><strong>Read Committed Isolation:</strong> 
        /// Ensures only committed transactional messages are consumed</item>
        /// </list>
        /// <para><strong>Note:</strong> Complete exactly-once semantics require additional producer configuration
        /// and transactional application logic that must be implemented separately.</para>
        /// </remarks>
        public ConsumerExactlyOnceStrategy(ConsumerConfig config, ConsumerSettings options)
        {
            _options = options;

            // Consumer Configuration:
            // Note: This strategy handles consumer-side configuration only. Complete exactly-once 
            // semantics require coordinated producer configuration and transactional application logic.

            // EnableAutoCommit = false: Manual offset control for transactional consistency
            // This ensures that offset commits can be included in the same transaction as message processing
            config.EnableAutoCommit = false;

            // IsolationLevel = ReadCommitted: Only consume committed transactional messages
            // This prevents reading uncommitted or aborted transactional messages, maintaining consistency
            config.IsolationLevel = IsolationLevel.ReadCommitted;
        }

        /// <summary>
        /// Handles post-processing logic for the "Exactly Once" strategy by manually committing
        /// the message offset after successful processing.
        /// </summary>
        /// <param name="consumer">The Kafka consumer instance used for offset management.</param>
        /// <param name="result">The consumed message result containing offset information.</param>
        /// <returns>A completed task after the offset has been committed.</returns>
        /// <remarks>
        /// <para><strong>Important:</strong> This method performs a simple offset commit, but for true exactly-once
        /// semantics, the offset commit should be part of a larger transaction that includes:</para>
        /// <list type="bullet">
        /// <item>Message processing operations (database updates, business logic)</item>
        /// <item>Any outbound message production</item>
        /// <item>The offset commit operation</item>
        /// </list>
        /// 
        /// <para>Consider using Kafka's transactional APIs or a framework like Kafka Streams
        /// for proper transactional exactly-once processing. This implementation provides the
        /// consumer configuration foundation but requires additional transactional coordination
        /// at the application level.</para>
        /// 
        /// <para><strong>Transactional Flow (when properly implemented):</strong></para>
        /// <list type="number">
        /// <item>Begin transaction</item>
        /// <item>Process message (database operations, business logic)</item>
        /// <item>Produce any outbound messages within transaction</item>
        /// <item>Commit offsets within transaction</item>
        /// <item>Commit entire transaction atomically</item>
        /// </list>
        /// </remarks>
        public void HandleAfterProcess(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result)
        {
            // commit offsets manually after processing
            // Note: For true exactly-once semantics, this commit should be part of a larger
            // transaction that includes all processing operations
            consumer.Commit(result);
        }
    }
}