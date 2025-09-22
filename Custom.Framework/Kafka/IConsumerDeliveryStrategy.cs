using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Consumer strategy interface
    public interface IConsumerDeliveryStrategy
    {
        Task HandleAfterProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result);
    }
}