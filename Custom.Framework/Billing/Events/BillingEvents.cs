namespace Custom.Framework.Billing.Events;

/// <summary>
/// Event raised when a deposit is completed
/// </summary>
public record DepositCompletedEvent
{
    public required Guid TransactionId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a withdrawal is completed
/// </summary>
public record WithdrawalCompletedEvent
{
    public required Guid TransactionId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when payment succeeds
/// </summary>
public record PaymentSuccessEvent
{
    public required Guid TransactionId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public string? StripePaymentIntentId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when payment fails
/// </summary>
public record PaymentFailedEvent
{
    public required Guid TransactionId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when user balance is low
/// </summary>
public record UserBalanceLowEvent
{
    public required string UserId { get; init; }
    public required decimal CurrentBalance { get; init; }
    public required decimal ThresholdAmount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a subscription is created
/// </summary>
public record SubscriptionCreatedEvent
{
    public required Guid SubscriptionId { get; init; }
    public required string UserId { get; init; }
    public required string PlanId { get; init; }
    public required decimal Amount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a subscription is canceled
/// </summary>
public record SubscriptionCanceledEvent
{
    public required Guid SubscriptionId { get; init; }
    public required string UserId { get; init; }
    public string? Reason { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a subscription is updated
/// </summary>
public record SubscriptionUpdatedEvent
{
    public required Guid SubscriptionId { get; init; }
    public required string UserId { get; init; }
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when an invoice is created
/// </summary>
public record InvoiceCreatedEvent
{
    public required Guid InvoiceId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when an order is created
/// </summary>
public record OrderCreatedEvent
{
    public required Guid OrderId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when travel booking is created
/// </summary>
public record TravelBookingCreatedEvent
{
    public required string BookingId { get; init; }
    public required string UserId { get; init; }
    public required decimal TotalAmount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when travel booking is compensated (rollback)
/// </summary>
public record TravelBookingCompensatedEvent
{
    public required string BookingId { get; init; }
    public required string UserId { get; init; }
    public required string ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when travel booking fails
/// </summary>
public record TravelBookingFailedEvent
{
    public required string BookingId { get; init; }
    public required string UserId { get; init; }
    public required string ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when compensation fails
/// </summary>
public record CompensationFailedEvent
{
    public required string BookingId { get; init; }
    public required string Service { get; init; }
    public required string ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event sent to Dead Letter Queue when compensation fails critically
/// Contains all context needed for manual intervention and recovery
/// </summary>
public record DeadLetterQueueEvent
{
    public required string CorrelationId { get; init; }
    public required string BookingId { get; init; }
    public required string UserId { get; init; }
    public required string FailedService { get; init; }
    public required string ErrorMessage { get; init; }
    public required string StackTrace { get; init; }
    public required string CompensationAction { get; init; }
    public string? FlightReservationId { get; init; }
    public string? HotelReservationId { get; init; }
    public string? CarRentalReservationId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int RetryCount { get; init; }
    public DateTime FirstAttemptTime { get; init; }
    public DateTime FailedAttemptTime { get; init; } = DateTime.UtcNow;
    public required string Severity { get; init; } // Critical, High, Medium
    public Dictionary<string, object>? AdditionalContext { get; init; }
}
