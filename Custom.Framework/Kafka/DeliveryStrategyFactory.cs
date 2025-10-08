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
        
        public static IProducerDeliveryStrategy CreateProducerStrategy(DeliverySemantics deliverySemantics, ProducerSettings settings)
        {
            ProducerConfig config = ConfigureProducer(settings);
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
        public static IConsumerDeliveryStrategy CreateConsumerStrategy(DeliverySemantics deliverySemantics, ConsumerSettings settings)
        {
            var config = settings.ToConsumerConfig();
            return deliverySemantics switch
            {
                DeliverySemantics.AtMostOnce => new ConsumerAtMostOnceStrategy(config, settings),
                DeliverySemantics.AtLeastOnce => new ConsumerAtLeastOnceStrategy(config, settings),
                DeliverySemantics.ExactlyOnce => new ConsumerExactlyOnceStrategy(config, settings),
                _ => new ConsumerAtLeastOnceStrategy(config, settings)
            };
        }

        private static ProducerConfig ConfigureProducer(ProducerSettings settings)
        {
            var config = new ProducerConfig
            {
                BootstrapServers = settings.BootstrapServers,
                ClientId = settings.ClientId,
                LingerMs = settings.LingerMs,
                BatchSize = settings.BatchSize,
                MessageMaxBytes = settings.MessageMaxBytes,
                CompressionType = settings.CompressionType,
                CompressionLevel = settings.CompressionLevel,
                RetryBackoffMs = settings.RetryBackoffMs,
            };

            ConfigureSecurity(config, settings);

            if (settings.CustomProducerConfig != null)
            {
                foreach (var kv in settings.CustomProducerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
        }

        private static void ConfigureSecurity(ProducerConfig config, ProducerSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.SaslUsername))
            {
                config.SaslUsername = settings.SaslUsername;
                config.SaslPassword = settings.SaslPassword;
                config.SaslMechanism = 
                    (Confluent.Kafka.SaslMechanism)Enum.Parse(typeof(Confluent.Kafka.SaslMechanism), settings.SaslMechanism.ToString() ?? "Plain");
                config.SecurityProtocol = 
                    (Confluent.Kafka.SecurityProtocol)Enum.Parse(typeof(Confluent.Kafka.SecurityProtocol), settings.SecurityProtocol.ToString() ?? "SaslSsl");
            }
        }
    }
    
}