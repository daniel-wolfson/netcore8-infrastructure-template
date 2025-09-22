namespace Custom.Framework.Kafka
{
    public enum DeliverySemantics
    {
        /// <summary>
        /// Maximum-once - Fire once and forget, possible message loss
        /// </summary>
        AtMostOnce = 0,

        /// <summary>
        /// Minimum-once - Guaranteed delivery with possible duplicates
        /// </summary>
        AtLeastOnce = 1,

        /// <summary>
        /// Exactly-once delivery with transactions
        /// </summary>
        ExactlyOnce = 2
    }
}