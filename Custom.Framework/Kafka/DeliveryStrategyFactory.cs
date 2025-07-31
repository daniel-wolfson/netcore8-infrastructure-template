namespace Custom.Framework.Kafka
{
    public static class DeliveryStrategyFactory
    {
        public static IProducerDeliveryStrategy CreateProducerStrategy(DeliverySemantics semantics, ProducerSettings settings)
        {
            return semantics switch
            {
                DeliverySemantics.AtMostOnce => new AtMostOnceProducerStrategy(settings),
                DeliverySemantics.AtLeastOnce => new AtLeastOnceProducerStrategy(settings),
                DeliverySemantics.ExactlyOnce => new ExactlyOnceProducerStrategy(settings),
                _ => new AtLeastOnceProducerStrategy(settings)
            };
        }

        public static IConsumerDeliveryStrategy CreateConsumerStrategy(DeliverySemantics semantics, ConsumerSettings settings)
        {
            return semantics switch
            {
                DeliverySemantics.AtMostOnce => new AtMostOnceConsumerStrategy(settings),
                DeliverySemantics.AtLeastOnce => new AtLeastOnceConsumerStrategy(settings),
                DeliverySemantics.ExactlyOnce => new ExactlyOnceConsumerStrategy(settings),
                _ => new AtLeastOnceConsumerStrategy(settings)
            };
        }
    }
}