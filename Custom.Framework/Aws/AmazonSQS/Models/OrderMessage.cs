namespace Custom.Framework.Aws.AmazonSQS.Models;

/// <summary>
/// Scenario: Order processing in e-commerce platform
/// Use case: High-volume order queue with millions of orders per day
/// </summary>
public class OrderMessage
{
    /// <summary>
    /// Unique order identifier
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Customer identifier
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Order items
    /// </summary>
    public List<OrderItem> Items { get; set; } = new();

    /// <summary>
    /// Total order amount
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Order status
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Order timestamp
    /// </summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Shipping address
    /// </summary>
    public Address? ShippingAddress { get; set; }

    /// <summary>
    /// Payment method
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public int Priority { get; set; } = 0;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
