namespace Custom.Framework.Kafka
{
    public enum DeliverySemantics
    {
        AtMostOnce = 0,    // Fire and forget
        AtLeastOnce = 1,   // Guaranteed delivery with possible duplicates
        ExactlyOnce = 2    // Exactly-once delivery with transactions
    }
}