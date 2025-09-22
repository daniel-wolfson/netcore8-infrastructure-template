using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public interface IKafkaMessage
    {
        Offset Offset { get; set; }
        Partition Partition { get; set; }
        string Topic { get; set; }
    }
}