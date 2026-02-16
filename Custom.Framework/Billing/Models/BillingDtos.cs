namespace Custom.Framework.Billing.Models;

/// <summary>
/// DTO for travel booking request
/// </summary>
public record TravelBookingDto
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

/// <summary>
/// Response DTO for travel booking
/// </summary>
public record TravelBookingResponseDto
{
    public required string BookingId { get; init; }
    public string? FlightReservationId { get; init; }
    public string? HotelReservationId { get; init; }
    public string? CarRentalReservationId { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Flight reservation result
/// </summary>
public record FlightReservationResult
{
    public required string ReservationId { get; init; }
    public required string ConfirmationCode { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>
/// Hotel reservation result
/// </summary>
public record HotelReservationResult
{
    public required string ReservationId { get; init; }
    public required string ConfirmationCode { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>
/// Car rental reservation result
/// </summary>
public record CarRentalReservationResult
{
    public required string ReservationId { get; init; }
    public required string ConfirmationCode { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>
/// Deposit request DTO
/// </summary>
public record DepositDto
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public string? PaymentMethodId { get; init; }
}

/// <summary>
/// Withdrawal request DTO
/// </summary>
public record WithdrawDto
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>
/// Balance response DTO
/// </summary>
public record BalanceResponseDto
{
    public required string UserId { get; init; }
    public required decimal Balance { get; init; }
    public required string Currency { get; init; }
}

/// <summary>
/// Create subscription DTO
/// </summary>
public record CreateSubscriptionDto
{
    public required string UserId { get; init; }
    public required string PlanId { get; init; }
    public required decimal Amount { get; init; }
    public string Interval { get; init; } = "month";
    public string? PaymentMethodId { get; init; }
}

/// <summary>
/// Cancel subscription DTO
/// </summary>
public record CancelSubscriptionDto
{
    public required string SubscriptionId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Create invoice DTO
/// </summary>
public record CreateInvoiceDto
{
    public required string UserId { get; init; }
    public string? SubscriptionId { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public DateTime? DueDate { get; init; }
}
