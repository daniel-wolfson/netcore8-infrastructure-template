using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    internal sealed class AtLeastOnceProducerStrategy(ProducerSettings settings) : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _settings = settings;

        public bool RequiresTransactionalProducer => false;

        public void ConfigureProducerConfig(ProducerConfig config, ProducerSettings settings)
        {
            config.Acks = Acks.All;
            config.EnableIdempotence = settings.EnableIdempotence ?? false;
            config.MessageSendMaxRetries = settings.MaxRetries;
        }

        public Task<DeliveryResult<string, string>> ProduceAsync(
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            string topic,
            Message<string, string> message,
            CancellationToken cancellationToken)
        {
            if (_settings.EnableDuplicateDetection)
            {
                // add message id header for deduplication
                message.Headers ??= [];
                message.Headers.Add("message-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            return producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}