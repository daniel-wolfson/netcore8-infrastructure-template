using Amazon.DynamoDBv2.DataModel;

namespace Custom.Framework.Aws.DynamoDB;

/// <summary>
/// Scenario: User session data for high-traffic web application
/// Use case: Store and retrieve millions of active user sessions
/// </summary>
[DynamoDBTable("UserSessions")]
public class UserSession
{
    /// <summary>
    /// Partition Key: User ID
    /// </summary>
    [DynamoDBHashKey("UserId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: Session ID for multiple sessions per user
    /// </summary>
    [DynamoDBRangeKey("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Session token
    /// </summary>
    [DynamoDBProperty("Token")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// User's IP address
    /// </summary>
    [DynamoDBProperty("IpAddress")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent string
    /// </summary>
    [DynamoDBProperty("UserAgent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    [DynamoDBProperty("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    [DynamoDBProperty("LastActivityAt")]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session expiration timestamp (TTL)
    /// </summary>
    [DynamoDBProperty("ExpiresAt")]
    public long ExpiresAt { get; set; }

    /// <summary>
    /// Additional session metadata
    /// </summary>
    [DynamoDBProperty("Metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Is session active
    /// </summary>
    [DynamoDBProperty("IsActive")]
    public bool IsActive { get; set; } = true;
}
