namespace Custom.Framework.Billing.Commands;

/// <summary>
/// Command to deposit funds
/// </summary>
public record DepositCommand
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public string? PaymentMethodId { get; init; }
}

/// <summary>
/// Command to withdraw funds
/// </summary>
public record WithdrawCommand
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>
/// Command to create a subscription
/// </summary>
public record CreateSubscriptionCommand
{
    public required string UserId { get; init; }
    public required string PlanId { get; init; }
    public required decimal Amount { get; init; }
    public string Interval { get; init; } = "month";
    public string? PaymentMethodId { get; init; }
}

/// <summary>
/// Command to cancel a subscription
/// </summary>
public record CancelSubscriptionCommand
{
    public required string SubscriptionId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Command to create an invoice
/// </summary>
public record CreateInvoiceCommand
{
    public required string UserId { get; init; }
    public string? SubscriptionId { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public DateTime? DueDate { get; init; }
}

/// <summary>
/// Command to book travel (saga orchestration)
/// </summary>
public record BookTravelCommand
{
    public required string UserId { get; init; }
    public required string FlightOrigin { get; init; }
    public required string FlightDestination { get; init; }
    public required string DepartureDate { get; init; }
    public required string ReturnDate { get; init; }
    public required string HotelId { get; init; }
    public required string CheckInDate { get; init; }
    public required string CheckOutDate { get; init; }
    public required string CarPickupLocation { get; init; }
    public required string CarDropoffLocation { get; init; }
    public required string CarPickupDate { get; init; }
    public required string CarDropoffDate { get; init; }
    public required decimal TotalAmount { get; init; }
}
