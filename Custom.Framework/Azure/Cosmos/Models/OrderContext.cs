using System.Text.Json.Serialization;

namespace Custom.Framework.Azure.Cosmos.Models;

/// <summary>
/// Order context entity for hospitality reservation flow
/// Represents the current state of an order through the reservation process
/// </summary>
public class OrderContext
{
    /// <summary>
    /// Unique order identifier (Cosmos DB document id)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Hotel code (partition key for efficient querying)
    /// </summary>
    [JsonPropertyName("hotelCode")]
    public string HotelCode { get; set; } = string.Empty;

    /// <summary>
    /// Session identifier for tracking user flow
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Customer identifier
    /// </summary>
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }

    /// <summary>
    /// Current order status
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>
    /// Current step in the reservation flow
    /// </summary>
    [JsonPropertyName("currentStep")]
    public string CurrentStep { get; set; } = "SearchHeader";

    /// <summary>
    /// Complete order data as JSON
    /// </summary>
    [JsonPropertyName("orderData")]
    public OrderData OrderData { get; set; } = new();

    /// <summary>
    /// Payment information (if applicable)
    /// </summary>
    [JsonPropertyName("paymentInfo")]
    public PaymentInfo? PaymentInfo { get; set; }

    /// <summary>
    /// Edge service verification result
    /// </summary>
    [JsonPropertyName("verificationResult")]
    public VerificationResult? VerificationResult { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration timestamp (when the document will be deleted)
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Time to live in seconds (Cosmos DB TTL)
    /// -1 means never expire, null means use container default
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// ETag for optimistic concurrency control (managed by Cosmos DB)
    /// </summary>
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

/// <summary>
/// Order data containing reservation details
/// </summary>
public class OrderData
{
    /// <summary>
    /// Check-in date
    /// </summary>
    [JsonPropertyName("checkInDate")]
    public DateTime? CheckInDate { get; set; }

    /// <summary>
    /// Check-out date
    /// </summary>
    [JsonPropertyName("checkOutDate")]
    public DateTime? CheckOutDate { get; set; }

    /// <summary>
    /// Number of adults
    /// </summary>
    [JsonPropertyName("adults")]
    public int Adults { get; set; }

    /// <summary>
    /// Number of children
    /// </summary>
    [JsonPropertyName("children")]
    public int Children { get; set; }

    /// <summary>
    /// Number of infants
    /// </summary>
    [JsonPropertyName("infants")]
    public int Infants { get; set; }

    /// <summary>
    /// Selected room code
    /// </summary>
    [JsonPropertyName("roomCode")]
    public string? RoomCode { get; set; }

    /// <summary>
    /// Selected plan code
    /// </summary>
    [JsonPropertyName("planCode")]
    public string? PlanCode { get; set; }

    /// <summary>
    /// Selected price code
    /// </summary>
    [JsonPropertyName("priceCode")]
    public string? PriceCode { get; set; }

    /// <summary>
    /// Total amount
    /// </summary>
    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Guest information
    /// </summary>
    [JsonPropertyName("guestInfo")]
    public GuestInfo? GuestInfo { get; set; }

    /// <summary>
    /// Additional services or requests
    /// </summary>
    [JsonPropertyName("additionalServices")]
    public List<string> AdditionalServices { get; set; } = new();

    /// <summary>
    /// Special requests or notes
    /// </summary>
    [JsonPropertyName("specialRequests")]
    public string? SpecialRequests { get; set; }

    /// <summary>
    /// Raw search criteria
    /// </summary>
    [JsonPropertyName("searchCriteria")]
    public Dictionary<string, object> SearchCriteria { get; set; } = new();
}

/// <summary>
/// Guest information
/// </summary>
public class GuestInfo
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }
}

/// <summary>
/// Payment information
/// </summary>
public class PaymentInfo
{
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("paymentStatus")]
    public string? PaymentStatus { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("paidAt")]
    public DateTime? PaidAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Edge service verification result
/// </summary>
public class VerificationResult
{
    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("verificationTimestamp")]
    public DateTime VerificationTimestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("verificationErrors")]
    public List<string> VerificationErrors { get; set; } = new();

    [JsonPropertyName("verificationWarnings")]
    public List<string> VerificationWarnings { get; set; } = new();
}
