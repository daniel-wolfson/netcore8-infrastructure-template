using Amazon.DynamoDBv2.DataModel;

namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Scenario: Real-time event tracking for analytics platform
/// Use case: High-volume event ingestion (millions of events per hour)
/// </summary>
[DynamoDBTable("Events")]
public class Event
{
    /// <summary>
    /// Partition Key: Event type (for distributing load)
    /// </summary>
    [DynamoDBHashKey("EventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: Timestamp + EventId for chronological ordering
    /// Format: YYYY-MM-DDTHH:mm:ss.fff_EventId
    /// </summary>
    [DynamoDBRangeKey("TimestampEventId")]
    public string TimestampEventId { get; set; } = string.Empty;

    /// <summary>
    /// Unique event identifier
    /// </summary>
    [DynamoDBProperty("EventId")]
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID who triggered the event
    /// </summary>
    [DynamoDBProperty("UserId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Event timestamp
    /// </summary>
    [DynamoDBProperty("Timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Event source (web, mobile, api, etc.)
    /// </summary>
    [DynamoDBProperty("Source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Event payload as JSON
    /// </summary>
    [DynamoDBProperty("Payload")]
    public string? Payload { get; set; }

    /// <summary>
    /// Event properties for querying
    /// </summary>
    [DynamoDBProperty("Properties")]
    public Dictionary<string, string>? Properties { get; set; }

    /// <summary>
    /// Geographic information
    /// </summary>
    [DynamoDBProperty("Country")]
    public string? Country { get; set; }

    /// <summary>
    /// Device type
    /// </summary>
    [DynamoDBProperty("DeviceType")]
    public string? DeviceType { get; set; }

    /// <summary>
    /// TTL for automatic data expiration (30 days)
    /// </summary>
    [DynamoDBProperty("ExpiresAt")]
    public long ExpiresAt { get; set; }

    /// <summary>
    /// Version for optimistic locking
    /// </summary>
    [DynamoDBVersion]
    public int? Version { get; set; }
}
