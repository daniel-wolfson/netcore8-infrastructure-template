using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public static class KafkaOptionsExtensions
    {
        public static ProducerConfig ToProducerConfig(this ProducerOptions options)
        {
            return CreateProducerConfig(options);
        }

        private static ProducerConfig CreateProducerConfig(ProducerOptions _options)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                ClientId = _options.ClientId,
                LingerMs = _options.LingerMs,
                BatchSize = _options.BatchSize,
                MessageMaxBytes = _options.MessageMaxBytes,
                CompressionType = (CompressionType)Enum.Parse(typeof(CompressionType), _options.CompressionType ?? "None"),
                CompressionLevel = _options.CompressionLevel,
                RetryBackoffMs = _options.RetryBackoffMs,
            };

            // Let the strategy configure delivery-specific producer options
            //_deliveryStrategy.ConfigureProducerConfig(producerConfig, _options);

            if (_options.ProducerConfig != null)
            {
                foreach (var kv in _options.ProducerConfig)
                    producerConfig.Set(kv.Key, kv.Value);
            }

            return producerConfig;
        }
    }
}