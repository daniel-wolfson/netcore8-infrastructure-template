using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Producer strategy interface
    public interface IProducerDeliveryStrategy
    {
        bool RequiresTransactionalProducer { get; }

        void ConfigureProducerConfig(ProducerConfig config, ProducerSettings settings);

        Task<DeliveryResult<string, string>> ProduceAsync(
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            string topic,
            Message<string, string> message,
            CancellationToken cancellationToken);
    }
}