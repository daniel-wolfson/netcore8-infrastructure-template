using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    
    internal sealed class ProducerAtLeastOnceStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _options;
        private readonly ProducerConfig _config;
        public bool RequiresTransactionalProducer => false;

        public ProducerAtLeastOnceStrategy(ProducerConfig config, ProducerSettings options)
        {
            _options = options;
            _config = config;
            config.Acks = Acks.None;
            config.EnableIdempotence = false;
            config.MessageSendMaxRetries = options.MessageSendMaxRetries;
        }

        public Task<DeliveryResult<string, byte[]>> PublishAsync(
            string topic, 
            Message<string, byte[]> message, 
            IProducer<string, byte[]> producer, 
            IProducer<string, byte[]>? transactionalProducer, 
            CancellationToken cancellationToken)
        {
            return producer.ProduceAsync(topic, message, cancellationToken);
        }

        public ProducerConfig ProducerConfig => _config;
    }
}