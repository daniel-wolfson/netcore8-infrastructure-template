
namespace Custom.Framework.Kafka
{
    public class KafkaDeadLetterMessage<TMessage>
    {
        public string OriginalTopic { get; set; } = string.Empty;
        public int OriginalPartition { get; set; }
        public long OriginalOffset { get; set; }
        public string OriginalKey { get; set; } = string.Empty;
        public Byte[]? OriginalValue { get; set; }
        public DateTime OriginalTimestamp { get; set; }
        public FailureInfo? FailureInfo { get; set; }
    }
}