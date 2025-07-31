using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    internal sealed class AtLeastOnceConsumerStrategy(ConsumerSettings settings) : IConsumerDeliveryStrategy
    {
        public void ConfigureConsumerConfig(ConsumerConfig config, ConsumerSettings settings)
        {
            config.EnableAutoCommit = false;
            // Manual commit after successful processing
        }

        public Task HandleAfterProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
        {
            consumer.Commit(result);
            return Task.CompletedTask;
        }
    }
}