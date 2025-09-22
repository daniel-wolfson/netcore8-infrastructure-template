using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    public class KafkaMessage : Message<string, string>, IKafkaMessage
    {
        public int Order { get; set; }
        public bool IsError { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Offset Offset { get; set; }
        public Partition Partition { get; set; }
        public string Topic { get; set; } = string.Empty;
    }
}