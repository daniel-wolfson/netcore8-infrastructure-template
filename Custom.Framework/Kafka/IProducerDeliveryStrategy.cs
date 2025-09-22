using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Producer strategy interface
    public interface IProducerDeliveryStrategy
    {
        bool RequiresTransactionalProducer { get; }

        Task<DeliveryResult<string, string>> PublishAsync(
            string topic,
            Message<string, string> message,
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            CancellationToken cancellationToken);
    }
}