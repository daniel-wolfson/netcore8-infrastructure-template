using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public interface IKafkaMessage
    {
        string Topic { get; set; }
        Offset Offset { get; set; }
        Partition Partition { get; set; }
        public string Value { get; set; }
    }

    public interface IKafkaMessage<TMessage>
    {
        string Topic { get; set; }
        Offset Offset { get; set; }
        Partition Partition { get; set; }
        public TMessage Value { get; set; }
    }
}