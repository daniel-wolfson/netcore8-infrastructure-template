using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Producer strategies

    internal sealed class AtMostOnceProducerStrategy(ProducerSettings settings) : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _settings = settings;

        public bool RequiresTransactionalProducer => false;

        public void ConfigureProducerConfig(ProducerConfig config, ProducerSettings settings)
        {
            config.Acks = Acks.None;
            config.EnableIdempotence = false;
            config.MessageSendMaxRetries = 0;
        }

        public Task<DeliveryResult<string, string>> ProduceAsync(
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            string topic,
            Message<string, string> message,
            CancellationToken cancellationToken)
        {
            // Fire and forget semantics (still await produce to get DeliveryResult)
            return producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}