using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Aws.AuroraDB.Models;

/// <summary>
/// Example entity: Order
/// Demonstrates relationships and complex queries
/// </summary>
[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("order_number")]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("order_date")]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column("total_amount", TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column("tax_amount", TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column("shipping_amount", TypeName = "decimal(18,2)")]
    public decimal ShippingAmount { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = OrderStatus.Pending;

    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("shipped_at")]
    public DateTime? ShippedAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Order status constants
/// </summary>
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";
}
