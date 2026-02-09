namespace Custom.Framework.Tests.RabbitMQ;

/// <summary>
/// Test message models for hospitality domain
/// </summary>

public class ReservationMessage
{
    public string ReservationId { get; set; } = string.Empty;
    public string HotelCode { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int RoomNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BookingMessage
{
    public string BookingId { get; set; } = string.Empty;
    public string HotelCode { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
}

public class PaymentMessage
{
    public string PaymentId { get; set; } = string.Empty;
    public string ReservationId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationMessage
{
    public string NotificationId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
