namespace Custom.Framework.Billing.Models;

/// <summary>
/// Transaction status enumeration
/// </summary>
public enum TransactionStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Canceled
}

/// <summary>
/// Billing transaction state enumeration for state machine
/// </summary>
public enum BillingTransactionState
{
    Created,
    Processing,
    Completed,
    Error,
    Canceled
}

/// <summary>
/// Transaction type enumeration
/// </summary>
public enum TransactionType
{
    Deposit,
    Withdrawal,
    Payment,
    Refund
}

/// <summary>
/// Subscription status enumeration
/// </summary>
public enum SubscriptionStatus
{
    Active,
    Trialing,
    PastDue,
    Canceled,
    Unpaid
}

/// <summary>
/// Invoice status enumeration
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Open,
    Paid,
    Void,
    Uncollectible
}

/// <summary>
/// Booking status enumeration
/// </summary>
public enum BookingStatus
{
    Pending,
    Confirmed,
    Compensated,
    Failed
}
