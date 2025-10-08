namespace Custom.Framework.Kafka
{
    public interface IKafkaMessage
    {
        string Topic { get; set; }
        long Offset { get; set; }
        int Partition { get; set; }
        public string? Value { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}