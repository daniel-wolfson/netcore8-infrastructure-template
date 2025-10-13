namespace Custom.Framework.Aws.AmazonSQS.Models;

/// <summary>
/// Scenario: Background job processing
/// Use case: Long-running tasks like report generation, data export, etc.
/// </summary>
public class JobMessage
{
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Job type/name
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// User who initiated the job
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Job parameters as JSON
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Job priority
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Maximum execution time in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Current retry count
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Job created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Scheduled execution time
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Callback URL for completion notification
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
