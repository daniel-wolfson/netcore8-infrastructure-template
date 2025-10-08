using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Producer strategy interface
    public interface IProducerDeliveryStrategy
    {
        bool RequiresTransactionalProducer { get; }

        Task<DeliveryResult<string, byte[]>> PublishAsync(
            string topic,
            Message<string, byte[]> message,
            IProducer<string, byte[]> producer,
            IProducer<string, byte[]>? transactionalProducer,
            CancellationToken cancellationToken);

        public ProducerConfig ProducerConfig { get; }
    }
}