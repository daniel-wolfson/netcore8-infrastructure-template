using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public static class DeliveryStrategyFactory
    {
        /// <summary>
        /// Creates an appropriate producer delivery strategy based on the specified delivery semantics.
        /// Each strategy configures the underlying Kafka producer with specific settings to achieve
        /// the desired delivery guarantees and performance characteristics.
        /// </summary>
        /// <param name="producer">The Kafka producer instance that will be used for message production. 
        /// Note that for ExactlyOnce semantics, a transactional producer wrapper will be used instead.</param>
        /// <param name="config">The Kafka producer configuration that will be modified by the strategy 
        /// to set appropriate acknowledgment levels, retry policies, and other delivery-specific settings.</param>
        /// <param name="options">The producer options containing the delivery semantics preference and 
        /// other strategy-specific configuration parameters.</param>
        /// <returns>
        /// An <see cref="IProducerDeliveryStrategy"/> implementation that handles message production according to the specified delivery guarantee:
        /// <list type="bullet">
        /// <item><see cref="ProducerAtMostOnceStrategy"/> - For high-throughput scenarios with acceptable message loss</item>
        /// <item><see cref="ProducerAtLeastOnceStrategy"/> - For reliable delivery with possible duplicates (default)</item>
        /// <item><see cref="ProducerExactlyOnceStrategy"/> - For transactional processing with no loss or duplicates</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>The method uses a switch expression to map delivery semantics to their corresponding strategy implementations.
        /// If an invalid or unrecognized <see cref="DeliverySemantics"/> value is provided, the method defaults to 
        /// <see cref="DeliverySemantics.AtLeastOnce"/> for safety and reliability.</para>
        /// 
        /// <para>Each strategy modifies the provided <paramref name="config"/> object with appropriate Kafka producer 
        /// settings such as acknowledgment requirements, retry policies, and idempotence configuration to achieve 
        /// the desired delivery semantics.</para>
        /// </remarks>
        public static IProducerDeliveryStrategy CreateProducerStrategy(
            IProducer<string, string> producer, 
            ProducerConfig config, 
            ProducerOptions options)
        {
            return options.DeliverySemantics switch
            {
                DeliverySemantics.AtMostOnce => new ProducerAtMostOnceStrategy(config, options),
                DeliverySemantics.AtLeastOnce => new ProducerAtLeastOnceStrategy(config, options),
                DeliverySemantics.ExactlyOnce => new ProducerExactlyOnceStrategy(config, options),
                _ => new ProducerAtLeastOnceStrategy(config, options)
            };
        }

        /// <summary>
        /// Creates an appropriate consumer delivery strategy based on the specified delivery semantics.
        /// </summary>
        /// <returns>
        /// An <see cref="IConsumerDeliveryStrategy"/> implementation that handles message processing according to the specified delivery guarantee:
        /// <list type="bullet">
        /// <item><see cref="ConsumerAtMostOnceStrategy"/> - For fire-and-forget scenarios with possible message loss</item>
        /// <item><see cref="ConsumerAtLeastOnceStrategy"/> - For guaranteed delivery with possible duplicates (default)</item>
        /// <item><see cref="ConsumerExactlyOnceStrategy"/> - For transactional processing with no loss or duplicates</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>The method uses a switch expression to map delivery semantics to their corresponding strategy implementations.
        /// If an invalid or unrecognized <see cref="DeliverySemantics"/> value is provided, the method defaults to 
        /// <see cref="DeliverySemantics.AtLeastOnce"/> for safety and reliability.</para>
        /// 
        /// <para>Each strategy configures the provided <paramref name="config"/> object with appropriate Kafka consumer 
        /// settings and provides post-processing logic through the <see cref="IConsumerDeliveryStrategy.HandleAfterProcessAsync"/> method.</para>
        /// </remarks>
        public static IConsumerDeliveryStrategy CreateConsumerStrategy(ConsumerConfig config, ConsumerOptions options)
        {
            return options.DeliverySemantics switch
            {
                DeliverySemantics.AtMostOnce => new ConsumerAtMostOnceStrategy(config, options),
                DeliverySemantics.AtLeastOnce => new ConsumerAtLeastOnceStrategy(config, options),
                DeliverySemantics.ExactlyOnce => new ConsumerExactlyOnceStrategy(config, options),
                _ => new ConsumerAtLeastOnceStrategy(config, options)
            };
        }
    }
}