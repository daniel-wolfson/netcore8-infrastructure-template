namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Detailed information about messages in a specific partition.
    /// </summary>
    public class PartitionMessageInfo
    {
        /// <summary>
        /// Topic name
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// Partition number
        /// </summary>
        public int Partition { get; set; }

        /// <summary>
        /// Total number of messages that exist in this partition (high watermark).
        /// This is ALL messages ever written to this partition (subject to retention policy).
        /// </summary>
        public long TotalMessages { get; set; }

        /// <summary>
        /// Current consumer position - the offset that will be read next.
        /// This equals the number of messages already consumed.
        /// </summary>
        public long ConsumedPosition { get; set; }

        /// <summary>
        /// Number of messages not yet consumed (lag).
        /// RemainingMessages = TotalMessages - ConsumedPosition
        /// </summary>
        public long RemainingMessages { get; set; }

        /// <summary>
        /// Low watermark - earliest available offset in the partition.
        /// Messages before this have been deleted due to retention policy.
        /// </summary>
        public long LowWatermark { get; set; }

        /// <summary>
        /// High watermark - offset of the next message to be produced.
        /// This equals the total message count.
        /// </summary>
        public long HighWatermark { get; set; }

        /// <summary>
        /// Percentage of messages consumed (0-100).
        /// </summary>
        public double ConsumedPercentage => TotalMessages > 0 
            ? (ConsumedPosition / (double)TotalMessages) * 100 
            : 0;

        public override string ToString()
        {
            return $"{Topic}:{Partition} - Total: {TotalMessages}, " +
                   $"Consumed: {ConsumedPosition} ({ConsumedPercentage:F1}%), " +
                   $"Remaining: {RemainingMessages}";
        }
    }
}
