using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    internal sealed class ExactlyOnceProducerStrategy : IProducerDeliveryStrategy
    {
        private readonly ProducerSettings _settings;
        public ExactlyOnceProducerStrategy(ProducerSettings settings) => _settings = settings;
        public bool RequiresTransactionalProducer => true;

        public void ConfigureProducerConfig(ProducerConfig config, ProducerSettings settings)
        {
            config.Acks = Acks.All;
            config.EnableIdempotence = true;
            config.MessageSendMaxRetries = settings.MaxRetries;
        }

        public async Task<DeliveryResult<string, string>> ProduceAsync(
            IProducer<string, string> producer,
            IProducer<string, string>? transactionalProducer,
            string topic,
            Message<string, string> message,
            CancellationToken cancellationToken)
        {
            if (transactionalProducer == null)
                throw new InvalidOperationException("Transactional producer not initialized for ExactlyOnce delivery");

            try
            {
                transactionalProducer.InitTransactions(TimeSpan.FromSeconds(10));
                transactionalProducer.BeginTransaction();

                var delivery = await transactionalProducer.ProduceAsync(topic, message, cancellationToken).ConfigureAwait(false);

                transactionalProducer.CommitTransaction(TimeSpan.FromSeconds(10));
                return delivery;
            }
            catch (Exception)
            {
                try { transactionalProducer.AbortTransaction(TimeSpan.FromSeconds(5)); }
                catch (Exception abortEx)
                {
                    // Use Serilog/ILogger from calling class for logging; here swallow to avoid introducing logger dependency
                    Console.Error.WriteLine($"Failed to abort transaction: {abortEx}");
                }
                throw;
            }
        }
    }
}