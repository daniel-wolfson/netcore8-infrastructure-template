using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public interface IKafkaConsumerGroup
    {
        DateTime CreatedTime { get; set; }
        DateTime LastAccessTime { get; set; }
        int AccessCount { get; set; }
        IEnumerable<IKafkaConsumer> Consumers { get; }

        //void GetOrAdd(string groupId, 
        //    Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler);
        IKafkaConsumer GetOrAdd(string consumerName);
        IKafkaConsumer GetOrAdd(IKafkaConsumer existConsumer);
        void RemoveFromGroup(string consumerName);
    }
}