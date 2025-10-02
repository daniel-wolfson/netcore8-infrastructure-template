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
        
        public static IProducerDeliveryStrategy CreateProducerStrategy(
            DeliverySemantics deliverySemantics,
            ProducerConfig config, 
            ProducerSettings settings)
        {
            return deliverySemantics switch
            {
                DeliverySemantics.AtMostOnce => new ProducerAtMostOnceStrategy(config, settings),
                DeliverySemantics.AtLeastOnce => new ProducerAtLeastOnceStrategy(config, settings),
                DeliverySemantics.ExactlyOnce => new ProducerExactlyOnceStrategy(config, settings),
                DeliverySemantics.DeadLetter => new ProducerAtLeastOnceStrategy(config, settings), // DLQ uses AtLeastOnce semantics
                _ => new ProducerAtLeastOnceStrategy(config, settings)
            };
        }

        /// <summary>
        /// Creates an appropriate consumer delivery strategy based on the specified delivery semantics.
        /// </summary>
        public static IConsumerDeliveryStrategy CreateConsumerStrategy(ConsumerConfig config, ConsumerSettings settings)
        {
            return settings.DeliverySemantics switch
            {
                DeliverySemantics.AtMostOnce => new ConsumerAtMostOnceStrategy(config, settings),
                DeliverySemantics.AtLeastOnce => new ConsumerAtLeastOnceStrategy(config, settings),
                DeliverySemantics.ExactlyOnce => new ConsumerExactlyOnceStrategy(config, settings),
                _ => new ConsumerAtLeastOnceStrategy(config, settings)
            };
        }
    }
}