using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Consumer strategy interface
    public interface IConsumerDeliveryStrategy
    {
        void ConfigureConsumerConfig(ConsumerConfig config, ConsumerSettings settings);
        Task HandleAfterProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result);
    }
}