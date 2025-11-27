namespace Custom.Framework.Azure.Cosmos.Models;

/// <summary>
/// Order status enumeration for hospitality reservations
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order is pending (10 minutes TTL)
    /// </summary>
    Pending,

    /// <summary>
    /// Payment in progress
    /// </summary>
    PaymentInProgress,

    /// <summary>
    /// Payment successful, order confirmed (long TTL - 7 days default)
    /// </summary>
    Succeeded,

    /// <summary>
    /// Payment failed
    /// </summary>
    Failed,

    /// <summary>
    /// Order cancelled by user
    /// </summary>
    Cancelled,

    /// <summary>
    /// Order expired (TTL exceeded)
    /// </summary>
    Expired
}
