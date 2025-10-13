namespace Custom.Framework.Aws.AmazonSQS.Models;

/// <summary>
/// Scenario: Real-time notification system
/// Use case: Send email, SMS, push notifications to millions of users
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// Unique notification identifier
    /// </summary>
    public string NotificationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Recipient user ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Notification type (Email, SMS, Push)
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Notification title/subject
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Notification message body
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Recipient contact information
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Priority (0=Low, 1=Normal, 2=High, 3=Urgent)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Schedule delivery time (null for immediate)
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Template ID for templated messages
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Template variables
    /// </summary>
    public Dictionary<string, string>? TemplateVariables { get; set; }

    /// <summary>
    /// Metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

public enum NotificationType
{
    Email = 0,
    SMS = 1,
    Push = 2,
    InApp = 3
}
