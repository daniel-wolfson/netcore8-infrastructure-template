namespace Custom.Framework.Kafka
{
public static partial class KafkaManager
    {
        /// <summary>
        /// Configuration for creating a Kafka topic
        /// </summary>
        public class KafkaTopicConfiguration
        {
            public string Name { get; set; } = string.Empty;
            public int NumPartitions { get; set; } = 1;
            public short ReplicationFactor { get; set; } = 1;
        }
    }
}