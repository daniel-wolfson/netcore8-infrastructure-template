using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Consumer strategies

    internal sealed class AtMostOnceConsumerStrategy(ConsumerSettings settings) : IConsumerDeliveryStrategy
    {
        private readonly ConsumerSettings _settings = settings;

        public void ConfigureConsumerConfig(ConsumerConfig config, ConsumerSettings settings)
        {
            config.EnableAutoCommit = true;
            config.AutoCommitIntervalMs = settings.AutoCommitIntervalMs;
        }

        public Task HandleAfterProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
        {
            // auto commit enabled - nothing to do
            return Task.CompletedTask;
        }
    }
}