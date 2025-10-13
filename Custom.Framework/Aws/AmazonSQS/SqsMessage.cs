namespace Custom.Framework.Aws.AmazonSQS;

/// <summary>
/// Wrapper class for SQS messages with metadata
/// </summary>
/// <typeparam name="T">Message body type</typeparam>
public class SqsMessage<T>
{
    /// <summary>
    /// Unique message identifier
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Receipt handle for deleting/modifying the message
    /// </summary>
    public string ReceiptHandle { get; set; } = string.Empty;

    /// <summary>
    /// MD5 hash of the message body
    /// </summary>
    public string MD5OfBody { get; set; } = string.Empty;

    /// <summary>
    /// Deserialized message body
    /// </summary>
    public T? Body { get; set; }

    /// <summary>
    /// Raw message body as string
    /// </summary>
    public string RawBody { get; set; } = string.Empty;

    /// <summary>
    /// Message attributes
    /// </summary>
    public Dictionary<string, string>? Attributes { get; set; }

    /// <summary>
    /// Message system attributes
    /// </summary>
    public Dictionary<string, string>? MessageAttributes { get; set; }

    /// <summary>
    /// Number of times this message has been received
    /// </summary>
    public int ApproximateReceiveCount { get; set; }

    /// <summary>
    /// Timestamp when the message was first sent
    /// </summary>
    public DateTime? SentTimestamp { get; set; }

    /// <summary>
    /// Timestamp when the message became available for receive
    /// </summary>
    public DateTime? ApproximateFirstReceiveTimestamp { get; set; }

    /// <summary>
    /// Sender ID
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// Message group ID (FIFO queues only)
    /// </summary>
    public string? MessageGroupId { get; set; }

    /// <summary>
    /// Message deduplication ID (FIFO queues only)
    /// </summary>
    public string? MessageDeduplicationId { get; set; }

    /// <summary>
    /// Sequence number (FIFO queues only)
    /// </summary>
    public string? SequenceNumber { get; set; }
}
