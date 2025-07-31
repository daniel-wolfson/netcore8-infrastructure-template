using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    internal sealed class ExactlyOnceConsumerStrategy : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _settings;
        public ExactlyOnceConsumerStrategy(ConsumerSettings settings) => _settings = settings;

        public void ConfigureConsumerConfig(ConsumerConfig config, ConsumerSettings settings)
        {
            config.EnableAutoCommit = false;
            config.IsolationLevel = IsolationLevel.ReadCommitted;
            // transactional processing expected at application level
        }

        public Task HandleAfterProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
        {
            // commit offsets manually after processing
            consumer.Commit(result);
            return Task.CompletedTask;
        }
    }
}