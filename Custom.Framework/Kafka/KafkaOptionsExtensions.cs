using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public static class KafkaOptionsExtensions
    {
        public static ProducerConfig ToProducerConfig(this ProducerSettings options)
        {
            return CreateProducerConfig(options);
        }

        private static ProducerConfig CreateProducerConfig(ProducerSettings _options)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                ClientId = _options.ClientId,
                LingerMs = _options.LingerMs,
                BatchSize = _options.BatchSize,
                MessageMaxBytes = _options.MessageMaxBytes,
                CompressionType = _options.CompressionType,
                //(CompressionType)Enum.Parse(typeof(CompressionType), _kafkaOptions.CompressionType ?? "None"),
                CompressionLevel = _options.CompressionLevel,
                RetryBackoffMs = _options.RetryBackoffMs,
            };

            // Let the strategy configure delivery-specific producer options
            //_deliveryStrategy.ConfigureProducerConfig(producerConfig, _kafkaOptions);

            if (_options.CustomProducerConfig != null)
            {
                foreach (var kv in _options.CustomProducerConfig)
                    producerConfig.Set(kv.Key, kv.Value);
            }

            return producerConfig;
        }
    }
}