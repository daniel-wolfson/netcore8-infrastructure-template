namespace Custom.Framework.Billing.Models;

/// <summary>
/// Represents a financial transaction
/// </summary>
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillingUserId { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public BillingTransactionState State { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Represents a billing user's virtual account
/// </summary>
public class BillingUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public string? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a subscription
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillingUserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Interval { get; set; } = "month";
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an invoice
/// </summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BillingUserId { get; set; } = string.Empty;
    public string? SubscriptionId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a travel booking with distributed transaction
/// </summary>
public class TravelBooking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BookingId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public string? FlightReservationId { get; set; }
    public string? HotelReservationId { get; set; }
    public string? CarRentalReservationId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
