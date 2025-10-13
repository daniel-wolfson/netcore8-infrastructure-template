namespace Custom.Framework.Aws.AmazonSQS;

/// <summary>
/// Configuration options for AWS SQS
/// </summary>
public class AmazonSqsOptions
{
    /// <summary>
    /// AWS region (e.g., us-east-1, eu-west-1)
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// AWS Access Key ID (optional if using IAM roles)
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS Secret Access Key (optional if using IAM roles)
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Service URL (optional, for local SQS like LocalStack)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Default queue name
    /// </summary>
    public string DefaultQueueName { get; set; } = "default-queue";

    /// <summary>
    /// Enable batch processing for message operations
    /// </summary>
    public bool EnableBatchProcessing { get; set; } = true;

    /// <summary>
    /// Maximum batch size for send/receive operations (SQS limit is 10)
    /// </summary>
    public int MaxBatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum number of messages to receive per request
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Maximum retries for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for operations in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable request metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Wait time for long polling in seconds (0-20)
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Message visibility timeout in seconds
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable FIFO queue support
    /// </summary>
    public bool EnableFifoQueues { get; set; } = false;

    /// <summary>
    /// Enable content-based deduplication for FIFO queues
    /// </summary>
    public bool EnableContentBasedDeduplication { get; set; } = true;

    /// <summary>
    /// Enable dead letter queue
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Maximum receive count before moving to dead letter queue
    /// </summary>
    public int MaxReceiveCount { get; set; } = 3;

    /// <summary>
    /// Dead letter queue suffix
    /// </summary>
    public string DeadLetterQueueSuffix { get; set; } = "-dlq";
}
