using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    // Consumer strategy interface
    public interface IConsumerDeliveryStrategy
    {
        public void HandleAfterProcess(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result);

        public ConsumerConfig ConsumerConfig { get; }
    }
}