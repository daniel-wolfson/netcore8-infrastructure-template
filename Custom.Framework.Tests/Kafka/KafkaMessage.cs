using Custom.Framework.Kafka;

namespace Custom.Framework.Tests.Kafka
{
    public class KafkaMessage : IKafkaMessage
    {
        public KafkaMessage()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        public bool IsError { get; set; }
        public string Reason { get; set; } = string.Empty;
        public long Offset { get; set; }
        public int Partition { get; set; }
        public string Topic { get; set; } = string.Empty;
        public long Timestamp { get; set; } //UnixTimestampMs
        public required string Key { get; set; }
        public string? Value { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }

    public class KafkaMessage<TValue> : KafkaMessage, IKafkaMessage<TValue>
    {
        // Remove 'new' and nullable annotation to match interface signature
        public new TValue Value { get; set; } = default!;
    }
    public interface IKafkaMessage<TValue> : IKafkaMessage
    {
        public new TValue Value { get; set; }
    }
}
