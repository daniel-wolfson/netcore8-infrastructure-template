namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Represents comprehensive metrics for a Kafka consumer instance.
    /// </summary>
    public class ConsumerMetrics
    {
        /// <summary>
        /// Number of messages in the internal processing channel waiting to be processed by workers.
        /// These messages have been consumed from Kafka but are queued for processing.
        /// </summary>
        public int InProcessMessageCount { get; set; }

        /// <summary>
        /// Kafka lag per partition - messages available in Kafka but not yet consumed.
        /// Key format: "topic:partition"
        /// </summary>
        public Dictionary<string, long> KafkaLagByPartition { get; set; } = new();

        /// <summary>
        /// Total Kafka lag across all partitions - messages in Kafka not yet consumed.
        /// </summary>
        public long TotalKafkaLag { get; set; }

        /// <summary>
        /// Maximum number of concurrent worker threads processing messages.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Maximum capacity of the internal channel for buffering messages.
        /// </summary>
        public int ChannelCapacity { get; set; }

        /// <summary>
        /// Total unhandled messages - includes BOTH Kafka lag (not consumed) AND channel pending (consumed but not processed).
        /// </summary>
        public long TotalMessages { get; set; }

        /// <summary>
        /// Channel utilization percentage (0-100).
        /// </summary>
        public double ChannelUtilizationPercent => ChannelCapacity > 0 
            ? (InProcessMessageCount / (double)ChannelCapacity) * 100 
            : 0;

        /// <summary>
        /// Indicates if the channel is near capacity (>80% full).
        /// </summary>
        public bool IsChannelNearCapacity => ChannelUtilizationPercent > 80;

        /// <summary>
        /// Total backlog - sum of messages in channel and Kafka lag (same as TotalMessages).
        /// </summary>
        public long TotalBacklog => InProcessMessageCount + TotalKafkaLag;
    }
}
